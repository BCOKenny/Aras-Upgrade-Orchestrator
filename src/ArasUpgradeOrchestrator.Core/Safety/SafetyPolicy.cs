using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Safety;

public enum SafetyLevel
{
    Automatic,
    SingleConfirmation,
    Blocked
}

public sealed record ControlledAction(
    string ActionId,
    string ActionVersion,
    string Target,
    string InputDigest,
    bool ChangesImportantState,
    IReadOnlyDictionary<string, bool> Prerequisites);

public sealed record SafetyWhitelistEntry(
    string ActionId,
    string ActionVersion,
    IReadOnlyList<string> AllowedTargetRoots,
    IReadOnlySet<string> RequiredPrerequisites,
    string? RequiredInputDigest = null);

public sealed record SafetyDecision(SafetyLevel Level, string Reason, string DecisionDigest);

public sealed record ActionConfirmation(string DecisionDigest, string Actor, DateTimeOffset ConfirmedAt);

public sealed class SafetyPolicy
{
    private readonly IReadOnlyList<SafetyWhitelistEntry> _entries;

    public SafetyPolicy(IEnumerable<SafetyWhitelistEntry> entries) => _entries = entries.ToArray();

    public SafetyDecision Evaluate(ControlledAction action)
    {
        var digest = ComputeDecisionDigest(action);
        if (string.IsNullOrWhiteSpace(action.ActionId) || string.IsNullOrWhiteSpace(action.ActionVersion) ||
            string.IsNullOrWhiteSpace(action.Target) || string.IsNullOrWhiteSpace(action.InputDigest))
            return new SafetyDecision(SafetyLevel.Blocked, "動作識別、版本、目標或輸入完整性不足。", digest);

        var missing = action.Prerequisites.Where(item => !item.Value).Select(item => item.Key).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            return new SafetyDecision(SafetyLevel.Blocked, $"必要條件不足：{string.Join("、", missing)}。", digest);

        var match = _entries.FirstOrDefault(entry => Matches(entry, action));
        if (match is not null)
            return new SafetyDecision(SafetyLevel.Automatic, "動作版本、目標、輸入限制與前置條件完整符合安全白名單。", digest);

        return new SafetyDecision(
            SafetyLevel.SingleConfirmation,
            action.ChangesImportantState ? "動作會改變重要狀態，須對本次快照單次確認。" : "動作未完整符合安全白名單，須對本次快照單次確認。",
            digest);
    }

    public static bool IsConfirmationValid(SafetyDecision decision, ActionConfirmation? confirmation) =>
        decision.Level != SafetyLevel.SingleConfirmation ||
        (confirmation is not null &&
         !string.IsNullOrWhiteSpace(confirmation.Actor) &&
         CryptographicOperations.FixedTimeEquals(
             Encoding.UTF8.GetBytes(decision.DecisionDigest),
             Encoding.UTF8.GetBytes(confirmation.DecisionDigest)));

    private static bool Matches(SafetyWhitelistEntry entry, ControlledAction action)
    {
        if (!string.Equals(entry.ActionId, action.ActionId, StringComparison.Ordinal) ||
            !string.Equals(entry.ActionVersion, action.ActionVersion, StringComparison.Ordinal)) return false;
        if (entry.RequiredInputDigest is not null && !string.Equals(entry.RequiredInputDigest, action.InputDigest, StringComparison.OrdinalIgnoreCase)) return false;
        if (entry.RequiredPrerequisites.Any(required => !action.Prerequisites.TryGetValue(required, out var met) || !met)) return false;

        string target;
        try { target = Path.GetFullPath(action.Target); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { return false; }

        return entry.AllowedTargetRoots.Any(root => IsSameOrDescendant(target, Path.GetFullPath(root)));
    }

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(candidate);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root);
        return string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeDecisionDigest(ControlledAction action)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            action.ActionId,
            action.ActionVersion,
            Target = action.Target,
            action.InputDigest,
            action.ChangesImportantState,
            Prerequisites = action.Prerequisites.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
