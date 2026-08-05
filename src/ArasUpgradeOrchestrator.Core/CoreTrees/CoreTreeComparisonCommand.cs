using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public enum CoreTreeComparisonCommandStatus
{
    Completed,
    Incomplete,
    Blocked,
    Failed
}

public sealed record CoreTreeComparisonCommandRequest(
    string CaseRoot,
    string Actor,
    CoreTreeInputEvidence Customer,
    CoreTreeInputEvidence SourceOotb,
    CoreTreeInputEvidence TargetOotb,
    string OutputRoot,
    CoreTreeServerTextRuleSet ServerTextRules,
    RetryEvidence? RetryEvidence = null,
    ActionConfirmation? Confirmation = null,
    IReadOnlyDictionary<string, bool>? Prerequisites = null);

public sealed record CoreTreeComparisonCommandResult(
    Guid CaseId,
    Guid AttemptId,
    CoreTreeComparisonCommandStatus CommandStatus,
    CoreTreeComparisonStatus? ComparisonStatus,
    SafetyLevel SafetyLevel,
    string Message,
    string SnapshotDigest,
    string OutputRoot,
    string HistoryPath,
    int ACount,
    int BCount,
    int CCount,
    int ManualReviewCount,
    int ErrorCount)
{
    public CoreTreeComparisonStatus Status => ComparisonStatus ?? CoreTreeComparisonStatus.Blocked;
}

public sealed class CoreTreeComparisonCommand
{
    public const string ActionId = "core-tree.compare";
    public const string ActionVersion = "1";
    public const string TaskId = "core-tree.compare";

    private readonly SafetyPolicy _safetyPolicy;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string _toolVersion;

    public CoreTreeComparisonCommand(SafetyPolicy safetyPolicy, Func<DateTimeOffset>? clock = null, string toolVersion = "core-tree-command/1")
    {
        _safetyPolicy = safetyPolicy ?? throw new ArgumentNullException(nameof(safetyPolicy));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _toolVersion = string.IsNullOrWhiteSpace(toolVersion) ? throw new ArgumentException(nameof(toolVersion)) : toolVersion;
    }

    public async Task<CoreTreeComparisonCommandResult> ExecuteAsync(
        CoreTreeComparisonCommandRequest commandRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandRequest);
        if (string.IsNullOrWhiteSpace(commandRequest.Actor)) throw new ArgumentException("Actor is required.", nameof(commandRequest));

        var caseStore = new CaseStore(commandRequest.CaseRoot);
        var history = new AppendOnlyHistoryStore(caseStore.ToolDataPath);
        CaseManifest manifest;
        try
        {
            manifest = await caseStore.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Blocked(Guid.Empty, CoreTreeComparisonCommandStatus.Blocked, SafetyLevel.Blocked, exception.Message, string.Empty, commandRequest.OutputRoot, history.Path, cancellationToken);
        }

        if (!string.Equals(manifest.SourceVersion, commandRequest.Customer.InnovatorVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifest.SourceVersion, commandRequest.SourceOotb.InnovatorVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifest.TargetVersion, commandRequest.TargetOotb.InnovatorVersion, StringComparison.OrdinalIgnoreCase))
            return await BlockedAsync(manifest.CaseId, CoreTreeComparisonCommandStatus.Blocked, SafetyLevel.Blocked, "Case and Core Tree version evidence do not match.", string.Empty, commandRequest.OutputRoot, history, commandRequest.Actor, cancellationToken);

        var snapshot = CreateSnapshot(manifest, commandRequest);
        var snapshotDigest = snapshot.ComputeDigest();
        CoreTreeComparisonRequest comparisonRequest;
        try
        {
            comparisonRequest = new CoreTreeComparisonRequest(
                Guid.NewGuid(),
                manifest.SourceVersion,
                manifest.TargetVersion,
                commandRequest.Customer,
                commandRequest.SourceOotb,
                commandRequest.TargetOotb,
                commandRequest.OutputRoot,
                commandRequest.ServerTextRules,
                _clock());
            CoreTreeInputValidator.ValidateInputs(comparisonRequest);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException or IOException)
        {
            return await BlockedAsync(manifest.CaseId, CoreTreeComparisonCommandStatus.Blocked, SafetyLevel.Blocked, exception.Message, snapshotDigest, commandRequest.OutputRoot, history, commandRequest.Actor, cancellationToken);
        }

