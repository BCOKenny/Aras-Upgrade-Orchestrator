using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;
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
    Console.WriteLine("Create a Core Tree workflow case: dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --create-core-tree-case <request.json>");
    Console.WriteLine("Run the compiled CLI: dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --preflight <request.json>");
    Console.WriteLine("                         dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --request <request.json>");
    Console.WriteLine("                         dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --approve-reviews <request.json>");
    Console.WriteLine("                         dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --finalize-comparison <request.json>");
    Console.WriteLine("                         dotnet tools/ArasUpgradeOrchestrator.CoreTree.Cli/bin/Release/net8.0/ArasUpgradeOrchestrator.CoreTree.Cli.dll --build-delivery <request.json>");
    Console.WriteLine("The request must contain case roots, three version evidences, Server rule paths, and a Safety whitelist for --request.");
    return 0;
}

if (args is ["--create-core-tree-case", var creationRequestPath] && !string.IsNullOrWhiteSpace(creationRequestPath))
{
    try
    {
        var creationInput = JsonSerializer.Deserialize<CliCoreTreeCaseCreationRequest>(await File.ReadAllTextAsync(creationRequestPath), jsonOptions)
            ?? throw new InvalidDataException("Request JSON is empty.");
        var definition = ValidateCoreTreeCaseCreationInput(creationInput);
        var caseStore = new CaseStore(creationInput.CaseRoot);
        var manifest = CaseManifest.CreateCoreTreeWorkflow(
            Guid.NewGuid(),
            creationInput.CustomerCode,
            creationInput.SourceVersion,
            creationInput.TargetVersion,
            definition,
            DateTimeOffset.UtcNow);
        await caseStore.CreateAsync(manifest);
        Console.WriteLine(JsonSerializer.Serialize(new CliCoreTreeCaseCreationResult(
            manifest.CaseId,
            "CoreTree",
            caseStore.ManifestPath,
            caseStore.ToolDataPath), jsonOptions));
        return 0;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
    {
        return await WriteFailureAsync("CliInputError", exception.Message, 1, jsonOptions);
    }
}

if (args is ["--approve-reviews", var approvalRequestPath] && !string.IsNullOrWhiteSpace(approvalRequestPath))
{
    try
    {
        var approvalInput = JsonSerializer.Deserialize<CliApprovalRequest>(await File.ReadAllTextAsync(approvalRequestPath), jsonOptions)
            ?? throw new InvalidDataException("Request JSON is empty.");
        ValidateApprovalInput(approvalInput);
        var whitelist = approvalInput.SafetyWhitelist.Select(entry => new SafetyWhitelistEntry(
            entry.ActionId, entry.ActionVersion, entry.AllowedTargetRoots,
            new HashSet<string>(entry.RequiredPrerequisites, StringComparer.Ordinal), entry.RequiredInputDigest)).ToArray();
        var result = await new CoreTreeManualReviewApprovalCommand(new SafetyPolicy(whitelist)).ExecuteAsync(new(
            approvalInput.CaseRoot, approvalInput.Actor, approvalInput.ComparisonOutputRoot,
            approvalInput.ReviewRegisterPath, approvalInput.ApprovalOutputRoot,
            approvalInput.Confirmation is null ? null : new ActionConfirmation(approvalInput.Confirmation.DecisionDigest, approvalInput.Confirmation.Actor, approvalInput.Confirmation.ConfirmedAt),
            approvalInput.Prerequisites));
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
        return result.Status == CoreTreeManualReviewApprovalStatus.Approved ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
    {
        return await WriteFailureAsync("CliInputError", exception.Message, 1, jsonOptions);
    }
}

if (args is ["--finalize-comparison", var finalizationRequestPath] && !string.IsNullOrWhiteSpace(finalizationRequestPath))
{
    try
    {
        var finalizationInput = JsonSerializer.Deserialize<CliFinalizationRequest>(await File.ReadAllTextAsync(finalizationRequestPath), jsonOptions)
            ?? throw new InvalidDataException("Request JSON is empty.");
        ValidateFinalizationInput(finalizationInput);
        var whitelist = finalizationInput.SafetyWhitelist.Select(entry => new SafetyWhitelistEntry(
            entry.ActionId, entry.ActionVersion, entry.AllowedTargetRoots,
            new HashSet<string>(entry.RequiredPrerequisites, StringComparer.Ordinal), entry.RequiredInputDigest)).ToArray();
        var result = await new CoreTreeComparisonFinalizationCommand(new SafetyPolicy(whitelist)).ExecuteAsync(new(
            finalizationInput.CaseRoot, finalizationInput.Actor, finalizationInput.ComparisonOutputRoot,
            finalizationInput.ApprovalManifestPath, finalizationInput.CompletionOutputRoot,
            finalizationInput.Confirmation is null ? null : new ActionConfirmation(finalizationInput.Confirmation.DecisionDigest, finalizationInput.Confirmation.Actor, finalizationInput.Confirmation.ConfirmedAt),
            finalizationInput.Prerequisites));
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
        return result.Status == CoreTreeComparisonFinalizationStatus.Completed ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
    {
        return await WriteFailureAsync("CliInputError", exception.Message, 1, jsonOptions);
    }
}

if (args is ["--build-delivery", var deliveryRequestPath] && !string.IsNullOrWhiteSpace(deliveryRequestPath))
{
    try
    {
        var deliveryInput = JsonSerializer.Deserialize<CliDeliveryRequest>(await File.ReadAllTextAsync(deliveryRequestPath), jsonOptions)
            ?? throw new InvalidDataException("Request JSON is empty.");
        ValidateDeliveryInput(deliveryInput);
        var whitelist = deliveryInput.SafetyWhitelist.Select(entry => new SafetyWhitelistEntry(
            entry.ActionId, entry.ActionVersion, entry.AllowedTargetRoots,
            new HashSet<string>(entry.RequiredPrerequisites, StringComparer.Ordinal), entry.RequiredInputDigest)).ToArray();
        var result = await new CoreTreeDeliveryCommand(new SafetyPolicy(whitelist)).ExecuteAsync(new(
            deliveryInput.CaseRoot, deliveryInput.Actor, deliveryInput.ComparisonOutputRoot,
            deliveryInput.CompletionManifestPath, deliveryInput.DeliveryOutputRoot,
            deliveryInput.Confirmation is null ? null : new ActionConfirmation(deliveryInput.Confirmation.DecisionDigest, deliveryInput.Confirmation.Actor, deliveryInput.Confirmation.ConfirmedAt),
            deliveryInput.Prerequisites));
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
        return result.Status == CoreTreeDeliveryStatus.Completed ? 0 : 2;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or InvalidOperationException)
    {
        return await WriteFailureAsync("CliInputError", exception.Message, 1, jsonOptions);
    }
}

if (args is not ["--preflight" or "--request", var requestPath] || string.IsNullOrWhiteSpace(requestPath))
    return await WriteFailureAsync("CliArgumentError", "Expected --create-core-tree-case <request.json>, --preflight <request.json>, --request <request.json>, --approve-reviews <request.json>, --finalize-comparison <request.json>, or --build-delivery <request.json>. Use --help for usage.", 2, jsonOptions);

try
{
    var json = await File.ReadAllTextAsync(requestPath);
    var input = JsonSerializer.Deserialize<CliRequest>(json, jsonOptions)
        ?? throw new InvalidDataException("Request JSON is empty.");
    ValidateInput(input, args[0] == "--request");

    var caseManifest = await new CaseStore(input.CaseRoot).LoadAsync();
    var resolvedInputs = ResolveCoreTreeInputs(input, caseManifest);
    var customer = new CoreTreeInputEvidence(resolvedInputs.CustomerRoot, input.SourceVersion, resolvedInputs.CustomerEvidence);
    var sourceOotb = new CoreTreeInputEvidence(resolvedInputs.SourceOotbRoot, input.SourceVersion, resolvedInputs.SourceOotbEvidence);
    var targetOotb = new CoreTreeInputEvidence(resolvedInputs.TargetOotbRoot, input.TargetVersion, resolvedInputs.TargetOotbEvidence);
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
    if (!input.UseCaseCoreTreeInputs)
    {
        Require(input.CustomerRoot, nameof(input.CustomerRoot));
        Require(input.CustomerEvidence, nameof(input.CustomerEvidence));
        Require(input.SourceOotbRoot, nameof(input.SourceOotbRoot));
        Require(input.SourceOotbEvidence, nameof(input.SourceOotbEvidence));
        Require(input.TargetOotbRoot, nameof(input.TargetOotbRoot));
        Require(input.TargetOotbEvidence, nameof(input.TargetOotbEvidence));
    }
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

static CoreTreeComparisonDefinition ValidateCoreTreeCaseCreationInput(CliCoreTreeCaseCreationRequest input)
{
    Require(input.CaseRoot, nameof(input.CaseRoot));
    Require(input.CustomerCode, nameof(input.CustomerCode));
    Require(input.SourceVersion, nameof(input.SourceVersion));
    Require(input.TargetVersion, nameof(input.TargetVersion));
    if (string.Equals(input.SourceVersion, input.TargetVersion, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("SourceVersion and TargetVersion must differ.");

    var definition = new CoreTreeComparisonDefinition(
        input.CustomerInputId,
        input.SourceOotbInputId,
        input.TargetOotbInputId,
        input.CustomerTreePath,
        input.CustomerEvidencePath,
        input.SourceOotbTreePath,
        input.SourceOotbEvidencePath,
        input.TargetOotbTreePath,
        input.TargetOotbEvidencePath,
        input.ComparisonName);
    definition.Validate();

    var caseRoot = Path.GetFullPath(input.CaseRoot);
    if (!Directory.Exists(caseRoot))
        throw new InvalidDataException("CaseRoot must already exist before creating a formal Core Tree workflow case.");

    var inputRoots = new[]
    {
        ResolveCaseRelativePath(caseRoot, definition.CustomerTreePath),
        ResolveCaseRelativePath(caseRoot, definition.SourceOotbTreePath),
        ResolveCaseRelativePath(caseRoot, definition.TargetOotbTreePath)
    };
    var evidenceRoots = new[]
    {
        ResolveCaseRelativePath(caseRoot, definition.CustomerEvidencePath),
        ResolveCaseRelativePath(caseRoot, definition.SourceOotbEvidencePath),
        ResolveCaseRelativePath(caseRoot, definition.TargetOotbEvidencePath)
    };
    for (var index = 0; index < inputRoots.Length; index++)
        ValidateCoreTreeCaseInput(inputRoots[index], evidenceRoots[index]);
    for (var left = 0; left < inputRoots.Length; left++)
        for (var right = left + 1; right < inputRoots.Length; right++)
            if (PathsOverlap(inputRoots[left], inputRoots[right]))
                throw new InvalidDataException("Core Tree input roots must not overlap.");

    return definition;
}

static string ResolveCaseRelativePath(string caseRoot, string relativePath)
{
    var resolved = Path.GetFullPath(Path.Combine(caseRoot, relativePath));
    if (!resolved.StartsWith(Path.TrimEndingDirectorySeparator(caseRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Core Tree paths must remain under CaseRoot.");
    return resolved;
}

static void ValidateCoreTreeCaseInput(string treePath, string evidencePath)
{
    if (!Directory.Exists(treePath))
        throw new InvalidDataException($"Core Tree input directory does not exist: {treePath}");
    foreach (var side in new[] { "Client", "Server" })
        if (!Directory.Exists(Path.Combine(treePath, "Innovator", side)))
            throw new InvalidDataException($"Core Tree input is missing Innovator\\{side}: {treePath}");
    if (!Directory.Exists(evidencePath))
        throw new InvalidDataException($"Core Tree evidence directory does not exist: {evidencePath}");

    var names = Directory.EnumerateFiles(evidencePath, "*", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var required in new[] { "version-primary.md", "integrity.md", "integrity.sha256", "source-provenance.md" })
        if (!names.Contains(required))
            throw new InvalidDataException($"Core Tree evidence is missing {required}: {evidencePath}");
}

static bool PathsOverlap(string left, string right) =>
    IsSameOrDescendant(left, right) || IsSameOrDescendant(right, left);

static bool IsSameOrDescendant(string candidate, string root) =>
    string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
    candidate.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

static ResolvedCoreTreeInputs ResolveCoreTreeInputs(CliRequest input, CaseManifest manifest)
{
    if (!input.UseCaseCoreTreeInputs)
        return new(
            input.CustomerRoot!, input.CustomerEvidence!, input.SourceOotbRoot!, input.SourceOotbEvidence!,
            input.TargetOotbRoot!, input.TargetOotbEvidence!);

    var definition = manifest.CoreTreeComparison
        ?? throw new InvalidDataException("案件清單缺少 coreTreeComparison，無法依案件設定解析 Core Tree 輸入。 ");
    definition.Validate();
    return new(
        Path.Combine(Path.GetFullPath(input.CaseRoot), definition.CustomerTreePath),
        Path.Combine(Path.GetFullPath(input.CaseRoot), definition.CustomerEvidencePath),
        Path.Combine(Path.GetFullPath(input.CaseRoot), definition.SourceOotbTreePath),
        Path.Combine(Path.GetFullPath(input.CaseRoot), definition.SourceOotbEvidencePath),
        Path.Combine(Path.GetFullPath(input.CaseRoot), definition.TargetOotbTreePath),
        Path.Combine(Path.GetFullPath(input.CaseRoot), definition.TargetOotbEvidencePath));
}

static void ValidateApprovalInput(CliApprovalRequest input)
{
    Require(input.CaseRoot, nameof(input.CaseRoot));
    Require(input.Actor, nameof(input.Actor));
    Require(input.ComparisonOutputRoot, nameof(input.ComparisonOutputRoot));
    Require(input.ReviewRegisterPath, nameof(input.ReviewRegisterPath));
    Require(input.ApprovalOutputRoot, nameof(input.ApprovalOutputRoot));
    if (input.SafetyWhitelist is null || input.SafetyWhitelist.Count == 0)
        throw new InvalidDataException("SafetyWhitelist is required.");
}

static void ValidateFinalizationInput(CliFinalizationRequest input)
{
    Require(input.CaseRoot, nameof(input.CaseRoot));
    Require(input.Actor, nameof(input.Actor));
    Require(input.ComparisonOutputRoot, nameof(input.ComparisonOutputRoot));
    Require(input.ApprovalManifestPath, nameof(input.ApprovalManifestPath));
    Require(input.CompletionOutputRoot, nameof(input.CompletionOutputRoot));
    if (input.SafetyWhitelist is null || input.SafetyWhitelist.Count == 0)
        throw new InvalidDataException("SafetyWhitelist is required.");
}

static void ValidateDeliveryInput(CliDeliveryRequest input)
{
    Require(input.CaseRoot, nameof(input.CaseRoot));
    Require(input.Actor, nameof(input.Actor));
    Require(input.ComparisonOutputRoot, nameof(input.ComparisonOutputRoot));
    Require(input.CompletionManifestPath, nameof(input.CompletionManifestPath));
    Require(input.DeliveryOutputRoot, nameof(input.DeliveryOutputRoot));
    if (input.SafetyWhitelist is null || input.SafetyWhitelist.Count == 0)
        throw new InvalidDataException("SafetyWhitelist is required.");
}

static async Task<int> WriteFailureAsync(string code, string message, int exitCode, JsonSerializerOptions options)
{
    Console.WriteLine(JsonSerializer.Serialize(new CliFailure("Error", code, message), options));
    await Console.Error.WriteLineAsync($"{code}: {message}");
    return exitCode;
}

public sealed record CliFailure(string Status, string Code, string Message);

public sealed record CliCoreTreeCaseCreationRequest(
    string CaseRoot,
    string CustomerCode,
    string SourceVersion,
    string TargetVersion,
    string CustomerInputId,
    string SourceOotbInputId,
    string TargetOotbInputId,
    string CustomerTreePath,
    string CustomerEvidencePath,
    string SourceOotbTreePath,
    string SourceOotbEvidencePath,
    string TargetOotbTreePath,
    string TargetOotbEvidencePath,
    string ComparisonName);

public sealed record CliCoreTreeCaseCreationResult(
    Guid CaseId,
    string Workflow,
    string ManifestPath,
    string ToolDataPath);

public sealed record CliRequest(
    string CaseRoot,
    string Actor,
    string SourceVersion,
    string TargetVersion,
    string? CustomerRoot,
    string? CustomerEvidence,
    string? SourceOotbRoot,
    string? SourceOotbEvidence,
    string? TargetOotbRoot,
    string? TargetOotbEvidence,
    string OutputRoot,
    string ServerRuleVersion,
    IReadOnlyList<string> ServerRulePaths,
    IReadOnlyList<CliSafetyWhitelistEntry> SafetyWhitelist,
    IReadOnlyDictionary<string, bool>? Prerequisites = null,
    CliRetryEvidence? RetryEvidence = null,
    CliConfirmation? Confirmation = null,
    bool UseCaseCoreTreeInputs = false);

public sealed record CliSafetyWhitelistEntry(
    string ActionId,
    string ActionVersion,
    IReadOnlyList<string> AllowedTargetRoots,
    IReadOnlyList<string> RequiredPrerequisites,
    string? RequiredInputDigest = null);

public sealed record CliRetryEvidence(RetryBasis Basis, string EvidenceReference);
public sealed record CliConfirmation(string DecisionDigest, string Actor, DateTimeOffset ConfirmedAt);
public sealed record CliApprovalRequest(
    string CaseRoot,
    string Actor,
    string ComparisonOutputRoot,
    string ReviewRegisterPath,
    string ApprovalOutputRoot,
    IReadOnlyList<CliSafetyWhitelistEntry> SafetyWhitelist,
    IReadOnlyDictionary<string, bool>? Prerequisites = null,
    CliConfirmation? Confirmation = null);

public sealed record CliFinalizationRequest(
    string CaseRoot,
    string Actor,
    string ComparisonOutputRoot,
    string ApprovalManifestPath,
    string CompletionOutputRoot,
    IReadOnlyList<CliSafetyWhitelistEntry> SafetyWhitelist,
    IReadOnlyDictionary<string, bool>? Prerequisites = null,
    CliConfirmation? Confirmation = null);

public sealed record CliDeliveryRequest(
    string CaseRoot,
    string Actor,
    string ComparisonOutputRoot,
    string CompletionManifestPath,
    string DeliveryOutputRoot,
    IReadOnlyList<CliSafetyWhitelistEntry> SafetyWhitelist,
    IReadOnlyDictionary<string, bool>? Prerequisites = null,
    CliConfirmation? Confirmation = null);

public sealed record ResolvedCoreTreeInputs(
    string CustomerRoot,
    string CustomerEvidence,
    string SourceOotbRoot,
    string SourceOotbEvidence,
    string TargetOotbRoot,
    string TargetOotbEvidence);
