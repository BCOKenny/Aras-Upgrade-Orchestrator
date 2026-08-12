using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public enum CoreTreeComparisonFinalizationStatus { Completed, Blocked }

public sealed record CoreTreeComparisonFinalizationRequest(
    string CaseRoot,
    string Actor,
    string ComparisonOutputRoot,
    string ApprovalManifestPath,
    string CompletionOutputRoot,
    ActionConfirmation? Confirmation = null,
    IReadOnlyDictionary<string, bool>? Prerequisites = null);

public sealed record CoreTreeComparisonFinalizationResult(
    Guid CaseId,
    Guid ComparisonAttemptId,
    CoreTreeComparisonFinalizationStatus Status,
    SafetyLevel SafetyLevel,
    string Message,
    string CompletionManifestPath,
    string HistoryPath);

public sealed record CoreTreeComparisonCompletionManifest(
    string State,
    string CompletionKind,
    Guid CaseId,
    Guid ComparisonAttemptId,
    string ComparisonOutputRoot,
    string ApprovalManifestPath,
    IReadOnlyDictionary<string, string> ArtifactChecksums,
    CoreTreeComparisonCounts Counts,
    string CompletedBy,
    DateTimeOffset CompletedAt);

public sealed record CoreTreeComparisonCounts(int A, int B, int C, int ManualReview, int Errors, int Notices);

public sealed class CoreTreeComparisonFinalizationCommand
{
    public const string ActionId = "core-tree.finalize-comparison";
    public const string ActionVersion = "1";
    public const string TaskId = "core-tree.comparison-finalization";

    private readonly SafetyPolicy _safetyPolicy;
    private readonly Func<DateTimeOffset> _clock;

    public CoreTreeComparisonFinalizationCommand(SafetyPolicy safetyPolicy, Func<DateTimeOffset>? clock = null)
    {
        _safetyPolicy = safetyPolicy ?? throw new ArgumentNullException(nameof(safetyPolicy));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<CoreTreeComparisonFinalizationResult> ExecuteAsync(
        CoreTreeComparisonFinalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Actor)) throw new ArgumentException("Actor is required.", nameof(request));

        var store = new CaseStore(request.CaseRoot);
        var history = new AppendOnlyHistoryStore(store.ToolDataPath);
        CaseManifest caseManifest;
        try
        {
            caseManifest = await store.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Blocked(Guid.Empty, Guid.Empty, exception.Message, request.CompletionOutputRoot, history.Path);
        }

