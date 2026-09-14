using System.Text;
using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Packages;

public sealed record CustomerPatchPreparationScaffoldHop(string SourceVersion, string TargetVersion);

public sealed record CustomerPatchPreparationScaffoldRequest(
    string CaseRoot,
    string PreparationId,
    string CustomerPackageBaselineId,
    string CustomerPackageBaselineActualVersion,
    string Actor,
    IReadOnlyList<CustomerPatchPreparationScaffoldHop> Hops);

public sealed record CustomerPatchPreparationScaffoldResult(
    string Status,
    string PreparationPath,
    string PlanPath,
    IReadOnlyList<string> EvidenceRequestPaths);

public sealed class CustomerPatchPreparationScaffoldCommand
{
    private const string PackageUpgradeDirectoryName = "package-upgrade";
    private const string EvidenceRequestFileName = "patch-support-evidence.request.json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<CustomerPatchPreparationScaffoldResult> ExecuteAsync(
        CustomerPatchPreparationScaffoldRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prepared = Prepare(request);

        if (!Directory.Exists(prepared.CaseRoot))
            throw new InvalidDataException("案件根目錄不存在。");
        if (!Directory.Exists(prepared.PackageUpgradePath))
            throw new InvalidDataException("案件根目錄下必須預先存在 package-upgrade 目錄。");
        if (Directory.Exists(prepared.PreparationPath) || File.Exists(prepared.PreparationPath))
            throw new InvalidOperationException("目標 preparation 目錄已存在，禁止覆寫或修復。");

        var planJson = SerializeAndValidate(prepared.Plan);
        var evidenceJson = prepared.EvidenceRequests
            .Select(SerializeAndValidate)
            .ToArray();
        var stagingPath = Path.Combine(prepared.PackageUpgradePath, $".{prepared.PreparationId}.staging-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingPath);
            await CreateDirectoriesAsync(stagingPath, prepared, cancellationToken);
            var planPath = Path.Combine(stagingPath, "manifests", "package-preparation-plan.template.json");
            await WriteNewJsonAsync(planPath, planJson, cancellationToken);

            for (var index = 0; index < prepared.EvidenceRequests.Count; index++)
            {
                var requestPath = Path.Combine(stagingPath, ToSystemPath(prepared.EvidenceRequestRelativePaths[index]));
                await WriteNewJsonAsync(requestPath, evidenceJson[index], cancellationToken);
            }

            await ValidateStagedJsonAsync(planPath, cancellationToken);
            foreach (var relativePath in prepared.EvidenceRequestRelativePaths)
                await ValidateStagedJsonAsync(Path.Combine(stagingPath, ToSystemPath(relativePath)), cancellationToken);

            Directory.Move(stagingPath, prepared.PreparationPath);
        }
        catch
        {
            // Keep the staging workspace for diagnosis. The target preparation path has not been created.
            throw;
        }