        var prerequisites = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["case.loaded"] = true,
            ["inputs.valid"] = true
        };
        if (commandRequest.Prerequisites is not null)
            foreach (var prerequisite in commandRequest.Prerequisites) prerequisites[prerequisite.Key] = prerequisite.Value;

        var action = new ControlledAction(ActionId, ActionVersion, commandRequest.OutputRoot, snapshot.ComputeInputDigest(), true, prerequisites);
        var decision = _safetyPolicy.Evaluate(action);
        if (!SafetyPolicy.IsConfirmationValid(decision, commandRequest.Confirmation))
            return await BlockedAsync(manifest.CaseId, CoreTreeComparisonCommandStatus.Blocked, decision.Level, decision.Reason, snapshotDigest, commandRequest.OutputRoot, history, commandRequest.Actor, cancellationToken);

        var leaseManager = new DirectoryLeaseManager(caseStore.ToolDataPath);
        DirectoryLease commandLease;
        try
        {
            commandLease = await leaseManager.AcquireAsync([Path.Combine(caseStore.ToolDataPath, "core-tree-command")], cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return await BlockedAsync(manifest.CaseId, CoreTreeComparisonCommandStatus.Blocked, SafetyLevel.Blocked, exception.Message, snapshotDigest, commandRequest.OutputRoot, history, commandRequest.Actor, cancellationToken);
        }

        await using (commandLease)
        {
        var attempts = new ExecutionAttemptService(manifest.CaseId, history, _clock);
        await attempts.RecoverInterruptedAsync(commandRequest.Actor, cancellationToken);
        var attempt = await attempts.StartAsync(snapshot with { ActionVersion = ActionVersion }, commandRequest.Actor, commandRequest.RetryEvidence, cancellationToken);
        comparisonRequest = comparisonRequest with { AttemptId = attempt.AttemptId };
        try
        {
            var comparison = await CoreTreeComparisonBuilder.BuildAsync(comparisonRequest, leaseManager, _clock, cancellationToken);
            await attempts.SucceedAsync(attempt, commandRequest.Actor, Path.Combine(commandRequest.OutputRoot, comparison.Status == CoreTreeComparisonStatus.Completed ? "completion-manifest.json" : "incomplete-manifest.json"), cancellationToken);
            return CreateResult(manifest.CaseId, attempt.AttemptId, comparison, decision.Level, snapshotDigest, history.Path, string.Empty);
        }
        catch (OperationCanceledException exception)
        {
            await attempts.FailAsync(attempt, commandRequest.Actor, "Core Tree comparison was cancelled.", null, CancellationToken.None);
            return CreateFailure(manifest.CaseId, attempt.AttemptId, CoreTreeComparisonCommandStatus.Failed, decision.Level, exception.Message, snapshotDigest, commandRequest.OutputRoot, history.Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            await attempts.FailAsync(attempt, commandRequest.Actor, exception.Message, null, CancellationToken.None);
            return CreateFailure(manifest.CaseId, attempt.AttemptId, CoreTreeComparisonCommandStatus.Failed, decision.Level, exception.Message, snapshotDigest, commandRequest.OutputRoot, history.Path);
        }
        }
    }

    private ExecutionSnapshot CreateSnapshot(CaseManifest manifest, CoreTreeComparisonCommandRequest request) => new(
        TaskId,
        ActionId,
        ActionVersion,
        request.OutputRoot,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["caseId"] = manifest.CaseId.ToString("D"),
            ["sourceVersion"] = manifest.SourceVersion,
            ["targetVersion"] = manifest.TargetVersion,
            ["customerRoot"] = Path.GetFullPath(request.Customer.RootPath),
            ["customerEvidence"] = request.Customer.EvidenceReference,
            ["sourceOotbRoot"] = Path.GetFullPath(request.SourceOotb.RootPath),
            ["sourceOotbEvidence"] = request.SourceOotb.EvidenceReference,
            ["targetOotbRoot"] = Path.GetFullPath(request.TargetOotb.RootPath),
            ["targetOotbEvidence"] = request.TargetOotb.EvidenceReference,
            ["serverRuleVersion"] = request.ServerTextRules.Version,
            ["serverRuleChecksum"] = request.ServerTextRules.Checksum
        },
        _toolVersion,
        null);

    private static CoreTreeComparisonCommandResult CreateResult(Guid caseId, Guid attemptId, CoreTreeComparisonResult comparison, SafetyLevel safetyLevel, string snapshotDigest, string historyPath, string message) => new(
        caseId,
        attemptId,
        comparison.Status == CoreTreeComparisonStatus.Completed ? CoreTreeComparisonCommandStatus.Completed : CoreTreeComparisonCommandStatus.Incomplete,
        comparison.Status,
        safetyLevel,
        message,
        snapshotDigest,
        comparison.OutputRoot,
        historyPath,
        comparison.Items.Count(item => item.Classification == CoreTreeClassification.A),
        comparison.Items.Count(item => item.Classification == CoreTreeClassification.B),
        comparison.Items.Count(item => item.Classification == CoreTreeClassification.C),
        comparison.ManualReviews.Count,
        comparison.Errors.Count);

    private static CoreTreeComparisonCommandResult CreateFailure(Guid caseId, Guid attemptId, CoreTreeComparisonCommandStatus status, SafetyLevel safetyLevel, string message, string snapshotDigest, string outputRoot, string historyPath) => new(
        caseId, attemptId, status, CoreTreeComparisonStatus.Incomplete, safetyLevel, message, snapshotDigest, outputRoot, historyPath, 0, 0, 0, 0, 1);

    private async Task<CoreTreeComparisonCommandResult> BlockedAsync(Guid caseId, CoreTreeComparisonCommandStatus status, SafetyLevel safetyLevel, string message, string snapshotDigest, string outputRoot, AppendOnlyHistoryStore history, string actor, CancellationToken cancellationToken)
    {
        await history.AppendAsync(caseId, HistoryEventTypes.ActionBlocked, TaskId, actor, new { Reason = message, SnapshotDigest = snapshotDigest }, _clock(), cancellationToken: cancellationToken);
        return new(caseId, Guid.Empty, status, CoreTreeComparisonStatus.Blocked, safetyLevel, message, snapshotDigest, outputRoot, history.Path, 0, 0, 0, 0, 0);
    }

    private CoreTreeComparisonCommandResult Blocked(Guid caseId, CoreTreeComparisonCommandStatus status, SafetyLevel safetyLevel, string message, string snapshotDigest, string outputRoot, string historyPath, CancellationToken cancellationToken) =>
        new(caseId, Guid.Empty, status, CoreTreeComparisonStatus.Blocked, safetyLevel, message, snapshotDigest, outputRoot, historyPath, 0, 0, 0, 0, 0);
}
