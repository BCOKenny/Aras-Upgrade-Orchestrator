using ArasUpgradeOrchestrator.Core.Cases;

namespace ArasUpgradeOrchestrator.Core.Tasks;

public enum UpgradeTaskKind
{
    RebuildCustomerEnvironment,
    GenerateCustomerPackage,
    PackagePreparation,
    HopPackage,
    HopExecution,
    HopLoginValidation,
    HopDatabaseBackupEvidence,
    CompareAndClassifyCoreTree,
    FinalDelivery
}

public sealed record UpgradeTask(
    string Id,
    UpgradeTaskKind Kind,
    string DisplayName,
    IReadOnlyList<string> Dependencies,
    string? HopKey = null,
    bool IsExternalManualAction = false);

public sealed class TaskGraph
{
    private readonly IReadOnlyDictionary<string, UpgradeTask> _tasks;

    private TaskGraph(IEnumerable<UpgradeTask> tasks)
    {
        _tasks = tasks.ToDictionary(task => task.Id, StringComparer.Ordinal);
        ValidateAcyclic();
    }

    public IReadOnlyCollection<UpgradeTask> Tasks => _tasks.Values.ToArray();
    public UpgradeTask Get(string taskId) => _tasks.TryGetValue(taskId, out var task)
        ? task
        : throw new KeyNotFoundException($"找不到任務 {taskId}。 ");

    public bool AreDependenciesSatisfied(string taskId, IReadOnlySet<string> completedTaskIds) =>
        Get(taskId).Dependencies.All(completedTaskIds.Contains);

    public static TaskGraph Build(UpgradeRoute route)
    {
        var tasks = new List<UpgradeTask>
        {
            new("environment.rebuild", UpgradeTaskKind.RebuildCustomerEnvironment, "重建客戶環境", []),
            new("customer-package.generate", UpgradeTaskKind.GenerateCustomerPackage, "產生客戶 Package", ["environment.rebuild"]),
            new("packages.prepare", UpgradeTaskKind.PackagePreparation, "Package 比較／產生升級 Package", ["customer-package.generate"]),
            new("core-tree.compare", UpgradeTaskKind.CompareAndClassifyCoreTree, "比較及分類 Core Tree", [])
        };

        string? previousCheckpointId = null;
        for (var index = 0; index < route.Hops.Count; index++)
        {
            var hop = route.Hops[index];
            var number = index + 1;
            var packageId = $"hop.{number}.package";
            var executionId = $"hop.{number}.execute";
            var validationId = $"hop.{number}.validate-login";
            var backupId = $"hop.{number}.record-db-backup";

            tasks.Add(new UpgradeTask(packageId, UpgradeTaskKind.HopPackage, $"跳點 Package 子任務：{hop.Key}", ["packages.prepare"], hop.Key));
            var dependencies = previousCheckpointId is null ? new[] { packageId } : new[] { packageId, previousCheckpointId };
            tasks.Add(new UpgradeTask(executionId, UpgradeTaskKind.HopExecution, $"跳點執行：{hop.Key}", dependencies, hop.Key, true));
            tasks.Add(new UpgradeTask(validationId, UpgradeTaskKind.HopLoginValidation, $"人工登入驗證：{hop.TargetVersion}", [executionId], hop.Key, true));
            tasks.Add(new UpgradeTask(backupId, UpgradeTaskKind.HopDatabaseBackupEvidence, $"記錄 DB 備份證據：{hop.TargetVersion}", [validationId], hop.Key, true));
            previousCheckpointId = backupId;
        }

        tasks.Add(new UpgradeTask(
            "delivery.final",
            UpgradeTaskKind.FinalDelivery,
            "最終交付",
            previousCheckpointId is null ? ["core-tree.compare"] : [previousCheckpointId, "core-tree.compare"]));

        return new TaskGraph(tasks);
    }

    private void ValidateAcyclic()
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var task in _tasks.Values) Visit(task.Id);
        return;

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new InvalidOperationException("任務圖包含循環相依。 ");
            var task = Get(id);
            foreach (var dependency in task.Dependencies)
            {
                if (!_tasks.ContainsKey(dependency)) throw new InvalidOperationException($"任務 {id} 依賴不存在的任務 {dependency}。 ");
                Visit(dependency);
            }

            visiting.Remove(id);
            visited.Add(id);
        }
    }
}
