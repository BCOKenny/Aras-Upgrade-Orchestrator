using System.Security.Cryptography;
using System.Text;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Rules;
using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.Packages;

public sealed record Rule1PreparationCommandRequest(
    string CaseRoot,
    string PreparationId,
    string AttemptId,
    string Actor,
    RuleSetResolutionResult RuleResolution,
    ActionConfirmation? Confirmation = null,
    PackageComparisonPreparation? DirectPreparation = null);

public sealed record Rule1PreparationCommandResult(
    Guid CaseId,
    string AttemptRoot,
    Rule1PreparationStatus Status,
    SafetyLevel SafetyLevel,
    string HistoryPath,
    string SnapshotDigest);

public sealed class Rule1PreparationCommand
{
    public const string ActionId = "package.rule1-preparation";
    public const string ActionVersion = "1";
    private readonly SafetyPolicy _safetyPolicy;
    private readonly Func<DateTimeOffset> _clock;

    public Rule1PreparationCommand(SafetyPolicy safetyPolicy, Func<DateTimeOffset>? clock = null)
    {
        _safetyPolicy = safetyPolicy ?? throw new ArgumentNullException(nameof(safetyPolicy));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<Rule1PreparationCommandResult> ExecuteAsync(Rule1PreparationCommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Actor) || string.IsNullOrWhiteSpace(request.AttemptId))
            throw new ArgumentException("Rule 1 預先比較需要操作者與 attempt 識別。", nameof(request));
        var caseStore = new CaseStore(request.CaseRoot);
        var manifest = await caseStore.LoadAsync(cancellationToken);
        var preparation = request.DirectPreparation ?? manifest.PackageComparisonPreparations.SingleOrDefault(item =>
            string.Equals(item.PreparationId, request.PreparationId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("案件未登錄指定的 Package 比較計畫，且 request 未提供完整直接輸入。 ");
        ValidatePreparation(request.PreparationId, preparation);
        if (request.RuleResolution.Status != RuleResolutionStatus.Resolved || request.RuleResolution.Issues.Count > 0 ||
            request.RuleResolution.PinnedVersions.Count == 0 || string.IsNullOrWhiteSpace(request.RuleResolution.EffectiveChecksum))
            throw new InvalidOperationException("Rule 1 預先比較需要可解析的規則快照；未發布或衝突規則不可自動推測。 ");

        var sourceRoot = ResolveCasePath(caseStore.CaseRoot, preparation.OotbSourceSolutionsRelativePath);
        var targetRoot = ResolveCasePath(caseStore.CaseRoot, preparation.OotbTargetSolutionsRelativePath);
        var patchRoot = ResolveCasePath(caseStore.CaseRoot, preparation.PatchSupportInputRelativePath);
        var attemptsRoot = ResolveCasePath(caseStore.CaseRoot, preparation.PreparationAttemptRelativePath);
        var attemptRoot = ResolveCasePath(attemptsRoot, request.AttemptId);
        if (!Directory.Exists(sourceRoot) || !Directory.Exists(targetRoot) || !Directory.Exists(patchRoot))
            throw new DirectoryNotFoundException("Rule 1 預先比較所登錄的 OOTB 或 Patch/Support 輸入目錄不存在。 ");
        if (Directory.Exists(attemptRoot) || File.Exists(attemptRoot))
            throw new InvalidOperationException("Rule 1 預先比較 attempt 必須使用不存在的新目錄。 ");
        if (Overlaps(attemptRoot, sourceRoot) || Overlaps(attemptRoot, targetRoot) || Overlaps(attemptRoot, patchRoot))
            throw new InvalidOperationException("Rule 1 預先比較輸出不得與任何輸入目錄重疊。 ");

        var sourceDigest = await PackageInputTreeDigest.ComputeAsync(sourceRoot, cancellationToken);
        var targetDigest = await PackageInputTreeDigest.ComputeAsync(targetRoot, cancellationToken);
        var patchDigest = await PackageInputTreeDigest.ComputeAsync(patchRoot, cancellationToken);
        var snapshotDigest = Digest(manifest.CaseId.ToString("D"), preparation.PreparationId, request.AttemptId, sourceDigest.TreeChecksum, targetDigest.TreeChecksum, patchDigest.TreeChecksum, request.RuleResolution.EffectiveChecksum!);
        var action = new ControlledAction(ActionId, ActionVersion, attemptRoot, snapshotDigest, true,
            new Dictionary<string, bool> { ["case.loaded"] = true, ["inputs.valid"] = true, ["rules.resolved"] = true });
        var decision = _safetyPolicy.Evaluate(action);
        if (!SafetyPolicy.IsConfirmationValid(decision, request.Confirmation))
            throw new InvalidOperationException(decision.Reason);

        var history = new AppendOnlyHistoryStore(caseStore.ToolDataPath);
        var leaseManager = new DirectoryLeaseManager(caseStore.ToolDataPath);
        await using var lease = await leaseManager.AcquireAsync([attemptRoot], cancellationToken);
        await history.AppendAsync(manifest.CaseId, HistoryEventTypes.Rule1PreparationStarted, request.AttemptId, request.Actor,
            new { preparation.PreparationId, preparation.SourceVersion, preparation.TargetVersion, SnapshotDigest = snapshotDigest }, _clock(), cancellationToken: cancellationToken);
        var build = await OotbHopDiffBuilder.BuildAsync(new OotbHopDiffRequest(Guid.NewGuid(), preparation.SourceVersion, preparation.TargetVersion,
            sourceRoot, targetRoot, Path.Combine(attemptRoot, "diff"), request.RuleResolution, _clock()), _clock, cancellationToken);
        var status = build.Status == OotbHopDiffBuildStatus.ReadyToPackage ? Rule1PreparationStatus.PendingVerification : Rule1PreparationStatus.Draft;
        var snapshot = new Rule1PreparationRuleSnapshot(request.RuleResolution.Steps, request.RuleResolution.PinnedVersions,
            request.RuleResolution.EffectiveChecksum, null);
        var receipt = new Rule1PreparationReceipt(manifest.CaseId, preparation.PreparationId, request.AttemptId, preparation.SourceVersion,
            preparation.TargetVersion, status, snapshot, sourceDigest, targetDigest, patchDigest, build.Summary, build.Errors, build.ManualReviews, _clock(), request.Actor);
        await Rule1PreparationReceiptStore.WriteAsync(attemptRoot, receipt, cancellationToken);
        await history.AppendAsync(manifest.CaseId,
            status == Rule1PreparationStatus.PendingVerification ? HistoryEventTypes.Rule1PreparationPendingVerification : HistoryEventTypes.Rule1PreparationDraft,
            request.AttemptId, request.Actor, new { preparation.PreparationId, status, build.Summary, SourceInputChecksum = sourceDigest.TreeChecksum, TargetInputChecksum = targetDigest.TreeChecksum, PatchSupportInputChecksum = patchDigest.TreeChecksum }, _clock(), cancellationToken: cancellationToken);
        return new Rule1PreparationCommandResult(manifest.CaseId, attemptRoot, status, decision.Level, history.Path, snapshotDigest);
    }

    private static string ResolveCasePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) throw new InvalidDataException("Package 比較路徑無效。 ");
        var fullRoot = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(fullRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Package 比較路徑逸出案件根目錄。 ");
        return path;
    }

    private static void ValidatePreparation(string preparationId, PackageComparisonPreparation preparation)
    {
        if (string.IsNullOrWhiteSpace(preparationId) ||
            !string.Equals(preparationId, preparation.PreparationId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Rule 1 預先比較的 preparationId 必須與直接輸入一致。 ");
        if (string.IsNullOrWhiteSpace(preparation.SourceVersion) || string.IsNullOrWhiteSpace(preparation.TargetVersion) ||
            string.Equals(preparation.SourceVersion, preparation.TargetVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Rule 1 預先比較必須提供不同且非空白的來源與目標版本。 ");
    }

    private static bool Overlaps(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
        left.StartsWith(Path.TrimEndingDirectorySeparator(right) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        right.StartsWith(Path.TrimEndingDirectorySeparator(left) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Digest(params string[] values) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));
}
