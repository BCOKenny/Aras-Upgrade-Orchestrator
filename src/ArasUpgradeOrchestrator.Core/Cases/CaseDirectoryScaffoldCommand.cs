using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArasUpgradeOrchestrator.Core.Cases;

public sealed record CaseDirectoryScaffoldRequest(
    string CaseRoot,
    string CaseDirectoryName,
    string CustomerCode,
    string SourceVersion,
    string SourceSlug,
    string TargetVersion,
    string TargetSlug,
    string Actor,
    string ExecutionMode);

public sealed record CaseDirectoryScaffoldTemplate(string RelativePath, string Content);

public sealed record CaseDirectoryScaffoldPlan(
    string CaseRoot,
    string ExecutionMode,
    string TargetPath,
    IReadOnlyList<string> DirectoryPaths,
    IReadOnlyList<string> TemplatePaths,
    IReadOnlyList<string> ForbiddenFormalPaths,
    string ScaffoldIdentifier,
    IReadOnlyList<CaseDirectoryScaffoldTemplate> Templates);

public sealed record CaseDirectoryScaffoldResult(
    string Status,
    string TargetPath,
    string ScaffoldIdentifier,
    IReadOnlyList<string> DirectoryPaths,
    IReadOnlyList<string> TemplatePaths,
    string? StagingPath)
{
    public static CaseDirectoryScaffoldResult Created(CaseDirectoryScaffoldPlan plan) => new(
        "Created",
        plan.TargetPath,
        plan.ScaffoldIdentifier,
        plan.DirectoryPaths,
        plan.TemplatePaths,
        null);
}

/// <summary>
/// Builds and validates the non-formal DIRECTORY_SCAFFOLD_ONLY case layout.
/// Validate is zero-write; ApplyAsync writes and publishes a fully validated staging layout.
/// </summary>
public sealed class CaseDirectoryScaffoldCommand
{
    public const string DirectoryScaffoldOnly = "DIRECTORY_SCAFFOLD_ONLY";

    private static readonly Regex SlugPattern = new("^[a-z0-9.-]+$", RegexOptions.CultureInvariant);
    private static readonly Regex ReservedDosNamePattern = new(
        "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\\..*)?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions TemplateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public CaseDirectoryScaffoldPlan Validate(CaseDirectoryScaffoldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caseRoot = RequireExistingDirectory(request.CaseRoot, nameof(request.CaseRoot));
        ValidateRequest(request);
        var targetPath = RequireDirectChild(caseRoot, request.CaseDirectoryName);
        var plan = BuildPlan(caseRoot, targetPath, request);
        RejectExistingCaseData(plan);
        ValidatePlannedTemplates(plan);
        return plan;
    }