        return new CustomerPatchPreparationScaffoldResult(
            "Created",
            prepared.PreparationPath,
            Path.Combine(prepared.PreparationPath, "manifests", "package-preparation-plan.template.json"),
            prepared.EvidenceRequestRelativePaths
                .Select(relativePath => Path.Combine(prepared.PreparationPath, ToSystemPath(relativePath)))
                .ToArray());
    }

    private static PreparedScaffold Prepare(CustomerPatchPreparationScaffoldRequest request)
    {
        var caseRoot = RequireExistingRoot(request.CaseRoot, "caseRoot");
        RequireSafeSegment(request.PreparationId, "preparationId");
        RequireSafeSegment(request.CustomerPackageBaselineId, "customerPackageBaselineId");
        RequireText(request.CustomerPackageBaselineActualVersion, "customerPackageBaselineActualVersion");
        RequireText(request.Actor, "actor");
        if (request.Hops is null || request.Hops.Count == 0)
            throw new InvalidDataException("hops 至少需要一筆完整跳點。");

        var hops = request.Hops.Select((hop, index) => PrepareHop(hop, index, request)).ToArray();
        if (hops.Select(hop => hop.HopSlug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != hops.Length)
            throw new InvalidDataException("完整跳點不可重複，且不得產生相同 hopSlug。");

        var packageUpgradePath = Path.Combine(caseRoot, PackageUpgradeDirectoryName);
        var preparationPath = Path.Combine(packageUpgradePath, request.PreparationId);
        var baselineInput = RelativePath(request.PreparationId, "customer-package-baseline", request.CustomerPackageBaselineId, "input");
        var baselineEvidence = RelativePath(request.PreparationId, "customer-package-baseline", request.CustomerPackageBaselineId, "evidence");
        var planHops = hops.Select(hop => hop.ToPlanHop(
            request.CustomerPackageBaselineId,
            request.CustomerPackageBaselineActualVersion,
            baselineInput)).ToArray();
        var plan = new CustomerPatchPreparationPlan(
            request.PreparationId,
            "CUSTOMER_PATCH_COMPARISON",
            "DIRECTORY_SCAFFOLD_ONLY",
            request.CustomerPackageBaselineId,
            request.CustomerPackageBaselineActualVersion,
            request.Actor,
            planHops,
            [
                "僅建立非正式 request 範本。",
                "不得讀取或修改實際輸入。",
                "不得執行 Package 比較、DB、Aras Export 或升級工具。"
            ]);
        var evidenceRequests = hops.Select(hop => new CustomerPatchEvidenceRequestDocument(
            caseRoot,
            request.PreparationId,
            hop.HopSlug,
            new CustomerPatchComparisonSource(request.CustomerPackageBaselineId, request.CustomerPackageBaselineActualVersion, baselineInput),
            new CustomerPatchComparisonTarget(hop.TargetVersion, hop.PatchSupportInput),
            hop.Evidence,
            hop.PatchSupportInput,
            request.Actor)).ToArray();
        var evidencePaths = hops.Select(hop => Path.Combine(
            "hops", hop.HopSlug, "customer-patch-comparison", "evidence", EvidenceRequestFileName).Replace('\\', '/')).ToArray();

        return new PreparedScaffold(
            caseRoot,
            packageUpgradePath,
            preparationPath,
            request.PreparationId,
            request.CustomerPackageBaselineId,
            baselineInput,
            baselineEvidence,
            hops,
            plan,
            evidenceRequests,
            evidencePaths);
    }

    private static PreparedHop PrepareHop(CustomerPatchPreparationScaffoldHop? hop, int index, CustomerPatchPreparationScaffoldRequest request)
    {
        if (hop is null)
            throw new InvalidDataException($"hops[{index}] 不可為空白。");
        RequireText(hop.SourceVersion, $"hops[{index}].sourceVersion");
        RequireText(hop.TargetVersion, $"hops[{index}].targetVersion");
        if (string.Equals(hop.SourceVersion.Trim(), hop.TargetVersion.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"hops[{index}] 的 sourceVersion 與 targetVersion 不可相同。");

        var sourceSlug = ToSlug(hop.SourceVersion);
        var targetSlug = ToSlug(hop.TargetVersion);
        if (string.IsNullOrEmpty(sourceSlug) || string.IsNullOrEmpty(targetSlug))
            throw new InvalidDataException($"hops[{index}] 無法產生安全的 hop slug。");
        var hopSlug = $"{sourceSlug}-to-{targetSlug}";
        var comparisonRoot = RelativePath(request.PreparationId, "hops", hopSlug, "customer-patch-comparison");
        return new PreparedHop(
            hop.SourceVersion.Trim(),
            hop.TargetVersion.Trim(),
            hopSlug,
            $"{comparisonRoot}/patch-support-input",
            $"{comparisonRoot}/evidence",
            $"{comparisonRoot}/comparison-attempts",
            $"{comparisonRoot}/candidate-package-imports");
    }

    private static async Task CreateDirectoriesAsync(string stagingPath, PreparedScaffold prepared, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.Combine(stagingPath, "customer-package-baseline", prepared.BaselineId, "input"));
        Directory.CreateDirectory(Path.Combine(stagingPath, "customer-package-baseline", prepared.BaselineId, "evidence"));
        Directory.CreateDirectory(Path.Combine(stagingPath, "manifests"));
        foreach (var hop in prepared.Hops)
        {
            var root = Path.Combine(stagingPath, "hops", hop.HopSlug, "customer-patch-comparison");
            Directory.CreateDirectory(Path.Combine(root, "patch-support-input"));
            Directory.CreateDirectory(Path.Combine(root, "evidence"));
            Directory.CreateDirectory(Path.Combine(root, "comparison-attempts"));
            Directory.CreateDirectory(Path.Combine(root, "candidate-package-imports"));
        }
        await Task.CompletedTask;
    }

    private static async Task WriteNewJsonAsync(string path, string json, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(json.AsMemory(), cancellationToken);
    }

    private static async Task ValidateStagedJsonAsync(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"輸出 JSON 不可為空白：{path}");
        using var _ = JsonDocument.Parse(text);
    }

    private static string SerializeAndValidate<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Json);
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("輸出 JSON 不可為空白。");
        using var _ = JsonDocument.Parse(json);
        return json;
    }

    private static string RequireExistingRoot(string value, string name)
    {
        RequireText(value, name);
        return Path.GetFullPath(value);
    }

    private static void RequireSafeSegment(string value, string name)
    {
        RequireText(value, name);
        if (value is "." or ".." || Path.IsPathRooted(value) || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains('/') || value.Contains('\\') || value.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException($"{name} 必須是安全的單一目錄名稱。");
    }

    private static void RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{name} 不可空白。");
    }

    private static string RelativePath(string preparationId, params string[] segments) =>
        string.Join('/', (new[] { PackageUpgradeDirectoryName, preparationId }).Concat(segments));

    private static string ToSystemPath(string relativePath) => relativePath.Replace('/', Path.DirectorySeparatorChar);

    private static string ToSlug(string version)
    {
        var builder = new StringBuilder();
        var previousHyphen = false;
        foreach (var character in version.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character == '.')
            {
                builder.Append(character);
                previousHyphen = false;
            }
            else if (!previousHyphen)
            {
                builder.Append('-');
                previousHyphen = true;
            }
        }
        return builder.ToString().Trim('-');
    }

    private sealed record PreparedScaffold(
        string CaseRoot,
        string PackageUpgradePath,
        string PreparationPath,
        string PreparationId,
        string BaselineId,
        string BaselineInput,
        string BaselineEvidence,
        IReadOnlyList<PreparedHop> Hops,
        CustomerPatchPreparationPlan Plan,
        IReadOnlyList<CustomerPatchEvidenceRequestDocument> EvidenceRequests,
        IReadOnlyList<string> EvidenceRequestRelativePaths);

    private sealed record PreparedHop(
        string SourceVersion,
        string TargetVersion,
        string HopSlug,
        string PatchSupportInput,
        string Evidence,
        string ComparisonAttempts,
        string CandidatePackageImports)
    {
        public CustomerPatchPreparationPlanHop ToPlanHop(string baselineId, string actualVersion, string baselineInput) => new(
            SourceVersion,
            TargetVersion,
            HopSlug,
            "PENDING",
            HopSlug,
            baselineInput,
            PatchSupportInput,
            Evidence,
            ComparisonAttempts,
            CandidatePackageImports,
            new CustomerPatchPreparationComparison(
                new CustomerPatchPreparationSource("CustomerBaseline", baselineId, actualVersion, baselineInput),
                new CustomerPatchPreparationTarget("TargetPackage", TargetVersion, PatchSupportInput)));
    }

    private sealed record CustomerPatchPreparationPlan(
        string PreparationId,
        string Workflow,
        string ExecutionMode,
        string CustomerPackageBaselineId,
        string CustomerPackageBaselineActualVersion,
        string Actor,
        IReadOnlyList<CustomerPatchPreparationPlanHop> Hops,
        IReadOnlyList<string> Constraints);

    private sealed record CustomerPatchPreparationPlanHop(
        string SourceVersion,
        string TargetVersion,
        string HopSlug,
        string Status,
        string ComparisonId,
        string CustomerBaselineInput,
        string PatchSupportInput,
        string Evidence,
        string ComparisonAttempts,
        string CandidatePackageImports,
        CustomerPatchPreparationComparison Comparison);

    private sealed record CustomerPatchPreparationComparison(CustomerPatchPreparationSource Source, CustomerPatchPreparationTarget Target);
    private sealed record CustomerPatchPreparationSource(string Role, string? BaselineId, string? ActualVersion, string? InputRelativePath);
    private sealed record CustomerPatchPreparationTarget(string Role, string TargetRelease, string InputRelativePath);
    private sealed record CustomerPatchEvidenceRequestDocument(
        string CaseRoot,
        string PreparationId,
        string HopId,
        CustomerPatchComparisonSource Source,
        CustomerPatchComparisonTarget Target,
        string EvidenceRelativePath,
        string PatchSupportSource,
        string Actor);
}
