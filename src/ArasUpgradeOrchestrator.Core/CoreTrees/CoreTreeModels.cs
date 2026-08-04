using System.Security.Cryptography;
using System.Text;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public sealed record CoreTreeInputEvidence(
    string RootPath,
    string InnovatorVersion,
    string EvidenceReference);

public sealed class CoreTreeValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed record CoreTreeServerTextRuleSet(
    string Version,
    string Checksum,
    IReadOnlyList<string> RelativePaths)
{
    public static CoreTreeServerTextRuleSet Create(string version, IEnumerable<string> relativePaths)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("規則版本不可為空。", nameof(version));
        ArgumentNullException.ThrowIfNull(relativePaths);
        var paths = relativePaths.ToArray();
        if (paths.Any(path => !IsCanonicalServerRelativePath(path)))
            throw new ArgumentException("Server text rule paths must be canonical relative Server paths.", nameof(relativePaths));
        paths = CoreTreePathOrdering.ByPath(paths).ToArray();
        return new(version, CalculateChecksum(version, paths), paths);
    }

    internal static string CalculateChecksum(string version, IEnumerable<string> relativePaths)
    {
        var canonical = version.Trim() + "\n" + string.Join("\n", relativePaths
            .Select(path => path.Replace('\\', '/').TrimStart('/').ToUpperInvariant())
            .OrderBy(path => path, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    internal static bool IsCanonicalServerRelativePath(string? path)
    {
        if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.StartsWith('/') || path.StartsWith('\\')) return false;
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':') return false;
        if (path.Contains('\\')) return false;
        var segments = path.Split('/', StringSplitOptions.None);
        return segments.Length > 1 && string.Equals(segments[0], "Server", StringComparison.Ordinal) &&
            segments.All(segment => segment is not ("" or "." or ".."));
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

public sealed record CoreTreeComparisonError(string RelativePath, string Code, string Message);
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
