using System.Security.Cryptography;
using System.Text;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.Packages;

public sealed record Rule1PreparationRegistrationRequest(
    string CaseRoot,
    string Actor,
    PackageComparisonPreparation Preparation,
    ActionConfirmation? Confirmation = null);

public sealed record Rule1PreparationRegistrationResult(Guid CaseId, string PreparationId, SafetyLevel SafetyLevel, string HistoryPath);

public sealed class Rule1PreparationRegistrationCommand
{
    public const string ActionId = "package.rule1-preparation.register";
    public const string ActionVersion = "1";
    private readonly SafetyPolicy _safetyPolicy;
    private readonly Func<DateTimeOffset> _clock;

    public Rule1PreparationRegistrationCommand(SafetyPolicy safetyPolicy, Func<DateTimeOffset>? clock = null)
    {
        _safetyPolicy = safetyPolicy ?? throw new ArgumentNullException(nameof(safetyPolicy));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<Rule1PreparationRegistrationResult> ExecuteAsync(Rule1PreparationRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Actor)) throw new ArgumentException("登錄者不可為空。", nameof(request));
        var recordedPreparation = request.Preparation with { CreatedAt = _clock(), CreatedBy = request.Actor };
        var store = new CaseStore(request.CaseRoot);
        var existing = await store.LoadAsync(cancellationToken);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", recordedPreparation.PreparationId, recordedPreparation.SourceVersion, recordedPreparation.TargetVersion, recordedPreparation.CreatedBy))));
        var action = new ControlledAction(ActionId, ActionVersion, store.CaseRoot, digest, true,
            new Dictionary<string, bool> { ["case.loaded"] = true, ["preparation.valid"] = true });
        var decision = _safetyPolicy.Evaluate(action);
        if (!SafetyPolicy.IsConfirmationValid(decision, request.Confirmation)) throw new InvalidOperationException(decision.Reason);
        var manifest = await store.AddPackageComparisonPreparationAsync(recordedPreparation, cancellationToken);
        var history = new AppendOnlyHistoryStore(store.ToolDataPath);
        await history.AppendAsync(manifest.CaseId, HistoryEventTypes.Rule1PreparationRegistered, recordedPreparation.PreparationId, request.Actor,
            new { recordedPreparation.SourceVersion, recordedPreparation.TargetVersion, recordedPreparation.OotbSourceSolutionsRelativePath, recordedPreparation.OotbTargetSolutionsRelativePath, recordedPreparation.PatchSupportInputRelativePath, recordedPreparation.PreparationAttemptRelativePath }, _clock(), cancellationToken: cancellationToken);
        return new(manifest.CaseId, recordedPreparation.PreparationId, decision.Level, history.Path);
    }
}
