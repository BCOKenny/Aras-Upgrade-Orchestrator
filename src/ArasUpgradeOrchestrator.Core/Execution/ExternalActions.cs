namespace ArasUpgradeOrchestrator.Core.Execution;

public sealed record ExternalActionContext(
    Guid AttemptId,
    string ActionId,
    string ActionVersion,
    string Target,
    IReadOnlyDictionary<string, string> Inputs);

public sealed record ExternalActionResult(bool Succeeded, string Message, string? EvidenceReference = null);

public interface IExternalActionExecutor
{
    Task<ExternalActionResult> ExecuteAsync(ExternalActionContext context, CancellationToken cancellationToken = default);
}

public sealed class BlockedExternalActionExecutor : IExternalActionExecutor
{
    public Task<ExternalActionResult> ExecuteAsync(ExternalActionContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalActionResult(
            false,
            "外部操作未獲授權；預設執行器不連接 DB、不啟動 Aras 工具，也不修改 Package、Core Tree 或升級目錄。"));
}
