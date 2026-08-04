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
        await using var lease = await leaseManager.AcquireAsync([request.OutputRoot], cancellationToken);
        Directory.CreateDirectory(request.OutputRoot);
        try
        {
            var comparison = await CoreTreeComparisonEngine.CompareAsync(request, clock, cancellationToken);

            if (comparison.Status == CoreTreeComparisonStatus.ReadyToComplete)
                await CopyClassifiedItemsAsync(request, comparison.Items, cancellationToken);

            await WriteJsonAsync(Path.Combine(request.OutputRoot, "processing-summary.json"), new
            {
                comparison.AttemptId,
                SourceVersion = request.SourceVersion,
                TargetVersion = request.TargetVersion,
                Customer = request.Customer,
                SourceOotb = request.SourceOotb,
                TargetOotb = request.TargetOotb,
                ServerTextRules = new { request.ServerTextRules.Version, request.ServerTextRules.Checksum },
                comparison.StartedAt,
                comparison.FinishedAt,
                Counts = new
                {
                    A = comparison.Items.Count(item => item.Classification == CoreTreeClassification.A),
                    B = comparison.Items.Count(item => item.Classification == CoreTreeClassification.B),
                    C = comparison.Items.Count(item => item.Classification == CoreTreeClassification.C),
                    ManualReview = comparison.ManualReviews.Count,
                    Errors = comparison.Errors.Count,
                    Notices = comparison.Notices.Count
                }
            }, cancellationToken);

            if (comparison.ManualReviews.Count > 0)
                await WriteJsonAsync(Path.Combine(request.OutputRoot, "manual-reviews.json"), comparison.ManualReviews, cancellationToken);
            if (comparison.Errors.Count > 0)
                await WriteJsonAsync(Path.Combine(request.OutputRoot, "errors.json"), comparison.Errors, cancellationToken);
            if (comparison.Notices.Count > 0)
                await WriteJsonAsync(Path.Combine(request.OutputRoot, "notices.json"), comparison.Notices, cancellationToken);

            var completed = comparison.Status == CoreTreeComparisonStatus.ReadyToComplete;
            var final = comparison with { Status = completed ? CoreTreeComparisonStatus.Completed : CoreTreeComparisonStatus.Incomplete };
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

    private static async Task CopyAsync(
        string inputRoot,
        string inputRelative,
        string outputRoot,
        string outputRelative,
        CancellationToken cancellationToken)
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
