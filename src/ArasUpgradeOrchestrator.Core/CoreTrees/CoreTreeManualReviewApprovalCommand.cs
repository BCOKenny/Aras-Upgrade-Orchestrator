using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Safety;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public enum CoreTreeManualReviewApprovalStatus { Approved, Blocked }

public sealed record CoreTreeManualReviewApprovalRequest(
    string CaseRoot,
    string Actor,
    string ComparisonOutputRoot,
    string ReviewRegisterPath,
    string ApprovalOutputRoot,
    ActionConfirmation? Confirmation = null,
    IReadOnlyDictionary<string, bool>? Prerequisites = null);

public sealed record CoreTreeManualReviewApprovalResult(
    Guid CaseId,
    CoreTreeManualReviewApprovalStatus Status,
    SafetyLevel SafetyLevel,
    string Message,
    string ApprovalManifestPath,
    string HistoryPath,
    int ResolvedReviewCount);

public sealed record CoreTreeManualReviewApprovalManifest(
    string State,
    Guid CaseId,
    Guid ComparisonAttemptId,
    string ComparisonOutputRoot,
    string ManualReviewsPath,
    string ManualReviewsChecksum,
    string ReviewRegisterPath,
    string ReviewRegisterChecksum,
    string ApprovedBy,
    DateTimeOffset ApprovedAt,
    int ResolvedReviewCount);

public sealed class CoreTreeManualReviewApprovalCommand
{
    public const string ActionId = "core-tree.approve-manual-reviews";
    public const string ActionVersion = "1";
    public const string TaskId = "core-tree.manual-review-approval";
    private readonly SafetyPolicy _safetyPolicy;
    private readonly Func<DateTimeOffset> _clock;

    public CoreTreeManualReviewApprovalCommand(SafetyPolicy safetyPolicy, Func<DateTimeOffset>? clock = null)
    {
        _safetyPolicy = safetyPolicy ?? throw new ArgumentNullException(nameof(safetyPolicy));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<CoreTreeManualReviewApprovalResult> ExecuteAsync(CoreTreeManualReviewApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Actor)) throw new ArgumentException("Actor is required.", nameof(request));

