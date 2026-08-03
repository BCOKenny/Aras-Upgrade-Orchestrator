using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.Execution;

public sealed record ControlledExecutionRequest(
    ExecutionSnapshot Snapshot,
    ControlledAction Action,
    IReadOnlyList<string> WriteScopes,
    string Actor,
    RetryEvidence? RetryEvidence = null,
    ActionConfirmation? Confirmation = null);

public sealed record ControlledExecutionOutcome(
    SafetyDecision Decision,
    bool Executed,
    Guid? AttemptId,
    ExternalActionResult? Result);

public sealed class ControlledExecutionCoordinator
{
    private readonly Guid _caseId;
    private readonly SafetyPolicy _policy;
    private readonly DirectoryLeaseManager _leases;
    private readonly ExecutionAttemptService _attempts;
    private readonly AppendOnlyHistoryStore _history;
    private readonly IExternalActionExecutor _executor;
    private readonly Func<DateTimeOffset> _now;

    public ControlledExecutionCoordinator(
        Guid caseId,
        SafetyPolicy policy,
        DirectoryLeaseManager leases,
        ExecutionAttemptService attempts,
        AppendOnlyHistoryStore history,
        IExternalActionExecutor executor,
        Func<DateTimeOffset>? now = null)
    {
        _caseId = caseId;
        _policy = policy;
        _leases = leases;
        _attempts = attempts;
        _history = history;
        _executor = executor;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<ControlledExecutionOutcome> ExecuteAsync(ControlledExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (!SnapshotMatchesAction(request.Snapshot, request.Action))
        {
            var mismatchDecision = new SafetyDecision(
                SafetyLevel.Blocked,
                "執行快照與待執行動作的識別、版本、目標或輸入不一致。",
                request.Snapshot.ComputeDigest());
            await _history.AppendAsync(_caseId, HistoryEventTypes.ActionBlocked, request.Snapshot.TaskId, request.Actor, mismatchDecision, _now(), cancellationToken: cancellationToken);
            return new ControlledExecutionOutcome(mismatchDecision, false, null, null);
        }

        var decision = _policy.Evaluate(request.Action);
        if (decision.Level == SafetyLevel.Blocked)
        {
            await _history.AppendAsync(_caseId, HistoryEventTypes.ActionBlocked, request.Snapshot.TaskId, request.Actor, decision, _now(), cancellationToken: cancellationToken);
            return new ControlledExecutionOutcome(decision, false, null, null);
        }

        if (!SafetyPolicy.IsConfirmationValid(decision, request.Confirmation))
            return new ControlledExecutionOutcome(decision, false, null, null);

        if (decision.Level == SafetyLevel.SingleConfirmation)
        {
            await _history.AppendAsync(
                _caseId,
                HistoryEventTypes.ConfirmationRecorded,
                request.Snapshot.TaskId,
                request.Actor,
                request.Confirmation!,
                _now(),
                cancellationToken: cancellationToken);
        }

        var attempt = await _attempts.StartAsync(request.Snapshot, request.Actor, request.RetryEvidence, cancellationToken);
        try
        {
            await using var lease = await _leases.AcquireAsync(request.WriteScopes, cancellationToken);
            var result = await _executor.ExecuteAsync(
                new ExternalActionContext(
                    attempt.AttemptId,
                    request.Action.ActionId,
                    request.Action.ActionVersion,
                    request.Action.Target,
                    request.Snapshot.Inputs),
                cancellationToken);

            if (result.Succeeded)
                await _attempts.SucceedAsync(attempt, request.Actor, result.EvidenceReference ?? "executor-result", cancellationToken);
            else
                await _attempts.FailAsync(attempt, request.Actor, result.Message, result.EvidenceReference, cancellationToken);
            return new ControlledExecutionOutcome(decision, true, attempt.AttemptId, result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _attempts.FailAsync(attempt, request.Actor, exception.Message, cancellationToken: cancellationToken);
            throw;
        }
    }

    private static bool SnapshotMatchesAction(ExecutionSnapshot snapshot, ControlledAction action)
    {
        string snapshotTarget;
        string actionTarget;
        try
        {
            snapshotTarget = Path.GetFullPath(snapshot.Target);
            actionTarget = Path.GetFullPath(action.Target);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return string.Equals(snapshot.ActionId, action.ActionId, StringComparison.Ordinal) &&
               string.Equals(snapshot.ActionVersion, action.ActionVersion, StringComparison.Ordinal) &&
               string.Equals(snapshotTarget, actionTarget, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(snapshot.ComputeInputDigest(), action.InputDigest, StringComparison.OrdinalIgnoreCase);
    }
}
