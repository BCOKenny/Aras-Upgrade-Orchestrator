using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public enum CoreTreePreflightStatus
{
    Ready,
    Incomplete,
    Blocked
}

public sealed record CoreTreeComparisonPreflightRequest(
    string CaseRoot,
    CoreTreeInputEvidence Customer,
    CoreTreeInputEvidence SourceOotb,
    CoreTreeInputEvidence TargetOotb,
    string OutputRoot,
    CoreTreeServerTextRuleSet ServerTextRules);

public sealed record CoreTreePreflightIssue(string Code, string Message);

public sealed record CoreTreePreflightInput(
    bool ClientDirectoryExists,
    bool ServerDirectoryExists,
    int ClientFileCount,
    int ServerFileCount)
{
    public int FileCount => ClientFileCount + ServerFileCount;
}

public sealed record CoreTreeComparisonPreflightResult(
    Guid CaseId,
    Guid AttemptId,
    CoreTreePreflightStatus Status,
    IReadOnlyList<CoreTreePreflightIssue> Issues,
    CoreTreePreflightInput Customer,
    CoreTreePreflightInput SourceOotb,
    CoreTreePreflightInput TargetOotb,
    string ExpectedLeasePath,
    string ExpectedAttemptPath,
    HistoryEntry? LastHistoryEvent);

