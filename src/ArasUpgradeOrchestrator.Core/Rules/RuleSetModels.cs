namespace ArasUpgradeOrchestrator.Core.Rules;

public enum RuleSetKind
{
    Rule1,
    Rule2
}

public enum RuleSetScope
{
    Common,
    VersionException
}

public enum RuleStepKind
{
    Rule1ItemDisposition,
    RemoveEqualScalarProperties,
    RemoveNamedProperties,
    PreferGreaterSourceNumber,
    PreferSourceUnderTargetPath,
    KeepTargetForValuePairs,
    KeepTargetNamedProperties,
    DefaultPreferSourceUnlessSourceEmpty
}

public enum RuleValueConditionKind
{
    Exact,
    Empty,
    NonEmpty
}

public sealed record RuleValueCondition(RuleValueConditionKind Kind, string? Value)
{
    public static RuleValueCondition Exact(string value) => new(RuleValueConditionKind.Exact, value);
    public static RuleValueCondition Empty() => new(RuleValueConditionKind.Empty, null);
    public static RuleValueCondition NonEmpty() => new(RuleValueConditionKind.NonEmpty, null);
}

public sealed record RuleValuePair(string PropertyName, RuleValueCondition Source, RuleValueCondition Target);

public sealed record RuleStepDefinition(
    string StepId,
    int Order,
    RuleStepKind Kind,
    IReadOnlyList<string> PropertyNames,
    IReadOnlyList<RuleValuePair> ValuePairs,
    string? TargetPathConstraint);

public sealed record RuleSetDraft(
    Guid DraftId,
    Guid RuleSetId,
    string DisplayName,
    RuleSetKind Kind,
    RuleSetScope Scope,
    string? SourceVersion,
    string? TargetVersion,
    IReadOnlyList<RuleStepDefinition> Steps,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public enum RuleValidationErrorCode
{
    MissingIdentity,
    MissingDisplayName,
    MissingActor,
    MissingSteps,
    InvalidScopeVersions,
    DuplicateStepId,
    DuplicateStepOrder,
    InvalidStepOrder,
    UnsupportedStep,
    DuplicatePropertyName,
    InvalidStepConfiguration,
    DuplicateValuePair
}

public sealed record RuleValidationError(RuleValidationErrorCode Code, string Location, string Message);

public sealed record RuleSetValidationResult(IReadOnlyList<RuleValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public enum RuleActorKind
{
    Human,
    Ai,
    Automation
}

public sealed record RuleActor(string Name, RuleActorKind Kind);

public sealed record RulePublicationApproval(RuleActor Actor, string EvidenceReference);

public sealed record PublishedRuleSet(
    Guid RuleSetId,
    Guid SourceDraftId,
    int Version,
    string DisplayName,
    RuleSetKind Kind,
    RuleSetScope Scope,
    string? SourceVersion,
    string? TargetVersion,
    IReadOnlyList<RuleStepDefinition> Steps,
    DateTimeOffset PublishedAt,
    string PublishedBy,
    string ApprovalEvidenceReference,
    string ContentChecksum)
{
    public static PublishedRuleSet Create(
        RuleSetDraft draft,
        int version,
        DateTimeOffset publishedAt,
        string publishedBy,
        string evidenceReference)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var checksum = RuleSetCanonicalizer.Checksum(draft.DisplayName, draft.Kind, draft.Scope,
            draft.SourceVersion, draft.TargetVersion, draft.Steps);
        return new PublishedRuleSet(draft.RuleSetId, draft.DraftId, version, draft.DisplayName, draft.Kind, draft.Scope,
            draft.SourceVersion, draft.TargetVersion, draft.Steps.ToArray(), publishedAt, publishedBy,
            evidenceReference, checksum);
    }
}

public enum RuleResolutionStatus
{
    Resolved,
    NotFound,
    Blocked
}

public enum RuleResolutionIssueCode
{
    MissingCommonRuleSet,
    MultipleCommonRuleSets,
    ConflictingVersionExceptions,
    InvalidEffectiveRuleSet
}

public sealed record RuleResolutionIssue(RuleResolutionIssueCode Code, string Message, string? StepId = null);

public sealed record RuleSetVersionReference(Guid RuleSetId, int Version, RuleSetScope Scope, string ContentChecksum);

public sealed record RuleSetResolutionResult(
    RuleResolutionStatus Status,
    IReadOnlyList<RuleStepDefinition> Steps,
    IReadOnlyList<RuleSetVersionReference> PinnedVersions,
    IReadOnlyList<RuleResolutionIssue> Issues,
    string? EffectiveChecksum);
