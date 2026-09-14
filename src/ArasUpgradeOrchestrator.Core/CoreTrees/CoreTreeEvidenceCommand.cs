using System.Security.Cryptography;
using System.Text;

namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public enum CoreTreeEvidenceSetStatus
{
    Missing,
    CompleteCompatible,
    Partial,
    StaleOrConflict,
    Blocked
}

public enum CoreTreeEvidenceOperationStatus
{
    Completed,
    Blocked,
    Failed
}

public sealed record CoreTreeEvidenceInput(
    string InputId,
    string InnovatorVersion,
    string TreePath,
    string EvidencePath);

public sealed record CoreTreeEvidenceSetResult(
    string InputId,
    CoreTreeEvidenceSetStatus Status,
    int TreeFileCount,
    int IntegrityEntryCount,
    IReadOnlyList<string> ExistingFiles,
    string? StagingPath,
    string? Message);

public sealed record CoreTreeEvidenceWriteResult(
    CoreTreeEvidenceOperationStatus OperationStatus,
    CoreTreeEvidenceSetResult Evidence,
    IReadOnlyList<string> CreatedFiles);

public sealed class CoreTreeEvidenceCommand
{
    private static readonly string[] RequiredFiles = [
        "version-primary.md", "integrity.md", "integrity.sha256", "source-provenance.md"
    ];

