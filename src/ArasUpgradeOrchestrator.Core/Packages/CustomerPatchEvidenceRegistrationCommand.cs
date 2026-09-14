using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;

namespace ArasUpgradeOrchestrator.Core.Packages;

public sealed record CustomerPatchEvidence(
    string PreparationId,
    string ComparisonId,
    CustomerPatchComparisonSource Source,
    CustomerPatchComparisonTarget Target,
    string PatchSupportSource,
    string PatchSupportInputChecksum,
    string VerifiedBy,
    DateTimeOffset VerifiedAt);

public sealed record CustomerPatchEvidenceRegistrationRequest(
    string CaseRoot,
    string PreparationId,
    string ComparisonId,
    CustomerPatchComparisonSource Source,
    CustomerPatchComparisonTarget Target,
    string EvidenceRelativePath,
    string PatchSupportSource,
    string Actor);

public sealed record CustomerPatchEvidenceRegistrationResult(
    Guid CaseId,
    string EvidencePath,
    CustomerPatchEvidence Evidence,
    string HistoryPath);

public sealed record CustomerPatchComparisonSource(string BaselineId, string ActualVersion, string InputRelativePath);
public sealed record CustomerPatchComparisonTarget(string TargetRelease, string InputRelativePath);

public sealed class CustomerPatchEvidenceRegistrationCommand
{
    private readonly Func<DateTimeOffset> _clock;

    public CustomerPatchEvidenceRegistrationCommand(Func<DateTimeOffset>? clock = null) =>
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public async Task<CustomerPatchEvidenceRegistrationResult> ExecuteAsync(
        CustomerPatchEvidenceRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Target);
        if (new[] { request.PreparationId, request.ComparisonId, request.Source.BaselineId, request.Source.ActualVersion, request.Source.InputRelativePath, request.Target.TargetRelease, request.Target.InputRelativePath, request.PatchSupportSource, request.Actor }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Customer-Patch 證據必須包含比較識別、source、target、來源說明與具名操作者。", nameof(request));
        if (!string.Equals(request.PatchSupportSource, request.Target.InputRelativePath, StringComparison.Ordinal))
            throw new ArgumentException("Customer-Patch 證據來源必須等於案件內 Patch／Support 輸入相對路徑。", nameof(request));

        var store = new CaseStore(request.CaseRoot);
        var manifest = await store.LoadAsync(cancellationToken);
        var patchRoot = ResolveCasePath(store.CaseRoot, request.Target.InputRelativePath);
        var evidenceDirectory = ResolveCasePath(store.CaseRoot, request.EvidenceRelativePath);
        var evidencePath = Path.Combine(evidenceDirectory, "patch-support-evidence.json");
        if (!Directory.Exists(patchRoot)) throw new DirectoryNotFoundException("找不到 Patch／Support 輸入目錄。");
        if (File.Exists(evidencePath) || Directory.Exists(evidencePath)) throw new InvalidOperationException("Patch／Support 證據已存在，不可覆寫。");
        if (!Directory.Exists(evidenceDirectory)) throw new DirectoryNotFoundException("Patch／Support 證據目錄不存在。");

        var digest = await PackageInputTreeDigest.ComputeAsync(patchRoot, cancellationToken);
        var evidence = new CustomerPatchEvidence(request.PreparationId, request.ComparisonId, request.Source, request.Target,
            request.PatchSupportSource, digest.TreeChecksum, request.Actor, _clock());
        await using (var stream = new FileStream(evidencePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, evidence, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }, cancellationToken);

        var history = new AppendOnlyHistoryStore(store.ToolDataPath);
        await history.AppendAsync(manifest.CaseId, HistoryEventTypes.CustomerPatchEvidenceRecorded, request.ComparisonId, request.Actor,
            new { evidence.PreparationId, evidence.Source, evidence.Target, evidence.PatchSupportInputChecksum, EvidenceRelativePath = request.EvidenceRelativePath },
            evidence.VerifiedAt, cancellationToken: cancellationToken);
        return new CustomerPatchEvidenceRegistrationResult(manifest.CaseId, evidencePath, evidence, history.Path);
    }

    private static string ResolveCasePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment == ".."))
            throw new InvalidDataException("Customer-Patch 證據路徑無效。");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Customer-Patch 證據路徑逸出案件根目錄。");
        return path;
    }
}
