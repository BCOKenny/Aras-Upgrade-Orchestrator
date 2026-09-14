using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Rules;

namespace ArasUpgradeOrchestrator.Core.Packages;

public enum CustomerPatchComparisonPreflightStatus { Ready, Blocked }
public sealed record CustomerPatchComparisonPreflightIssue(string Code, string Message);
public sealed record CustomerPatchComparisonPreflightRequest(
    string CaseRoot, string PreparationId, string ComparisonId, CustomerPatchComparisonSource Source, CustomerPatchComparisonTarget Target, string EvidenceRelativePath,
    string ComparisonAttemptsRelativePath, string AttemptId, RuleSetResolutionResult Policy);
public sealed record CustomerPatchComparisonPreflightResult(
    Guid CaseId, CustomerPatchComparisonPreflightStatus Status, IReadOnlyList<CustomerPatchComparisonPreflightIssue> Issues,
    string ExpectedAttemptPath, string? CustomerInputChecksum, string? PatchInputChecksum, string? PolicyChecksum);

public sealed class CustomerPatchComparisonPreflightCommand
{
    public async Task<CustomerPatchComparisonPreflightResult> ExecuteAsync(CustomerPatchComparisonPreflightRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Target);
        var issues = new List<CustomerPatchComparisonPreflightIssue>();
        CaseStore store;
        CaseManifest manifest;
        try { store = new CaseStore(request.CaseRoot); manifest = await store.LoadAsync(cancellationToken); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException)
        { return new(Guid.Empty, CustomerPatchComparisonPreflightStatus.Blocked, [new("case.unreadable", ex.Message)], string.Empty, null, null, null); }

        var customer = Resolve(store.CaseRoot, request.Source.InputRelativePath, issues, "customer.path.invalid");
        var patch = Resolve(store.CaseRoot, request.Target.InputRelativePath, issues, "patch.path.invalid");
        var evidenceDirectory = Resolve(store.CaseRoot, request.EvidenceRelativePath, issues, "evidence.path.invalid");
        var evidencePath = string.IsNullOrEmpty(evidenceDirectory) ? string.Empty : Path.Combine(evidenceDirectory, "patch-support-evidence.json");
        var attempts = Resolve(store.CaseRoot, request.ComparisonAttemptsRelativePath, issues, "attempts.path.invalid");
        var attempt = string.IsNullOrEmpty(attempts) || string.IsNullOrWhiteSpace(request.AttemptId) ? string.Empty : Path.GetFullPath(Path.Combine(attempts, request.AttemptId));
        if (string.IsNullOrWhiteSpace(request.PreparationId) || string.IsNullOrWhiteSpace(request.ComparisonId) || string.IsNullOrWhiteSpace(request.Source.BaselineId) || string.IsNullOrWhiteSpace(request.Target.TargetRelease)) issues.Add(new("identity.invalid", "比較 source 或 target 無效。"));
        if (request.Policy.Status != RuleResolutionStatus.Resolved || request.Policy.Issues.Count != 0 || request.Policy.PinnedVersions.Count == 0 || string.IsNullOrWhiteSpace(request.Policy.EffectiveChecksum)) issues.Add(new("policy.invalid", "Customer-Patch Policy 未解析或未固定 Checksum。"));
        if (!Directory.Exists(customer) || !Directory.Exists(patch)) issues.Add(new("input.missing", "客戶基準或 Patch／Support 輸入不存在。"));
        if (string.IsNullOrEmpty(attempt) || Directory.Exists(attempt) || File.Exists(attempt)) issues.Add(new("attempt.invalid", "比較 attempt 必須是不存在的新目錄。"));
        if (!string.IsNullOrEmpty(attempt) && (Overlaps(attempt, customer) || Overlaps(attempt, patch))) issues.Add(new("attempt.overlap", "比較 attempt 不得與輸入重疊。"));
        if (!File.Exists(evidencePath)) issues.Add(new("evidence.missing", "找不到 Patch／Support 證據。"));

        PackageInputTreeDigestResult? customerDigest = null, patchDigest = null;
        if (Directory.Exists(customer) && Directory.Exists(patch))
        {
            customerDigest = await PackageInputTreeDigest.ComputeAsync(customer, cancellationToken);
            patchDigest = await PackageInputTreeDigest.ComputeAsync(patch, cancellationToken);
            if (!Directory.EnumerateFiles(customer, "*.xml", SearchOption.AllDirectories).Any() || !Directory.EnumerateFiles(patch, "*.xml", SearchOption.AllDirectories).Any()) issues.Add(new("input.xml.missing", "客戶基準與 Patch／Support 均必須包含 XML。"));
        }
        if (File.Exists(evidencePath) && patchDigest is not null)
        {
            await using var stream = File.OpenRead(evidencePath);
            var evidence = await JsonSerializer.DeserializeAsync<CustomerPatchEvidence>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);
            if (evidence is null || evidence.PreparationId != request.PreparationId || evidence.ComparisonId != request.ComparisonId || evidence.Source != request.Source || evidence.Target != request.Target || evidence.PatchSupportInputChecksum != patchDigest.TreeChecksum)
                issues.Add(new("evidence.mismatch", "Patch／Support 證據與目前跳點或輸入摘要不相符。"));
        }
        return new(manifest.CaseId, issues.Count == 0 ? CustomerPatchComparisonPreflightStatus.Ready : CustomerPatchComparisonPreflightStatus.Blocked, issues, attempt, customerDigest?.TreeChecksum, patchDigest?.TreeChecksum, request.Policy.EffectiveChecksum);
    }

    private static string Resolve(string root, string relative, List<CustomerPatchComparisonPreflightIssue> issues, string code)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == "..")) throw new InvalidDataException("案件內相對路徑無效。");
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("案件內相對路徑逸出根目錄。");
            return path;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { issues.Add(new(code, ex.Message)); return string.Empty; }
    }
    private static bool Overlaps(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase) || left.StartsWith(right.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || right.StartsWith(left.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
