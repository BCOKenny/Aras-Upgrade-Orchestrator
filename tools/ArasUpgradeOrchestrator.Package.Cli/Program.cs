using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Packages;
using ArasUpgradeOrchestrator.Core.Rules;
using ArasUpgradeOrchestrator.Core.Safety;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, PropertyNameCaseInsensitive = true };
if (args is [] or ["--help"] or ["-h"])
{
    Console.WriteLine("Package CLI: --register-rule1-preparation <request.json> | --prepare-rule1 <request.json> | --confirm-rule1 <request.json> | --scaffold-customer-patch-preparation <request.json> | --prepare-customer-patch-rule-publication <request.json> | --record-customer-patch-evidence <request.json> | --preflight-customer-patch-common-rule <request.json> | --publish-customer-patch-common-rule <request.json> | --preflight-codex-customer-patch-common-rule <request.json> | --execute-customer-patch-common-rule <request.json> | --preflight-customer-patch-comparison <request.json>");
    return 0;
}
if (args.Length != 2 || args[0] is not "--register-rule1-preparation" and not "--prepare-rule1" and not "--confirm-rule1" and not "--scaffold-customer-patch-preparation" and not "--prepare-customer-patch-rule-publication" and not "--record-customer-patch-evidence" and not "--preflight-customer-patch-common-rule" and not "--publish-customer-patch-common-rule" and not "--preflight-codex-customer-patch-common-rule" and not "--execute-customer-patch-common-rule" and not "--preflight-customer-patch-comparison") return 2;
try
{
    var text = await File.ReadAllTextAsync(args[1]);
    if (args[0] == "--register-rule1-preparation")
    {
        var input = JsonSerializer.Deserialize<RegistrationRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
        var preparation = new PackageComparisonPreparation(input.PreparationId, input.SourceVersion, input.TargetVersion,
            input.OotbSourceSolutionsRelativePath, input.OotbTargetSolutionsRelativePath, input.PatchSupportInputRelativePath,
            input.PreparationAttemptRelativePath, DateTimeOffset.MinValue, input.Actor, input.Notes);
        var result = await new Rule1PreparationRegistrationCommand(Policy(input.SafetyWhitelist)).ExecuteAsync(new(input.CaseRoot, input.Actor, preparation, Confirm(input.Confirmation)));
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return 0;
    }
    if (args[0] == "--prepare-rule1")
    {
        var input = JsonSerializer.Deserialize<PrepareRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
        var preparation = new PackageComparisonPreparation(input.PreparationId, input.SourceVersion, input.TargetVersion,
            input.OotbSourceSolutionsRelativePath, input.OotbTargetSolutionsRelativePath, input.PatchSupportInputRelativePath,
            input.PreparationAttemptRelativePath, DateTimeOffset.MinValue, input.Actor, input.Notes);
        var result = await new Rule1PreparationCommand(Policy(input.SafetyWhitelist)).ExecuteAsync(new(input.CaseRoot, input.PreparationId, input.AttemptId, input.Actor, await Resolve(input.RuleStoreRoot, RuleSetKind.Rule1, input.SourceVersion, input.TargetVersion), Confirm(input.Confirmation), preparation));
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return result.Status == Rule1PreparationStatus.PendingVerification ? 0 : 2;
    }
    if (args[0] == "--record-customer-patch-evidence")
    {
        var input = JsonSerializer.Deserialize<CustomerPatchEvidenceRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
        var result = await new CustomerPatchEvidenceRegistrationCommand().ExecuteAsync(new(input.CaseRoot, input.PreparationId, input.HopId,
            input.Source, input.Target, input.EvidenceRelativePath,
            input.PatchSupportSource, input.Actor));
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return 0;
    }
    if (args[0] == "--scaffold-customer-patch-preparation")
    {
        var input = JsonSerializer.Deserialize<CustomerPatchPreparationScaffoldRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
        var result = await new CustomerPatchPreparationScaffoldCommand().ExecuteAsync(input);
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return 0;
    }
    if (args[0] == "--prepare-customer-patch-rule-publication")
    {
        var input = JsonSerializer.Deserialize<CustomerPatchRulePublicationPreparationCliRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
        var result = await new CustomerPatchCommonRulePublicationPreparationCommand().ExecuteAsync(new(
            input.CaseRoot, input.Actor, input.HumanApprovalDecision));
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return 0;
    }
    if (args[0] == "--publish-customer-patch-common-rule")
    {
        var input = JsonSerializer.Deserialize<CustomerPatchCommonRulePublicationRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
        var result = await new CustomerPatchCommonRulePublicationCommand().ExecuteAsync(new ArasUpgradeOrchestrator.Core.Rules.CustomerPatchCommonRulePublicationRequest(
            input.CaseRoot, input.RuleStoreRelativePath, input.ApprovalEvidenceRelativePath, input.ApprovalReceiptRelativePath, input.Actor));
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return 0;
    }
    if (args[0] is "--preflight-codex-customer-patch-common-rule" or "--execute-customer-patch-common-rule")
    {
        var requestBytes = await File.ReadAllBytesAsync(args[1]);
        var input = ParseCodexExecutionRequest(requestBytes, json);
        if (args[0] == "--preflight-codex-customer-patch-common-rule")
        {
            var preflightResult = await new CustomerPatchCommonRulePublicationPreflightCommand().ExecuteAsync(new(
                input.CaseRoot,
                input.RuleStoreRelativePath,
                input.ApprovalEvidenceRelativePath,
                input.ApprovalReceiptRelativePath,
                input.ApprovedBy));
            Console.WriteLine(JsonSerializer.Serialize(preflightResult, json)); return 0;
        }
        var result = await new CustomerPatchCodexExecutionCommand().ExecuteAsync(new(
            input.CaseRoot,
            input.RuleStoreRelativePath,
            input.ApprovalEvidenceRelativePath,
            input.ApprovalReceiptRelativePath,
            input.ApprovedBy,
            requestBytes));
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return 0;
    }
    if (args[0] == "--preflight-customer-patch-common-rule")
    {
        var input = JsonSerializer.Deserialize<CustomerPatchCommonRulePublicationRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
        var result = await new CustomerPatchCommonRulePublicationPreflightCommand().ExecuteAsync(new(
            input.CaseRoot, input.RuleStoreRelativePath, input.ApprovalEvidenceRelativePath, input.ApprovalReceiptRelativePath, input.Actor));
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return 0;
    }
    if (args[0] == "--preflight-customer-patch-comparison")
    {
        var input = JsonSerializer.Deserialize<CustomerPatchPreflightRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
        var result = await new CustomerPatchComparisonPreflightCommand().ExecuteAsync(new(input.CaseRoot, input.PreparationId, input.HopId,
            input.Source, input.Target,
            input.EvidenceRelativePath, input.ComparisonAttemptsRelativePath, input.AttemptId,
            await Resolve(input.RuleStoreRoot, RuleSetKind.CustomerPatchComparison, input.Source.BaselineId, input.Target.TargetRelease)));
        Console.WriteLine(JsonSerializer.Serialize(result, json)); return result.Status == CustomerPatchComparisonPreflightStatus.Ready ? 0 : 2;
    }
    var confirm = JsonSerializer.Deserialize<ConfirmRequest>(text, json) ?? throw new InvalidDataException("Request JSON is empty.");
    var confirmation = await new Rule1AvailabilityConfirmationCommand(Policy(confirm.SafetyWhitelist)).ExecuteAsync(new(confirm.CaseRoot, confirm.PreparationId, confirm.AttemptRelativePath, confirm.ArtifactRelativePath, confirm.Actor, await Resolve(confirm.RuleStoreRoot, RuleSetKind.Rule1, confirm.SourceVersion, confirm.TargetVersion), Confirm(confirm.Confirmation)));
    Console.WriteLine(JsonSerializer.Serialize(confirmation, json)); return confirmation.Completed ? 0 : 2;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException) { Console.Error.WriteLine($"CliInputError: {exception.Message}"); return 1; }

