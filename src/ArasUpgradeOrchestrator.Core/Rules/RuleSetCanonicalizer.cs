using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Rules;

internal static class RuleSetCanonicalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Checksum(
        string displayName,
        RuleSetKind kind,
        RuleSetScope scope,
        string? sourceVersion,
        string? targetVersion,
        IReadOnlyList<RuleStepDefinition> steps)
    {
        var content = new
        {
            displayName,
            kind,
            scope,
            sourceVersion,
            targetVersion,
            steps = steps.OrderBy(step => step.Order).ThenBy(step => step.StepId, StringComparer.Ordinal).ToArray()
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content, JsonOptions)));
    }

    public static string StepFingerprint(RuleStepDefinition step) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(step, JsonOptions))));
}
