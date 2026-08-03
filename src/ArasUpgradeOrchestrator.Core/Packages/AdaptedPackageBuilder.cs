using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ArasUpgradeOrchestrator.Core.Aml;
using ArasUpgradeOrchestrator.Core.Rules;

namespace ArasUpgradeOrchestrator.Core.Packages;

public enum AdaptedPackageStatus { Blocked, ReadyForFinalization, Completed }

public sealed record AdaptedPackageRequest(
    Guid AttemptId,
    string SourceVersion,
    string TargetVersion,
    string CustomerBaselineRoot,
    string SolutionsRoot,
    string BackupRoot,
    string AttemptRoot,
    string Rule1ArchivePath,
    OotbHopDiffReuseRequirement Rule1Requirement,
    RuleSetResolutionResult Rule2Rules,
    DateTimeOffset StartedAt);

public sealed record AdaptedPackageError(string RelativePath, string Message);

public sealed record AdaptedPackageBuildResult(
    Guid AttemptId,
    AdaptedPackageStatus Status,
    string SourceVersion,
    string TargetVersion,
    string SourceWorkRoot,
    string SolutionsRoot,
    string SolutionsBackupRoot,
    string Rule1ArchivePath,
    string Rule1ArchiveChecksum,
    IReadOnlyList<RuleSetVersionReference> Rule2RuleSets,
    string Rule2EffectiveChecksum,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    int XmlFilesProcessed,
    int ManualReviewCount,
    IReadOnlyList<AdaptedPackageError> Errors,
    IReadOnlyList<Rule2ManualReview> ManualReviews,
    string? CompletionManifestPath = null);

