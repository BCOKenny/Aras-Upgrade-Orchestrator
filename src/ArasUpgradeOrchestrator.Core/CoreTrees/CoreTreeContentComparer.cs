using System.Text;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public static class CoreTreeContentComparer
{
    private static readonly HashSet<string> ClientTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".ts", ".tsx", ".html", ".cshtml", ".htm", ".xml"
    };

    public static async Task<bool> AreEqualAsync(
        string leftPath,
        string rightPath,
        string relativePath,
        CoreTreeServerTextRuleSet serverTextRules,
        CancellationToken cancellationToken = default)
        => (await CompareAsync(leftPath, rightPath, relativePath, serverTextRules, cancellationToken)).AreEqual;

    public static async Task<CoreTreeContentComparison> CompareAsync(
        string leftPath,
        string rightPath,
        string relativePath,
        CoreTreeServerTextRuleSet serverTextRules,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var useText = normalized.StartsWith("Client/", StringComparison.OrdinalIgnoreCase)
            ? ClientTextExtensions.Contains(Path.GetExtension(normalized))
            : normalized.StartsWith("Server/", StringComparison.OrdinalIgnoreCase) &&
              serverTextRules.RelativePaths.Select(NormalizeRelativePath)
                  .Contains(normalized, StringComparer.OrdinalIgnoreCase);

        if (useText)
        {
            var left = await TryReadTextAsync(leftPath, cancellationToken);
            var right = await TryReadTextAsync(rightPath, cancellationToken);
            if (left.Text is not null && right.Text is not null)
                return new(NormalizeLineEndings(left.Text) == NormalizeLineEndings(right.Text),
                    CoreTreeContentComparisonMode.Text, null);
            var equal = await BinaryEqualsAsync(leftPath, rightPath, cancellationToken);
            return new(equal, CoreTreeContentComparisonMode.BinaryFallback,
                left.Error ?? right.Error ?? "文字檔無法可靠解碼，已改用二進位比較。 ");
        }
        return new(await BinaryEqualsAsync(leftPath, rightPath, cancellationToken),
            CoreTreeContentComparisonMode.Binary, null);
    }

    private static async Task<(string? Text, string? Error)> TryReadTextAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            Encoding encoding;
            var offset = 0;
            if (bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()))
            {
                encoding = new UTF8Encoding(false, true);
                offset = Encoding.UTF8.GetPreamble().Length;
            }
            else if (bytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
            {
                encoding = new UnicodeEncoding(false, true, true);
                offset = Encoding.Unicode.GetPreamble().Length;
            }
            else if (bytes.AsSpan().StartsWith(Encoding.BigEndianUnicode.GetPreamble()))
            {
                encoding = new UnicodeEncoding(true, true, true);
                offset = Encoding.BigEndianUnicode.GetPreamble().Length;
            }
            else
            {
                encoding = new UTF8Encoding(false, true);
            }
            return (encoding.GetString(bytes, offset, bytes.Length - offset).TrimStart('\uFEFF'), null);
        }
        catch (DecoderFallbackException)
        {
            return (null, $"{Path.GetFileName(path)} 無法可靠解碼，已改用二進位比較。 ");
        }
    }

    private static string NormalizeLineEndings(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static async Task<bool> BinaryEqualsAsync(string leftPath, string rightPath, CancellationToken cancellationToken)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length) return false;
        await using var left = new FileStream(leftPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        await using var right = new FileStream(rightPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        var leftBuffer = new byte[81920];
        var rightBuffer = new byte[81920];
        while (true)
        {
            var leftRead = await left.ReadAsync(leftBuffer, cancellationToken);
            var rightRead = await right.ReadAsync(rightBuffer, cancellationToken);
            if (leftRead != rightRead) return false;
            if (leftRead == 0) return true;
            if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead))) return false;
        }
    }

    internal static string NormalizeRelativePath(string path) => path.Replace('\\', '/').TrimStart('/');
}

public static class DefaultCoreTreeServerTextRules
{
    public static CoreTreeServerTextRuleSet Create() =>
        CoreTreeServerTextRuleSet.Create("server-text-1", ["Server/method-config.xml"]);
}
