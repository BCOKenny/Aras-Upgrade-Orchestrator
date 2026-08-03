using System.Xml.Linq;

namespace ArasUpgradeOrchestrator.Core.Aml;

public enum AmlComparisonStatus
{
    Equal,
    Different,
    ManualReview
}

public enum AmlComparisonIssueCode
{
    MissingCompareKey,
    DuplicateCompareKey,
    DuplicateScalarProperty,
    DuplicateItemProperty,
    StructureDifferent,
    ValueDifferent,
    AttributesDifferent
}

public sealed record AmlComparisonIssue(
    AmlComparisonIssueCode Code,
    string? LeftPath,
    string? RightPath,
    string Message);

public sealed record AmlComparisonResult(AmlComparisonStatus Status, IReadOnlyList<AmlComparisonIssue> Issues);

public static class AmlSemanticComparer
{
    public static AmlComparisonResult Compare(AmlDocument left, AmlDocument right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var context = new ComparisonContext();
        if (left.Root.QualifiedName != right.Root.QualifiedName)
            context.Different(AmlComparisonIssueCode.StructureDifferent, left.Root.Path, right.Root.Path, "AML Root Namespace 或名稱不同。");
        if (!AttributesEqual(left.Root, right.Root))
            context.Different(AmlComparisonIssueCode.AttributesDifferent, left.Root.Path, right.Root.Path, "AML Root attributes 不同。");
        CompareItemCollections(left.TopLevelItems, right.TopLevelItems, context);
        return context.Result();
    }

    private static void CompareItemCollections(
        IEnumerable<AmlNode> leftItems,
        IEnumerable<AmlNode> rightItems,
        ComparisonContext context)
    {
        var leftIndex = PackageCompareKeyIndex.Build(leftItems);
        var rightIndex = PackageCompareKeyIndex.Build(rightItems);
        AddKeyReviews(leftIndex.ManualReviews, true, context);
        AddKeyReviews(rightIndex.ManualReviews, false, context);

        var allKeys = leftIndex.UniqueItems.Keys.Concat(rightIndex.UniqueItems.Keys).Distinct(StringComparer.Ordinal);
        foreach (var key in allKeys)
        {
            var hasLeft = leftIndex.UniqueItems.TryGetValue(key, out var left);
            var hasRight = rightIndex.UniqueItems.TryGetValue(key, out var right);
            if (!hasLeft || !hasRight)
            {
                context.Different(
                    AmlComparisonIssueCode.StructureDifferent,
                    left?.Path,
                    right?.Path,
                    $"CompareKey {key} 只存在於單側。");
                continue;
            }
            CompareItem(left!, right!, context);
        }
    }

    private static void CompareItem(AmlNode left, AmlNode right, ComparisonContext context)
    {
        if (left.QualifiedName != right.QualifiedName)
            context.Different(AmlComparisonIssueCode.StructureDifferent, left.Path, right.Path, "Item Namespace 或名稱不同。");
        if (!AttributesEqual(left, right))
            context.Different(AmlComparisonIssueCode.AttributesDifferent, left.Path, right.Path, "Item attributes 不同。");

        CompareNamedProperties(
            left.Children.Where(node => node.Kind == AmlNodeKind.ScalarProperty),
            right.Children.Where(node => node.Kind == AmlNodeKind.ScalarProperty),
            AmlNodeKind.ScalarProperty,
            context);
        CompareNamedProperties(
            left.Children.Where(node => node.Kind == AmlNodeKind.ItemProperty),
            right.Children.Where(node => node.Kind == AmlNodeKind.ItemProperty),
            AmlNodeKind.ItemProperty,
            context);

        var leftRelationships = left.Children.Where(node => node.Kind == AmlNodeKind.RelationshipsContainer).ToArray();
        var rightRelationships = right.Children.Where(node => node.Kind == AmlNodeKind.RelationshipsContainer).ToArray();
        if (leftRelationships.Length > 1 || rightRelationships.Length > 1)
        {
            context.Manual(AmlComparisonIssueCode.StructureDifferent, left.Path, right.Path, "同一 Item 含多個 Relationships Container，無法可靠配對。");
        }
        else if (leftRelationships.Length != rightRelationships.Length)
        {
            context.Different(AmlComparisonIssueCode.StructureDifferent, left.Path, right.Path, "Relationships Container 只存在於單側。");
        }
        else if (leftRelationships.Length == 1)
        {
            if (!AttributesEqual(leftRelationships[0], rightRelationships[0]))
                context.Different(AmlComparisonIssueCode.AttributesDifferent, leftRelationships[0].Path, rightRelationships[0].Path,
                    "Relationships Container attributes 不同。");
            CompareItemCollections(leftRelationships[0].Children.Where(node => node.Kind == AmlNodeKind.RelationshipItem),
                rightRelationships[0].Children.Where(node => node.Kind == AmlNodeKind.RelationshipItem), context);
        }

        var leftUnexpected = left.Children.Where(node => node.Kind == AmlNodeKind.Item).ToArray();
        var rightUnexpected = right.Children.Where(node => node.Kind == AmlNodeKind.Item).ToArray();
        if (leftUnexpected.Length > 0 || rightUnexpected.Length > 0)
            context.Manual(AmlComparisonIssueCode.StructureDifferent, leftUnexpected.FirstOrDefault()?.Path, rightUnexpected.FirstOrDefault()?.Path,
                "Item 直接包含 Item，缺少 Item Property 或 Relationships 語意邊界。");
    }