public static class AdaptedPackageBuilder
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public static async Task<AdaptedPackageBuildResult> BuildAsync(
        AdaptedPackageRequest request,
        Func<DateTimeOffset>? clock = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var verification = await OotbHopDiffArtifactVerifier.VerifyAsync(request.Rule1ArchivePath,
            request.Rule1Requirement, cancellationToken);
        if (!verification.IsReusable)
            throw new InvalidOperationException("Rule 1 OOTB 跳點差異包不可重用：" + string.Join("; ", verification.Issues));

        var timestamp = request.StartedAt.UtcDateTime.ToString("yyyyMMddTHHmmssfffZ");
        Directory.CreateDirectory(request.BackupRoot); // Must succeed before any Solutions mutation.
        var backup = Path.Combine(Path.GetFullPath(request.BackupRoot),
            $"{SafeSegment(request.SourceVersion)}-to-{SafeSegment(request.TargetVersion)}-{timestamp}-{request.AttemptId:N}");
        CopyDirectoryNew(request.SolutionsRoot, backup, includeNonXml: true);

        var attemptRoot = Path.GetFullPath(request.AttemptRoot);
        var sourceWork = Path.Combine(attemptRoot, "CustomerSourceWork");
        CopyDirectoryNew(request.CustomerBaselineRoot, sourceWork, includeNonXml: false);

        foreach (var xml in Directory.EnumerateFiles(request.SolutionsRoot, "*", SearchOption.AllDirectories)
                     .Where(path => string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase)))
            File.Delete(xml);
        await ExtractTargetDiffXmlAsync(request.Rule1ArchivePath, request.SolutionsRoot, cancellationToken);

        var errors = new List<AdaptedPackageError>();
        var reviews = new List<Rule2ManualReview>();
        var processed = 0;
        foreach (var pair in PackageXmlPathMatcher.Match(sourceWork, request.SolutionsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = pair.SourcePath is null ? null : AmlDocument.Load(pair.SourcePath);
                var target = pair.TargetPath is null ? null : AmlDocument.Load(pair.TargetPath);
                var template = source ?? target ?? throw new InvalidDataException("XML 配對沒有可讀輸入。 ");
                var result = Rule2AdaptationEngine.Apply(source ?? EmptyLike(template), target ?? EmptyLike(template),
                    request.Rule2Rules, pair.RelativePath);
                if (pair.SourcePath is not null)
                    await File.WriteAllTextAsync(pair.SourcePath, result.SourceWorkCopy.ToXml(), Utf8, cancellationToken);
                if (pair.TargetPath is not null)
                    await File.WriteAllTextAsync(pair.TargetPath, result.TargetWorkCopy.ToXml(), Utf8, cancellationToken);
                reviews.AddRange(result.ManualReviews.Select(review => review with
                {
                    SourcePath = review.SourcePath is null ? null : pair.RelativePath + review.SourcePath,
                    TargetPath = review.TargetPath is null ? null : pair.RelativePath + review.TargetPath
                }));
                processed++;
            }
            catch (Exception exception) when (exception is AmlParseException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add(new AdaptedPackageError(pair.RelativePath, exception.Message));
            }
        }

        var finished = (clock ?? (() => DateTimeOffset.UtcNow))();
        return new AdaptedPackageBuildResult(request.AttemptId,
            errors.Count == 0 && reviews.Count == 0 ? AdaptedPackageStatus.ReadyForFinalization : AdaptedPackageStatus.Blocked,
            request.SourceVersion, request.TargetVersion, sourceWork, Path.GetFullPath(request.SolutionsRoot), backup,
            Path.GetFullPath(request.Rule1ArchivePath), request.Rule1Requirement.ArchiveChecksum,
            request.Rule2Rules.PinnedVersions.ToArray(), request.Rule2Rules.EffectiveChecksum!, request.StartedAt, finished,
            processed, reviews.Count, errors, reviews);
    }

    private static void ValidateRequest(AdaptedPackageRequest request)
    {
        if (request.AttemptId == Guid.Empty) throw new ArgumentException("Rule 2 執行嘗試 ID 不可為空。 ", nameof(request));
        if (!string.Equals(request.SourceVersion, request.Rule1Requirement.SourceVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.TargetVersion, request.Rule1Requirement.TargetVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Rule 2 跳點版本與 Rule 1 差異包重用條件不相符。 ");
        if (!Directory.Exists(request.CustomerBaselineRoot) || !Directory.Exists(request.SolutionsRoot))
            throw new DirectoryNotFoundException("客戶 Package 基準與 Solutions 必須存在。 ");
        if (request.Rule2Rules.Status != RuleResolutionStatus.Resolved || request.Rule2Rules.Issues.Count != 0 ||
            request.Rule2Rules.PinnedVersions.Count == 0 || string.IsNullOrWhiteSpace(request.Rule2Rules.EffectiveChecksum))
            throw new InvalidOperationException("Rule 2 規則快照尚未解析或固定版本。 ");
        if (Directory.Exists(request.AttemptRoot) || File.Exists(request.AttemptRoot))
            throw new InvalidOperationException("Rule 2 執行嘗試必須寫入全新目錄。 ");
        var protectedRoots = new[] { request.CustomerBaselineRoot, request.SolutionsRoot, request.Rule1ArchivePath };
        if (protectedRoots.Any(path => Overlaps(path, request.AttemptRoot) || Overlaps(path, request.BackupRoot)))
            throw new InvalidOperationException("備份或嘗試目錄不得與輸入 Package 重疊。 ");
    }

    private static async Task ExtractTargetDiffXmlAsync(string archivePath, string solutionsRoot, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith("TargetDiff/", StringComparison.Ordinal) &&
                     !string.IsNullOrEmpty(entry.Name) && string.Equals(Path.GetExtension(entry.Name), ".xml", StringComparison.OrdinalIgnoreCase)))
        {
            var relative = entry.FullName["TargetDiff/".Length..];
            var destination = SafeDestination(solutionsRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = entry.Open();
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, token);
        }
    }

    private static void CopyDirectoryNew(string sourceRoot, string destinationRoot, bool includeNonXml)
    {
        if (Directory.Exists(destinationRoot) || File.Exists(destinationRoot))
            throw new IOException("備份或工作副本目的地已存在。 ");
        Directory.CreateDirectory(destinationRoot);
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            if (!includeNonXml && !string.Equals(Path.GetExtension(file), ".xml", StringComparison.OrdinalIgnoreCase)) continue;
            var relative = Path.GetRelativePath(sourceRoot, file);
            var destination = SafeDestination(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: false);
        }
    }

    private static string SafeDestination(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("封裝項目逸出 Solutions。 ");
        return destination;
    }
    private static AmlDocument EmptyLike(AmlDocument document)
    {
        var root = document.Root.CloneSubtree(); root.RemoveNodes();
        return AmlDocument.Parse(root.ToString(SaveOptions.DisableFormatting));
    }
    private static bool Overlaps(string left, string right)
    {
        var a = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar);
        var b = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
               a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    private static string SafeSegment(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}

public static class AdaptedPackageFinalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<AdaptedPackageBuildResult> FinalizeAsync(
        AdaptedPackageBuildResult build,
        string completionRoot,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (build.Status != AdaptedPackageStatus.ReadyForFinalization || build.Errors.Count != 0 ||
            build.ManualReviews.Count != 0 || build.ManualReviewCount != 0)
            throw new InvalidOperationException("仍有錯誤或人工確認時不得成為正式適配 Package。 ");
        if (Directory.Exists(completionRoot) || File.Exists(completionRoot))
            throw new InvalidOperationException("完成紀錄必須寫入全新且不可覆寫的目錄。 ");
        Directory.CreateDirectory(completionRoot);
        var manifestPath = Path.Combine(Path.GetFullPath(completionRoot), "completion-manifest.json");
        var completed = build with { Status = AdaptedPackageStatus.Completed, CompletionManifestPath = manifestPath };
        await using var stream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, new
        {
            state = "Completed", completed.AttemptId, completed.SourceVersion, completed.TargetVersion,
            completed.SourceWorkRoot, completed.SolutionsRoot, completed.SolutionsBackupRoot,
            completed.Rule1ArchivePath, completed.Rule1ArchiveChecksum, completed.Rule2RuleSets,
            completed.Rule2EffectiveChecksum, completed.StartedAt, completed.FinishedAt, completedAt,
            completed.XmlFilesProcessed, completed.ManualReviewCount
        }, JsonOptions, cancellationToken);
        return completed;
    }
}
