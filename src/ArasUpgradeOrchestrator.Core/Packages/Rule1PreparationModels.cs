using ArasUpgradeOrchestrator.Core.Rules;

namespace ArasUpgradeOrchestrator.Core.Packages;

public enum Rule1PreparationStatus
{
    Draft,
    PendingVerification
}

public sealed record Rule1PreparationRuleSnapshot(
    IReadOnlyList<RuleStepDefinition> Steps,
    IReadOnlyList<RuleSetVersionReference>? PublishedRuleSets,
    string? EffectiveRuleChecksum,
    string? CandidateRuleReference)
{
    public bool IsPublished => PublishedRuleSets is { Count: > 0 } && !string.IsNullOrWhiteSpace(EffectiveRuleChecksum);
}

public sealed record Rule1PreparationReceipt(
    Guid CaseId,
    string PreparationId,
    string AttemptId,
    string SourceVersion,
    string TargetVersion,
    Rule1PreparationStatus Status,
    Rule1PreparationRuleSnapshot RuleSnapshot,
    PackageInputTreeDigestResult SourceInput,
    PackageInputTreeDigestResult TargetInput,
    PackageInputTreeDigestResult PatchSupportInput,
    OotbHopDiffSummary Summary,
    IReadOnlyList<OotbHopDiffError> Errors,
    IReadOnlyList<OotbHopDiffManualReview> ManualReviews,
    DateTimeOffset CreatedAt,
    string CreatedBy);
