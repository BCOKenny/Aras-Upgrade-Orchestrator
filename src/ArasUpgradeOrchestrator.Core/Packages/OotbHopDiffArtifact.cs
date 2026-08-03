using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Rules;

namespace ArasUpgradeOrchestrator.Core.Packages;

public enum OotbHopDiffArtifactState
{
    Incomplete,
    Completed
}

public sealed record OotbHopDiffCompletionManifest(
    OotbHopDiffArtifactState State,
    Guid AttemptId,
    string SourceVersion,
    string TargetVersion,
    IReadOnlyList<RuleSetVersionReference> RuleSets,
    string EffectiveRuleChecksum,
    DateTimeOffset CompletedAt,
    OotbHopDiffSummary Summary,
    bool ManualReviewsResolved);

public sealed record OotbHopDiffArtifact(
    OotbHopDiffArtifactState State,
    OotbHopDiffCompletionManifest? Manifest,
    string? ArchivePath,
    string? ArchiveChecksum,
    string ProcessingSummaryPath);

public sealed record OotbHopDiffReuseRequirement(
    string SourceVersion,
    string TargetVersion,
    IReadOnlyList<RuleSetVersionReference> RuleSets,
    string EffectiveRuleChecksum,
    string ArchiveChecksum);

public sealed record OotbHopDiffVerificationResult(bool IsReusable, IReadOnlyList<string> Issues);

public static class OotbHopDiffPackager
{
    private const string ManifestName = "completion-manifest.json";
    private const string SummaryName = "processing-summary.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<OotbHopDiffArtifact> PackageAsync(
        OotbHopDiffBuildResult build,
        string archivePath,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        var summaryPath = Path.Combine(build.OutputRoot, SummaryName);
        if (build.Status != OotbHopDiffBuildStatus.ReadyToPackage || build.Errors.Count > 0 || build.ManualReviews.Count > 0)
        {
            await WriteNewJsonAsync(summaryPath, build, cancellationToken);
            return new OotbHopDiffArtifact(OotbHopDiffArtifactState.Incomplete, null, null, null, summaryPath);
        }
        var fullArchivePath = Path.GetFullPath(archivePath);
        if (File.Exists(fullArchivePath) || Directory.Exists(fullArchivePath))
            throw new InvalidOperationException("封裝輸出必須是不存在的新檔案。 ");
        if (IsUnder(fullArchivePath, build.OutputRoot))
            throw new InvalidOperationException("ZIP 封裝不可放在待封裝的差異輸出目錄內。 ");

        await WriteNewJsonAsync(summaryPath, build, cancellationToken);
        var manifest = new OotbHopDiffCompletionManifest(
            OotbHopDiffArtifactState.Completed,
            build.AttemptId,
            build.SourceVersion,
            build.TargetVersion,
            build.RuleSets,
            build.EffectiveRuleChecksum,
            completedAt,
            build.Summary,
            true);
        await WriteNewJsonAsync(Path.Combine(build.OutputRoot, ManifestName), manifest, cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(fullArchivePath)!);
        await using (var archiveStream = new FileStream(fullArchivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var directoryName in new[] { "SourceDiff/", "TargetDiff/" })
            {
                var directoryEntry = archive.CreateEntry(directoryName);
                directoryEntry.LastWriteTime = completedAt;
            }
            foreach (var path in Directory.EnumerateFiles(build.OutputRoot, "*", SearchOption.AllDirectories)
                         .OrderBy(path => Path.GetRelativePath(build.OutputRoot, path), StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(build.OutputRoot, path).Replace(Path.DirectorySeparatorChar, '/');
                var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                entry.LastWriteTime = completedAt;
                await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var output = entry.Open();
                await input.CopyToAsync(output, cancellationToken);
            }
        }
        var checksum = await ComputeChecksumAsync(fullArchivePath, cancellationToken);
        return new OotbHopDiffArtifact(OotbHopDiffArtifactState.Completed, manifest, fullArchivePath, checksum, summaryPath);
    }

    internal static async Task<string> ComputeChecksumAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static async Task WriteNewJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    internal static JsonSerializerOptions SerializerOptions => JsonOptions;
    internal static string CompletionManifestName => ManifestName;
    internal static string ProcessingSummaryName => SummaryName;
}

