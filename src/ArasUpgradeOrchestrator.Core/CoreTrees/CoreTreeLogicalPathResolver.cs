namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public static class CoreTreeLogicalPathResolver
{
    private static readonly IReadOnlyDictionary<string, string[]> Evolutions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".htm"] = [".html", ".cshtml"],
            [".html"] = [".cshtml"],
            [".js"] = [".ts", ".tsx"]
        };

    public static CoreTreeLogicalMatch Resolve(string sourceRelativePath, string targetInnovatorRoot)
    {
        var relative = CoreTreeContentComparer.NormalizeRelativePath(sourceRelativePath);
        var exact = ToFullPath(targetInnovatorRoot, relative);
        if (File.Exists(exact)) return new(CoreTreeLogicalMatchStatus.Unique, [relative], null);

        var extension = Path.GetExtension(relative);
        if (!Evolutions.TryGetValue(extension, out var targets))
            return new(CoreTreeLogicalMatchStatus.None, [], null);
        var directory = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(relative);
        var candidates = targets
            .Select(target => string.IsNullOrEmpty(directory) ? baseName + target : $"{directory}/{baseName}{target}")
            .Where(candidate => File.Exists(ToFullPath(targetInnovatorRoot, candidate)))
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var status = candidates.Length switch
        {
            0 => CoreTreeLogicalMatchStatus.None,
            1 => CoreTreeLogicalMatchStatus.Unique,
            _ => CoreTreeLogicalMatchStatus.Ambiguous
        };
        return new(status, candidates, candidates.Length == 0 ? null : $"{extension} → {string.Join("／", candidates.Select(Path.GetExtension))}");
    }

    internal static string ToFullPath(string innovatorRoot, string relativePath)
    {
        var root = Path.GetFullPath(innovatorRoot);
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Core Tree 相對路徑逸出 Innovator 根目錄。 ");
        return full;
    }
}