    private static void CompareNamedProperties(
        IEnumerable<AmlNode> leftNodes,
        IEnumerable<AmlNode> rightNodes,
        AmlNodeKind kind,
        ComparisonContext context)
    {
        var leftGroups = leftNodes.GroupBy(node => node.QualifiedName).ToDictionary(group => group.Key, group => group.ToArray());
        var rightGroups = rightNodes.GroupBy(node => node.QualifiedName).ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var group in leftGroups.Values.Where(group => group.Length > 1)) AddDuplicatePropertyIssues(group, true, kind, context);
        foreach (var group in rightGroups.Values.Where(group => group.Length > 1)) AddDuplicatePropertyIssues(group, false, kind, context);

        foreach (var name in leftGroups.Keys.Concat(rightGroups.Keys).Distinct())
        {
            var hasLeft = leftGroups.TryGetValue(name, out var leftGroup);
            var hasRight = rightGroups.TryGetValue(name, out var rightGroup);
            if (!hasLeft || !hasRight)
            {
                context.Different(AmlComparisonIssueCode.StructureDifferent, leftGroup?.FirstOrDefault()?.Path, rightGroup?.FirstOrDefault()?.Path,
                    $"Property {name} 只存在於單側。");
                continue;
            }
            if (leftGroup!.Length != 1 || rightGroup!.Length != 1) continue;
            var left = leftGroup[0];
            var right = rightGroup[0];
            if (!AttributesEqual(left, right))
                context.Different(AmlComparisonIssueCode.AttributesDifferent, left.Path, right.Path, $"Property {name} attributes 不同。");
            if (kind == AmlNodeKind.ScalarProperty)
            {
                if (!string.Equals(left.ScalarValue, right.ScalarValue, StringComparison.Ordinal))
                    context.Different(AmlComparisonIssueCode.ValueDifferent, left.Path, right.Path, $"Scalar Property {name} 值不同。");
            }
            else
            {
                CompareItemCollections(left.Children.Where(node => node.IsItem), right.Children.Where(node => node.IsItem), context);
            }
        }
    }

    private static void AddDuplicatePropertyIssues(AmlNode[] nodes, bool isLeft, AmlNodeKind kind, ComparisonContext context)
    {
        var code = kind == AmlNodeKind.ScalarProperty
            ? AmlComparisonIssueCode.DuplicateScalarProperty
            : AmlComparisonIssueCode.DuplicateItemProperty;
        foreach (var node in nodes)
            context.Manual(code, isLeft ? node.Path : null, isLeft ? null : node.Path,
                $"同一 Item 內有多個同名 {kind}，無法可靠配對。");
    }

    private static void AddKeyReviews(IEnumerable<PackageCompareKeyResult> reviews, bool isLeft, ComparisonContext context)
    {
        foreach (var review in reviews)
        {
            var code = review.Issue == CompareKeyIssue.DuplicateOnSameSide
                ? AmlComparisonIssueCode.DuplicateCompareKey
                : AmlComparisonIssueCode.MissingCompareKey;
            context.Manual(code, isLeft ? review.AmlPath : null, isLeft ? null : review.AmlPath, review.Reason ?? "CompareKey 需要人工確認。");
        }
    }

    private static bool AttributesEqual(AmlNode left, AmlNode right)
    {
        var leftAttributes = ComparableAttributes(left);
        var rightAttributes = ComparableAttributes(right);
        return leftAttributes.Count == rightAttributes.Count &&
               leftAttributes.All(attribute => rightAttributes.TryGetValue(attribute.Key, out var value) &&
                                               string.Equals(attribute.Value, value, StringComparison.Ordinal));
    }

    private static Dictionary<XName, string> ComparableAttributes(AmlNode node) =>
        node.Attributes
            .Where(attribute => attribute.Key.Namespace != XNamespace.Xmlns && attribute.Key.LocalName != "xmlns")
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);

    private sealed class ComparisonContext
    {
        private readonly List<AmlComparisonIssue> _issues = [];
        private bool _different;
        private bool _manual;

        public void Different(AmlComparisonIssueCode code, string? left, string? right, string message)
        {
            _different = true;
            _issues.Add(new AmlComparisonIssue(code, left, right, message));
        }

        public void Manual(AmlComparisonIssueCode code, string? left, string? right, string message)
        {
            _manual = true;
            _issues.Add(new AmlComparisonIssue(code, left, right, message));
        }

        public AmlComparisonResult Result() => new(
            _manual ? AmlComparisonStatus.ManualReview : _different ? AmlComparisonStatus.Different : AmlComparisonStatus.Equal,
            _issues);
    }
}
