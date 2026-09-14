using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Packages;

public static class Rule1PreparationReceiptStore
{
    public const string ManifestFileName = "preparation-manifest.json";
    public const string SummaryFileName = "processing-summary.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task WriteAsync(string outputRoot, Rule1PreparationReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (string.IsNullOrWhiteSpace(outputRoot)) throw new ArgumentException("receipt 輸出目錄不可為空。", nameof(outputRoot));
        if (receipt.Status is not Rule1PreparationStatus.Draft and not Rule1PreparationStatus.PendingVerification)
            throw new InvalidDataException("Rule 1 預先比較 receipt 狀態無效。 ");
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        if (File.Exists(fullOutputRoot))
            throw new InvalidOperationException("每次 Rule 1 預先比較必須使用不存在的新輸出目錄。 ");
        if (Directory.Exists(fullOutputRoot) && (File.Exists(Path.Combine(fullOutputRoot, ManifestFileName)) || File.Exists(Path.Combine(fullOutputRoot, SummaryFileName))))
            throw new InvalidOperationException("Rule 1 預先比較 receipt 已存在，不可覆寫。 ");
        Directory.CreateDirectory(fullOutputRoot);
        try
        {
            await WriteNewAsync(Path.Combine(fullOutputRoot, ManifestFileName), receipt, cancellationToken);
            await WriteNewAsync(Path.Combine(fullOutputRoot, SummaryFileName), new
            {
                receipt.Status, receipt.AttemptId, receipt.SourceVersion, receipt.TargetVersion, receipt.Summary,
                ErrorCount = receipt.Errors.Count, ManualReviewCount = receipt.ManualReviews.Count
            }, cancellationToken);
        }
        catch
        {
            throw;
        }
    }

    public static async Task<Rule1PreparationReceipt> LoadAsync(string outputRoot, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetFullPath(outputRoot), ManifestFileName);
        if (!File.Exists(path)) throw new FileNotFoundException("找不到 Rule 1 預先比較 receipt。", path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Rule1PreparationReceipt>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Rule 1 預先比較 receipt 內容無效。 ");
    }

    private static async Task WriteNewAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }
}
