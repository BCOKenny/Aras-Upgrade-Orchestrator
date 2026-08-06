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
    Console.WriteLine("Build once: dotnet build ArasUpgradeOrchestrator.sln --configuration Release --no-restore");
    Console.WriteLine("Run the compiled CLI: dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --preflight <request.json>");
    Console.WriteLine("                         dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --request <request.json>");
    Console.WriteLine("The request must contain case roots, three version evidences, Server rule paths, and a Safety whitelist for --request.");
    return 0;
}

if (args is not ["--preflight" or "--request", var requestPath] || string.IsNullOrWhiteSpace(requestPath))
    return await WriteFailureAsync("CliArgumentError", "Expected --preflight <request.json> or --request <request.json>. Use --help for usage.", 2, jsonOptions);

try
{
    var json = await File.ReadAllTextAsync(requestPath);
    var input = JsonSerializer.Deserialize<CliRequest>(json, jsonOptions)
        ?? throw new InvalidDataException("Request JSON is empty.");
    ValidateInput(input, args[0] == "--request");

    var customer = new CoreTreeInputEvidence(input.CustomerRoot, input.SourceVersion, input.CustomerEvidence);
    var sourceOotb = new CoreTreeInputEvidence(input.SourceOotbRoot, input.SourceVersion, input.SourceOotbEvidence);
    var targetOotb = new CoreTreeInputEvidence(input.TargetOotbRoot, input.TargetVersion, input.TargetOotbEvidence);
    var serverTextRules = CoreTreeServerTextRuleSet.Create(input.ServerRuleVersion, input.ServerRulePaths);

    if (args[0] == "--preflight")
    {
        var preflightResult = await new CoreTreeComparisonPreflightCommand().ExecuteAsync(new CoreTreeComparisonPreflightRequest(
            input.CaseRoot,
            customer,
            sourceOotb,
            targetOotb,
            input.OutputRoot,
            serverTextRules));
        Console.WriteLine(JsonSerializer.Serialize(preflightResult, jsonOptions));
        return preflightResult.Status switch
        {
            CoreTreePreflightStatus.Ready or CoreTreePreflightStatus.Incomplete => 0,
            CoreTreePreflightStatus.Blocked => 2,
            _ => 1
        };
    }

    var commandRequest = new CoreTreeComparisonCommandRequest(
        input.CaseRoot,
        input.Actor,
        customer,
        sourceOotb,
        targetOotb,
        input.OutputRoot,
        serverTextRules,
        input.RetryEvidence is null ? null : new RetryEvidence(input.RetryEvidence.Basis, input.RetryEvidence.EvidenceReference),
        input.Confirmation is null ? null : new ActionConfirmation(input.Confirmation.DecisionDigest, input.Confirmation.Actor, input.Confirmation.ConfirmedAt),
        input.Prerequisites);
    var whitelist = input.SafetyWhitelist!.Select(entry => new SafetyWhitelistEntry(
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
catch (OperationCanceledException)
{
    return await WriteFailureAsync("CliCancelled", "Core Tree comparison was cancelled.", 1, jsonOptions);
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
{
    return await WriteFailureAsync("CliInputError", exception.Message, 1, jsonOptions);
}
catch (Exception)
{
    return await WriteFailureAsync("CliUnexpectedError", "The CLI could not complete the request. Inspect the case history and request evidence before retrying.", 1, jsonOptions);
}

static void ValidateInput(CliRequest input, bool requiresRequestFields)
{
    Require(input.CaseRoot, nameof(input.CaseRoot));
    Require(input.SourceVersion, nameof(input.SourceVersion));
    Require(input.TargetVersion, nameof(input.TargetVersion));
    Require(input.CustomerRoot, nameof(input.CustomerRoot));
    Require(input.CustomerEvidence, nameof(input.CustomerEvidence));
    Require(input.SourceOotbRoot, nameof(input.SourceOotbRoot));
    Require(input.SourceOotbEvidence, nameof(input.SourceOotbEvidence));
    Require(input.TargetOotbRoot, nameof(input.TargetOotbRoot));
    Require(input.TargetOotbEvidence, nameof(input.TargetOotbEvidence));
    Require(input.OutputRoot, nameof(input.OutputRoot));
    Require(input.ServerRuleVersion, nameof(input.ServerRuleVersion));
    if (input.ServerRulePaths is null || input.ServerRulePaths.Count == 0)
        throw new InvalidDataException("ServerRulePaths is required.");
    if (!requiresRequestFields) return;
    Require(input.Actor, nameof(input.Actor));
    if (input.SafetyWhitelist is null || input.SafetyWhitelist.Count == 0)
        throw new InvalidDataException("SafetyWhitelist is required.");
}

static void Require(string? value, string fieldName)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidDataException($"{fieldName} is required.");
}

static async Task<int> WriteFailureAsync(string code, string message, int exitCode, JsonSerializerOptions options)
{
    Console.WriteLine(JsonSerializer.Serialize(new CliFailure("Error", code, message), options));
    await Console.Error.WriteLineAsync($"{code}: {message}");
    return exitCode;
}

public sealed record CliFailure(string Status, string Code, string Message);

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