    public async Task<CoreTreeEvidenceSetResult> PreviewAsync(
        CoreTreeEvidenceInput input,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));

        var treePath = Path.GetFullPath(input.TreePath);
        var evidencePath = Path.GetFullPath(input.EvidencePath);
        if (!Directory.Exists(treePath))
            return Result(input, CoreTreeEvidenceSetStatus.Blocked, 0, 0, [], null, "Tree directory is missing.");
        if (!Directory.Exists(Path.Combine(treePath, "Innovator", "Client")) ||
            !Directory.Exists(Path.Combine(treePath, "Innovator", "Server")))
            return Result(input, CoreTreeEvidenceSetStatus.Blocked, 0, 0, [], null, "Tree is missing Innovator Client or Server.");
        if (!Directory.Exists(evidencePath))
            return Result(input, CoreTreeEvidenceSetStatus.Blocked, 0, 0, [], null, "Evidence directory is missing.");

        var existing = RequiredFiles.Where(name => File.Exists(Path.Combine(evidencePath, name))).ToArray();
        var treeFiles = Directory.EnumerateFiles(treePath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (existing.Length == 0)
            return Result(input, CoreTreeEvidenceSetStatus.Missing, treeFiles.Length, 0, existing, null, null);
        if (existing.Length != RequiredFiles.Length)
            return Result(input, CoreTreeEvidenceSetStatus.Partial, treeFiles.Length, 0, existing, null, "Only part of the Evidence Set exists.");

        var integrityPath = Path.Combine(evidencePath, "integrity.sha256");
        var entries = await File.ReadAllLinesAsync(integrityPath, cancellationToken);
        var actual = await BuildIntegrityLinesAsync(treePath, cancellationToken);
        var version = await File.ReadAllTextAsync(Path.Combine(evidencePath, "version-primary.md"), cancellationToken);
        var compatible = entries.SequenceEqual(actual, StringComparer.Ordinal) &&
            (string.IsNullOrWhiteSpace(input.InputId) || version.Contains("Input ID: " + input.InputId, StringComparison.Ordinal)) &&
            version.Contains("Product version: " + input.InnovatorVersion, StringComparison.Ordinal);
        return Result(input, compatible ? CoreTreeEvidenceSetStatus.CompleteCompatible : CoreTreeEvidenceSetStatus.StaleOrConflict,
            treeFiles.Length, entries.Length, existing, null, compatible ? null : "Evidence does not match the current Tree or input metadata.");
    }

    public async Task<CoreTreeEvidenceWriteResult> WriteAsync(
        CoreTreeEvidenceInput input,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(input, actor, cancellationToken);
        if (preview.Status == CoreTreeEvidenceSetStatus.CompleteCompatible)
            return new CoreTreeEvidenceWriteResult(CoreTreeEvidenceOperationStatus.Completed, preview, []);
        if (preview.Status != CoreTreeEvidenceSetStatus.Missing)
            return new CoreTreeEvidenceWriteResult(CoreTreeEvidenceOperationStatus.Blocked, preview, []);

        var evidencePath = Path.GetFullPath(input.EvidencePath);
        var stagingPath = Path.Combine(Path.GetDirectoryName(evidencePath)!, ".evidence-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingPath);
        try
        {
            var lines = await BuildIntegrityLinesAsync(Path.GetFullPath(input.TreePath), cancellationToken);
            var encoding = new UTF8Encoding(true);
            await File.WriteAllTextAsync(Path.Combine(stagingPath, "version-primary.md"),
                $"# Version Evidence{Environment.NewLine}{Environment.NewLine}- Input ID: {input.InputId}{Environment.NewLine}- Product version: {input.InnovatorVersion}{Environment.NewLine}- Build: <待提供>{Environment.NewLine}- Collector: {actor}{Environment.NewLine}", encoding, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(stagingPath, "integrity.md"),
                $"# Integrity Evidence{Environment.NewLine}{Environment.NewLine}- Input ID: {input.InputId}{Environment.NewLine}- Tree root: {Path.GetFullPath(input.TreePath)}{Environment.NewLine}- File count: {lines.Count}{Environment.NewLine}- SHA-256 list: integrity.sha256{Environment.NewLine}", encoding, cancellationToken);
            await File.WriteAllLinesAsync(Path.Combine(stagingPath, "integrity.sha256"), lines, encoding, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(stagingPath, "source-provenance.md"),
                $"# Source Provenance{Environment.NewLine}{Environment.NewLine}- Input ID: {input.InputId}{Environment.NewLine}- Destination: {Path.GetFullPath(input.TreePath)}{Environment.NewLine}- Collector: {actor}{Environment.NewLine}- Source location: <待提供>{Environment.NewLine}", encoding, cancellationToken);

            if (!(await BuildIntegrityLinesAsync(Path.GetFullPath(input.TreePath), cancellationToken)).SequenceEqual(lines, StringComparer.Ordinal))
                throw new InvalidOperationException("Tree changed while Evidence was being generated.");
            foreach (var name in RequiredFiles)
            {
                var destination = Path.Combine(evidencePath, name);
                if (File.Exists(destination)) throw new IOException($"Evidence destination already exists: {destination}");
                File.Move(Path.Combine(stagingPath, name), destination);
            }
            Directory.Delete(stagingPath, false);
            var completed = await PreviewAsync(input, actor, cancellationToken);
            return new CoreTreeEvidenceWriteResult(CoreTreeEvidenceOperationStatus.Completed, completed,
                RequiredFiles.Select(name => Path.Combine(evidencePath, name)).ToArray());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var failed = preview with { Status = CoreTreeEvidenceSetStatus.Blocked, StagingPath = stagingPath, Message = exception.Message };
            return new CoreTreeEvidenceWriteResult(CoreTreeEvidenceOperationStatus.Failed, failed, []);
        }
    }

    private static CoreTreeEvidenceSetResult Result(CoreTreeEvidenceInput input, CoreTreeEvidenceSetStatus status, int files, int entries,
        IReadOnlyList<string> existing, string? staging, string? message) =>
        new(input.InputId, status, files, entries, existing, staging, message);

    private static async Task<IReadOnlyList<string>> BuildIntegrityLinesAsync(string treePath, CancellationToken cancellationToken)
    {
        var prefix = Path.TrimEndingDirectorySeparator(treePath) + Path.DirectorySeparatorChar;
        var rows = new List<string>();
        foreach (var path in Directory.EnumerateFiles(treePath, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Tree file is outside its root.");
            await using var stream = File.OpenRead(fullPath);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            rows.Add(hash + "  " + fullPath[prefix.Length..].Replace(Path.DirectorySeparatorChar, '/'));
        }
        return rows;
    }
}
