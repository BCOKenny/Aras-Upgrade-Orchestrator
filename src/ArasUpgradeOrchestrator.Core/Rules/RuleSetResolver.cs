namespace ArasUpgradeOrchestrator.Core.Rules;

public static class RuleSetResolver
{
    public static RuleSetResolutionResult Resolve(
        IEnumerable<PublishedRuleSet> published,
        RuleSetKind kind,
        string sourceVersion,
        string targetVersion)
    {
        ArgumentNullException.ThrowIfNull(published);
        var latest = published.Where(item => item.Kind == kind)
            .GroupBy(item => item.RuleSetId)
            .Select(group => group.OrderByDescending(item => item.Version).First())
            .ToArray();
        var common = latest.Where(item => item.Scope == RuleSetScope.Common).ToArray();
        if (common.Length == 0)
            return Block(RuleResolutionStatus.NotFound, RuleResolutionIssueCode.MissingCommonRuleSet, "找不到已發布的共用規則集。 ");
        if (common.Length > 1)
            return Block(RuleResolutionStatus.Blocked, RuleResolutionIssueCode.MultipleCommonRuleSets, "同一規則類型存在多個共用規則集，無法可靠選擇。 ");

        var exceptions = latest.Where(item => item.Scope == RuleSetScope.VersionException &&
            string.Equals(item.SourceVersion, sourceVersion, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.TargetVersion, targetVersion, StringComparison.OrdinalIgnoreCase)).ToArray();
        var effective = common[0].Steps.ToDictionary(step => step.StepId, StringComparer.OrdinalIgnoreCase);
        var issues = new List<RuleResolutionIssue>();
        foreach (var group in exceptions.SelectMany(item => item.Steps)
                     .GroupBy(step => step.StepId, StringComparer.OrdinalIgnoreCase))
        {
            var alternatives = group.GroupBy(RuleSetCanonicalizer.StepFingerprint, StringComparer.Ordinal).ToArray();
            if (alternatives.Length > 1)
            {
                effective.Remove(group.Key);
                issues.Add(new RuleResolutionIssue(RuleResolutionIssueCode.ConflictingVersionExceptions,
                    $"多個版本例外對步驟 {group.Key} 定義不同結果；該步驟阻擋，其他獨立步驟仍可解析。", group.Key));
                continue;
            }
            effective[group.Key] = group.First();
        }
        var ordered = effective.Values.OrderBy(step => step.Order).ThenBy(step => step.StepId, StringComparer.Ordinal).ToArray();
        var effectiveDraft = new RuleSetDraft(Guid.NewGuid(), common[0].RuleSetId, common[0].DisplayName, kind,
            RuleSetScope.Common, null, null, ordered, common[0].PublishedAt, common[0].PublishedBy);
        var validation = RuleSetValidator.Validate(effectiveDraft);
        if (!validation.IsValid)
            return new RuleSetResolutionResult(RuleResolutionStatus.Blocked, [], References(common.Concat(exceptions)),
                [new RuleResolutionIssue(RuleResolutionIssueCode.InvalidEffectiveRuleSet,
                    "套用版本例外後的規則集未通過驗證：" + string.Join("; ", validation.Errors.Select(error => error.Message)))], null);

        var checksum = RuleSetCanonicalizer.Checksum(common[0].DisplayName, kind, RuleSetScope.Common, sourceVersion, targetVersion, ordered);
        return new RuleSetResolutionResult(issues.Count == 0 ? RuleResolutionStatus.Resolved : RuleResolutionStatus.Blocked,
            ordered, References(common.Concat(exceptions)), issues, checksum);
    }

    private static IReadOnlyList<RuleSetVersionReference> References(IEnumerable<PublishedRuleSet> sets) =>
        sets.Select(item => new RuleSetVersionReference(item.RuleSetId, item.Version, item.Scope, item.ContentChecksum)).ToArray();

    private static RuleSetResolutionResult Block(RuleResolutionStatus status, RuleResolutionIssueCode code, string message) =>
        new(status, [], [], [new RuleResolutionIssue(code, message)], null);
}
