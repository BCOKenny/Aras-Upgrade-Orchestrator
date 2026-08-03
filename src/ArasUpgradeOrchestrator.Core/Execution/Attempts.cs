using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Execution;

public enum RetryBasis
{
    VerifiedIdempotency,
    RolledBack,
    RestoredCheckpoint
}

public sealed record RetryEvidence(RetryBasis Basis, string EvidenceReference)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EvidenceReference))
            throw new InvalidOperationException("安全重試必須有可追溯證據。 ");
    }
}

public sealed record ExecutionSnapshot(
    string TaskId,
    string ActionId,
    string ActionVersion,
    string Target,
    IReadOnlyDictionary<string, string> Inputs,
    string ToolVersion,
    string? Checksum)
{
    public string ComputeInputDigest()
    {
        var json = JsonSerializer.Serialize(Inputs.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(), AppendOnlyHistoryStore.JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public string ComputeDigest()
    {
        var json = JsonSerializer.Serialize(this, AppendOnlyHistoryStore.JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}

public sealed record AttemptStartedPayload(
    Guid AttemptId,
    int Sequence,
    ExecutionSnapshot Snapshot,
    string SnapshotDigest,
    RetryEvidence? RetryEvidence);

public sealed record AttemptResultPayload(Guid AttemptId, string? EvidenceReference, string? Message);

public enum AttemptState
{
    Running,
    Succeeded,
    Failed,
    Interrupted
}

public sealed record AttemptView(Guid AttemptId, string TaskId, int Sequence, AttemptState State, ExecutionSnapshot Snapshot);

public sealed class ExecutionAttemptService
{
    private readonly Guid _caseId;
    private readonly AppendOnlyHistoryStore _history;
    private readonly Func<DateTimeOffset> _now;

    public ExecutionAttemptService(Guid caseId, AppendOnlyHistoryStore history, Func<DateTimeOffset>? now = null)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("案件識別不可為空。", nameof(caseId));
        _caseId = caseId;
        _history = history;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<AttemptView> StartAsync(
        ExecutionSnapshot snapshot,
        string actor,
        RetryEvidence? retryEvidence = null,
        CancellationToken cancellationToken = default)
    {
        var attempts = await GetAttemptsAsync(snapshot.TaskId, cancellationToken);
        if (attempts.LastOrDefault() is { } latest)
        {
            if (latest.State == AttemptState.Running)
                throw new InvalidOperationException("任務已有進行中的執行嘗試。 ");
            if (latest.State == AttemptState.Succeeded)
                throw new InvalidOperationException("已完成任務不得建立覆蓋性重跑。 ");
            if (retryEvidence is null)
                throw new InvalidOperationException("失敗或中斷後必須證明 Idempotency、Rollback 或已回到指定檢查點。 ");
            retryEvidence.Validate();
        }

        var attemptId = Guid.NewGuid();
        var payload = new AttemptStartedPayload(attemptId, attempts.Count + 1, snapshot, snapshot.ComputeDigest(), retryEvidence);
        await _history.AppendAsync(_caseId, HistoryEventTypes.AttemptStarted, snapshot.TaskId, actor, payload, _now(), cancellationToken: cancellationToken);
        return new AttemptView(attemptId, snapshot.TaskId, payload.Sequence, AttemptState.Running, snapshot);
    }

    public Task SucceedAsync(AttemptView attempt, string actor, string evidenceReference, CancellationToken cancellationToken = default) =>
        FinishAsync(attempt, actor, HistoryEventTypes.AttemptSucceeded, new AttemptResultPayload(attempt.AttemptId, evidenceReference, null), cancellationToken);

    public Task FailAsync(AttemptView attempt, string actor, string message, string? evidenceReference = null, CancellationToken cancellationToken = default) =>
        FinishAsync(attempt, actor, HistoryEventTypes.AttemptFailed, new AttemptResultPayload(attempt.AttemptId, evidenceReference, message), cancellationToken);

    public async Task<int> RecoverInterruptedAsync(string actor, CancellationToken cancellationToken = default)
    {
        var attempts = await GetAttemptsAsync(null, cancellationToken);
        var running = attempts.Where(attempt => attempt.State == AttemptState.Running).ToArray();
        foreach (var attempt in running)
        {
            await FinishAsync(
                attempt,
                actor,
                HistoryEventTypes.AttemptInterrupted,
                new AttemptResultPayload(attempt.AttemptId, null, "重新開啟案件時偵測到未完成嘗試；不自動續跑。"),
                cancellationToken);
        }

        return running.Length;
    }

    public async Task<IReadOnlyList<AttemptView>> GetAttemptsAsync(string? taskId = null, CancellationToken cancellationToken = default)
    {
        var started = new Dictionary<Guid, (string TaskId, AttemptStartedPayload Payload)>();
        var states = new Dictionary<Guid, AttemptState>();

        await foreach (var entry in _history.ReadAllAsync(cancellationToken))
        {
            if (entry.CaseId != _caseId) continue;
            if (entry.EventType == HistoryEventTypes.AttemptStarted)
            {
                var payload = entry.Payload.Deserialize<AttemptStartedPayload>(AppendOnlyHistoryStore.JsonOptions)
                    ?? throw new InvalidDataException("執行嘗試開始事件缺少內容。 ");
                started[payload.AttemptId] = (entry.SubjectId, payload);
                states[payload.AttemptId] = AttemptState.Running;
            }
            else if (TryGetTerminalState(entry.EventType, out var state))
            {
                var payload = entry.Payload.Deserialize<AttemptResultPayload>(AppendOnlyHistoryStore.JsonOptions)
                    ?? throw new InvalidDataException("執行嘗試結果事件缺少內容。 ");
                if (started.ContainsKey(payload.AttemptId)) states[payload.AttemptId] = state;
            }
        }

        return started.Values
            .Where(item => taskId is null || string.Equals(item.TaskId, taskId, StringComparison.Ordinal))
            .Select(item => new AttemptView(item.Payload.AttemptId, item.TaskId, item.Payload.Sequence, states[item.Payload.AttemptId], item.Payload.Snapshot))
            .OrderBy(item => item.TaskId, StringComparer.Ordinal)
            .ThenBy(item => item.Sequence)
            .ToArray();
    }

    private async Task FinishAsync(AttemptView attempt, string actor, string eventType, AttemptResultPayload result, CancellationToken cancellationToken)
    {
        var current = (await GetAttemptsAsync(attempt.TaskId, cancellationToken)).SingleOrDefault(item => item.AttemptId == attempt.AttemptId)
            ?? throw new InvalidOperationException("找不到執行嘗試。 ");
        if (current.State != AttemptState.Running) throw new InvalidOperationException("執行嘗試已有終止結果，不得覆寫。 ");
        await _history.AppendAsync(_caseId, eventType, attempt.TaskId, actor, result, _now(), cancellationToken: cancellationToken);
    }

    private static bool TryGetTerminalState(string eventType, out AttemptState state)
    {
        state = eventType switch
        {
            HistoryEventTypes.AttemptSucceeded => AttemptState.Succeeded,
            HistoryEventTypes.AttemptFailed => AttemptState.Failed,
            HistoryEventTypes.AttemptInterrupted => AttemptState.Interrupted,
            _ => default
        };
        return eventType is HistoryEventTypes.AttemptSucceeded or HistoryEventTypes.AttemptFailed or HistoryEventTypes.AttemptInterrupted;
    }
}
