namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public static class CoreTreePathOrdering
{
    public static IOrderedEnumerable<T> ByPath<T>(IEnumerable<T> source, Func<T, string> selector) =>
        source.OrderBy(selector, StringComparer.OrdinalIgnoreCase)
            .ThenBy(selector, StringComparer.Ordinal);

    public static IOrderedEnumerable<string> ByPath(IEnumerable<string> paths) =>
        ByPath(paths, path => path);
}