public static class OotbHopDiffArtifactVerifier
{
    public static async Task<OotbHopDiffVerificationResult> VerifyAsync(
        string archivePath,
        OotbHopDiffReuseRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        var issues = new List<string>();
        var fullPath = Path.GetFullPath(archivePath);
        if (!File.Exists(fullPath)) return new OotbHopDiffVerificationResult(false, ["找不到 OOTB 跳點差異包封裝檔。"]);
        try
        {
            var checksum = await OotbHopDiffPackager.ComputeChecksumAsync(fullPath, cancellationToken);
            if (!string.Equals(checksum, requirement.ArchiveChecksum, StringComparison.OrdinalIgnoreCase))
                issues.Add("封裝 Checksum 不相符。");
            using var archive = ZipFile.OpenRead(fullPath);
            if (archive.Entries.Any(entry => !IsSafeEntryName(entry.FullName)))
                issues.Add("封裝含非安全相對路徑項目。");
            if (archive.Entries.GroupBy(entry => entry.FullName, StringComparer.Ordinal).Any(group => group.Count() > 1))
                issues.Add("封裝含重複項目路徑。");
            if (!archive.Entries.Any(entry => entry.FullName == "SourceDiff/" || entry.FullName.StartsWith("SourceDiff/", StringComparison.Ordinal) && !string.IsNullOrEmpty(entry.Name)))
                issues.Add("封裝缺少 SourceDiff 內容邊界。");
            if (!archive.Entries.Any(entry => entry.FullName == "TargetDiff/" || entry.FullName.StartsWith("TargetDiff/", StringComparison.Ordinal) && !string.IsNullOrEmpty(entry.Name)))
                issues.Add("封裝缺少 TargetDiff 內容邊界。");
            if (archive.Entries.Count(entry => entry.FullName == OotbHopDiffPackager.ProcessingSummaryName) != 1)
                issues.Add("封裝必須包含唯一 processing-summary.json。");
            var entries = archive.Entries.Where(entry => entry.FullName == OotbHopDiffPackager.CompletionManifestName).ToArray();
            if (entries.Length != 1)
            {
                issues.Add("封裝必須包含唯一 completion-manifest.json。");
                return new OotbHopDiffVerificationResult(false, issues);
            }
            await using var stream = entries[0].Open();
            var manifest = await JsonSerializer.DeserializeAsync<OotbHopDiffCompletionManifest>(stream,
                OotbHopDiffPackager.SerializerOptions, cancellationToken);
            if (manifest is null) issues.Add("完成標記內容無效。");
            else
            {
                if (manifest.State != OotbHopDiffArtifactState.Completed) issues.Add("完成標記不是 Completed。");
                if (!string.Equals(manifest.SourceVersion, requirement.SourceVersion, StringComparison.OrdinalIgnoreCase)) issues.Add("來源版本不相符。");
                if (!string.Equals(manifest.TargetVersion, requirement.TargetVersion, StringComparison.OrdinalIgnoreCase)) issues.Add("目標版本不相符。");
                if (!SameRuleSets(manifest.RuleSets, requirement.RuleSets) ||
                    !string.Equals(manifest.EffectiveRuleChecksum, requirement.EffectiveRuleChecksum, StringComparison.OrdinalIgnoreCase))
                    issues.Add("Rule 1 規則版本或 Checksum 不相符。");
                if (!manifest.ManualReviewsResolved || manifest.Summary.ManualReviewCount > 0 || manifest.Summary.ErrorCount > 0)
                    issues.Add("封裝仍包含錯誤或未解除人工確認。");
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            issues.Add("封裝無法安全驗證：" + exception.Message);
        }
        return new OotbHopDiffVerificationResult(issues.Count == 0, issues);
    }

    private static bool SameRuleSets(
        IReadOnlyList<RuleSetVersionReference> left,
        IReadOnlyList<RuleSetVersionReference> right) =>
        left.OrderBy(item => item.RuleSetId).ThenBy(item => item.Version)
            .SequenceEqual(right.OrderBy(item => item.RuleSetId).ThenBy(item => item.Version));

    private static bool IsSafeEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || name.StartsWith('/') || Path.IsPathRooted(name) || name.Contains(':'))
            return false;
        return !name.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..");
    }
}