public sealed class CoreTreeComparisonPreflightCommand
{
    private static readonly JsonSerializerOptions HistoryJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CoreTreeComparisonPreflightResult> ExecuteAsync(
        CoreTreeComparisonPreflightRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<CoreTreePreflightIssue>();
        CaseStore caseStore;
        try
        {
            caseStore = new CaseStore(request.CaseRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Blocked(Guid.Empty, issues, string.Empty, string.Empty, exception.Message);
        }

        var expectedLeasePath = Path.Combine(caseStore.ToolDataPath, "core-tree-command");
        var expectedAttemptPath = GetFullPathOrEmpty(request.OutputRoot, issues, "output.path.invalid");
        CaseManifest manifest;
        try
        {
            manifest = await caseStore.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            return Blocked(Guid.Empty, issues, expectedLeasePath, expectedAttemptPath, exception.Message);
        }

        var customer = InspectInput(request.Customer, issues, "customer");
        var sourceOotb = InspectInput(request.SourceOotb, issues, "source-ootb");
        var targetOotb = InspectInput(request.TargetOotb, issues, "target-ootb");

        ValidateVersions(manifest, request, issues);
        ValidateInputRoots(request, issues);
        ValidateOutputCandidate(request, expectedAttemptPath, issues);
        ValidateServerTextRules(request.ServerTextRules, issues);

        HistoryEntry? lastHistoryEvent = null;
        var historyPath = Path.Combine(caseStore.ToolDataPath, AppendOnlyHistoryStore.FileName);
        try
        {
            lastHistoryEvent = await ReadLastHistoryEventAsync(historyPath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            issues.Add(new CoreTreePreflightIssue("history.unreadable", exception.Message));
        }

        var status = issues.Count == 0
            ? CoreTreePreflightStatus.Ready
            : issues.All(IsEvidenceQualityIssue)
                ? CoreTreePreflightStatus.Incomplete
                : CoreTreePreflightStatus.Blocked;

        return new CoreTreeComparisonPreflightResult(
            manifest.CaseId,
            Guid.Empty,
            status,
            issues,
            customer,
            sourceOotb,
            targetOotb,
            expectedLeasePath,
            expectedAttemptPath,
            lastHistoryEvent);
    }

    private static CoreTreeComparisonPreflightResult Blocked(
        Guid caseId,
        List<CoreTreePreflightIssue> issues,
        string expectedLeasePath,
        string expectedAttemptPath,
        string message)
    {
        issues.Add(new CoreTreePreflightIssue("case.unreadable", message));
        return new CoreTreeComparisonPreflightResult(
            caseId,
            Guid.Empty,
            CoreTreePreflightStatus.Blocked,
            issues,
            EmptyInput,
            EmptyInput,
            EmptyInput,
            expectedLeasePath,
            expectedAttemptPath,
            null);
    }

    private static readonly CoreTreePreflightInput EmptyInput = new(false, false, 0, 0);

    private static CoreTreePreflightInput InspectInput(
        CoreTreeInputEvidence input,
        List<CoreTreePreflightIssue> issues,
        string role)
    {
        if (input is null)
        {
            issues.Add(new CoreTreePreflightIssue($"input.{role}.missing", "Core Tree input is required."));
            return EmptyInput;
        }
        if (string.IsNullOrWhiteSpace(input.EvidenceReference))
            issues.Add(new CoreTreePreflightIssue($"input.{role}.evidence.missing", "Core Tree evidence reference is required."));
        else
            InspectEvidence(input.EvidenceReference, issues, role);

        var root = GetFullPathOrEmpty(input.RootPath, issues, $"input.{role}.path.invalid");
        if (string.IsNullOrEmpty(root)) return EmptyInput;

        var clientPath = Path.Combine(root, "Innovator", "Client");
        var serverPath = Path.Combine(root, "Innovator", "Server");
        var clientExists = Directory.Exists(clientPath);
        var serverExists = Directory.Exists(serverPath);
        if (!clientExists) issues.Add(new CoreTreePreflightIssue($"input.{role}.client.missing", $"Missing {clientPath}."));
        if (!serverExists) issues.Add(new CoreTreePreflightIssue($"input.{role}.server.missing", $"Missing {serverPath}."));

        return new CoreTreePreflightInput(
            clientExists,
            serverExists,
            CountFiles(clientPath, clientExists, issues, role, "client"),
            CountFiles(serverPath, serverExists, issues, role, "server"));
    }

    private static void InspectEvidence(string evidenceReference, List<CoreTreePreflightIssue> issues, string role)
    {
        var evidencePath = GetFullPathOrEmpty(evidenceReference, issues, $"input.{role}.evidence.path.invalid");
        if (string.IsNullOrEmpty(evidencePath)) return;

        if (!Directory.Exists(evidencePath))
        {
            issues.Add(new CoreTreePreflightIssue($"input.{role}.evidence.directory.missing", $"Missing evidence directory {evidencePath}."));
            return;
        }

        try
        {
            var files = Directory.EnumerateFiles(evidencePath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetFileName(path) ?? string.Empty)
                .ToArray();
            var hasVersionPrimary = files.Any(file => file.StartsWith("version-primary.", StringComparison.OrdinalIgnoreCase));
            var hasIntegrityRecord = files.Any(file => file.StartsWith("integrity.", StringComparison.OrdinalIgnoreCase));
            if (!hasVersionPrimary || !hasIntegrityRecord)
                issues.Add(new CoreTreePreflightIssue($"input.{role}.evidence.incomplete", "Evidence requires both a version-primary record and an integrity record."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new CoreTreePreflightIssue($"input.{role}.evidence.unreadable", exception.Message));
        }
    }

    private static bool IsEvidenceQualityIssue(CoreTreePreflightIssue issue) =>
        issue.Code.EndsWith(".evidence.incomplete", StringComparison.Ordinal);

    private static int CountFiles(
        string directory,
        bool exists,
        List<CoreTreePreflightIssue> issues,
        string role,
        string side)
    {
        if (!exists) return 0;
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new CoreTreePreflightIssue($"input.{role}.{side}.unreadable", exception.Message));
            return 0;
        }
    }

    private static void ValidateVersions(CaseManifest manifest, CoreTreeComparisonPreflightRequest request, List<CoreTreePreflightIssue> issues)
    {
        if (!string.Equals(manifest.SourceVersion, request.Customer?.InnovatorVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifest.SourceVersion, request.SourceOotb?.InnovatorVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifest.TargetVersion, request.TargetOotb?.InnovatorVersion, StringComparison.OrdinalIgnoreCase))
            issues.Add(new CoreTreePreflightIssue("input.version.mismatch", "Case and Core Tree version evidence do not match."));
    }

    private static void ValidateInputRoots(CoreTreeComparisonPreflightRequest request, List<CoreTreePreflightIssue> issues)
    {
        var roots = new[] { request.Customer, request.SourceOotb, request.TargetOotb }
            .Where(input => input is not null)
            .Select(input => GetFullPathOrEmpty(input.RootPath, issues, "input.path.invalid"))
            .Where(path => !string.IsNullOrEmpty(path))
            .ToArray();

        for (var left = 0; left < roots.Length; left++)
            for (var right = left + 1; right < roots.Length; right++)
                if (Overlaps(roots[left], roots[right]))
                    issues.Add(new CoreTreePreflightIssue("input.roots.overlap", "Core Tree input roots must not overlap."));
    }

    private static void ValidateOutputCandidate(CoreTreeComparisonPreflightRequest request, string outputPath, List<CoreTreePreflightIssue> issues)
    {
        if (string.IsNullOrEmpty(outputPath)) return;
        if (Directory.Exists(outputPath) || File.Exists(outputPath))
            issues.Add(new CoreTreePreflightIssue("output.exists", "Core Tree output candidate must not already exist."));

        foreach (var input in new[] { request.Customer, request.SourceOotb, request.TargetOotb })
        {
            if (input is null) continue;
            var root = GetFullPathOrEmpty(input.RootPath, issues, "input.path.invalid");
            if (!string.IsNullOrEmpty(root) && Overlaps(root, outputPath))
                issues.Add(new CoreTreePreflightIssue("input.output.overlap", "Core Tree output candidate must not overlap an input root."));
        }
    }

    private static void ValidateServerTextRules(CoreTreeServerTextRuleSet? rules, List<CoreTreePreflightIssue> issues)
    {
        if (rules is null || string.IsNullOrWhiteSpace(rules.Version) || string.IsNullOrWhiteSpace(rules.Checksum))
        {
            issues.Add(new CoreTreePreflightIssue("server-rules.invalid", "Server text rules require a version and checksum."));
            return;
        }

        var expectedChecksum = CoreTreeServerTextRuleSet.CalculateChecksum(rules.Version, rules.RelativePaths);
        if (!string.Equals(expectedChecksum, rules.Checksum, StringComparison.OrdinalIgnoreCase))
            issues.Add(new CoreTreePreflightIssue("server-rules.checksum.invalid", "Server text rule checksum does not match the rule paths."));

        var normalizedPaths = rules.RelativePaths.Select(CoreTreeContentComparer.NormalizeRelativePath).ToArray();
        if (normalizedPaths.Length != normalizedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
            normalizedPaths.Any(path => !path.StartsWith("Server/", StringComparison.OrdinalIgnoreCase) || path.Contains("../", StringComparison.Ordinal)))
            issues.Add(new CoreTreePreflightIssue("server-rules.invalid", "Server text rules must be unique, remain under Server, and not traverse parent directories."));
    }

    private static string GetFullPathOrEmpty(string? path, List<CoreTreePreflightIssue> issues, string issueCode)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            issues.Add(new CoreTreePreflightIssue(issueCode, "Path is required."));
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(new CoreTreePreflightIssue(issueCode, exception.Message));
            return string.Empty;
        }
    }

    private static bool Overlaps(string left, string right) =>
        IsSameOrDescendant(left, right) || IsSameOrDescendant(right, left);

    private static bool IsSameOrDescendant(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static async Task<HistoryEntry?> ReadLastHistoryEventAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;

        HistoryEntry? last = null;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            last = JsonSerializer.Deserialize<HistoryEntry>(line, HistoryJsonOptions)
                ?? throw new InvalidDataException("History entry is invalid.");
        }
        return last;
    }
}
