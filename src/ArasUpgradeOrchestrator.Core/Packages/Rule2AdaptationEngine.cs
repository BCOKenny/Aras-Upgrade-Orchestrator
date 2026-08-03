using System.Globalization;
using System.Xml.Linq;
using ArasUpgradeOrchestrator.Core.Aml;
using ArasUpgradeOrchestrator.Core.Rules;

namespace ArasUpgradeOrchestrator.Core.Packages;

public enum Rule2AdaptationStatus { Ready, Blocked }

public sealed record Rule2ManualReview(string Code, string? SourcePath, string? TargetPath, string Message);

public sealed record Rule2AdaptationSummary(
    int SourceOnlyDeleted,
    int TargetOnlyRetained,
    int EqualPairsDeleted,
    int DifferentPairsProcessed,
    int ScalarPropertiesDeleted,
    int ScalarPropertiesUpdated,
    int FederatedPropertiesCopied,
    int ManualReviewCount);

public sealed record Rule2DocumentResult(
    Rule2AdaptationStatus Status,
    AmlDocument SourceWorkCopy,
    AmlDocument TargetWorkCopy,
    Rule2AdaptationSummary Summary,
    IReadOnlyList<Rule2ManualReview> ManualReviews);

public static class Rule2AdaptationEngine
{
    public static Rule2DocumentResult Apply(
        AmlDocument source,
        AmlDocument target,
        RuleSetResolutionResult rules,
        string targetRelativePath)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Status != RuleResolutionStatus.Resolved || rules.Issues.Count != 0 ||
            string.IsNullOrWhiteSpace(rules.EffectiveChecksum))
            throw new InvalidOperationException("Rule 2 必須使用已解析且固定版本的有效規則快照。 ");
        ValidateRule2Steps(rules.Steps);

        var sourceRoot = source.Root.CloneSubtree();
        var targetRoot = target.Root.CloneSubtree();
        var state = new State(rules.Steps.OrderBy(step => step.Order).ToArray(), NormalizePath(targetRelativePath));
        ProcessItemContainer(sourceRoot, targetRoot, "/AML", state);
        var sourceResult = ParseLike(source, sourceRoot);
        var targetResult = ParseLike(target, targetRoot);
        var summary = new Rule2AdaptationSummary(state.SourceOnlyDeleted, state.TargetOnlyRetained,
            state.EqualPairsDeleted, state.DifferentPairsProcessed, state.ScalarsDeleted,
            state.ScalarsUpdated, state.FederatedCopied, state.Reviews.Count);
        return new Rule2DocumentResult(state.Reviews.Count == 0 ? Rule2AdaptationStatus.Ready : Rule2AdaptationStatus.Blocked,
            sourceResult, targetResult, summary, state.Reviews);
    }

    private static void ProcessItemContainer(XElement sourceContainer, XElement targetContainer, string path, State state)
    {
        var source = IndexItems(sourceContainer, path, true, state);
        var target = IndexItems(targetContainer, path, false, state);
        foreach (var key in source.Keys.Concat(target.Keys).Distinct(StringComparer.Ordinal).ToArray())
        {
            if (!source.TryGetValue(key, out var sourceItem)) { state.TargetOnlyRetained++; continue; }
            if (!target.TryGetValue(key, out var targetItem))
            {
                if (IsFederatedProperty(sourceItem))
                {
                    var copy = new XElement(sourceItem);
                    SetScalar(copy, "data_type", "text");
                    SetScalar(copy, "is_federated", "1");
                    SetScalar(copy, "is_discoverable", "1");
                    targetContainer.Add(copy);
                    state.FederatedCopied++;
                }
                sourceItem.Remove();
                state.SourceOnlyDeleted++;
                continue;
            }
            if (SemanticallyEqual(sourceItem, targetItem))
            {
                sourceItem.Remove();
                targetItem.Remove();
                state.EqualPairsDeleted++;
                continue;
            }
            state.DifferentPairsProcessed++;
            ProcessDifferentItem(sourceItem, targetItem, path + "/Item[" + key + "]", state);
        }
    }

    private static void ProcessDifferentItem(XElement source, XElement target, string path, State state)
    {
        ProcessScalars(source, target, path, state);

        var sourceRelationships = source.Elements().Where(e => e.Name.LocalName == "Relationships").ToArray();
        var targetRelationships = target.Elements().Where(e => e.Name.LocalName == "Relationships").ToArray();
        if (sourceRelationships.Length > 1 || targetRelationships.Length > 1)
            state.Reviews.Add(new("AmbiguousRelationships", path, path, "同一 Item 具有重複 Relationships container。 "));
        else if (sourceRelationships.Length == 1)
        {
            var targetRelationshipsElement = targetRelationships.SingleOrDefault();
            if (targetRelationshipsElement is null)
            {
                targetRelationshipsElement = new XElement(sourceRelationships[0].Name);
                target.Add(targetRelationshipsElement);
            }
            ProcessItemContainer(sourceRelationships[0], targetRelationshipsElement, path + "/Relationships", state);
        }

        var sourceProperties = source.Elements().Where(IsItemProperty).GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.ToArray());
        var targetProperties = target.Elements().Where(IsItemProperty).GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var name in sourceProperties.Keys.Concat(targetProperties.Keys).Distinct())
        {
            sourceProperties.TryGetValue(name, out var sourceGroup);
            targetProperties.TryGetValue(name, out var targetGroup);
            sourceGroup ??= [];
            targetGroup ??= [];
            if (sourceGroup.Length > 1 || targetGroup.Length > 1)
            {
                state.Reviews.Add(new("AmbiguousItemProperty", path + "/" + name.LocalName, path + "/" + name.LocalName,
                    "Item Property 無法唯一配對。 "));
                continue;
            }
            if (sourceGroup.Length == 0 || targetGroup.Length == 0) continue;
            ProcessItemContainer(sourceGroup[0], targetGroup[0], path + "/" + name.LocalName, state);
        }
    }

    private static void ProcessScalars(XElement source, XElement target, string path, State state)
    {
        var sourceGroups = source.Elements().Where(IsScalar).GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.ToArray());
        var targetGroups = target.Elements().Where(IsScalar).GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var name in sourceGroups.Keys.Concat(targetGroups.Keys).Distinct())
        {
            sourceGroups.TryGetValue(name, out var sourceGroup);
            targetGroups.TryGetValue(name, out var targetGroup);
            sourceGroup ??= [];
            targetGroup ??= [];
            if (sourceGroup.Length > 1 || targetGroup.Length > 1)
            {
                state.Reviews.Add(new("AmbiguousScalarProperty", path + "/" + name.LocalName, path + "/" + name.LocalName,
                    "直接 Scalar Property 無法唯一配對；此 Property 保留兩端並等待人工處置。 "));
                continue;
            }
            var left = sourceGroup.SingleOrDefault();
            var right = targetGroup.SingleOrDefault();
            ApplyScalar(left, right, target, state);
        }
    }

    private static void ApplyScalar(XElement? source, XElement? target, XElement targetItem, State state)
    {
        var name = (source ?? target)!.Name.LocalName;
        foreach (var step in state.Steps)
        {
            switch (step.Kind)
            {
                case RuleStepKind.RemoveEqualScalarProperties when source is not null && target is not null && XNode.DeepEquals(source, target):
                    source.Remove(); target.Remove(); state.ScalarsDeleted += 2; return;
                case RuleStepKind.RemoveNamedProperties when Contains(step, name):
                    if (source is not null) { source.Remove(); state.ScalarsDeleted++; }
                    if (target is not null) { target.Remove(); state.ScalarsDeleted++; }
                    return;
                case RuleStepKind.PreferGreaterSourceNumber when Contains(step, name) && source is not null && target is not null:
                    if (decimal.TryParse(source.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var left) &&
                        decimal.TryParse(target.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var right) && left > right)
                        UpdateTarget(target, source, state);
                    return;
                case RuleStepKind.PreferSourceUnderTargetPath when Contains(step, name):
                    if (PathMatches(state.TargetPath, step.TargetPathConstraint) && source is not null)
                        AddOrUpdateTarget(target, source, targetItem, state);
                    return;
                case RuleStepKind.KeepTargetForValuePairs when step.ValuePairs.Any(pair => pair.PropertyName == name):
                    if (source is not null && (target is null || !step.ValuePairs.Any(pair => PairMatches(pair, source.Value, target.Value))))
                        AddOrUpdateTarget(target, source, targetItem, state);
                    return;
                case RuleStepKind.KeepTargetNamedProperties when Contains(step, name): return;
                case RuleStepKind.DefaultPreferSourceUnlessSourceEmpty:
                    if (source is not null && !(string.IsNullOrEmpty(source.Value) && target is not null && !string.IsNullOrEmpty(target.Value)))
                        AddOrUpdateTarget(target, source, targetItem, state);
                    return;
            }
        }
    }

    private static Dictionary<string, XElement> IndexItems(XElement container, string path, bool source, State state)
    {
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var item in container.Elements().Where(e => e.Name.LocalName == "Item"))
        {
            var keyResult = PackageCompareKey.Create(AmlDocument.Parse(new XElement("AML", new XElement(item)).ToString(SaveOptions.DisableFormatting)).TopLevelItems.Single());
            if (keyResult.Status != CompareKeyStatus.Success || result.ContainsKey(keyResult.Key!))
            {
                state.Reviews.Add(new(keyResult.Issue.ToString(), source ? path : null, source ? null : path,
                    keyResult.Reason ?? "Package CompareKey 無法唯一配對。 "));
                continue;
            }
            result.Add(keyResult.Key!, item);
        }
        return result;
    }

    private static bool IsScalar(XElement element) => element.Name.LocalName != "Relationships" && !element.Elements().Any(e => e.Name.LocalName == "Item");
    private static bool IsItemProperty(XElement element) => element.Name.LocalName != "Relationships" && element.Elements().Any(e => e.Name.LocalName == "Item");
    private static bool IsFederatedProperty(XElement item) =>
        string.Equals(item.Attributes().FirstOrDefault(a => a.Name.LocalName == "type")?.Value, "Property", StringComparison.Ordinal) &&
        item.Elements().Any(e => e.Name.LocalName == "data_type" && e.Value == "federated");
    private static bool Contains(RuleStepDefinition step, string name) => step.PropertyNames.Contains(name, StringComparer.Ordinal);
    private static bool PathMatches(string path, string? constraint) => constraint is not null && path.Replace('/', '\\').Contains(constraint.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase);
    private static bool PairMatches(RuleValuePair pair, string source, string target) => Matches(pair.Source, source) && Matches(pair.Target, target);
    private static bool Matches(RuleValueCondition condition, string value) => condition.Kind switch
    {
        RuleValueConditionKind.Exact => string.Equals(value, condition.Value, StringComparison.Ordinal),
        RuleValueConditionKind.Empty => string.IsNullOrEmpty(value),
        RuleValueConditionKind.NonEmpty => !string.IsNullOrEmpty(value),
        _ => false
    };
    private static void AddOrUpdateTarget(XElement? target, XElement source, XElement targetItem, State state)
    {
        if (target is null) { targetItem.Add(new XElement(source)); state.ScalarsUpdated++; }
        else UpdateTarget(target, source, state);
    }
    private static void UpdateTarget(XElement target, XElement source, State state) { target.ReplaceWith(new XElement(source)); state.ScalarsUpdated++; }
    private static void SetScalar(XElement item, string name, string value)
    {
        var element = item.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        if (element is null) item.Add(new XElement(item.Name.Namespace + name, value)); else element.Value = value;
    }
    private static bool SemanticallyEqual(XElement source, XElement target)
    {
        var left = AmlDocument.Parse(new XElement("AML", new XElement(source)).ToString(SaveOptions.DisableFormatting));
        var right = AmlDocument.Parse(new XElement("AML", new XElement(target)).ToString(SaveOptions.DisableFormatting));
        return AmlSemanticComparer.Compare(left, right).Status == AmlComparisonStatus.Equal;
    }
    private static AmlDocument ParseLike(AmlDocument original, XElement root)
    {
        var declaration = original.Declaration;
        var xml = declaration is null ? root.ToString(SaveOptions.DisableFormatting) : $"{declaration}{Environment.NewLine}{root.ToString(SaveOptions.DisableFormatting)}";
        return AmlDocument.Parse(xml, original.SourceName);
    }
    private static string NormalizePath(string path) => (path ?? string.Empty).Replace('/', '\\');
    private static void ValidateRule2Steps(IReadOnlyList<RuleStepDefinition> steps)
    {
        var draft = new RuleSetDraft(Guid.NewGuid(), Guid.NewGuid(), "resolved Rule 2", RuleSetKind.Rule2,
            RuleSetScope.Common, null, null, steps, DateTimeOffset.UnixEpoch, "resolved-rule-snapshot");
        if (!RuleSetValidator.Validate(draft).IsValid || steps.Any(step => step.Kind == RuleStepKind.Rule1ItemDisposition))
            throw new InvalidOperationException("解析結果不是有效的 Rule 2 規則快照。 ");
    }

    private sealed class State(IReadOnlyList<RuleStepDefinition> steps, string targetPath)
    {
        public IReadOnlyList<RuleStepDefinition> Steps { get; } = steps;
        public string TargetPath { get; } = targetPath;
        public List<Rule2ManualReview> Reviews { get; } = [];
        public int SourceOnlyDeleted, TargetOnlyRetained, EqualPairsDeleted, DifferentPairsProcessed;
        public int ScalarsDeleted, ScalarsUpdated, FederatedCopied;
    }
}
