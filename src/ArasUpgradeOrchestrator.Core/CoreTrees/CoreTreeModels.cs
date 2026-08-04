using System.Security.Cryptography;
using System.Text;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public sealed record CoreTreeInputEvidence(
    string RootPath,
    string InnovatorVersion,
    string EvidenceReference);

public sealed record CoreTreeServerTextRuleSet(
    string Version,
    string Checksum,
    IReadOnlyList<string> RelativePaths)
{
    public static CoreTreeServerTextRuleSet Create(string version, IEnumerable<string> relativePaths)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("規則版本不可為空。", nameof(version));
        var paths = relativePaths.Select(path => path.Replace('\\', '/').TrimStart('/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(version, CalculateChecksum(version, paths), paths);
    }

    internal static string CalculateChecksum(string version, IEnumerable<string> relativePaths)
    {
        var canonical = version.Trim() + "\n" + string.Join("\n", relativePaths
            .Select(path => path.Replace('\\', '/').TrimStart('/').ToUpperInvariant())
            .OrderBy(path => path, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public enum CoreTreeContentComparisonMode { Text, Binary, BinaryFallback }

public sealed record CoreTreeContentComparison(
    bool AreEqual,
    CoreTreeContentComparisonMode Mode,
    string? FallbackReason);

public sealed record CoreTreeComparisonRequest(
    Guid AttemptId,
    string SourceVersion,
    string TargetVersion,
    CoreTreeInputEvidence Customer,
    CoreTreeInputEvidence SourceOotb,
    CoreTreeInputEvidence TargetOotb,
    string OutputRoot,
    CoreTreeServerTextRuleSet ServerTextRules,
    DateTimeOffset StartedAt);

public enum CoreTreeLogicalMatchStatus
{
    None,
    Unique,
    Ambiguous
}

public sealed record CoreTreeLogicalMatch(
    CoreTreeLogicalMatchStatus Status,
    IReadOnlyList<string> Candidates,
    string? AppliedEvolution);

public enum CoreTreeClassification { A, B, C }

public enum CoreTreeComparisonStatus { ReadyToComplete, Blocked, Incomplete, Completed }

public sealed record CoreTreeClassifiedItem(
    CoreTreeClassification Classification,
    string SourceRelativePath,
    string? TargetRelativePath);

public sealed record CoreTreeManualReview(
    string SourceRelativePath,
    string Code,
    string? AppliedEvolution,
    IReadOnlyList<string> TargetCandidates,
    string Message);

public sealed record CoreTreeComparisonError(string RelativePath, string Message);
public sealed record CoreTreeComparisonNotice(string RelativePath, string Code, string Message);

public sealed record CoreTreeComparisonResult(
    Guid AttemptId,
    CoreTreeComparisonStatus Status,
    IReadOnlyList<CoreTreeClassifiedItem> Items,
    IReadOnlyList<CoreTreeManualReview> ManualReviews,
    IReadOnlyList<CoreTreeComparisonError> Errors,
    IReadOnlyList<CoreTreeComparisonNotice> Notices,
    string OutputRoot,
    string ServerRuleVersion,
    string ServerRuleChecksum,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);
