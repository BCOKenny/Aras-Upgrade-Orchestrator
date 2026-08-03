using System.Xml.Linq;
using ArasUpgradeOrchestrator.Core.Aml;

namespace ArasUpgradeOrchestrator.Core.Packages;

public sealed record Rule1DiffSummary(
    int SourceOnlyDeleted,
    int TargetOnlyRetained,
    int EqualPairsDeleted,
    int DifferentPairsRetained);

public sealed record Rule1ManualReview(string Code, string? SourcePath, string? TargetPath, string Message);

public sealed record Rule1DocumentDiff(
    AmlDocument SourceDiff,
    AmlDocument TargetDiff,
    Rule1DiffSummary Summary,
    IReadOnlyList<Rule1ManualReview> ManualReviews);

public static class Rule1DiffEngine
{
    public static Rule1DocumentDiff Compare(AmlDocument source, AmlDocument target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var sourceIndex = PackageCompareKeyIndex.Build(source.TopLevelItems);
        var targetIndex = PackageCompareKeyIndex.Build(target.TopLevelItems);
        var ambiguousKeys = sourceIndex.ManualReviews.Concat(targetIndex.ManualReviews)
            .Where(review => !string.IsNullOrWhiteSpace(review.Key))
            .Select(review => review.Key!)
            .ToHashSet(StringComparer.Ordinal);
        var sourceRemovals = new HashSet<string>(StringComparer.Ordinal);
        var targetRemovals = new HashSet<string>(StringComparer.Ordinal);
        var sourceOnly = 0;
        var targetOnly = 0;
        var equal = 0;
        var different = 0;
        var manualReviews = sourceIndex.ManualReviews.Select(review => FromKeyReview(review, true))
            .Concat(targetIndex.ManualReviews.Select(review => FromKeyReview(review, false)))
            .ToList();

        foreach (var key in sourceIndex.UniqueItems.Keys.Concat(targetIndex.UniqueItems.Keys).Distinct(StringComparer.Ordinal))
        {
            if (ambiguousKeys.Contains(key)) continue;
            var hasSource = sourceIndex.UniqueItems.TryGetValue(key, out var sourceItem);
            var hasTarget = targetIndex.UniqueItems.TryGetValue(key, out var targetItem);
            if (hasSource && !hasTarget)
            {
                sourceRemovals.Add(key);
                sourceOnly++;
                continue;
            }
            if (!hasSource && hasTarget)
            {
                targetOnly++;
                continue;
            }

            var comparison = AmlSemanticComparer.Compare(
                CreateSingleItemDocument(source, sourceItem!),
                CreateSingleItemDocument(target, targetItem!));
            if (comparison.Status == AmlComparisonStatus.Equal)
            {
                sourceRemovals.Add(key);
                targetRemovals.Add(key);
                equal++;
            }
            else if (comparison.Status == AmlComparisonStatus.Different)
            {
                different++;
            }
            else
            {
                manualReviews.AddRange(comparison.Issues.Select(issue => new Rule1ManualReview(
                    issue.Code.ToString(), issue.LeftPath, issue.RightPath, issue.Message)));
            }
        }

        return new Rule1DocumentDiff(
            RemoveItems(source, sourceRemovals),
            RemoveItems(target, targetRemovals),
            new Rule1DiffSummary(sourceOnly, targetOnly, equal, different),
            manualReviews);
    }

    private static AmlDocument CreateSingleItemDocument(AmlDocument original, AmlNode item)
    {
        var root = original.Root.CloneSubtree();
        root.RemoveNodes();
        root.Add(item.CloneSubtree());
        return AmlDocument.Parse(Serialize(original.Declaration, root));
    }

    private static AmlDocument RemoveItems(AmlDocument original, HashSet<string> removalKeys)
    {
        var root = original.Root.CloneSubtree();
        foreach (var element in root.Elements().Where(element => element.Name.LocalName == "Item").ToArray())
        {
            var document = AmlDocument.Parse(Serialize(original.Declaration, new XElement(root.Name, root.Attributes(), new XElement(element))));
            var key = PackageCompareKey.Create(document.TopLevelItems.Single());
            if (key.Status == CompareKeyStatus.Success && removalKeys.Contains(key.Key!)) element.Remove();
        }
        return AmlDocument.Parse(Serialize(original.Declaration, root), original.SourceName);
    }

    private static string Serialize(XDeclaration? declaration, XElement root)
    {
        var document = new XDocument(declaration is null ? null : new XDeclaration(declaration), root);
        return declaration is null
            ? document.ToString(SaveOptions.DisableFormatting)
            : $"{declaration}{Environment.NewLine}{root.ToString(SaveOptions.DisableFormatting)}";
    }

    private static Rule1ManualReview FromKeyReview(PackageCompareKeyResult review, bool source) => new(
        review.Issue.ToString(),
        source ? review.AmlPath : null,
        source ? null : review.AmlPath,
        review.Reason ?? "Package CompareKey 需要人工確認。");
}
