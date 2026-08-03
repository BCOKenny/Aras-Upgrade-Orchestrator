using System.Collections.Concurrent;
using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Execution;

namespace ArasUpgradeOrchestrator.Core.Packages;

public enum CustomerPackageFlowState
{
    NotStarted,
    Locked,
    RolledBack,
    Completed
}

public sealed record ApprovedPackageAction(string ActionId, string ActionVersion, string Checksum);

public sealed record CustomerPackageLockRequest(
    Guid FlowAttemptId,
    string TaskId,
    string Environment,
    string Target,
    string DatabaseBackupId,
    string DatabaseBackupEvidenceReference,
    string OriginalPackageBackupId,
    string OriginalPackageBackupEvidenceReference,
    IReadOnlyList<ApprovedPackageAction> ApprovedActions);

public enum PackageExportExclusionDisposition
{
    NotUpgradeCustomization,
    SupplementedElsewhere,
    RiskAccepted
}

public sealed record PackageExportExclusion(
    string Name,
    string Type,
    string Reason,
    PackageExportExclusionDisposition? Disposition,
    string? EvidenceReference);

public sealed record CustomerPackageFlowView(
    CustomerPackageFlowState State,
    Guid? FlowAttemptId,
    string? TaskId,
    string? Environment,
    string? Target,
    string? DatabaseBackupId,
    IReadOnlyList<ApprovedPackageAction> ApprovedActions,
    string? EvidenceReference);

internal sealed record CustomerPackageFlowLockedPayload(CustomerPackageLockRequest Request);
internal sealed record CustomerPackageFlowRolledBackPayload(Guid FlowAttemptId, string DatabaseBackupId, string RestoreEvidenceReference);
internal sealed record CustomerPackageFlowCompletedPayload(
    Guid FlowAttemptId,
    string BaselineEvidenceReference,
    IReadOnlyList<PackageExportExclusion> Exclusions);

