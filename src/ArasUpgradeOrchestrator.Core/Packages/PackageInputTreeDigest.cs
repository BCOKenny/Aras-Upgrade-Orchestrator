using System.Security.Cryptography;
using System.Text;

namespace ArasUpgradeOrchestrator.Core.Packages;

public sealed record PackageInputTreeDigestResult(string RootPath, int FileCount, long TotalBytes, string TreeChecksum);

public static class PackageInputTreeDigest
{
    public static async Task<PackageInputTreeDigestResult> ComputeAsync(string root, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("輸入根目錄不可為空。", nameof(root));
        var fullRoot = Path.GetFullPath(root);
        var rootInfo = new DirectoryInfo(fullRoot);
        if (!rootInfo.Exists) throw new DirectoryNotFoundException("找不到 Package 輸入根目錄。 ");
        if (IsReparsePoint(rootInfo.Attributes)) throw new InvalidDataException("Package 輸入根目錄不可為 reparse point。 ");

        var files = Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderBy(file => Path.GetRelativePath(fullRoot, file.FullName), StringComparer.Ordinal)
            .ToArray();
        using var treeHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long totalBytes = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsReparsePoint(file.Attributes)) throw new InvalidDataException("Package 輸入不可包含 reparse point 檔案。 ");
            var relativePath = Path.GetRelativePath(fullRoot, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
            var fileHash = await ComputeFileHashAsync(file.FullName, cancellationToken);
            Append(treeHash, relativePath);
            Append(treeHash, file.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(treeHash, fileHash);
            totalBytes += file.Length;
        }
        return new PackageInputTreeDigestResult(fullRoot, files.Length, totalBytes, Convert.ToHexString(treeHash.GetHashAndReset()));
    }

    private static bool IsReparsePoint(FileAttributes attributes) => (attributes & FileAttributes.ReparsePoint) != 0;

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }
}