        var store = new CaseStore(request.CaseRoot);
        var history = new AppendOnlyHistoryStore(store.ToolDataPath);
        CaseManifest manifest;
        try { manifest = await store.LoadAsync(cancellationToken); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        { return Blocked(Guid.Empty, exception.Message, request.ApprovalOutputRoot, history.Path); }

        try
        {
            var comparisonRoot = Path.GetFullPath(request.ComparisonOutputRoot);
            var approvalsRoot = Path.Combine(store.CaseRoot, "core-tree", "review-approvals");
            var approvalRoot = Path.GetFullPath(request.ApprovalOutputRoot);
            if (!IsDescendant(approvalRoot, approvalsRoot) || Directory.Exists(approvalRoot) || File.Exists(approvalRoot))
                throw new InvalidDataException("Approval output must be a new directory under core-tree/review-approvals.");

            var incompletePath = Path.Combine(comparisonRoot, "incomplete-manifest.json");
            var reviewsPath = Path.Combine(comparisonRoot, "manual-reviews.json");
            var generatedRegisterPath = Path.Combine(comparisonRoot, "manual-review-register.md");
            if (!File.Exists(incompletePath) || !File.Exists(reviewsPath))
                throw new InvalidDataException("The comparison output must contain incomplete-manifest.json and manual-reviews.json.");
            if (!string.Equals(Path.GetFullPath(request.ReviewRegisterPath), generatedRegisterPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(generatedRegisterPath))
                throw new InvalidDataException("Approval must use the register generated in the comparison output directory.");

            var incomplete = JsonSerializer.Deserialize<IncompleteManifest>(await File.ReadAllTextAsync(incompletePath, cancellationToken), JsonOptions)
                ?? throw new InvalidDataException("Incomplete manifest is invalid.");
            if (!string.Equals(incomplete.Status, "Incomplete", StringComparison.Ordinal) || incomplete.AttemptId == Guid.Empty)
                throw new InvalidDataException("Only an Incomplete comparison attempt can be approved.");
            var reviews = JsonSerializer.Deserialize<CoreTreeManualReview[]>(await File.ReadAllTextAsync(reviewsPath, cancellationToken), JsonOptions)
                ?? throw new InvalidDataException("Manual reviews are invalid.");
            if (reviews.Length == 0 || reviews.Length != incomplete.ManualReviewCount)
                throw new InvalidDataException("Manual review count does not match the incomplete manifest.");
            var resolved = ParseAndValidateRegister(await File.ReadAllLinesAsync(request.ReviewRegisterPath, cancellationToken), reviews);

            var prerequisites = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["case.loaded"] = true,
                ["comparison.incomplete"] = true,
                ["reviews.exactly.resolved"] = resolved.Count == reviews.Length
            };
            if (request.Prerequisites is not null)
                foreach (var item in request.Prerequisites) prerequisites[item.Key] = item.Value;
            var inputDigest = HashText(string.Join("\n", new[] { HashFile(reviewsPath), HashFile(request.ReviewRegisterPath), incomplete.AttemptId.ToString("D") }));
            var decision = _safetyPolicy.Evaluate(new ControlledAction(ActionId, ActionVersion, approvalRoot, inputDigest, true, prerequisites));
            if (!SafetyPolicy.IsConfirmationValid(decision, request.Confirmation))
                return await BlockedAsync(manifest.CaseId, decision.Level, decision.Reason, approvalRoot, history, request.Actor, cancellationToken);

            await using var lease = await new DirectoryLeaseManager(store.ToolDataPath).AcquireAsync([approvalRoot, Path.Combine(store.ToolDataPath, "core-tree-manual-review-approval")], cancellationToken);
            await foreach (var entry in history.ReadAllAsync(cancellationToken))
            {
                if (entry.CaseId != manifest.CaseId || entry.EventType != HistoryEventTypes.CoreTreeManualReviewsApproved) continue;
                var prior = entry.Payload.Deserialize<CoreTreeManualReviewApprovalHistoryPayload>(AppendOnlyHistoryStore.JsonOptions);
                if (prior?.ComparisonAttemptId == incomplete.AttemptId)
                    throw new InvalidOperationException("This comparison attempt already has an approval record.");
            }

            Directory.CreateDirectory(approvalRoot);
            var approvalPath = Path.Combine(approvalRoot, "manual-review-approval.json");
            var approval = new CoreTreeManualReviewApprovalManifest("Approved", manifest.CaseId, incomplete.AttemptId, comparisonRoot, reviewsPath, HashFile(reviewsPath), request.ReviewRegisterPath, HashFile(request.ReviewRegisterPath), request.Actor, _clock(), resolved.Count);
            await File.WriteAllTextAsync(approvalPath, JsonSerializer.Serialize(approval, JsonOptions), cancellationToken);
            await history.AppendAsync(manifest.CaseId, HistoryEventTypes.CoreTreeManualReviewsApproved, TaskId, request.Actor,
                new CoreTreeManualReviewApprovalHistoryPayload(incomplete.AttemptId, approvalPath, approval.ManualReviewsChecksum, approval.ReviewRegisterChecksum, resolved.Count), _clock(), cancellationToken: cancellationToken);
            return new(manifest.CaseId, CoreTreeManualReviewApprovalStatus.Approved, decision.Level, "Manual reviews were approved; this is not a Completed delivery.", approvalPath, history.Path, resolved.Count);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        { return await BlockedAsync(manifest.CaseId, SafetyLevel.Blocked, exception.Message, request.ApprovalOutputRoot, history, request.Actor, cancellationToken); }
    }

    private static IReadOnlyList<string> ParseAndValidateRegister(string[] lines, IReadOnlyList<CoreTreeManualReview> reviews)
    {
        var rows = lines.Where(line => line.TrimStart().StartsWith('|')).Skip(2).Select(line => line.Trim().Trim('|').Split('|').Select(value => value.Trim()).ToArray()).ToArray();
        if (rows.Length != reviews.Count) throw new InvalidDataException("Review register must contain exactly one row for every formal manual review.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < reviews.Count; index++)
        {
            var row = rows[index]; var review = reviews[index]; var expectedId = $"MR-{index + 1:000}";
            if (row.Length is not (7 or 8) || row[0] != expectedId || !seen.Add(row[0]) ||
                !string.Equals(row[1], review.SourceRelativePath, StringComparison.Ordinal) ||
                !string.Equals(row[2], review.Code, StringComparison.Ordinal) ||
                !IsResolvedRegisterRow(row))
                throw new InvalidDataException($"Review register row {expectedId} is incomplete or does not match the formal review.");
        }
        return rows.Select(row => row[0]).ToArray();
    }

    private static bool IsResolvedRegisterRow(string[] row) => row.Length switch
    {
        7 => !string.IsNullOrWhiteSpace(row[3]) && !string.IsNullOrWhiteSpace(row[4]) &&
             DateTimeOffset.TryParse(row[5], out _) && string.Equals(row[6], "Resolved", StringComparison.OrdinalIgnoreCase),
        // Legacy attempts created before automatic register generation retained a fixed evidence-reference column and date-only approval date.
        8 => !string.IsNullOrWhiteSpace(row[3]) && !string.IsNullOrWhiteSpace(row[4]) &&
             (DateOnly.TryParse(row[5], out _) || DateTimeOffset.TryParse(row[5], out _)) &&
             string.Equals(row[6], "manual-reviews.json", StringComparison.Ordinal) && string.Equals(row[7], "Resolved", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private static bool IsDescendant(string child, string parent)
    {
        var normalizedChild = Path.TrimEndingDirectorySeparator(Path.GetFullPath(child));
        var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        return normalizedChild.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private sealed record IncompleteManifest(Guid AttemptId, string Status, int ManualReviewCount);
    public sealed record CoreTreeManualReviewApprovalHistoryPayload(Guid ComparisonAttemptId, string ApprovalManifestPath, string ManualReviewsChecksum, string ReviewRegisterChecksum, int ResolvedReviewCount);
    private static CoreTreeManualReviewApprovalResult Blocked(Guid caseId, string message, string outputRoot, string historyPath) => new(caseId, CoreTreeManualReviewApprovalStatus.Blocked, SafetyLevel.Blocked, message, string.Empty, historyPath, 0);
    private async Task<CoreTreeManualReviewApprovalResult> BlockedAsync(Guid caseId, SafetyLevel level, string message, string outputRoot, AppendOnlyHistoryStore history, string actor, CancellationToken cancellationToken)
    {
        await history.AppendAsync(caseId, HistoryEventTypes.ActionBlocked, TaskId, actor, new { Reason = message, OutputRoot = outputRoot }, _clock(), cancellationToken: cancellationToken);
        return new(caseId, CoreTreeManualReviewApprovalStatus.Blocked, level, message, string.Empty, history.Path, 0);
    }
}
