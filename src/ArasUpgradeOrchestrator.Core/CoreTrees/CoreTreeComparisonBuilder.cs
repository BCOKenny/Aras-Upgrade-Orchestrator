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
            throw new InvalidDataException("The declared Core Tree classification does not match the delivery request.");
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