    public async Task<CaseDirectoryScaffoldResult> ApplyAsync(
        CaseDirectoryScaffoldRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plan = Validate(request);
        var stagingPath = Path.Combine(
            plan.CaseRoot,
            $".{request.CaseDirectoryName}.scaffold-staging-{Guid.NewGuid():N}");

        CreateNewStagingDirectory(stagingPath);

        await WritePlanToStagingAsync(stagingPath, plan, cancellationToken);
        await ValidateStagingAsync(stagingPath, plan, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.Move(stagingPath, plan.TargetPath);

        return CaseDirectoryScaffoldResult.Created(plan);
    }

    private static void ValidateRequest(CaseDirectoryScaffoldRequest request)
    {
        RequireValue(request.CaseDirectoryName, nameof(request.CaseDirectoryName));
        RequireValue(request.CustomerCode, nameof(request.CustomerCode));
        RequireValue(request.SourceVersion, nameof(request.SourceVersion));
        RequireValue(request.TargetVersion, nameof(request.TargetVersion));
        RequireValue(request.Actor, nameof(request.Actor));

        if (!string.Equals(request.ExecutionMode, DirectoryScaffoldOnly, StringComparison.Ordinal))
            throw new InvalidDataException($"執行模式必須精確為 {DirectoryScaffoldOnly}。 ");
        if (string.Equals(request.SourceVersion, request.TargetVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("來源版本與最終版本不可相同。 ");

        ValidateDirectoryName(request.CaseDirectoryName);
        ValidateSlug(request.SourceSlug, nameof(request.SourceSlug));
        ValidateSlug(request.TargetSlug, nameof(request.TargetSlug));
        if (string.Equals(request.SourceSlug, request.TargetSlug, StringComparison.Ordinal))
            throw new InvalidDataException("來源版本短名稱與最終版本短名稱不可相同。 ");
    }

    private static void RequireValue(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{name} 不可為空白。 ");
        if (value.Contains('<', StringComparison.Ordinal) || value.Contains('>', StringComparison.Ordinal))
            throw new InvalidDataException($"{name} 不可包含未替換的 <...> 佔位符。 ");
    }

    private static void ValidateDirectoryName(string name)
    {
        if (name is "." or ".." || Path.IsPathRooted(name) ||
            name.Contains('/') || name.Contains('\\') ||
            name.EndsWith('.') || name.EndsWith(' ') || ReservedDosNamePattern.IsMatch(name) ||
            name.IndexOfAny(new[] { '<', '>', ':', '"', '|', '?', '*', '\0' }) >= 0 ||
            name.Any(char.IsControl))
            throw new InvalidDataException("案件目錄名稱必須是安全的單一目錄名稱。 ");
    }

    private static void ValidateSlug(string? slug, string name)
    {
        RequireValue(slug, name);
        if (!SlugPattern.IsMatch(slug!) || slug is "." or "..")
            throw new InvalidDataException($"{name} 必須是小寫 ASCII 目錄 slug，且只可包含 a-z、0-9、. 與 -。 ");
    }

    private static string RequireExistingDirectory(string? path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException($"{name} 不可為空白。 ");
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidDataException($"{name} 必須是完整限定路徑。 ");

        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new InvalidOperationException($"{name} 必須是已存在的目錄。 ");
        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    private static string RequireDirectChild(string caseRoot, string caseDirectoryName)
    {
        var targetPath = Path.GetFullPath(Path.Combine(caseRoot, caseDirectoryName));
        var parentPath = Path.GetDirectoryName(targetPath);
        if (!string.Equals(parentPath, caseRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("案件目標路徑必須是案件根目錄的直接子目錄。 ");
        return targetPath;
    }

    private static CaseDirectoryScaffoldPlan BuildPlan(string caseRoot, string targetPath, CaseDirectoryScaffoldRequest request)
    {
        var directories = new[]
        {
            $"core-tree/inputs/customer-{request.SourceSlug}/tree",
            $"core-tree/inputs/customer-{request.SourceSlug}/evidence",
            $"core-tree/inputs/ootb-{request.SourceSlug}/tree",
            $"core-tree/inputs/ootb-{request.SourceSlug}/evidence",
            $"core-tree/inputs/ootb-{request.TargetSlug}/tree",
            $"core-tree/inputs/ootb-{request.TargetSlug}/evidence",
            "core-tree/attempts",
            "core-tree/completions",
            "core-tree/deliveries",
            "core-tree/requests",
            "core-tree/review-approvals",
            "package-upgrade/customer-baseline",
            "package-upgrade/hops",
            "package-upgrade/pending-route",
            "db-upgrade/hops",
            "handoff",
            ".orchestrator",
            ".orchestrator/locks",
            $"inputs/source-{request.SourceSlug}/version-evidence",
            $"inputs/source-{request.SourceSlug}/db-backup-evidence"
        };

        var templates = BuildTemplates(request);
        var directoryPaths = directories.Select(path => ResolvePlannedPath(targetPath, path)).ToArray();
        var templatePaths = templates.Select(template => ResolvePlannedPath(targetPath, template.RelativePath)).ToArray();
        var forbiddenFormalPaths = new[]
        {
            Path.Combine(targetPath, "aras-upgrade-case.json"),
            Path.Combine(targetPath, ".orchestrator", "history.jsonl")
        };

        foreach (var path in directoryPaths.Concat(templatePaths).Concat(forbiddenFormalPaths))
            EnsureUnderTarget(path, targetPath);

        return new CaseDirectoryScaffoldPlan(
            caseRoot,
            request.ExecutionMode,
            targetPath,
            directoryPaths,
            templatePaths,
            forbiddenFormalPaths,
            $"directory-scaffold-{request.SourceSlug}-to-{request.TargetSlug}",
            templates);
    }

    private static IReadOnlyList<CaseDirectoryScaffoldTemplate> BuildTemplates(CaseDirectoryScaffoldRequest request)
    {
        return new[]
        {
            JsonTemplate("core-tree/requests/preflight-request.template.json", new
            {
                status = "待提供",
                actor = "<待提供>",
                customerInput = "<待提供>",
                sourceOotbInput = "<待提供>",
                targetOotbInput = "<待提供>",
                sourceVersion = "<待提供>",
                targetVersion = "<待提供>"
            }),
            JsonTemplate("core-tree/requests/comparison-request.template.json", new
            {
                status = "待提供",
                actor = "<待提供>",
                comparisonName = "<待提供>",
                customerInput = "<待提供>",
                sourceOotbInput = "<待提供>",
                targetOotbInput = "<待提供>"
            }),
            MarkdownTemplate($"core-tree/inputs/customer-{request.SourceSlug}/evidence/VERSION-EVIDENCE-TEMPLATE.md"),
            MarkdownTemplate($"core-tree/inputs/ootb-{request.SourceSlug}/evidence/VERSION-EVIDENCE-TEMPLATE.md"),
            MarkdownTemplate($"core-tree/inputs/ootb-{request.TargetSlug}/evidence/VERSION-EVIDENCE-TEMPLATE.md"),
            JsonTemplate("package-upgrade/pending-route/route-input.template.json", new
            {
                status = "待提供",
                sourceVersion = "<待提供>",
                targetVersion = "<待提供>",
                route = "<待提供>"
            }),
            new CaseDirectoryScaffoldTemplate("handoff/preparation-checklist.md", "# 準備交接清單\n\n- 狀態：待提供\n- 客戶 Core Tree：<待提供>\n- OOTB Core Tree：<待提供>\n- 版本證據：<待提供>\n- Package／DB 升級路徑：<待提供>\n")
        };
    }

    private static CaseDirectoryScaffoldTemplate JsonTemplate(string relativePath, object content)
        => new(relativePath, JsonSerializer.Serialize(content, TemplateJsonOptions));

    private static CaseDirectoryScaffoldTemplate MarkdownTemplate(string relativePath)
        => new(relativePath, "# 版本證據範本\n\n- 輸入識別：<待提供>\n- 預期版本：<待提供>\n- 狀態：待提供\n- 版本證據位置：<待提供>\n- Build：<待提供>\n");

    private static string ResolvePlannedPath(string targetPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.None).Any(segment => segment is "" or "." or ".."))
            throw new InvalidDataException("案件骨架包含不安全的相對路徑。 ");

        var path = Path.GetFullPath(Path.Combine(targetPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureUnderTarget(path, targetPath);
        return path;
    }

    private static void EnsureUnderTarget(string path, string targetPath)
    {
        var prefix = Path.TrimEndingDirectorySeparator(targetPath) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("案件骨架路徑逸出案件目標目錄。 ");
    }

    private static void RejectExistingCaseData(CaseDirectoryScaffoldPlan plan)
    {
        if (Directory.Exists(plan.TargetPath) || File.Exists(plan.TargetPath))
            throw new InvalidOperationException("案件目標目錄已存在，拒絕覆寫、合併或修復既有資料。 ");

        foreach (var path in plan.DirectoryPaths.Concat(plan.TemplatePaths).Concat(plan.ForbiddenFormalPaths))
        {
            if (Directory.Exists(path) || File.Exists(path))
                throw new InvalidOperationException("預定案件骨架路徑已存在，拒絕覆寫、合併或修復既有資料。 ");
        }
    }

    private static void ValidatePlannedTemplates(CaseDirectoryScaffoldPlan plan)
    {
        if (plan.Templates.Count != 7 || plan.TemplatePaths.Count != 7 || plan.DirectoryPaths.Count != 20)
            throw new InvalidDataException("案件骨架的固定目錄或範本數量不正確。 ");
        if (plan.Templates.Select(template => template.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != plan.Templates.Count)
            throw new InvalidDataException("案件骨架包含重複的範本路徑。 ");

        foreach (var template in plan.Templates)
        {
            if (template.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(template.Content), TemplateJsonOptions);
                using var document = JsonDocument.Parse(json);
            }
            else if (template.RelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                var roundTripped = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(template.Content));
                if (!string.Equals(template.Content, roundTripped, StringComparison.Ordinal))
                    throw new InvalidDataException("Markdown 範本無法完成 UTF-8 round-trip 驗證。 ");
            }
            else
            {
                throw new InvalidDataException("案件骨架範本只允許 JSON 或 Markdown。 ");
            }
        }
    }

    private static async Task WritePlanToStagingAsync(
        string stagingPath,
        CaseDirectoryScaffoldPlan plan,
        CancellationToken cancellationToken)
    {
        foreach (var directoryPath in plan.DirectoryPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(ToStagingPath(stagingPath, plan.TargetPath, directoryPath));
        }

        foreach (var template in plan.Templates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var templatePath = ToStagingPath(stagingPath, plan.TargetPath, ResolvePlannedPath(plan.TargetPath, template.RelativePath));
            var parentDirectory = Path.GetDirectoryName(templatePath)
                ?? throw new InvalidDataException("案件骨架範本缺少父目錄。 ");
            Directory.CreateDirectory(parentDirectory);

            await using var stream = new FileStream(
                templatePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteAsync(template.Content.AsMemory(), cancellationToken);
        }
    }

    private static async Task ValidateStagingAsync(
        string stagingPath,
        CaseDirectoryScaffoldPlan plan,
        CancellationToken cancellationToken)
    {
        foreach (var directoryPath in plan.DirectoryPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(ToStagingPath(stagingPath, plan.TargetPath, directoryPath)))
                throw new InvalidDataException("staging 案件骨架缺少預定目錄。 ");
        }

        foreach (var template in plan.Templates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plannedPath = ResolvePlannedPath(plan.TargetPath, template.RelativePath);
            var stagingTemplatePath = ToStagingPath(stagingPath, plan.TargetPath, plannedPath);
            if (!File.Exists(stagingTemplatePath))
                throw new InvalidDataException("staging 案件骨架缺少預定範本。 ");

            var bytes = await File.ReadAllBytesAsync(stagingTemplatePath, cancellationToken);
            if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
                throw new InvalidDataException("staging 案件骨架範本不可包含 UTF-8 BOM。 ");

            var content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            if (!string.Equals(template.Content, content, StringComparison.Ordinal))
                throw new InvalidDataException("staging 案件骨架範本內容不符預定內容。 ");

            if (template.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var document = JsonDocument.Parse(content);
            }
        }

        foreach (var forbiddenPath in plan.ForbiddenFormalPaths)
        {
            var stagingForbiddenPath = ToStagingPath(stagingPath, plan.TargetPath, forbiddenPath);
            if (File.Exists(stagingForbiddenPath) || Directory.Exists(stagingForbiddenPath))
                throw new InvalidDataException("staging 案件骨架不可包含正式案件檔案或歷程。 ");
        }

        ValidateStagingInventory(stagingPath, plan);
    }

    private static void CreateNewStagingDirectory(string stagingPath)
    {
        if (Directory.Exists(stagingPath) || File.Exists(stagingPath))
            throw new InvalidOperationException("唯一 staging 路徑已存在，拒絕採用或覆寫既有資料。 ");

        Directory.CreateDirectory(stagingPath);
    }

    private static void ValidateStagingInventory(string stagingPath, CaseDirectoryScaffoldPlan plan)
    {
        var plannedDirectories = plan.DirectoryPaths
            .Select(path => NormalizeRelativePath(plan.TargetPath, path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var plannedFiles = plan.TemplatePaths
            .Select(path => NormalizeRelativePath(plan.TargetPath, path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedDirectories = new HashSet<string>(plannedDirectories, StringComparer.OrdinalIgnoreCase);

        foreach (var path in plannedDirectories.Concat(plannedFiles))
            AddNormalizedParentDirectories(path, allowedDirectories);

        var actualDirectories = Directory.EnumerateDirectories(stagingPath, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(stagingPath, path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actualFiles = Directory.EnumerateFiles(stagingPath, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(stagingPath, path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!actualDirectories.SetEquals(allowedDirectories) || !plannedDirectories.IsSubsetOf(actualDirectories))
            throw new InvalidDataException("staging 案件骨架目錄清單不符固定計畫。 ");
        if (!actualFiles.SetEquals(plannedFiles))
            throw new InvalidDataException("staging 案件骨架檔案清單不符固定計畫。 ");
    }

    private static void AddNormalizedParentDirectories(string normalizedPath, ISet<string> directories)
    {
        var separatorIndex = normalizedPath.LastIndexOf('/');
        while (separatorIndex >= 0)
        {
            directories.Add(normalizedPath[..separatorIndex]);
            separatorIndex = normalizedPath.LastIndexOf('/', separatorIndex - 1);
        }
    }

    private static string NormalizeRelativePath(string rootPath, string path)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);
        if (relativePath is "." or ".." || Path.IsPathRooted(relativePath) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidDataException("staging 案件骨架清單包含逸出根目錄的路徑。 ");

        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string ToStagingPath(string stagingPath, string targetPath, string plannedPath)
    {
        var relativePath = Path.GetRelativePath(targetPath, plannedPath);
        if (relativePath is "." or ".." || Path.IsPathRooted(relativePath) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidDataException("staging 案件骨架路徑逸出目標目錄。 ");

        var stagingItemPath = Path.GetFullPath(Path.Combine(stagingPath, relativePath));
        EnsureUnderTarget(stagingItemPath, stagingPath);
        return stagingItemPath;
    }
}