public sealed class CustomerPackageOneTimeFlow
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Guid _caseId;
    private readonly AppendOnlyHistoryStore _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _stateLock;

    public CustomerPackageOneTimeFlow(Guid caseId, AppendOnlyHistoryStore history, Func<DateTimeOffset>? now = null)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("案件識別不可為空。", nameof(caseId));
        _caseId = caseId;
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _stateLock = Locks.GetOrAdd(Path.GetFullPath(history.Path), _ => new SemaphoreSlim(1, 1));
    }

    public async Task<CustomerPackageFlowView> LockAsync(
        CustomerPackageLockRequest request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ValidateLockRequest(request);
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStateCoreAsync(cancellationToken);
            if (current.State == CustomerPackageFlowState.Completed)
                throw new InvalidOperationException("客戶 Package 基準已完成，一次性流程永久不可重開。 ");
            if (current.State == CustomerPackageFlowState.Locked)
                throw new InvalidOperationException("一次性 Package 流程已鎖定；失敗或中斷後必須先提交相符的 DB 還原證據。 ");

            await _history.AppendAsync(
                _caseId,
                HistoryEventTypes.CustomerPackageFlowLocked,
                request.TaskId,
                actor,
                new CustomerPackageFlowLockedPayload(request),
                _now(),
                cancellationToken: cancellationToken);
            return View(CustomerPackageFlowState.Locked, request, null);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<CustomerPackageFlowView> MarkRolledBackAsync(
        Guid flowAttemptId,
        string databaseBackupId,
        string restoreEvidenceReference,
        string actor,
        CancellationToken cancellationToken = default)
    {
        RequireText(databaseBackupId, nameof(databaseBackupId));
        RequireText(restoreEvidenceReference, nameof(restoreEvidenceReference));
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStateCoreAsync(cancellationToken);
            EnsureActiveAttempt(current, flowAttemptId);
            if (!string.Equals(current.DatabaseBackupId, databaseBackupId, StringComparison.Ordinal))
                throw new InvalidOperationException("DB 還原證據的備份識別與本次鎖定前備份不相符。 ");

            await _history.AppendAsync(
                _caseId,
                HistoryEventTypes.CustomerPackageFlowRolledBack,
                current.TaskId!,
                actor,
                new CustomerPackageFlowRolledBackPayload(flowAttemptId, databaseBackupId, restoreEvidenceReference),
                _now(),
                cancellationToken: cancellationToken);
            return current with { State = CustomerPackageFlowState.RolledBack, EvidenceReference = restoreEvidenceReference };
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<CustomerPackageFlowView> CompleteAsync(
        Guid flowAttemptId,
        string baselineEvidenceReference,
        IReadOnlyList<PackageExportExclusion> exclusions,
        string actor,
        CancellationToken cancellationToken = default)
    {
        RequireText(baselineEvidenceReference, nameof(baselineEvidenceReference));
        ArgumentNullException.ThrowIfNull(exclusions);
        var unresolved = exclusions.Where(item => item.Disposition is null || string.IsNullOrWhiteSpace(item.EvidenceReference)).ToArray();
        if (unresolved.Length > 0)
            throw new InvalidOperationException("所有 Aras Export 取消選取項目都必須有核准處置與可追溯證據。 ");

        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStateCoreAsync(cancellationToken);
            EnsureActiveAttempt(current, flowAttemptId);
            await _history.AppendAsync(
                _caseId,
                HistoryEventTypes.CustomerPackageFlowCompleted,
                current.TaskId!,
                actor,
                new CustomerPackageFlowCompletedPayload(flowAttemptId, baselineEvidenceReference, exclusions),
                _now(),
                cancellationToken: cancellationToken);
            return current with { State = CustomerPackageFlowState.Completed, EvidenceReference = baselineEvidenceReference };
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public Task<CustomerPackageFlowView> GetStateAsync(CancellationToken cancellationToken = default) =>
        GetStateCoreAsync(cancellationToken);

    private async Task<CustomerPackageFlowView> GetStateCoreAsync(CancellationToken cancellationToken)
    {
        CustomerPackageLockRequest? active = null;
        var state = CustomerPackageFlowState.NotStarted;
        string? evidence = null;
        await foreach (var entry in _history.ReadAllAsync(cancellationToken))
        {
            if (entry.CaseId != _caseId) continue;
            if (entry.EventType == HistoryEventTypes.CustomerPackageFlowLocked)
            {
                var payload = entry.Payload.Deserialize<CustomerPackageFlowLockedPayload>(AppendOnlyHistoryStore.JsonOptions)
                    ?? throw new InvalidDataException("一次性 Package 鎖定事件缺少內容。 ");
                active = payload.Request;
                state = CustomerPackageFlowState.Locked;
                evidence = null;
            }
            else if (entry.EventType == HistoryEventTypes.CustomerPackageFlowRolledBack)
            {
                var payload = entry.Payload.Deserialize<CustomerPackageFlowRolledBackPayload>(AppendOnlyHistoryStore.JsonOptions)
                    ?? throw new InvalidDataException("一次性 Package Rollback 事件缺少內容。 ");
                if (active?.FlowAttemptId != payload.FlowAttemptId)
                    throw new InvalidDataException("一次性 Package Rollback 事件未對應目前鎖定。 ");
                state = CustomerPackageFlowState.RolledBack;
                evidence = payload.RestoreEvidenceReference;
            }
            else if (entry.EventType == HistoryEventTypes.CustomerPackageFlowCompleted)
            {
                var payload = entry.Payload.Deserialize<CustomerPackageFlowCompletedPayload>(AppendOnlyHistoryStore.JsonOptions)
                    ?? throw new InvalidDataException("一次性 Package 完成事件缺少內容。 ");
                if (active?.FlowAttemptId != payload.FlowAttemptId)
                    throw new InvalidDataException("一次性 Package 完成事件未對應目前鎖定。 ");
                state = CustomerPackageFlowState.Completed;
                evidence = payload.BaselineEvidenceReference;
            }
        }

        return active is null
            ? new CustomerPackageFlowView(state, null, null, null, null, null, [], evidence)
            : View(state, active, evidence);
    }

    private static CustomerPackageFlowView View(CustomerPackageFlowState state, CustomerPackageLockRequest request, string? evidence) =>
        new(state, request.FlowAttemptId, request.TaskId, request.Environment, request.Target, request.DatabaseBackupId, request.ApprovedActions, evidence);

    private static void EnsureActiveAttempt(CustomerPackageFlowView current, Guid flowAttemptId)
    {
        if (current.State != CustomerPackageFlowState.Locked)
            throw new InvalidOperationException("只有目前 Locked 的一次性 Package 流程可以完成或標記 Rollback。 ");
        if (flowAttemptId == Guid.Empty || current.FlowAttemptId != flowAttemptId)
            throw new InvalidOperationException("執行嘗試識別與目前一次性 Package 鎖定不相符。 ");
    }

    private static void ValidateLockRequest(CustomerPackageLockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FlowAttemptId == Guid.Empty) throw new ArgumentException("流程嘗試識別不可為空。", nameof(request));
        RequireText(request.TaskId, nameof(request.TaskId));
        RequireText(request.Environment, nameof(request.Environment));
        RequireText(request.Target, nameof(request.Target));
        RequireText(request.DatabaseBackupId, nameof(request.DatabaseBackupId));
        RequireText(request.DatabaseBackupEvidenceReference, nameof(request.DatabaseBackupEvidenceReference));
        RequireText(request.OriginalPackageBackupId, nameof(request.OriginalPackageBackupId));
        RequireText(request.OriginalPackageBackupEvidenceReference, nameof(request.OriginalPackageBackupEvidenceReference));
        if (request.ApprovedActions.GroupBy(item => (item.ActionId, item.ActionVersion), StringTupleComparer.Instance).Any(group => group.Count() > 1))
            throw new ArgumentException("同一 action 與版本不得有多個核准 Checksum。", nameof(request));
        foreach (var action in request.ApprovedActions)
        {
            RequireText(action.ActionId, nameof(action.ActionId));
            RequireText(action.ActionVersion, nameof(action.ActionVersion));
            RequireText(action.Checksum, nameof(action.Checksum));
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("必要證據或識別不可為空。", name);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string ActionId, string ActionVersion)>
    {
        public static readonly StringTupleComparer Instance = new();
        public bool Equals((string ActionId, string ActionVersion) x, (string ActionId, string ActionVersion) y) =>
            string.Equals(x.ActionId, y.ActionId, StringComparison.Ordinal) && string.Equals(x.ActionVersion, y.ActionVersion, StringComparison.Ordinal);
        public int GetHashCode((string ActionId, string ActionVersion) obj) => HashCode.Combine(obj.ActionId, obj.ActionVersion);
    }
}

public static class CustomerPackageActions
{
    public const string DeletePackageTables = "customer-package.delete-package-tables";
    public const string ExportOotbTables = "customer-package.export-ootb-tables";
    public const string ImportOotbTables = "customer-package.import-ootb-tables";
    public const string ChecksumInput = "approvedChecksum";
    public const string FlowAttemptIdInput = "customerPackageFlowAttemptId";

    public static bool IsKnown(string actionId) => actionId is DeletePackageTables or ExportOotbTables or ImportOotbTables;
}

public sealed class CustomerPackageActionGate : IExternalActionExecutor
{
    private readonly CustomerPackageOneTimeFlow _flow;
    private readonly IExternalActionExecutor _inner;

    public CustomerPackageActionGate(CustomerPackageOneTimeFlow flow, IExternalActionExecutor inner)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<ExternalActionResult> ExecuteAsync(ExternalActionContext context, CancellationToken cancellationToken = default)
    {
        var state = await _flow.GetStateAsync(cancellationToken);
        if (!context.Inputs.TryGetValue(CustomerPackageActions.FlowAttemptIdInput, out var flowAttemptText) ||
            !Guid.TryParse(flowAttemptText, out var flowAttemptId) ||
            state.State != CustomerPackageFlowState.Locked || state.FlowAttemptId != flowAttemptId)
            return Blocked("一次性 Package 流程未鎖定，或動作不屬於目前鎖定的流程嘗試。");
        if (!TargetsMatch(state.Target!, context.Target))
            return Blocked("動作目標與一次性 Package 鎖定目標不相符。");
        if (!CustomerPackageActions.IsKnown(context.ActionId))
            return Blocked("動作不在固定的客戶 Package 流程 action 清單內。");
        if (!context.Inputs.TryGetValue(CustomerPackageActions.ChecksumInput, out var checksum) || string.IsNullOrWhiteSpace(checksum))
            return Blocked("動作缺少已核准 Script／工具 Checksum。");

        var approved = state.ApprovedActions.SingleOrDefault(item =>
            string.Equals(item.ActionId, context.ActionId, StringComparison.Ordinal) &&
            string.Equals(item.ActionVersion, context.ActionVersion, StringComparison.Ordinal));
        if (approved is null || !string.Equals(approved.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
            return Blocked("動作版本或 Checksum 與鎖定時的核准內容不相符。");
        return await _inner.ExecuteAsync(context, cancellationToken);
    }

    private static ExternalActionResult Blocked(string message) => new(false, message);

    private static bool TargetsMatch(string lockedTarget, string requestedTarget)
    {
        try
        {
            return string.Equals(Path.GetFullPath(lockedTarget), Path.GetFullPath(requestedTarget), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
