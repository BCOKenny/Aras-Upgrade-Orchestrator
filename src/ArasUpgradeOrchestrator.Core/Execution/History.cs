using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Execution;

public static class HistoryEventTypes
{
    public const string AttemptStarted = "attempt.started";
    public const string AttemptSucceeded = "attempt.succeeded";
    public const string AttemptFailed = "attempt.failed";
    public const string AttemptInterrupted = "attempt.interrupted";
    public const string ConfirmationRecorded = "confirmation.recorded";
    public const string ActionBlocked = "action.blocked";
    public const string CorrectionRecorded = "correction.recorded";
    public const string CustomerPackageFlowLocked = "customer-package.flow.locked";
    public const string CustomerPackageFlowRolledBack = "customer-package.flow.rolled-back";
    public const string CustomerPackageFlowCompleted = "customer-package.flow.completed";
}

public sealed record HistoryEntry(
    Guid EventId,
    Guid CaseId,
    DateTimeOffset OccurredAt,
    string EventType,
    string SubjectId,
    string Actor,
    JsonElement Payload,
    Guid? CorrectsEventId = null);

public sealed record CorrectionPayload(string Reason, JsonElement CorrectedContent);

public sealed class AppendOnlyHistoryStore
{
    public const string FileName = "history.jsonl";
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _path;
    private readonly SemaphoreSlim _appendLock = new(1, 1);

    public AppendOnlyHistoryStore(string toolDataDirectory)
    {
        if (string.IsNullOrWhiteSpace(toolDataDirectory)) throw new ArgumentException("工具資料目錄不可為空。", nameof(toolDataDirectory));
        var directory = System.IO.Path.GetFullPath(toolDataDirectory);
        Directory.CreateDirectory(directory);
        _path = System.IO.Path.Combine(directory, FileName);
    }

    public string Path => _path;

    public async Task<HistoryEntry> AppendAsync<TPayload>(
        Guid caseId,
        string eventType,
        string subjectId,
        string actor,
        TPayload payload,
        DateTimeOffset occurredAt,
        Guid? correctsEventId = null,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("案件識別不可為空。", nameof(caseId));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("事件類型不可為空。", nameof(eventType));
        if (string.IsNullOrWhiteSpace(subjectId)) throw new ArgumentException("事件主體不可為空。", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作者不可為空。", nameof(actor));

        var entry = new HistoryEntry(
            Guid.NewGuid(),
            caseId,
            occurredAt,
            eventType,
            subjectId,
            actor,
            JsonSerializer.SerializeToElement(payload, JsonOptions),
            correctsEventId);

        await _appendLock.WaitAsync(cancellationToken);
        try
        {
            await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            await JsonSerializer.SerializeAsync(stream, entry, JsonOptions, cancellationToken);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _appendLock.Release();
        }

        return entry;
    }

    public async IAsyncEnumerable<HistoryEntry> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) yield break;
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            yield return JsonSerializer.Deserialize<HistoryEntry>(line, JsonOptions)
                ?? throw new InvalidDataException("執行歷程包含空事件。 ");
        }
    }

    public async Task<HistoryEntry> AppendCorrectionAsync(
        Guid caseId,
        Guid originalEventId,
        string subjectId,
        string actor,
        string reason,
        object correctedContent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        HistoryEntry? original = null;
        await foreach (var entry in ReadAllAsync(cancellationToken))
        {
            if (entry.EventId == originalEventId) original = entry;
        }

        if (original is null || original.CaseId != caseId)
            throw new InvalidOperationException("更正紀錄必須指向同一案件中已存在的原始事件。 ");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("更正原因不可為空。", nameof(reason));

        return await AppendAsync(
            caseId,
            HistoryEventTypes.CorrectionRecorded,
            subjectId,
            actor,
            new CorrectionPayload(reason, JsonSerializer.SerializeToElement(correctedContent, JsonOptions)),
            occurredAt,
            originalEventId,
            cancellationToken);
    }
}
