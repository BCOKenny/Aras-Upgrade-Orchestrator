using System.Text.Json;
using ArasUpgradeOrchestrator.Core.CoreTrees;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Safety;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    PropertyNameCaseInsensitive = true
};

if (args is ["--help"] or ["-h"] or [])
{
    Console.WriteLine("Core Tree offline test CLI");
    Console.WriteLine("Usage: dotnet run --project tools/ArasUpgradeOrchestrator.CoreTree.Cli -- --request <request.json>");
    Console.WriteLine("The request must contain case roots, three version evidences, Server rule paths, and a Safety whitelist.");
    return 0;
}

if (args.Length != 2 || !string.Equals(args[0], "--request", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[1]))
{
    Console.Error.WriteLine("Missing --request <request.json>. Use --help for usage.");
    return 2;
}

try
{
    var json = await File.ReadAllTextAsync(args[1]);
    var input = JsonSerializer.Deserialize<CliRequest>(json, jsonOptions)
        ?? throw new InvalidDataException("Request JSON is empty.");
    var commandRequest = new CoreTreeComparisonCommandRequest(
        input.CaseRoot,
        input.Actor,
        new CoreTreeInputEvidence(input.CustomerRoot, input.SourceVersion, input.CustomerEvidence),
        new CoreTreeInputEvidence(input.SourceOotbRoot, input.SourceVersion, input.SourceOotbEvidence),
        new CoreTreeInputEvidence(input.TargetOotbRoot, input.TargetVersion, input.TargetOotbEvidence),
        input.OutputRoot,
        CoreTreeServerTextRuleSet.Create(input.ServerRuleVersion, input.ServerRulePaths),
        input.RetryEvidence is null ? null : new RetryEvidence(input.RetryEvidence.Basis, input.RetryEvidence.EvidenceReference),
        input.Confirmation is null ? null : new ActionConfirmation(input.Confirmation.DecisionDigest, input.Confirmation.Actor, input.Confirmation.ConfirmedAt),
        input.Prerequisites);
    var whitelist = input.SafetyWhitelist.Select(entry => new SafetyWhitelistEntry(
        entry.ActionId,
        entry.ActionVersion,
        entry.AllowedTargetRoots,
        new HashSet<string>(entry.RequiredPrerequisites, StringComparer.Ordinal),
        entry.RequiredInputDigest)).ToArray();
    var result = await new CoreTreeComparisonCommand(new SafetyPolicy(whitelist)).ExecuteAsync(commandRequest);
    Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
    return result.CommandStatus switch
    {
        CoreTreeComparisonCommandStatus.Completed or CoreTreeComparisonCommandStatus.Incomplete => 0,
        CoreTreeComparisonCommandStatus.Blocked => 2,
        _ => 1
    };
}
catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

public sealed record CliRequest(
    string CaseRoot,
    string Actor,
    string SourceVersion,
    string TargetVersion,
    string CustomerRoot,
    string CustomerEvidence,
    string SourceOotbRoot,
    string SourceOotbEvidence,
    string TargetOotbRoot,
    string TargetOotbEvidence,
    string OutputRoot,
    string ServerRuleVersion,
    IReadOnlyList<string> ServerRulePaths,
    IReadOnlyList<CliSafetyWhitelistEntry> SafetyWhitelist,
    IReadOnlyDictionary<string, bool>? Prerequisites = null,
    CliRetryEvidence? RetryEvidence = null,
    CliConfirmation? Confirmation = null);

public sealed record CliSafetyWhitelistEntry(
    string ActionId,
    string ActionVersion,
    IReadOnlyList<string> AllowedTargetRoots,
    IReadOnlyList<string> RequiredPrerequisites,
    string? RequiredInputDigest = null);

public sealed record CliRetryEvidence(RetryBasis Basis, string EvidenceReference);
public sealed record CliConfirmation(string DecisionDigest, string Actor, DateTimeOffset ConfirmedAt);