        try
        {
            var comparisonRoot = Path.GetFullPath(request.ComparisonOutputRoot);
            var approvalPath = Path.GetFullPath(request.ApprovalManifestPath);
            var completionRoot = Path.GetFullPath(request.CompletionOutputRoot);
            EnsureNewDescendant(comparisonRoot, Path.Combine(store.CaseRoot, "core-tree", "attempts"), mustBeNew: false, "Comparison output");
            EnsureNewDescendant(approvalPath, Path.Combine(store.CaseRoot, "core-tree", "review-approvals"), mustBeNew: false, "Approval manifest");
            EnsureNewDescendant(completionRoot, Path.Combine(store.CaseRoot, "core-tree", "completions"), mustBeNew: true, "Completion output");

            var incompletePath = RequireFile(comparisonRoot, "incomplete-manifest.json");
            var summaryPath = RequireFile(comparisonRoot, "processing-summary.json");
            var reviewsPath = RequireFile(comparisonRoot, "manual-reviews.json");
            var registerPath = RequireFile(comparisonRoot, "manual-review-register.md");

            var incomplete = await ReadJsonAsync<IncompleteManifest>(incompletePath, cancellationToken);
            var summary = await ReadJsonAsync<ProcessingSummary>(summaryPath, cancellationToken);
            var reviews = await ReadJsonAsync<CoreTreeManualReview[]>(reviewsPath, cancellationToken);
            var approval = await ReadJsonAsync<CoreTreeManualReviewApprovalManifest>(approvalPath, cancellationToken);

            if (!string.Equals(incomplete.Status, "Incomplete", StringComparison.Ordinal) || incomplete.AttemptId == Guid.Empty)
                throw new InvalidDataException("Finalization requires a formal Incomplete comparison manifest.");
            if (incomplete.ErrorCount != 0 || summary.Counts.Errors != 0)
                throw new InvalidDataException("A comparison with errors cannot be finalized.");
            if (summary.AttemptId != incomplete.AttemptId || summary.Counts.ManualReview != incomplete.ManualReviewCount || reviews.Length != incomplete.ManualReviewCount)
                throw new InvalidDataException("Comparison summary, manifest, and manual review counts do not match.");
            if (!string.Equals(approval.State, "Approved", StringComparison.Ordinal) || approval.CaseId != caseManifest.CaseId ||
                approval.ComparisonAttemptId != incomplete.AttemptId || approval.ResolvedReviewCount != reviews.Length ||
                !SamePath(approval.ComparisonOutputRoot, comparisonRoot) || !SamePath(approval.ManualReviewsPath, reviewsPath) ||
                !SamePath(approval.ReviewRegisterPath, registerPath) || !SamePath(approvalPath, request.ApprovalManifestPath))
                throw new InvalidDataException("Approval receipt does not match this case and comparison attempt.");
            if (!string.Equals(approval.ManualReviewsChecksum, HashFile(reviewsPath), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(approval.ReviewRegisterChecksum, HashFile(registerPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Manual review evidence changed after approval.");

            var historyState = await ValidateHistoryAsync(history, caseManifest.CaseId, incomplete.AttemptId, approvalPath, cancellationToken);
            var checksums = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["incomplete-manifest.json"] = HashFile(incompletePath),
                ["manual-review-approval.json"] = HashFile(approvalPath),
                ["manual-review-register.md"] = HashFile(registerPath),
                ["manual-reviews.json"] = HashFile(reviewsPath),
                ["processing-summary.json"] = HashFile(summaryPath)
            };
            var prerequisites = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["case.loaded"] = true,
                ["comparison.incomplete"] = true,
                ["comparison.errors.zero"] = true,
                ["reviews.approved"] = true,
                ["reviews.checksums.valid"] = true,
                ["history.consistent"] = historyState,
                ["completion.output.new"] = true
            };
            if (request.Prerequisites is not null)
                foreach (var prerequisite in request.Prerequisites)
                    prerequisites.TryAdd(prerequisite.Key, prerequisite.Value);

            var inputDigest = HashText(string.Join("\n", checksums.Select(item => $"{item.Key}:{item.Value}")));
            var decision = _safetyPolicy.Evaluate(new ControlledAction(ActionId, ActionVersion, completionRoot, inputDigest, true, prerequisites));
            if (!SafetyPolicy.IsConfirmationValid(decision, request.Confirmation))
                return await BlockedAsync(caseManifest.CaseId, incomplete.AttemptId, decision.Level, decision.Reason, completionRoot, history, request.Actor, cancellationToken);

            var leaseManager = new DirectoryLeaseManager(store.ToolDataPath);
            await using var lease = await leaseManager.AcquireAsync([completionRoot, Path.Combine(store.ToolDataPath, "core-tree-comparison-finalization")], cancellationToken);
            await EnsureNotCompletedAsync(history, caseManifest.CaseId, incomplete.AttemptId, cancellationToken);
            if (Directory.Exists(completionRoot) || File.Exists(completionRoot))
                throw new InvalidOperationException("Completion output must remain new until the formal command writes it.");

            Directory.CreateDirectory(completionRoot);
            var completionPath = Path.Combine(completionRoot, "completion-manifest.json");
            var completion = new CoreTreeComparisonCompletionManifest(
                "Completed",
                "ComparisonReviewFinalization",
                caseManifest.CaseId,
                incomplete.AttemptId,
                comparisonRoot,
                approvalPath,
                checksums,
                summary.Counts,
                request.Actor,
                _clock());
            await WriteJsonNewAsync(completionPath, completion, cancellationToken);
            await history.AppendAsync(caseManifest.CaseId, HistoryEventTypes.CoreTreeComparisonCompleted, TaskId, request.Actor,
                new CompletionHistoryPayload(incomplete.AttemptId, completionPath, HashFile(completionPath)), _clock(), cancellationToken: cancellationToken);
            return new(caseManifest.CaseId, incomplete.AttemptId, CoreTreeComparisonFinalizationStatus.Completed, decision.Level,
                "Comparison review finalization completed; the original attempt remains Incomplete and is not overwritten.", completionPath, history.Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        {
            return await BlockedAsync(caseManifest.CaseId, Guid.Empty, SafetyLevel.Blocked, exception.Message, request.CompletionOutputRoot, history, request.Actor, cancellationToken);
        }
    }

    private static async Task<bool> ValidateHistoryAsync(AppendOnlyHistoryStore history, Guid caseId, Guid attemptId, string approvalPath, CancellationToken cancellationToken)
    {
        var incompleteFound = false;
        var approvalFound = false;
        await foreach (var entry in history.ReadAllAsync(cancellationToken))
        {
            if (entry.CaseId != caseId) continue;
            if (entry.EventType == HistoryEventTypes.AttemptIncomplete)
            {
                var payload = entry.Payload.Deserialize<AttemptResultPayload>(AppendOnlyHistoryStore.JsonOptions);
                incompleteFound |= payload?.AttemptId == attemptId;
            }
            else if (entry.EventType == HistoryEventTypes.CoreTreeManualReviewsApproved)
            {
                var payload = entry.Payload.Deserialize<CoreTreeManualReviewApprovalCommand.CoreTreeManualReviewApprovalHistoryPayload>(AppendOnlyHistoryStore.JsonOptions);
                approvalFound |= payload?.ComparisonAttemptId == attemptId && SamePath(payload.ApprovalManifestPath, approvalPath);
            }
        }
        if (!incompleteFound || !approvalFound)
            throw new InvalidDataException("Append-only history does not contain matching Incomplete and approval events.");
        return true;
    }

    private static async Task EnsureNotCompletedAsync(AppendOnlyHistoryStore history, Guid caseId, Guid attemptId, CancellationToken cancellationToken)
    {
        await foreach (var entry in history.ReadAllAsync(cancellationToken))
        {
            if (entry.CaseId != caseId || entry.EventType != HistoryEventTypes.CoreTreeComparisonCompleted) continue;
            var payload = entry.Payload.Deserialize<CompletionHistoryPayload>(AppendOnlyHistoryStore.JsonOptions);
            if (payload?.ComparisonAttemptId == attemptId)
                throw new InvalidOperationException("This comparison attempt already has a completion receipt.");
        }
    }

    private static void EnsureNewDescendant(string candidate, string root, bool mustBeNew, string label)
    {
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} must be under {normalizedRoot}.");
        if (mustBeNew && (Directory.Exists(normalizedCandidate) || File.Exists(normalizedCandidate)))
            throw new InvalidDataException($"{label} must be a new path.");
    }

    private static string RequireFile(string root, string name)
    {
        var path = Path.Combine(root, name);
        return File.Exists(path) ? path : throw new InvalidDataException($"Required comparison artifact is missing: {name}.");
    }

    private static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken) =>
        JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken), JsonOptions)
        ?? throw new InvalidDataException($"Invalid JSON artifact: {path}.");

    private static async Task WriteJsonNewAsync(string path, object value, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private sealed record IncompleteManifest(Guid AttemptId, string Status, int ManualReviewCount, int ErrorCount);
    private sealed record ProcessingSummary(Guid AttemptId, CoreTreeComparisonCounts Counts);
    public sealed record CompletionHistoryPayload(Guid ComparisonAttemptId, string CompletionManifestPath, string CompletionManifestChecksum);

    private static CoreTreeComparisonFinalizationResult Blocked(Guid caseId, Guid attemptId, string message, string outputRoot, string historyPath) =>
        new(caseId, attemptId, CoreTreeComparisonFinalizationStatus.Blocked, SafetyLevel.Blocked, message, string.Empty, historyPath);

    private async Task<CoreTreeComparisonFinalizationResult> BlockedAsync(Guid caseId, Guid attemptId, SafetyLevel level, string message, string outputRoot, AppendOnlyHistoryStore history, string actor, CancellationToken cancellationToken)
    {
        await history.AppendAsync(caseId, HistoryEventTypes.ActionBlocked, TaskId, actor, new { Reason = message, OutputRoot = outputRoot }, _clock(), cancellationToken: cancellationToken);
        return new(caseId, attemptId, CoreTreeComparisonFinalizationStatus.Blocked, level, message, string.Empty, history.Path);
    }
}