static async Task<RuleSetResolutionResult> Resolve(string root, RuleSetKind kind, string source, string target) => RuleSetResolver.Resolve(await new RuleSetStore(root).ListPublishedAsync(), kind, source, target);
static CustomerPatchCodexExecutionCliRequest ParseCodexExecutionRequest(byte[] requestBytes, JsonSerializerOptions json)
{
    using var document = JsonDocument.Parse(requestBytes);
    if (document.RootElement.ValueKind != JsonValueKind.Object)
        throw new InvalidDataException("Codex 受控發布 request 必須是 JSON object。 ");
    var allowedProperties = new HashSet<string>(StringComparer.Ordinal)
    {
        "caseRoot",
        "ruleStoreRelativePath",
        "approvalEvidenceRelativePath",
        "approvalReceiptRelativePath",
        "approvedBy"
    };
    foreach (var property in document.RootElement.EnumerateObject())
        if (!allowedProperties.Contains(property.Name))
            throw new InvalidDataException($"Codex 受控發布 request 不允許欄位：{property.Name}");

    return JsonSerializer.Deserialize<CustomerPatchCodexExecutionCliRequest>(requestBytes, json)
        ?? throw new InvalidDataException("Request JSON is empty.");
}
static SafetyPolicy Policy(IReadOnlyList<CliWhitelist> entries) => new(entries.Select(x => new SafetyWhitelistEntry(x.ActionId, x.ActionVersion, x.AllowedTargetRoots, new HashSet<string>(x.RequiredPrerequisites, StringComparer.Ordinal), x.RequiredInputDigest)));
static ActionConfirmation? Confirm(CliConfirmation? value) => value is null ? null : new(value.DecisionDigest, value.Actor, value.ConfirmedAt);
public sealed record CliWhitelist(string ActionId, string ActionVersion, IReadOnlyList<string> AllowedTargetRoots, IReadOnlyList<string> RequiredPrerequisites, string? RequiredInputDigest = null);
public sealed record CliConfirmation(string DecisionDigest, string Actor, DateTimeOffset ConfirmedAt);
public sealed record RegistrationRequest(string CaseRoot, string Actor, string PreparationId, string SourceVersion, string TargetVersion, string OotbSourceSolutionsRelativePath, string OotbTargetSolutionsRelativePath, string PatchSupportInputRelativePath, string PreparationAttemptRelativePath, IReadOnlyList<CliWhitelist> SafetyWhitelist, string? Notes = null, CliConfirmation? Confirmation = null);
public sealed record PrepareRequest(string CaseRoot, string PreparationId, string AttemptId, string Actor, string RuleStoreRoot, string SourceVersion, string TargetVersion, string OotbSourceSolutionsRelativePath, string OotbTargetSolutionsRelativePath, string PatchSupportInputRelativePath, string PreparationAttemptRelativePath, IReadOnlyList<CliWhitelist> SafetyWhitelist, string? Notes = null, CliConfirmation? Confirmation = null);
public sealed record ConfirmRequest(string CaseRoot, string PreparationId, string AttemptRelativePath, string ArtifactRelativePath, string Actor, string RuleStoreRoot, string SourceVersion, string TargetVersion, IReadOnlyList<CliWhitelist> SafetyWhitelist, CliConfirmation? Confirmation = null);
public sealed record CustomerPatchEvidenceRequest(string CaseRoot, string PreparationId, string HopId, CustomerPatchComparisonSource Source, CustomerPatchComparisonTarget Target, string EvidenceRelativePath, string PatchSupportSource, string Actor);
public sealed record CustomerPatchCommonRulePublicationRequest(string CaseRoot, string RuleStoreRelativePath, string ApprovalEvidenceRelativePath, string ApprovalReceiptRelativePath, string Actor);
public sealed record CustomerPatchRulePublicationPreparationCliRequest(string CaseRoot, string Actor, string HumanApprovalDecision);
public sealed record CustomerPatchCodexExecutionCliRequest(string CaseRoot, string RuleStoreRelativePath, string ApprovalEvidenceRelativePath, string ApprovalReceiptRelativePath, string ApprovedBy);
public sealed record CustomerPatchPreflightRequest(string CaseRoot, string PreparationId, string HopId, CustomerPatchComparisonSource Source, CustomerPatchComparisonTarget Target, string EvidenceRelativePath, string ComparisonAttemptsRelativePath, string AttemptId, string RuleStoreRoot);
