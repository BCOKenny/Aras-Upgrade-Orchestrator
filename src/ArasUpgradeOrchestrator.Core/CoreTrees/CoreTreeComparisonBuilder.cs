using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public static class CoreTreeComparisonBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<CoreTreeComparisonResult> BuildAsync(
        CoreTreeComparisonRequest request,
        DirectoryLeaseManager leaseManager,
        Func<DateTimeOffset>? clock = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseManager);
        CoreTreeInputValidator.Validate(request);
        var classification = await CoreTreeComparisonEngine.CompareAsync(request, clock, cancellationToken);
        ValidateClassification(request, classification);
        return await WriteDeliveryAsync(request, classification, leaseManager, clock, cancellationToken);
    }

    public static async Task<CoreTreeComparisonResult> BuildFromClassificationAsync(
        CoreTreeComparisonRequest request,
        CoreTreeComparisonResult classification,
        DirectoryLeaseManager leaseManager,
        Func<DateTimeOffset>? clock = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaseManager);
        ArgumentNullException.ThrowIfNull(classification);
        CoreTreeInputValidator.Validate(request);
        ValidateClassification(request, classification);
        return await WriteDeliveryAsync(request, classification, leaseManager, clock, cancellationToken);
    }

    private static void ValidateClassification(CoreTreeComparisonRequest request, CoreTreeComparisonResult classification)
    {
        if (classification.AttemptId != request.AttemptId ||
            !string.Equals(Path.GetFullPath(classification.OutputRoot), Path.GetFullPath(request.OutputRoot), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(classification.ServerRuleVersion, request.ServerTextRules.Version, StringComparison.Ordinal) ||
            !string.Equals(classification.ServerRuleChecksum, request.ServerTextRules.Checksum, StringComparison.OrdinalIgnoreCase))
            throw InvalidClassification("已宣告的 Core Tree 分類結果與交付請求不一致。 ");
        if (classification.Status is not (CoreTreeComparisonStatus.ReadyToComplete or CoreTreeComparisonStatus.Blocked))
            throw InvalidClassification("交付只接受 ReadyToComplete 或 Blocked 的分類結果。 ");
        if (classification.Status == CoreTreeComparisonStatus.ReadyToComplete &&
            (classification.ManualReviews.Count != 0 || classification.Errors.Count != 0))
            throw InvalidClassification("ReadyToComplete 分類結果不得包含人工確認或錯誤。 ");
        if (classification.Status == CoreTreeComparisonStatus.Blocked &&
            classification.ManualReviews.Count == 0 && classification.Errors.Count == 0)
            throw InvalidClassification("Blocked 分類結果必須包含人工確認或錯誤。 ");

        foreach (var item in classification.Items)
        {
            EnsureSafeCoreTreePath(item.SourceRelativePath, "來源檔案");
            switch (item.Classification)
            {
                case CoreTreeClassification.A:
                case CoreTreeClassification.B:
                    if (item.TargetRelativePath is not null)
                        throw InvalidClassification("A 或 B 分類不得指定目標檔案。 ");
                    break;
                case CoreTreeClassification.C:
                    EnsureSafeCoreTreePath(item.TargetRelativePath, "目標檔案");
                    break;
                default:
                    throw InvalidClassification("分類項目包含不支援的分類。 ");
            }
        }

        foreach (var review in classification.ManualReviews)
        {
            EnsureSafeCoreTreePath(review.SourceRelativePath, "人工確認檔案");
            foreach (var candidate in review.TargetCandidates) EnsureSafeCoreTreePath(candidate, "人工確認候選檔案");
        }
        foreach (var error in classification.Errors) EnsureSafeCoreTreePath(error.RelativePath, "錯誤檔案");
        foreach (var notice in classification.Notices) EnsureSafeCoreTreePath(notice.RelativePath, "通知檔案");
    }

    private static CoreTreeValidationException InvalidClassification(string message) => new("InvalidRequest", message);

    private static void EnsureSafeCoreTreePath(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path)) throw InvalidClassification($"{label}不可為空。 ");
        var normalized = path.Replace('\\', '/');
        if (Path.IsPathRooted(path) || normalized.Split('/').Any(segment => segment is "" or "." or "..") ||
            (!normalized.StartsWith("Client/", StringComparison.OrdinalIgnoreCase) && !normalized.StartsWith("Server/", StringComparison.OrdinalIgnoreCase)))
            throw InvalidClassification($"{label}必須是 Client 或 Server 下的安全相對路徑。 ");
    }

    private static async Task<CoreTreeComparisonResult> WriteDeliveryAsync(
        CoreTreeComparisonRequest request,
        CoreTreeComparisonResult classification,
        DirectoryLeaseManager leaseManager,
        Func<DateTimeOffset>? clock,
        CancellationToken cancellationToken)
    {
        await using var lease = await leaseManager.AcquireAsync([request.OutputRoot], cancellationToken);
        Directory.CreateDirectory(request.OutputRoot);
        try
        {
            if (classification.Status == CoreTreeComparisonStatus.ReadyToComplete)
                await CopyClassifiedItemsAsync(request, classification.Items, cancellationToken);

            await WriteJsonAsync(Path.Combine(request.OutputRoot, "processing-summary.json"), new
            {
                classification.AttemptId,
                SourceVersion = request.SourceVersion,
                TargetVersion = request.TargetVersion,
                Customer = request.Customer,
                SourceOotb = request.SourceOotb,
                TargetOotb = request.TargetOotb,
                ServerTextRules = new { request.ServerTextRules.Version, request.ServerTextRules.Checksum },
                classification.StartedAt,
                classification.FinishedAt,
                Counts = new
                {
                    A = classification.Items.Count(item => item.Classification == CoreTreeClassification.A),
                    B = classification.Items.Count(item => item.Classification == CoreTreeClassification.B),
                    C = classification.Items.Count(item => item.Classification == CoreTreeClassification.C),
                    ManualReview = classification.ManualReviews.Count,
                    Errors = classification.Errors.Count,
                    Notices = classification.Notices.Count
                }
            }, cancellationToken);

            if (classification.ManualReviews.Count > 0)
                await WriteJsonAsync(Path.Combine(request.OutputRoot, "manual-reviews.json"), classification.ManualReviews, cancellationToken);
            if (classification.Errors.Count > 0)
                await WriteJsonAsync(Path.Combine(request.OutputRoot, "errors.json"), classification.Errors, cancellationToken);
            if (classification.Notices.Count > 0)
                await WriteJsonAsync(Path.Combine(request.OutputRoot, "notices.json"), classification.Notices, cancellationToken);

            var completed = classification.Status == CoreTreeComparisonStatus.ReadyToComplete;
            var final = classification with { Status = completed ? CoreTreeComparisonStatus.Completed : CoreTreeComparisonStatus.Incomplete };
            var markerName = completed ? "completion-manifest.json" : "incomplete-manifest.json";
            await WriteJsonAsync(Path.Combine(request.OutputRoot, markerName), new
            {
                final.AttemptId,
                Status = final.Status.ToString(),
                final.StartedAt,
                final.FinishedAt,
                final.ServerRuleVersion,
                final.ServerRuleChecksum,
                ManualReviewCount = final.ManualReviews.Count,
                ErrorCount = final.Errors.Count
            }, cancellationToken);
            return final;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or OperationCanceledException)
        {
            var marker = Path.Combine(request.OutputRoot, "incomplete-manifest.json");
            if (!File.Exists(marker))
            {
                await WriteJsonAsync(marker, new
                {
                    request.AttemptId,
                    Status = CoreTreeComparisonStatus.Incomplete.ToString(),
                    request.StartedAt,
                    InterruptedAt = (clock ?? (() => DateTimeOffset.UtcNow))(),
                    Error = exception.Message
                }, CancellationToken.None);
            }
            throw;
        }
    }

    private static async Task CopyClassifiedItemsAsync(
        CoreTreeComparisonRequest request,
        IReadOnlyList<CoreTreeClassifiedItem> items,
        CancellationToken cancellationToken)
    {
        var customerRoot = Path.Combine(Path.GetFullPath(request.Customer.RootPath), "Innovator");
        var sourceRoot = Path.Combine(Path.GetFullPath(request.SourceOotb.RootPath), "Innovator");
        var targetRoot = Path.Combine(Path.GetFullPath(request.TargetOotb.RootPath), "Innovator");
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outputRelative = item.Classification == CoreTreeClassification.C
                ? item.TargetRelativePath!
                : item.SourceRelativePath;
            await CopyAsync(customerRoot, item.SourceRelativePath,
                Path.Combine(request.OutputRoot, item.Classification.ToString(), "CustomerSource"), outputRelative, cancellationToken);
            if (item.Classification is CoreTreeClassification.B or CoreTreeClassification.C)
                await CopyAsync(sourceRoot, item.SourceRelativePath,
                    Path.Combine(request.OutputRoot, item.Classification.ToString(), "OOTBSource"), outputRelative, cancellationToken);
            if (item.Classification == CoreTreeClassification.C)
                await CopyAsync(targetRoot, item.TargetRelativePath!,
                    Path.Combine(request.OutputRoot, "C", "OOTBR38"), item.TargetRelativePath!, cancellationToken);
        }
    }

    private static async Task CopyAsync(string inputRoot, string inputRelative, string outputRoot, string outputRelative, CancellationToken cancellationToken)
    {
        var source = CoreTreeLogicalPathResolver.ToFullPath(inputRoot, inputRelative);
        var destination = CoreTreeLogicalPathResolver.ToFullPath(outputRoot, outputRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        await using var destinationStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        await destinationStream.FlushAsync(cancellationToken);
    }

    private static async Task WriteJsonAsync(string path, object value, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
