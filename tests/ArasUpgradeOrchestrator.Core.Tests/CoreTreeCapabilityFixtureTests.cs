using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArasUpgradeOrchestrator.Core.CoreTrees;
using ArasUpgradeOrchestrator.Core.Safety;

internal static class CoreTreeCapabilityFixtureTests
{
    private const string ContractVersion = "core-tree-capabilities/1";

    internal static async Task ValidateInputsAsync()
    {
        await ForEachCaseAsync("aras-validate-core-tree-inputs", async (caseId, input, expected) =>
        {
            await using var scope = FixtureScope.Create();
            var request = CreateRequest(scope, input.RootElement, out var inputIds);
            var evidence = new FixtureEvidence(inputIds, request.ServerTextRules.Version, request.ServerTextRules.Checksum);
            object actual;
            try
            {
                CoreTreeInputValidator.Validate(request);
                actual = Envelope("aras-validate-core-tree-inputs", "Validated",
                    new { validatedInputs = evidence.InputIds }, Array.Empty<object>(), evidence);
            }
            catch (CoreTreeValidationException exception)
            {
                var message = ValidationMessage(input.RootElement, exception.Code);
                actual = Envelope("aras-validate-core-tree-inputs", "Blocked", new { validatedInputs = Array.Empty<string>() },
                    new object[] { message }, evidence);
            }
            using var actualJson = ToJson(actual);
            AssertJsonSemanticEqual(expected.RootElement, actualJson.RootElement);
            await Task.CompletedTask;
        });
    }

    internal static async Task CompareContentAsync()
    {
        await ForEachCaseAsync("aras-compare-core-tree-content", async (_, input, expected) =>
        {
            await using var scope = FixtureScope.Create();
            var root = Path.Combine(scope.Root, "content");
            Directory.CreateDirectory(root);
            var left = Path.Combine(root, "left.bin");
            var right = Path.Combine(root, "right.bin");
            await File.WriteAllBytesAsync(left, DecodeBytes(input.RootElement.GetProperty("left")));
            await File.WriteAllBytesAsync(right, DecodeBytes(input.RootElement.GetProperty("right")));
            var rules = RuntimeRules(input.RootElement.GetProperty("serverRules"));
            var relativePath = input.RootElement.GetProperty("relativePath").GetString()!;
            var comparison = await CoreTreeContentComparer.CompareAsync(left, right, relativePath, rules);
            var evidence = new FixtureEvidence(["left", "right"], rules.Version, rules.Checksum);
            var messages = comparison.Mode == CoreTreeContentComparisonMode.BinaryFallback
                ? new[] { Message("Notice", "TextDecodeFallback", relativePath, new { }) }
                : [];
            var actual = Envelope("aras-compare-core-tree-content", "Compared",
                new { comparison = comparison.AreEqual ? "Equal" : "Different", mode = comparison.Mode.ToString() },
                messages, evidence);
            using var actualJson = ToJson(actual);
            AssertJsonSemanticEqual(expected.RootElement, actualJson.RootElement);
        });
    }

    internal static async Task ResolveMappingsAsync()
    {
        await ForEachCaseAsync("aras-resolve-core-tree-file-mappings", async (_, input, expected) =>
        {
            await using var scope = FixtureScope.Create();
            var targetRoot = Path.Combine(scope.Root, "target", "Innovator");
            Directory.CreateDirectory(targetRoot);
            foreach (var path in input.RootElement.GetProperty("targetRelativePaths").EnumerateArray().Select(item => item.GetString()!))
                await WriteFileAsync(targetRoot, path, [0]);
            var source = input.RootElement.GetProperty("sourceRelativePath").GetString()!;
            var match = CoreTreeLogicalPathResolver.Resolve(source, targetRoot);
            var evidence = Evidence(input.RootElement.GetProperty("evidence"));
            var messages = match.Status == CoreTreeLogicalMatchStatus.Ambiguous
                ? new[] { Message("ManualReview", "MultipleTargetMappings", source, new { candidates = match.Candidates }) }
                : [];
            var actual = Envelope("aras-resolve-core-tree-file-mappings",
                match.Status == CoreTreeLogicalMatchStatus.Ambiguous ? "Blocked" : "Resolved",
                new { mapping = match.Status.ToString(), candidates = match.Candidates, appliedEvolution = match.AppliedEvolution }, messages, evidence);
            using var actualJson = ToJson(actual);
            AssertJsonSemanticEqual(expected.RootElement, actualJson.RootElement);
        });
    }

