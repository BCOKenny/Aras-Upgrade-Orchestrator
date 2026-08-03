namespace ArasUpgradeOrchestrator.Core.Aml;

public sealed record PackageXmlFilePair(string RelativePath, string? SourcePath, string? TargetPath);

public static class PackageXmlPathMatcher
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public static IReadOnlyList<PackageXmlFilePair> Match(string sourceRoot, string targetRoot)
    {
        var source = Enumerate(sourceRoot, nameof(sourceRoot));
        var target = Enumerate(targetRoot, nameof(targetRoot));
        return source.Keys
            .Concat(target.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(relativePath => new PackageXmlFilePair(
                source.TryGetValue(relativePath, out var sourcePath)
                    ? NormalizeRelativePath(Path.GetRelativePath(Path.GetFullPath(sourceRoot), sourcePath))
                    : NormalizeRelativePath(Path.GetRelativePath(Path.GetFullPath(targetRoot), target[relativePath])),
                sourcePath,
                target.TryGetValue(relativePath, out var targetPath) ? targetPath : null))
            .ToArray();
    }

    private static Dictionary<string, string> Enumerate(string root, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Package 根目錄不可為空。", parameterName);
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot)) throw new DirectoryNotFoundException($"Package 根目錄不存在：{fullRoot}");
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(fullRoot, "*", Options))
        {
            if (!string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase)) continue;
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(fullRoot, path));
            if (!result.TryAdd(relativePath, Path.GetFullPath(path)))
                throw new InvalidDataException($"Package 同側包含大小寫無法唯一區分的重複 XML 相對路徑：{relativePath}");
        }
        return result;
    }

    private static string NormalizeRelativePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');
}
