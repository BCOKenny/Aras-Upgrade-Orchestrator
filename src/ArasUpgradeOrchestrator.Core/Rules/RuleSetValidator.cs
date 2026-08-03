namespace ArasUpgradeOrchestrator.Core.Rules;

public static class RuleSetValidator
{
    public static RuleSetValidationResult Validate(RuleSetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var errors = new List<RuleValidationError>();
        if (draft.DraftId == Guid.Empty || draft.RuleSetId == Guid.Empty)
            Add(errors, RuleValidationErrorCode.MissingIdentity, "draft", "DraftId 與 RuleSetId 不可為空。");
        if (string.IsNullOrWhiteSpace(draft.DisplayName))
            Add(errors, RuleValidationErrorCode.MissingDisplayName, "displayName", "Rule 顯示名稱不可為空。");
        if (string.IsNullOrWhiteSpace(draft.CreatedBy))
            Add(errors, RuleValidationErrorCode.MissingActor, "createdBy", "規則草稿建立者不可為空。");
        if (draft.Scope == RuleSetScope.Common &&
            (!string.IsNullOrWhiteSpace(draft.SourceVersion) || !string.IsNullOrWhiteSpace(draft.TargetVersion)))
            Add(errors, RuleValidationErrorCode.InvalidScopeVersions, "scope", "共同準則不得綁定來源或目標版本。");
        if (draft.Scope == RuleSetScope.VersionException &&
            (string.IsNullOrWhiteSpace(draft.SourceVersion) || string.IsNullOrWhiteSpace(draft.TargetVersion)))
            Add(errors, RuleValidationErrorCode.InvalidScopeVersions, "scope", "版本例外必須同時指定來源與目標版本。");
        if (draft.Steps is null || draft.Steps.Count == 0)
        {
            Add(errors, RuleValidationErrorCode.MissingSteps, "steps", "規則集至少需要一個步驟。");
            return new RuleSetValidationResult(errors);
        }

        foreach (var group in draft.Steps.Where(step => step is not null).GroupBy(step => step.StepId, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            Add(errors, RuleValidationErrorCode.DuplicateStepId, $"steps[{group.Key}]", "StepId 不得重複。");
        foreach (var group in draft.Steps.Where(step => step is not null).GroupBy(step => step.Order).Where(group => group.Count() > 1))
            Add(errors, RuleValidationErrorCode.DuplicateStepOrder, $"steps[order={group.Key}]", "步驟順序不得重複。");

        foreach (var step in draft.Steps)
        {
            if (step is null)
            {
                Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, "steps", "步驟不可為 null。");
                continue;
            }
            var location = $"steps[{step.StepId}]";
            if (string.IsNullOrWhiteSpace(step.StepId)) Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, location, "StepId 不可為空。");
            if (step.Order < 1) Add(errors, RuleValidationErrorCode.InvalidStepOrder, location, "步驟順序必須大於零。");
            if (!Enum.IsDefined(step.Kind) || !IsSupported(draft.Kind, step.Kind))
                Add(errors, RuleValidationErrorCode.UnsupportedStep, location, $"{draft.Kind} 不支援步驟 {step.Kind}。");

            var propertyNames = step.PropertyNames ?? [];
            if (propertyNames.Any(string.IsNullOrWhiteSpace))
                Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, location, "Property 名稱不可為空。");
            if (propertyNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                propertyNames.Count(name => !string.IsNullOrWhiteSpace(name)))
                Add(errors, RuleValidationErrorCode.DuplicatePropertyName, location, "同一步驟的 Property 名稱不得重複。");

            var pairs = step.ValuePairs ?? [];
            if (step.Kind == RuleStepKind.KeepTargetForValuePairs && pairs.Count == 0)
                Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, location, "保留目的值步驟至少需要一個值組合。");
            if (step.Kind != RuleStepKind.KeepTargetForValuePairs && pairs.Count > 0)
                Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, location, "只有保留目的值步驟可設定值組合。");
            if (pairs.Any(pair => pair is null || string.IsNullOrWhiteSpace(pair.PropertyName)))
                Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, location, "值組合的 Property 名稱不可為空。");
            if (pairs.Any(pair => pair is not null && (!IsValidCondition(pair.Source) || !IsValidCondition(pair.Target))))
                Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, location, "值組合必須使用有效的精確值、空值或非空值條件。");
            if (pairs.Where(pair => pair is not null).GroupBy(pair => (pair.PropertyName.ToUpperInvariant(), pair.Source, pair.Target)).Any(group => group.Count() > 1))
                Add(errors, RuleValidationErrorCode.DuplicateValuePair, location, "值組合不得重複。");

            if (step.Kind == RuleStepKind.PreferSourceUnderTargetPath && string.IsNullOrWhiteSpace(step.TargetPathConstraint))
                Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, location, "路徑限定更新步驟必須指定目的相對路徑條件。");
            if (step.Kind != RuleStepKind.PreferSourceUnderTargetPath && !string.IsNullOrWhiteSpace(step.TargetPathConstraint))
                Add(errors, RuleValidationErrorCode.InvalidStepConfiguration, location, "只有路徑限定更新步驟可設定路徑條件。");
        }

        return new RuleSetValidationResult(errors);
    }

    private static bool IsSupported(RuleSetKind kind, RuleStepKind step) => kind switch
    {
        RuleSetKind.Rule1 => step == RuleStepKind.Rule1ItemDisposition,
        RuleSetKind.Rule2 => step is not RuleStepKind.Rule1ItemDisposition,
        _ => false
    };

    private static bool IsValidCondition(RuleValueCondition? condition) => condition is not null && condition.Kind switch
    {
        RuleValueConditionKind.Exact => condition.Value is not null,
        RuleValueConditionKind.Empty or RuleValueConditionKind.NonEmpty => condition.Value is null,
        _ => false
    };

    private static void Add(List<RuleValidationError> errors, RuleValidationErrorCode code, string location, string message) =>
        errors.Add(new RuleValidationError(code, location, message));
}
