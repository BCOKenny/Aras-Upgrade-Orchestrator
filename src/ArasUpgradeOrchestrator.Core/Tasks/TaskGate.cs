using ArasUpgradeOrchestrator.Core.Execution;

namespace ArasUpgradeOrchestrator.Core.Tasks;

public enum TaskGateState
{
    Ready,
    WaitingForDependencies,
    Running,
    Completed,
    Failed,
    Interrupted
}

public sealed class TaskGate
{
    public TaskGateState Evaluate(UpgradeTask task, IReadOnlyCollection<AttemptView> attempts, IReadOnlySet<string> completedTaskIds)
    {
        var latest = attempts.Where(attempt => attempt.TaskId == task.Id).OrderBy(attempt => attempt.Sequence).LastOrDefault();
        if (latest is not null)
        {
            return latest.State switch
            {
                AttemptState.Running => TaskGateState.Running,
                AttemptState.Succeeded => TaskGateState.Completed,
                AttemptState.Failed => TaskGateState.Failed,
                AttemptState.Interrupted => TaskGateState.Interrupted,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        return task.Dependencies.All(completedTaskIds.Contains)
            ? TaskGateState.Ready
            : TaskGateState.WaitingForDependencies;
    }
}
