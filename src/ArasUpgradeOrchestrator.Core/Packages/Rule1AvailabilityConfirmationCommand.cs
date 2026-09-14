using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Rules;
using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.Packages;

public sealed record Rule1AvailabilityConfirmationRequest(
    string CaseRoot,
    string PreparationId,
    string AttemptRelativePath,
    string ArtifactRelativePath,
    string Actor,
    RuleSetResolutionResult PublishedRuleResolution,
    ActionConfirmation? Confirmation = null);

public sealed record Rule1AvailabilityConfirmationResult(
    Guid CaseId,
    bool Completed,
    string? ArchivePath,
    string? ArchiveChecksum,
    SafetyLevel SafetyLevel,
    string HistoryPath,
    string Message);

public sealed class Rule1AvailabilityConfirmationCommand
{
    public const string ActionId = "package.rule1-availability-confirmation";
    public const string ActionVersion = "1";
    private readonly SafetyPolicy _safetyPolicy;
    private readonly Func<DateTimeOffset> _clock;

    public Rule1AvailabilityConfirmationCommand(SafetyPolicy safetyPolicy, Func<DateTimeOffset>? clock = null)
    {
        _safetyPolicy = safetyPolicy ?? throw new ArgumentNullException(nameof(safetyPolicy));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<Rule1AvailabilityConfirmationResult> ExecuteAsync(Rule1AvailabilityConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var caseStore = new CaseStore(request.CaseRoot);
        var manifest = await caseStore.LoadAsync(cancellationToken);
        var history = new AppendOnlyHistoryStore(caseStore.ToolDataPath);
        var preparation = manifest.PackageComparisonPreparations.SingleOrDefault(item => string.Equals(item.PreparationId, request.PreparationId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("案件清單找不到指定的 Package 比較計畫。 ");
        var attemptRoot = ResolveCasePath(caseStore.CaseRoot, request.AttemptRelativePath);
        var artifactPath = ResolveCasePath(caseStore.CaseRoot, request.ArtifactRelativePath);
        var receipt = await Rule1PreparationReceiptStore.LoadAsync(attemptRoot, cancellationToken);
        if (receipt.Status != Rule1PreparationStatus.PendingVerification || receipt.Errors.Count > 0 || receipt.ManualReviews.Count > 0)
            return await BlockedAsync("Rule 1 預先比較 receipt 尚不可確認。 ");
        if (!string.Equals(receipt.PreparationId, preparation.PreparationId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(receipt.SourceVersion, preparation.SourceVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(receipt.TargetVersion, preparation.TargetVersion, StringComparison.OrdinalIgnoreCase))
            return await BlockedAsync("Rule 1 預先比較 receipt 與案件登錄跳點不相符。 ");
        if (request.PublishedRuleResolution.Status != RuleResolutionStatus.Resolved || request.PublishedRuleResolution.Issues.Count > 0 ||
            request.PublishedRuleResolution.PinnedVersions.Count == 0 || string.IsNullOrWhiteSpace(request.PublishedRuleResolution.EffectiveChecksum) ||
            !SameRuleSets(receipt.RuleSnapshot.PublishedRuleSets, request.PublishedRuleResolution.PinnedVersions) ||
            !string.Equals(receipt.RuleSnapshot.EffectiveRuleChecksum, request.PublishedRuleResolution.EffectiveChecksum, StringComparison.OrdinalIgnoreCase))
            return await BlockedAsync("已發布 Rule 1 規則版本或 Checksum 與 receipt 不相符。 ");
        var source = await PackageInputTreeDigest.ComputeAsync(ResolveCasePath(caseStore.CaseRoot, preparation.OotbSourceSolutionsRelativePath), cancellationToken);
        var target = await PackageInputTreeDigest.ComputeAsync(ResolveCasePath(caseStore.CaseRoot, preparation.OotbTargetSolutionsRelativePath), cancellationToken);
        var patch = await PackageInputTreeDigest.ComputeAsync(ResolveCasePath(caseStore.CaseRoot, preparation.PatchSupportInputRelativePath), cancellationToken);
        if (!Same(source, receipt.SourceInput) || !Same(target, receipt.TargetInput) || !Same(patch, receipt.PatchSupportInput))
            return await BlockedAsync("Package 輸入在 Rule 1 預先比較後已變動。 ");
        if (File.Exists(artifactPath) || Directory.Exists(artifactPath) || IsUnder(artifactPath, attemptRoot))
            return await BlockedAsync("Rule 1 artifact 輸出必須是 attempt 外不存在的新檔案。 ");
        var action = new ControlledAction(ActionId, ActionVersion, artifactPath, receipt.SourceInput.TreeChecksum,
            true, new Dictionary<string, bool> { ["receipt.pending-verification"] = true, ["inputs.unchanged"] = true, ["rules.published"] = true });
        var decision = _safetyPolicy.Evaluate(action);
        if (!SafetyPolicy.IsConfirmationValid(decision, request.Confirmation)) return await BlockedAsync(decision.Reason, decision.Level);
        var diffRoot = Path.Combine(attemptRoot, "diff");
        var build = new OotbHopDiffBuildResult(Guid.TryParse(receipt.AttemptId, out var id) ? id : Guid.NewGuid(), OotbHopDiffBuildStatus.ReadyToPackage,
            receipt.SourceVersion, receipt.TargetVersion, receipt.SourceInput.RootPath, receipt.TargetInput.RootPath, diffRoot,
            Path.Combine(diffRoot, "SourceDiff"), Path.Combine(diffRoot, "TargetDiff"), request.PublishedRuleResolution.PinnedVersions,
            request.PublishedRuleResolution.EffectiveChecksum!, receipt.CreatedAt, _clock(), receipt.Summary, receipt.Errors, receipt.ManualReviews);
        var artifact = await OotbHopDiffPackager.PackageAsync(build, artifactPath, _clock(), cancellationToken);
        var verification = await OotbHopDiffArtifactVerifier.VerifyAsync(artifactPath, new OotbHopDiffReuseRequirement(receipt.SourceVersion, receipt.TargetVersion,
            request.PublishedRuleResolution.PinnedVersions, request.PublishedRuleResolution.EffectiveChecksum!, artifact.ArchiveChecksum!), cancellationToken);
        if (!verification.IsReusable) throw new InvalidDataException("剛建立的 Rule 1 artifact 無法驗證：" + string.Join("；", verification.Issues));
        await history.AppendAsync(manifest.CaseId, HistoryEventTypes.Rule1AvailabilityConfirmationCompleted, receipt.AttemptId, request.Actor,
            new { preparation.PreparationId, request.ArtifactRelativePath, artifact.ArchiveChecksum, request.PublishedRuleResolution.EffectiveChecksum }, _clock(), cancellationToken: cancellationToken);
        return new(manifest.CaseId, true, artifact.ArchivePath, artifact.ArchiveChecksum, decision.Level, history.Path, "Rule 1 artifact 已完成可用性確認。 ");

        async Task<Rule1AvailabilityConfirmationResult> BlockedAsync(string message, SafetyLevel level = SafetyLevel.Blocked)
        {
            await history.AppendAsync(manifest.CaseId, HistoryEventTypes.Rule1AvailabilityConfirmationBlocked, request.PreparationId, request.Actor,
                new { Reason = message, request.AttemptRelativePath }, _clock(), cancellationToken: cancellationToken);
            return new(manifest.CaseId, false, null, null, level, history.Path, message);
        }
    }

    private static bool Same(PackageInputTreeDigestResult current, PackageInputTreeDigestResult receipt) => current.FileCount == receipt.FileCount && current.TotalBytes == receipt.TotalBytes && string.Equals(current.TreeChecksum, receipt.TreeChecksum, StringComparison.OrdinalIgnoreCase);
    private static bool SameRuleSets(IReadOnlyList<RuleSetVersionReference>? left, IReadOnlyList<RuleSetVersionReference> right) => left is not null && left.OrderBy(x => x.RuleSetId).ThenBy(x => x.Version).SequenceEqual(right.OrderBy(x => x.RuleSetId).ThenBy(x => x.Version));
    private static string ResolveCasePath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidDataException("Package 路徑必須相對於案件根目錄。 ");
        var fullRoot = Path.GetFullPath(root); var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(fullRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Package 路徑逸出案件根目錄。 ");
        return path;
    }
    private static bool IsUnder(string path, string root) => path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
