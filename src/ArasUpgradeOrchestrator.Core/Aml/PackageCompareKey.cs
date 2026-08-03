using System.Text;

namespace ArasUpgradeOrchestrator.Core.Aml;

public enum CompareKeyStatus
{
    Success,
    ManualReview
}

public enum CompareKeyIssue
{
    None,
    NotAnItem,
    MissingType,
    MissingAction,
    MissingIdAndWhere,
    InvalidWhere,
    DuplicateOnSameSide
}

public sealed record PackageCompareKeyResult(
    CompareKeyStatus Status,
    string? Key,
    CompareKeyIssue Issue,
    string AmlPath,
    string? Reason);

public static class PackageCompareKey
{
    public static PackageCompareKeyResult Create(AmlNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsItem)
            return Manual(node, CompareKeyIssue.NotAnItem, "Package CompareKey 只能套用至 Item 或 Relationship Item。");
        if (string.IsNullOrWhiteSpace(node.ItemType))
            return Manual(node, CompareKeyIssue.MissingType, "Item 缺少 type，無法建立可靠 Package CompareKey。");
        if (string.IsNullOrWhiteSpace(node.Action))
            return Manual(node, CompareKeyIssue.MissingAction, "Item 缺少 action；不得補預設值。");

        string? identity;
        try
        {
            identity = !string.IsNullOrWhiteSpace(node.ItemId)
                ? Normalize(node.ItemId)
                : !string.IsNullOrWhiteSpace(node.Where)
                    ? CanonicalizeWhere(node.Where)
                    : null;
        }
        catch (FormatException)
        {
            return Manual(node, CompareKeyIssue.InvalidWhere, "Item where 含未關閉的引號，無法安全 canonicalize。");
        }
        if (string.IsNullOrWhiteSpace(identity))
            return Manual(node, CompareKeyIssue.MissingIdAndWhere, "Item 同時缺少 id 與可用的 where。");

        return new PackageCompareKeyResult(
            CompareKeyStatus.Success,
            $"{Normalize(node.ItemType)}|{identity}|{Normalize(node.Action)}",
            CompareKeyIssue.None,
            node.Path,
            null);
    }

    public static string CanonicalizeWhere(string where)
    {
        ArgumentNullException.ThrowIfNull(where);
        var result = new StringBuilder(where.Length);
        var quote = '\0';
        var pendingSpace = false;
        for (var index = 0; index < where.Length; index++)
        {
            var character = where[index];
            if (quote != '\0')
            {
                result.Append(character);
                if (character == quote)
                {
                    if (index + 1 < where.Length && where[index + 1] == quote)
                        result.Append(where[++index]);
                    else
                        quote = '\0';
                }
                continue;
            }

            if (character is '\'' or '"')
            {
                if (pendingSpace && result.Length > 0) result.Append(' ');
                pendingSpace = false;
                quote = character;
                result.Append(character);
            }
            else if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
            }
            else
            {
                if (pendingSpace && result.Length > 0) result.Append(' ');
                pendingSpace = false;
                result.Append(character);
            }
        }
        if (quote != '\0') throw new FormatException("where 含未關閉的引號。");
        return result.ToString();
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static PackageCompareKeyResult Manual(AmlNode node, CompareKeyIssue issue, string reason) =>
        new(CompareKeyStatus.ManualReview, null, issue, node.Path, reason);
}

public sealed record PackageCompareKeyIndex(
    IReadOnlyDictionary<string, AmlNode> UniqueItems,
    IReadOnlyList<PackageCompareKeyResult> ManualReviews)
{
    public static PackageCompareKeyIndex Build(IEnumerable<AmlNode> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var candidates = items.Select(item => (Item: item, Result: PackageCompareKey.Create(item))).ToArray();
        var reviews = candidates
            .Where(candidate => candidate.Result.Status == CompareKeyStatus.ManualReview)
            .Select(candidate => candidate.Result)
            .ToList();
        var unique = new Dictionary<string, AmlNode>(StringComparer.Ordinal);

        foreach (var group in candidates
                     .Where(candidate => candidate.Result.Status == CompareKeyStatus.Success)
                     .GroupBy(candidate => candidate.Result.Key!, StringComparer.Ordinal))
        {
            var groupItems = group.ToArray();
            if (groupItems.Length == 1)
            {
                unique.Add(group.Key, groupItems[0].Item);
                continue;
            }

            reviews.AddRange(groupItems.Select(candidate => new PackageCompareKeyResult(
                CompareKeyStatus.ManualReview,
                group.Key,
                CompareKeyIssue.DuplicateOnSameSide,
                candidate.Item.Path,
                "同側 Package CompareKey 重複，無法建立可靠一對一配對。")));
        }

        return new PackageCompareKeyIndex(unique, reviews);
    }
}
