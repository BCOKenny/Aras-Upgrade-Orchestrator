using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public enum CoreTreeDeliveryStatus { Completed, Blocked }

public sealed record CoreTreeDeliveryRequest(
    string CaseRoot,
    string Actor,
    string ComparisonOutputRoot,
    string CompletionManifestPath,
    string DeliveryOutputRoot,
    ActionConfirmation? Confirmation = null,
    IReadOnlyDictionary<string, bool>? Prerequisites = null);

public sealed record CoreTreeDeliveryResult(
    Guid CaseId,
    Guid ComparisonAttemptId,
    Guid DeliveryId,
    CoreTreeDeliveryStatus Status,
    SafetyLevel SafetyLevel,
    string Message,
    string DeliveryManifestPath,
    string HistoryPath,
    int OutputFileCount);

public sealed record CoreTreeDeliveryFile(string RelativePath, string Sha256);

public sealed record CoreTreeDeliveryManifest(
    string State,
    string DeliveryKind,
    Guid CaseId,
    Guid ComparisonAttemptId,
    Guid DeliveryId,
    string ComparisonOutputRoot,
    string CompletionManifestPath,
    IReadOnlyDictionary<string, string> EvidenceChecksums,
    IReadOnlyList<CoreTreeDeliveryFile> OutputFiles,
    string DeliveredBy,
    DateTimeOffset DeliveredAt);

/// <summary>
/// Builds a new A/B/C delivery only from immutable comparison evidence that has
/// already received a formal comparison completion receipt.
/// </summary>
public sealed class CoreTreeDeliveryCommand
{
    public const string ActionId = "core-tree.build-delivery";
    public const string ActionVersion = "1";
    public const string TaskId = "core-tree.delivery";

    private readonly SafetyPolicy _safetyPolicy;
    private readonly Func<DateTimeOffset> _clock;

    public CoreTreeDeliveryCommand(SafetyPolicy safetyPolicy, Func<DateTimeOffset>? clock = null)
    {
        _safetyPolicy = safetyPolicy ?? throw new ArgumentNullException(nameof(safetyPolicy));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<CoreTreeDeliveryResult> ExecuteAsync(CoreTreeDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Actor)) throw new ArgumentException("Actor is required.", nameof(request));

        var store = new CaseStore(request.CaseRoot);
        var history = new AppendOnlyHistoryStore(store.ToolDataPath);
        CaseManifest caseManifest;
        try { caseManifest = await store.LoadAsync(cancellationToken); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        { return Blocked(Guid.Empty, Guid.Empty, exception.Message, request.DeliveryOutputRoot, history.Path); }

        Guid comparisonAttemptId = Guid.Empty;
        try
        {
            var comparisonRoot = Path.GetFullPath(request.ComparisonOutputRoot);
            var completionPath = Path.GetFullPath(request.CompletionManifestPath);
            var deliveryRoot = Path.GetFullPath(request.DeliveryOutputRoot);
            EnsureDescendant(comparisonRoot, Path.Combine(store.CaseRoot, "core-tree", "attempts"), false, "Comparison output");
            EnsureDescendant(completionPath, Path.Combine(store.CaseRoot, "core-tree", "completions"), false, "Completion manifest");
            EnsureDescendant(deliveryRoot, Path.Combine(store.CaseRoot, "core-tree", "deliveries"), true, "Delivery output");

            var incompletePath = RequireFile(comparisonRoot, "incomplete-manifest.json");
            var summaryPath = RequireFile(comparisonRoot, "processing-summary.json");
            var classificationPath = RequireFile(comparisonRoot, "classification-result.json");
            var reviewsPath = RequireFile(comparisonRoot, "manual-reviews.json");
            var registerPath = RequireFile(comparisonRoot, "manual-review-register.md");
            var completion = await ReadJsonAsync<CoreTreeComparisonCompletionManifest>(completionPath, cancellationToken);
            var incomplete = await ReadJsonAsync<IncompleteManifest>(incompletePath, cancellationToken);
            var summary = await ReadJsonAsync<ProcessingSummary>(summaryPath, cancellationToken);
            var snapshot = await ReadJsonAsync<CoreTreeComparisonSnapshot>(classificationPath, cancellationToken);
            var reviews = await ReadJsonAsync<CoreTreeManualReview[]>(reviewsPath, cancellationToken);
            comparisonAttemptId = incomplete.AttemptId;

            ValidateEvidence(caseManifest, completion, completionPath, comparisonRoot, incomplete, summary, snapshot, reviews, incompletePath, summaryPath, classificationPath, reviewsPath, registerPath);
            await ValidateHistoryAsync(history, caseManifest.CaseId, comparisonAttemptId, completionPath, cancellationToken);
            ValidateInputTreeDigests(snapshot, summary);

            var resolvedItems = ResolveManualReviewDecisions(await File.ReadAllLinesAsync(registerPath, cancellationToken), reviews);
            var items = snapshot.Classification.Items.Concat(resolvedItems).ToArray();
            ValidateItems(items);
            var evidence = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification-result.json"] = HashFile(classificationPath),
                ["completion-manifest.json"] = HashFile(completionPath),
                ["incomplete-manifest.json"] = HashFile(incompletePath),
                ["manual-review-register.md"] = HashFile(registerPath),
                ["manual-reviews.json"] = HashFile(reviewsPath),
                ["processing-summary.json"] = HashFile(summaryPath)
            };
            var prerequisites = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["case.loaded"] = true,
                ["comparison.completed"] = true,
                ["comparison.evidence.valid"] = true,
                ["inputs.immutable"] = true,
                ["manual.reviews.applied"] = true,
                ["delivery.output.new"] = true
            };
            if (request.Prerequisites is not null)
                foreach (var item in request.Prerequisites) prerequisites.TryAdd(item.Key, item.Value);
            var digest = HashText(string.Join("\n", evidence.Select(item => $"{item.Key}:{item.Value}")));
            var decision = _safetyPolicy.Evaluate(new ControlledAction(ActionId, ActionVersion, deliveryRoot, digest, true, prerequisites));
            if (!SafetyPolicy.IsConfirmationValid(decision, request.Confirmation))
                return await BlockedAsync(caseManifest.CaseId, comparisonAttemptId, decision.Level, decision.Reason, request.DeliveryOutputRoot, history, request.Actor, cancellationToken);