    internal static async Task ClassifyDifferencesAsync()
    {
        await ForEachCaseAsync("aras-classify-core-tree-differences", async (_, input, expected) =>
        {
            await using var scope = FixtureScope.Create();
            var request = CreateClassificationRequest(scope, input.RootElement, out var inputIds);
            FileStream? unreadableHandle = null;
            try
            {
                var unreadable = input.RootElement.GetProperty("unreadableCustomerPaths").EnumerateArray().Select(item => item.GetString()!).ToArray();
                if (unreadable.Length == 1)
                {
                    var path = SafePath(Path.Combine(request.Customer.RootPath, "Innovator"), unreadable[0]);
                    unreadableHandle = new FileStream(path, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.ReadWrite, Share = FileShare.None });
                }
                var result = await CoreTreeComparisonEngine.CompareAsync(request);
                var evidence = new FixtureEvidence(inputIds, result.ServerRuleVersion, result.ServerRuleChecksum);
                var messages = result.ManualReviews.Select(review => Message("ManualReview", review.Code, review.SourceRelativePath,
                    review.Code == "CustomerAdditionCollidesWithTarget"
                        ? new { additionDetected = true, candidateTargetRelativePaths = review.TargetCandidates }
                        : new { candidateTargetRelativePaths = review.TargetCandidates }))
                    .Concat(result.Errors.Select(error => Message("Error", error.Code, error.RelativePath,
                        new { reason = unreadable.Length > 0 ? "fixture unreadable customer file" : error.Message })))
                    .ToArray();
                var actual = Envelope("aras-classify-core-tree-differences", result.Status.ToString(),
                    new { items = result.Items.Select(item => new { classification = item.Classification.ToString(), sourceRelativePath = item.SourceRelativePath, targetRelativePath = item.TargetRelativePath }).ToArray() },
                    messages, evidence);
                using var actualJson = ToJson(actual);
                AssertJsonSemanticEqual(expected.RootElement, actualJson.RootElement);
            }
            finally
            {
                await (unreadableHandle?.DisposeAsync() ?? ValueTask.CompletedTask);
            }
        });
    }

    internal static async Task BuildDeliveryAsync()
    {
        await ForEachCaseAsync("aras-build-core-tree-delivery", async (caseId, input, expected) =>
        {
            await using var scope = FixtureScope.Create();
            var request = CreateClassificationRequest(scope, input.RootElement, out var inputIds, strictFileMaps: true);
            var outputState = input.RootElement.GetProperty("outputState");
            var attemptId = outputState.GetProperty("attemptId").GetString()!;
            request = request with { AttemptId = StableGuid(attemptId), OutputRoot = Path.Combine(scope.Root, "delivery", attemptId) };
            if (outputState.TryGetProperty("attemptExists", out var exists) && exists.GetBoolean())
            {
                Directory.CreateDirectory(request.OutputRoot);
                if (outputState.TryGetProperty("existingFiles", out var files))
                    MaterializeStrictTree(request.OutputRoot, files);
            }
            var inputBefore = SnapshotInputs(request);
            var outputBefore = SnapshotChecksums(request.OutputRoot);
            var declared = CreateDeclaredClassification(input.RootElement.GetProperty("classificationResult"), request);
            object actual;
            try
            {
                var leases = new DirectoryLeaseManager(Path.Combine(scope.Root, "tool-data"));
                var result = await CoreTreeComparisonBuilder.BuildFromClassificationAsync(request, declared, leases);
                var files = SnapshotChecksums(request.OutputRoot)
                    .Where(item => !item.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    .Select(item => new { relativePath = item.Key, checksum = item.Value }).ToArray();
                var manifests = Directory.EnumerateFiles(request.OutputRoot, "*-manifest.json", SearchOption.TopDirectoryOnly)
                    .Select(path => Path.GetFileName(path)!).OrderBy(name => name, StringComparer.Ordinal).ToArray();
                var messages = result.ManualReviews.Select(review => Message("ManualReview", review.Code, review.SourceRelativePath, new { })).ToArray();
                var resultProjection = DeliveryResult(caseId, attemptId, files, manifests, inputBefore, SnapshotInputs(request));
                var evidence = new FixtureEvidence(inputIds, result.ServerRuleVersion, result.ServerRuleChecksum);
                actual = Envelope("aras-build-core-tree-delivery", result.Status.ToString(),
                    resultProjection, messages, evidence);
            }
            catch (CoreTreeValidationException exception) when (exception.Code == "OutputAttemptAlreadyExists")
            {
                var outputAfter = SnapshotChecksums(request.OutputRoot);
                var evidence = new FixtureEvidence(inputIds, request.ServerTextRules.Version, request.ServerTextRules.Checksum);
                actual = Envelope("aras-build-core-tree-delivery", "Incomplete",
                    new { outputFiles = Array.Empty<object>(), manifestFiles = Array.Empty<string>(), writes = outputBefore.SequenceEqual(outputAfter) ? 0 : 1 },
                    new object[] { Message("Error", exception.Code, string.Empty, new { attemptId }) }, evidence);
            }
            using var actualJson = ToJson(actual);
            AssertJsonSemanticEqual(expected.RootElement, actualJson.RootElement);
        });
    }

    private static async Task ForEachCaseAsync(string skillName, Func<string, JsonDocument, JsonDocument, Task> run)
    {
        var cases = Directory.EnumerateDirectories(Path.Combine(ProjectPath(".agents", "skills", skillName), "assets", "acceptance-cases"))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (cases.Length == 0) throw new InvalidOperationException($"{skillName} has no fixtures.");
        foreach (var root in cases)
        {
            var caseId = Path.GetFileName(root);
            using var input = Load(skillName, caseId, "input.json");
            using var expected = Load(skillName, caseId, "expected", "result.json");
            try
            {
                await run(caseId, input, expected);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"{skillName}/{caseId}: {exception.Message}", exception);
            }
        }
    }

    private static JsonDocument Load(string skillName, string caseId, params string[] segments)
    {
        var path = Path.Combine(ProjectPath(".agents", "skills", skillName, "assets", "acceptance-cases", caseId), Path.Combine(segments));
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string MaterializeStrictTree(string workRoot, JsonElement fileMap)
    {
        Directory.CreateDirectory(workRoot);
        foreach (var property in fileMap.EnumerateObject())
            File.WriteAllBytes(SafePath(workRoot, property.Name), DecodeBytes(property.Value));
        return workRoot;
    }

    private static string MaterializeClassificationTree(string workRoot, JsonElement fileMap)
    {
        Directory.CreateDirectory(workRoot);
        foreach (var property in fileMap.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Classification fixture file requires a raw base64 string.");
            File.WriteAllBytes(SafePath(workRoot, property.Name), Convert.FromBase64String(property.Value.GetString()!));
        }
        return workRoot;
    }

    private static byte[] DecodeBytes(JsonElement encodedFile)
    {
        if (encodedFile.ValueKind != JsonValueKind.Object || !encodedFile.TryGetProperty("base64", out var base64) || base64.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Fixture file requires a base64 field.");
        return Convert.FromBase64String(base64.GetString()!);
    }

    private static void AssertJsonSemanticEqual(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
            throw new InvalidOperationException($"Expected JSON {expected.ValueKind}, got {actual.ValueKind}.");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedProperties = expected.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
                var actualProperties = actual.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
                if (!expectedProperties.Select(item => item.Name).SequenceEqual(actualProperties.Select(item => item.Name), StringComparer.Ordinal))
                    throw new InvalidOperationException("JSON object properties differ.");
                foreach (var property in expectedProperties)
                    AssertJsonSemanticEqual(property.Value, actual.GetProperty(property.Name));
                return;
            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToArray();
                var actualItems = actual.EnumerateArray().ToArray();
                if (expectedItems.Length != actualItems.Length) throw new InvalidOperationException("JSON array lengths differ.");
                for (var index = 0; index < expectedItems.Length; index++) AssertJsonSemanticEqual(expectedItems[index], actualItems[index]);
                return;
            case JsonValueKind.String:
                if (!string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal)) throw new InvalidOperationException("JSON strings differ.");
                return;
            case JsonValueKind.Number:
                if (decimal.Parse(expected.GetRawText(), System.Globalization.CultureInfo.InvariantCulture) != decimal.Parse(actual.GetRawText(), System.Globalization.CultureInfo.InvariantCulture))
                    throw new InvalidOperationException("JSON numbers differ.");
                return;
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (expected.GetBoolean() != actual.GetBoolean()) throw new InvalidOperationException("JSON booleans differ.");
                return;
            case JsonValueKind.Null:
                return;
            default:
                throw new InvalidOperationException("Unsupported JSON value.");
        }
    }

    private static IReadOnlyDictionary<string, string> SnapshotChecksums(string root)
    {
        if (!Directory.Exists(root)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new KeyValuePair<string, string>(NormalizeRelativePath(root, path), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static CoreTreeComparisonRequest CreateRequest(FixtureScope scope, JsonElement input, out IReadOnlyList<string> inputIds)
    {
        var customer = MaterializeInput(scope, input.GetProperty("customer"), "customer");
        var source = MaterializeInput(scope, input.GetProperty("sourceOotb"), "source");
        var target = MaterializeInput(scope, input.GetProperty("targetOotb"), "target");
        inputIds = [
            input.GetProperty("customer").GetProperty("rootId").GetString()!,
            input.GetProperty("sourceOotb").GetProperty("rootId").GetString()!,
            input.GetProperty("targetOotb").GetProperty("rootId").GetString()!];
        var output = input.GetProperty("outputRelation").GetString() == "inside-customer-input"
            ? Path.Combine(customer.RootPath, "Innovator", "output")
            : Path.Combine(scope.Root, "output");
        return new CoreTreeComparisonRequest(Guid.NewGuid(), input.GetProperty("sourceVersion").GetString()!, input.GetProperty("targetVersion").GetString()!,
            customer, source, target, output, RuntimeRules(input.GetProperty("serverRules")), DateTimeOffset.UtcNow);
    }

    private static CoreTreeComparisonRequest CreateClassificationRequest(FixtureScope scope, JsonElement input, out IReadOnlyList<string> inputIds, bool strictFileMaps = false)
    {
        var customerRoot = Path.Combine(scope.Root, "customer");
        var sourceRoot = Path.Combine(scope.Root, "source");
        var targetRoot = Path.Combine(scope.Root, "target");
        Func<string, JsonElement, string> materialize = strictFileMaps ? MaterializeStrictTree : MaterializeClassificationTree;
        materialize(Path.Combine(customerRoot, "Innovator"), input.GetProperty("customerFiles"));
        materialize(Path.Combine(sourceRoot, "Innovator"), input.GetProperty("sourceOotbFiles"));
        materialize(Path.Combine(targetRoot, "Innovator"), input.GetProperty("targetOotbFiles"));
        foreach (var root in new[] { customerRoot, sourceRoot, targetRoot })
        {
            Directory.CreateDirectory(Path.Combine(root, "Innovator", "Client"));
            Directory.CreateDirectory(Path.Combine(root, "Innovator", "Server"));
        }
        inputIds = input.GetProperty("evidence").GetProperty("inputIds").EnumerateArray().Select(item => item.GetString()!).ToArray();
        return new CoreTreeComparisonRequest(Guid.NewGuid(), "12SP9", "R38",
            new(customerRoot, "12SP9", "customer-evidence"), new(sourceRoot, "12SP9", "source-evidence"), new(targetRoot, "R38", "target-evidence"),
            Path.Combine(scope.Root, "output"), CoreTreeServerTextRuleSet.Create("server-text/1", ["Server/method-config.xml"]), DateTimeOffset.UtcNow);
    }

    private static CoreTreeInputEvidence MaterializeInput(FixtureScope scope, JsonElement input, string role)
    {
        var root = Path.Combine(scope.Root, role);
        if (input.GetProperty("hasClient").GetBoolean()) Directory.CreateDirectory(Path.Combine(root, "Innovator", "Client"));
        if (input.GetProperty("hasServer").GetBoolean()) Directory.CreateDirectory(Path.Combine(root, "Innovator", "Server"));
        return new(root, input.GetProperty("innovatorVersion").GetString()!, input.GetProperty("evidenceReference").GetString()!);
    }

    private static CoreTreeServerTextRuleSet RuntimeRules(JsonElement rules)
    {
        var runtime = CoreTreeServerTextRuleSet.Create(rules.GetProperty("version").GetString()!, rules.GetProperty("relativePaths").EnumerateArray().Select(item => item.GetString()!));
        return rules.TryGetProperty("checksumValid", out var checksumValid) && !checksumValid.GetBoolean()
            ? runtime with { Checksum = rules.GetProperty("checksum").GetString()! }
            : runtime;
    }

    private static object ValidationMessage(JsonElement input, string code)
    {
        return code switch
        {
            "VersionEvidenceMismatch" => Message("Error", code, "Innovator", new { input = "customer" }),
            "RequiredTreeStructureMissing" => Message("Error", code, "Server", new { input = "targetOotb" }),
            "InputOutputOverlap" => Message("Error", code, "Innovator", new { input = "customer" }),
            "RuleChecksumMismatch" => Message("Error", code, input.GetProperty("serverRules").GetProperty("relativePaths")[0].GetString()!, new { }),
            _ => throw new InvalidOperationException($"Unexpected validation code {code}.")
        };
    }

    private static object Envelope(string capability, string status, object result, object messages, FixtureEvidence evidence) => new
    {
        contractVersion = ContractVersion,
        capability,
        status,
        result,
        messages,
        evidence = new { inputIds = evidence.InputIds, ruleVersion = evidence.RuleVersion, ruleChecksum = evidence.RuleChecksum }
    };

    private static object Message(string kind, string code, string relativePath, object details) => new { kind, code, relativePath, details };

    private static IReadOnlyDictionary<string, object> DeliveryResult(
        string caseId,
        string attemptId,
        IEnumerable<object> files,
        string[] manifests,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> inputBefore,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> inputAfter)
    {
        var result = new Dictionary<string, object>
        {
            ["outputFiles"] = files,
            ["manifestFiles"] = manifests
        };
        if (caseId == "new-attempt") result["attemptId"] = attemptId;
        if (caseId == "input-immutable") result["inputChecksumsUnchanged"] = SnapshotsEqual(inputBefore, inputAfter);
        if (caseId == "c-target-extension") result["inputBytesUnchanged"] = SnapshotsEqual(inputBefore, inputAfter);
        return result;
    }

    private static CoreTreeComparisonResult CreateDeclaredClassification(JsonElement declared, CoreTreeComparisonRequest request)
    {
        var items = declared.GetProperty("items").EnumerateArray().Select(item => new CoreTreeClassifiedItem(
            Enum.Parse<CoreTreeClassification>(item.GetProperty("classification").GetString()!, ignoreCase: false),
            item.GetProperty("sourceRelativePath").GetString()!,
            item.TryGetProperty("targetRelativePath", out var target) && target.ValueKind != JsonValueKind.Null ? target.GetString() : null)).ToArray();
        var reviews = declared.GetProperty("manualReviews").EnumerateArray().Select(review => new CoreTreeManualReview(
            review.GetProperty("relativePath").GetString()!, review.GetProperty("code").GetString()!, null, [], "Declared fixture review.")).ToArray();
        var errors = declared.GetProperty("errors").EnumerateArray().Select(error => new CoreTreeComparisonError(
            error.GetProperty("relativePath").GetString()!, error.GetProperty("code").GetString()!, "Declared fixture error.")).ToArray();
        return new(request.AttemptId, Enum.Parse<CoreTreeComparisonStatus>(declared.GetProperty("status").GetString()!, ignoreCase: false),
            items, reviews, errors, [], request.OutputRoot, request.ServerTextRules.Version, request.ServerTextRules.Checksum, request.StartedAt, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SnapshotInputs(CoreTreeComparisonRequest request) =>
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["customer"] = SnapshotChecksums(Path.Combine(request.Customer.RootPath, "Innovator")),
            ["sourceOotb"] = SnapshotChecksums(Path.Combine(request.SourceOotb.RootPath, "Innovator")),
            ["targetOotb"] = SnapshotChecksums(Path.Combine(request.TargetOotb.RootPath, "Innovator"))
        };

    private static bool SnapshotsEqual(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> before, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> after) =>
        before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var later) && pair.Value.SequenceEqual(later));

    private static JsonDocument ToJson(object value) => JsonDocument.Parse(JsonSerializer.Serialize(value));
    private static FixtureEvidence Evidence(JsonElement element, IReadOnlyList<string>? inputIds = null)
    {
        var ruleVersion = element.TryGetProperty("version", out var version)
            ? version.GetString()!
            : element.GetProperty("ruleVersion").GetString()!;
        var ruleChecksum = element.TryGetProperty("checksum", out var checksum)
            ? checksum.GetString()!
            : element.GetProperty("ruleChecksum").GetString()!;
        return new(inputIds ?? element.GetProperty("inputIds").EnumerateArray().Select(item => item.GetString()!).ToArray(), ruleVersion, ruleChecksum);
    }
    private static string SafePath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Replace('\\', '/').Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Fixture path must be a normalized relative path.");
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Fixture path escapes materialization root.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return full;
    }
    private static async Task WriteFileAsync(string root, string relativePath, byte[] bytes) => await File.WriteAllBytesAsync(SafePath(root, relativePath), bytes);
    private static string NormalizeRelativePath(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static Guid StableGuid(string input) => new(SHA256.HashData(Encoding.UTF8.GetBytes(input)).AsSpan()[..16]);
    private static string ProjectPath(params string[] segments)
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ArasUpgradeOrchestrator.sln"))) current = current.Parent;
        if (current is null) throw new DirectoryNotFoundException("Project root not found.");
        return segments.Aggregate(current.FullName, Path.Combine);
    }

    private sealed record FixtureEvidence(IReadOnlyList<string> InputIds, string RuleVersion, string RuleChecksum);

    private sealed class FixtureScope : IAsyncDisposable
    {
        private FixtureScope(string root) => Root = root;
        internal string Root { get; }
        internal static FixtureScope Create()
        {
            var root = Path.Combine(Environment.CurrentDirectory, ".test-output", "core-tree-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new(root);
        }
        public ValueTask DisposeAsync()
        {
            var boundary = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".test-output", "core-tree-fixtures"));
            var resolved = Path.GetFullPath(Root);
            if (resolved.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved)) Directory.Delete(resolved, true);
            return ValueTask.CompletedTask;
        }
    }
}
