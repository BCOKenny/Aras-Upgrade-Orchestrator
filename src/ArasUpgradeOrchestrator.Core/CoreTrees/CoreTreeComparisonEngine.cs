namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public static class CoreTreeComparisonEngine
{
    public static async Task<CoreTreeComparisonResult> CompareAsync(
        CoreTreeComparisonRequest request,
        Func<DateTimeOffset>? clock = null,
        CancellationToken cancellationToken = default)
    {
        CoreTreeInputValidator.ValidateInputs(request);
        var customerRoot = Path.Combine(Path.GetFullPath(request.Customer.RootPath), "Innovator");
        var sourceRoot = Path.Combine(Path.GetFullPath(request.SourceOotb.RootPath), "Innovator");
        var targetRoot = Path.Combine(Path.GetFullPath(request.TargetOotb.RootPath), "Innovator");
        var items = new List<CoreTreeClassifiedItem>();
        var reviews = new List<CoreTreeManualReview>();
        var errors = new List<CoreTreeComparisonError>();
        var notices = new List<CoreTreeComparisonNotice>();

        foreach (var customerPath in EnumerateCustomerFiles(customerRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = CoreTreeContentComparer.NormalizeRelativePath(Path.GetRelativePath(customerRoot, customerPath));
            try
            {
                var sourcePath = CoreTreeLogicalPathResolver.ToFullPath(sourceRoot, relative);
                if (!File.Exists(sourcePath))
                {
                    var targetMatch = CoreTreeLogicalPathResolver.Resolve(relative, targetRoot);
                    if (targetMatch.Status != CoreTreeLogicalMatchStatus.None)
                    {
                        reviews.Add(new(relative, "CustomerAdditionCollidesWithTarget", targetMatch.AppliedEvolution,
                            targetMatch.Candidates, "A 類客戶新增檔案與 R38 邏輯檔案碰撞，必須人工確認。 "));
                    }
                    else
                    {
                        items.Add(new(CoreTreeClassification.A, relative, null));
                    }
                    continue;
                }

                var comparison = await CoreTreeContentComparer.CompareAsync(customerPath, sourcePath, relative,
                    request.ServerTextRules, cancellationToken);
                if (comparison.Mode == CoreTreeContentComparisonMode.BinaryFallback)
                    notices.Add(new(relative, "TextDecodeFallback", comparison.FallbackReason!));
                if (comparison.AreEqual)
                    continue;

                var match = CoreTreeLogicalPathResolver.Resolve(relative, targetRoot);
                switch (match.Status)
                {
                    case CoreTreeLogicalMatchStatus.None:
                        items.Add(new(CoreTreeClassification.B, relative, null));
                        break;
                    case CoreTreeLogicalMatchStatus.Unique:
                        items.Add(new(CoreTreeClassification.C, relative, match.Candidates.Single()));
                        break;
                    case CoreTreeLogicalMatchStatus.Ambiguous:
                        reviews.Add(new(relative, "MultipleTargetMappings", match.AppliedEvolution, match.Candidates,
                            "副檔名演進反查出多個 R38 候選，不能自動配對。 "));
                        break;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add(new(relative, "FileReadError", exception.Message));
            }
        }

        return new CoreTreeComparisonResult(
            request.AttemptId,
            reviews.Count == 0 && errors.Count == 0 ? CoreTreeComparisonStatus.ReadyToComplete : CoreTreeComparisonStatus.Blocked,
            CoreTreePathOrdering.ByPath(items, item => item.SourceRelativePath).ToArray(),
            CoreTreePathOrdering.ByPath(reviews, review => review.SourceRelativePath).ToArray(),
            CoreTreePathOrdering.ByPath(errors, error => error.RelativePath).ToArray(),
            CoreTreePathOrdering.ByPath(notices, notice => notice.RelativePath).ToArray(),
            Path.GetFullPath(request.OutputRoot),
            request.ServerTextRules.Version,
            request.ServerTextRules.Checksum,
            request.StartedAt,
            (clock ?? (() => DateTimeOffset.UtcNow))());
    }

    private static IEnumerable<string> EnumerateCustomerFiles(string innovatorRoot) =>
        new[] { "Client", "Server" }
            .SelectMany(side => Directory.EnumerateFiles(Path.Combine(innovatorRoot, side), "*", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal);
}