            var leases = new DirectoryLeaseManager(store.ToolDataPath);
            await using var lease = await leases.AcquireAsync([deliveryRoot, Path.Combine(store.ToolDataPath, "core-tree-delivery")], cancellationToken);
            await EnsureNotDeliveredAsync(history, caseManifest.CaseId, comparisonAttemptId, cancellationToken);
            if (Directory.Exists(deliveryRoot) || File.Exists(deliveryRoot)) throw new InvalidOperationException("Delivery output must be new.");

            Directory.CreateDirectory(deliveryRoot);
            try
            {
                var outputFiles = await CopyItemsAsync(items, summary, deliveryRoot, cancellationToken);
                var deliveryId = Guid.NewGuid();
                var manifestPath = Path.Combine(deliveryRoot, "delivery-manifest.json");
                var manifest = new CoreTreeDeliveryManifest("Completed", "CoreTreeABC", caseManifest.CaseId, comparisonAttemptId, deliveryId,
                    comparisonRoot, completionPath, evidence, outputFiles, request.Actor, _clock());
                await WriteJsonNewAsync(manifestPath, manifest, cancellationToken);
                await history.AppendAsync(caseManifest.CaseId, HistoryEventTypes.CoreTreeDeliveryCompleted, TaskId, request.Actor,
                    new DeliveryHistoryPayload(comparisonAttemptId, deliveryId, manifestPath, HashFile(manifestPath)), _clock(), cancellationToken: cancellationToken);
                return new(caseManifest.CaseId, comparisonAttemptId, deliveryId, CoreTreeDeliveryStatus.Completed, decision.Level,
                    "Core Tree A/B/C delivery completed in a new immutable directory.", manifestPath, history.Path, outputFiles.Count);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
            {
                var incompleteMarker = Path.Combine(deliveryRoot, "incomplete-manifest.json");
                if (!File.Exists(incompleteMarker))
                    await WriteJsonNewAsync(incompleteMarker, new { State = "Incomplete", ComparisonAttemptId = comparisonAttemptId, Error = exception.Message, InterruptedAt = _clock() }, CancellationToken.None);
                throw;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        { return await BlockedAsync(caseManifest.CaseId, comparisonAttemptId, SafetyLevel.Blocked, exception.Message, request.DeliveryOutputRoot, history, request.Actor, cancellationToken); }
    }

    private static void ValidateEvidence(CaseManifest caseManifest, CoreTreeComparisonCompletionManifest completion, string completionPath, string comparisonRoot,
        IncompleteManifest incomplete, ProcessingSummary summary, CoreTreeComparisonSnapshot snapshot, IReadOnlyList<CoreTreeManualReview> reviews,
        string incompletePath, string summaryPath, string classificationPath, string reviewsPath, string registerPath)
    {
        if (incomplete.AttemptId == Guid.Empty || !string.Equals(incomplete.Status, "Incomplete", StringComparison.Ordinal) || incomplete.ErrorCount != 0)
            throw new InvalidDataException("Delivery requires a zero-error Incomplete comparison attempt.");
        if (completion.State != "Completed" || completion.CompletionKind != "ComparisonReviewFinalization" || completion.CaseId != caseManifest.CaseId ||
            completion.ComparisonAttemptId != incomplete.AttemptId || !SamePath(completion.ComparisonOutputRoot, comparisonRoot) || !SamePath(completionPath, completionPath))
            throw new InvalidDataException("Completion receipt does not match this comparison attempt.");
        if (summary.AttemptId != incomplete.AttemptId || snapshot.AttemptId != incomplete.AttemptId || snapshot.Classification.AttemptId != incomplete.AttemptId ||
            snapshot.Classification.Errors.Count != 0 || snapshot.Classification.ManualReviews.Count != reviews.Count ||
            snapshot.Classification.Items.Count(item => item.Classification == CoreTreeClassification.A) != summary.Counts.A ||
            snapshot.Classification.Items.Count(item => item.Classification == CoreTreeClassification.B) != summary.Counts.B ||
            snapshot.Classification.Items.Count(item => item.Classification == CoreTreeClassification.C) != summary.Counts.C || summary.Counts.Errors != 0)
            throw new InvalidDataException("Classification snapshot does not match the formal comparison evidence.");
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["classification-result.json"] = HashFile(classificationPath), ["incomplete-manifest.json"] = HashFile(incompletePath),
            ["manual-review-register.md"] = HashFile(registerPath), ["manual-reviews.json"] = HashFile(reviewsPath), ["processing-summary.json"] = HashFile(summaryPath)
        };
        foreach (var item in expected)
            if (!completion.ArtifactChecksums.TryGetValue(item.Key, out var actual) || !string.Equals(actual, item.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Completion evidence checksum is invalid: {item.Key}.");
    }

    private static void ValidateInputTreeDigests(CoreTreeComparisonSnapshot snapshot, ProcessingSummary summary)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["customer"] = CoreTreeComparisonBuilder.ComputeTreeDigest(summary.Customer.RootPath),
            ["source-ootb"] = CoreTreeComparisonBuilder.ComputeTreeDigest(summary.SourceOotb.RootPath),
            ["target-ootb"] = CoreTreeComparisonBuilder.ComputeTreeDigest(summary.TargetOotb.RootPath)
        };
        if (snapshot.InputTreeDigests.Count != expected.Count || expected.Any(item => !snapshot.InputTreeDigests.TryGetValue(item.Key, out var digest) || !string.Equals(digest, item.Value, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Core Tree inputs changed after comparison; delivery is blocked.");
    }

    private static IReadOnlyList<CoreTreeClassifiedItem> ResolveManualReviewDecisions(string[] lines, IReadOnlyList<CoreTreeManualReview> reviews)
    {
        var rows = lines.Where(line => line.TrimStart().StartsWith('|')).Skip(2).Select(line => line.Trim().Trim('|').Split('|').Select(value => value.Trim()).ToArray()).ToArray();
        if (rows.Length != reviews.Count) throw new InvalidDataException("Manual review register row count does not match.");
        var results = new List<CoreTreeClassifiedItem>();
        for (var index = 0; index < reviews.Count; index++)
        {
            var row = rows[index]; var review = reviews[index]; var expectedId = $"MR-{index + 1:000}";
            if (row.Length is not (7 or 8) || row[0] != expectedId || row[1] != review.SourceRelativePath || row[2] != review.Code || !IsResolved(row))
                throw new InvalidDataException($"Manual review {expectedId} is not a valid resolved register row.");
            var decision = row[3];
            if (string.Equals(decision, "Exclude", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(decision, "A", StringComparison.OrdinalIgnoreCase)) { results.Add(new(CoreTreeClassification.A, review.SourceRelativePath, null)); continue; }
            if (string.Equals(decision, "B", StringComparison.OrdinalIgnoreCase)) { results.Add(new(CoreTreeClassification.B, review.SourceRelativePath, null)); continue; }
            if (decision.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
            {
                var target = decision[2..].Trim().Replace('\\', '/');
                if (!review.TargetCandidates.Contains(target, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Manual review {expectedId} selects a target that is not a formal candidate.");
                results.Add(new(CoreTreeClassification.C, review.SourceRelativePath, target));
                continue;
            }
            throw new InvalidDataException($"Manual review {expectedId} decision must be A, B, C:<targetRelativePath>, or Exclude.");
        }
        return results;
    }

    private static bool IsResolved(string[] row) => row.Length switch
    {
        7 => !string.IsNullOrWhiteSpace(row[3]) && !string.IsNullOrWhiteSpace(row[4]) && DateTimeOffset.TryParse(row[5], out _) && row[6].Equals("Resolved", StringComparison.OrdinalIgnoreCase),
        8 => !string.IsNullOrWhiteSpace(row[3]) && !string.IsNullOrWhiteSpace(row[4]) && (DateOnly.TryParse(row[5], out _) || DateTimeOffset.TryParse(row[5], out _)) && row[6].Equals("manual-reviews.json", StringComparison.Ordinal) && row[7].Equals("Resolved", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private static void ValidateItems(IReadOnlyList<CoreTreeClassifiedItem> items)
    {
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            ValidateRelative(item.SourceRelativePath);
            if (item.Classification == CoreTreeClassification.C) ValidateRelative(item.TargetRelativePath!);
            else if (item.TargetRelativePath is not null) throw new InvalidDataException("Only C items may specify a target path.");
            var relative = item.Classification == CoreTreeClassification.C ? item.TargetRelativePath! : item.SourceRelativePath;
            foreach (var root in item.Classification switch
            {
                CoreTreeClassification.A => new[] { "CustomerSource" },
                CoreTreeClassification.B => new[] { "CustomerSource", "OOTBSource" },
                CoreTreeClassification.C => new[] { "CustomerSource", "OOTBSource", "OOTBR38" },
                _ => throw new InvalidDataException("Unknown classification.")
            })
                if (!destinations.Add($"{item.Classification}/{root}/{relative}")) throw new InvalidDataException($"Duplicate delivery destination: {item.Classification}/{root}/{relative}.");
        }
    }

    private static async Task<IReadOnlyList<CoreTreeDeliveryFile>> CopyItemsAsync(IReadOnlyList<CoreTreeClassifiedItem> items, ProcessingSummary summary, string deliveryRoot, CancellationToken cancellationToken)
    {
        var files = new List<CoreTreeDeliveryFile>();
        foreach (var item in items)
        {
            var outputRelative = item.Classification == CoreTreeClassification.C ? item.TargetRelativePath! : item.SourceRelativePath;
            await CopyAsync(Path.Combine(summary.Customer.RootPath, "Innovator"), item.SourceRelativePath, Path.Combine(deliveryRoot, item.Classification.ToString(), "CustomerSource"), outputRelative, deliveryRoot, files, cancellationToken);
            if (item.Classification is CoreTreeClassification.B or CoreTreeClassification.C)
                await CopyAsync(Path.Combine(summary.SourceOotb.RootPath, "Innovator"), item.SourceRelativePath, Path.Combine(deliveryRoot, item.Classification.ToString(), "OOTBSource"), outputRelative, deliveryRoot, files, cancellationToken);
            if (item.Classification == CoreTreeClassification.C)
                await CopyAsync(Path.Combine(summary.TargetOotb.RootPath, "Innovator"), item.TargetRelativePath!, Path.Combine(deliveryRoot, "C", "OOTBR38"), item.TargetRelativePath!, deliveryRoot, files, cancellationToken);
        }
        return files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    private static async Task CopyAsync(string inputRoot, string inputRelative, string outputRoot, string outputRelative, string deliveryRoot, List<CoreTreeDeliveryFile> files, CancellationToken cancellationToken)
    {
        var source = CoreTreeLogicalPathResolver.ToFullPath(inputRoot, inputRelative);
        var destination = CoreTreeLogicalPathResolver.ToFullPath(outputRoot, outputRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous))
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
        files.Add(new(Path.GetRelativePath(deliveryRoot, destination).Replace('\\', '/'), HashFile(destination)));
    }

    private static async Task ValidateHistoryAsync(AppendOnlyHistoryStore history, Guid caseId, Guid attemptId, string completionPath, CancellationToken cancellationToken)
    {
        var completionFound = false;
        await foreach (var entry in history.ReadAllAsync(cancellationToken))
        {
            if (entry.CaseId != caseId || entry.EventType != HistoryEventTypes.CoreTreeComparisonCompleted) continue;
            var payload = entry.Payload.Deserialize<CoreTreeComparisonFinalizationCommand.CompletionHistoryPayload>(AppendOnlyHistoryStore.JsonOptions);
            completionFound |= payload?.ComparisonAttemptId == attemptId && SamePath(payload.CompletionManifestPath, completionPath);
        }
        if (!completionFound) throw new InvalidDataException("Append-only history does not contain this comparison completion receipt.");
    }

    private static async Task EnsureNotDeliveredAsync(AppendOnlyHistoryStore history, Guid caseId, Guid attemptId, CancellationToken cancellationToken)
    {
        await foreach (var entry in history.ReadAllAsync(cancellationToken))
        {
            if (entry.CaseId != caseId || entry.EventType != HistoryEventTypes.CoreTreeDeliveryCompleted) continue;
            var payload = entry.Payload.Deserialize<DeliveryHistoryPayload>(AppendOnlyHistoryStore.JsonOptions);
            if (payload?.ComparisonAttemptId == attemptId) throw new InvalidOperationException("This comparison attempt already has a delivery.");
        }
    }

    private static void EnsureDescendant(string candidate, string root, bool mustBeNew, string label)
    {
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"{label} must be under {normalizedRoot}.");
        if (mustBeNew && (Directory.Exists(normalizedCandidate) || File.Exists(normalizedCandidate))) throw new InvalidDataException($"{label} must be new.");
    }
    private static string RequireFile(string root, string name) { var path = Path.Combine(root, name); return File.Exists(path) ? path : throw new InvalidDataException($"Required artifact is missing: {name}."); }
    private static void ValidateRelative(string path)
    {
        var normalized = path?.Replace('\\', '/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(path) || normalized.Split('/').Any(part => part is "" or "." or "..") || (!normalized.StartsWith("Client/", StringComparison.OrdinalIgnoreCase) && !normalized.StartsWith("Server/", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Delivery path is not a safe Client or Server relative path.");
    }
    private static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken) => JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken), JsonOptions) ?? throw new InvalidDataException($"Invalid JSON: {path}.");
    private static async Task WriteJsonNewAsync(string path, object value, CancellationToken cancellationToken) { await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough); await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken); await stream.FlushAsync(cancellationToken); }
    private static bool SamePath(string left, string right) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private sealed record IncompleteManifest(Guid AttemptId, string Status, int ManualReviewCount, int ErrorCount);
    private sealed record ProcessingSummary(Guid AttemptId, CoreTreeInputEvidence Customer, CoreTreeInputEvidence SourceOotb, CoreTreeInputEvidence TargetOotb, CoreTreeComparisonCounts Counts);
    public sealed record DeliveryHistoryPayload(Guid ComparisonAttemptId, Guid DeliveryId, string DeliveryManifestPath, string DeliveryManifestChecksum);
    private static CoreTreeDeliveryResult Blocked(Guid caseId, Guid attemptId, string message, string outputRoot, string historyPath) => new(caseId, attemptId, Guid.Empty, CoreTreeDeliveryStatus.Blocked, SafetyLevel.Blocked, message, string.Empty, historyPath, 0);
    private async Task<CoreTreeDeliveryResult> BlockedAsync(Guid caseId, Guid attemptId, SafetyLevel level, string message, string outputRoot, AppendOnlyHistoryStore history, string actor, CancellationToken cancellationToken) { await history.AppendAsync(caseId, HistoryEventTypes.ActionBlocked, TaskId, actor, new { Reason = message, OutputRoot = outputRoot }, _clock(), cancellationToken: cancellationToken); return new(caseId, attemptId, Guid.Empty, CoreTreeDeliveryStatus.Blocked, level, message, string.Empty, history.Path, 0); }
}
