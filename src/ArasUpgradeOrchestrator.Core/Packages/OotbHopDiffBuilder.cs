using System.Text;
using System.Xml.Linq;
using ArasUpgradeOrchestrator.Core.Aml;
using ArasUpgradeOrchestrator.Core.Rules;

namespace ArasUpgradeOrchestrator.Core.Packages;

public enum OotbHopDiffBuildStatus
{
    ReadyToPackage,
    Blocked
}

public sealed record OotbHopDiffRequest(
    Guid AttemptId,
    string SourceVersion,
    string TargetVersion,
    string SourceRoot,
    string TargetRoot,
    string OutputRoot,
    RuleSetResolutionResult RuleResolution,
    DateTimeOffset StartedAt);

public sealed record OotbHopDiffError(string RelativePath, string Message);

public sealed record OotbHopDiffManualReview(
    string RelativePath,
    string Code,
    string? SourcePath,
    string? TargetPath,
    string Message);

public sealed record OotbHopDiffSummary(
    int XmlFilesProcessed,
    int SourceOnlyDeleted,
    int TargetOnlyRetained,
    int EqualPairsDeleted,
    int DifferentPairsRetained,
    int ErrorCount,
    int ManualReviewCount);

public sealed record OotbHopDiffBuildResult(
    Guid AttemptId,
    OotbHopDiffBuildStatus Status,
    string SourceVersion,
    string TargetVersion,
    string SourceRoot,
    string TargetRoot,
    string OutputRoot,
    string SourceDiffRoot,
    string TargetDiffRoot,
    IReadOnlyList<RuleSetVersionReference> RuleSets,
    string EffectiveRuleChecksum,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    OotbHopDiffSummary Summary,
    IReadOnlyList<OotbHopDiffError> Errors,
    IReadOnlyList<OotbHopDiffManualReview> ManualReviews);

public static class OotbHopDiffBuilder
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public static async Task<OotbHopDiffBuildResult> BuildAsync(
        OotbHopDiffRequest request,
        Func<DateTimeOffset>? clock = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var outputRoot = Path.GetFullPath(request.OutputRoot);
        var sourceDiffRoot = Path.Combine(outputRoot, "SourceDiff");
        var targetDiffRoot = Path.Combine(outputRoot, "TargetDiff");
        Directory.CreateDirectory(sourceDiffRoot);
        Directory.CreateDirectory(targetDiffRoot);

        var errors = new List<OotbHopDiffError>();
        var reviews = new List<OotbHopDiffManualReview>();
        var processed = 0;
        var sourceOnly = 0;
        var targetOnly = 0;
        var equal = 0;
        var different = 0;
        foreach (var pair in PackageXmlPathMatcher.Match(request.SourceRoot, request.TargetRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = pair.SourcePath is null ? null : AmlDocument.Load(pair.SourcePath);
                var target = pair.TargetPath is null ? null : AmlDocument.Load(pair.TargetPath);
                var template = source ?? target ?? throw new InvalidDataException("XML 配對沒有任何輸入。 ");
                var diff = Rule1DiffEngine.Compare(source ?? EmptyLike(template), target ?? EmptyLike(template));
                if (pair.SourcePath is not null)
                    await WriteAsync(sourceDiffRoot, pair.RelativePath, diff.SourceDiff.ToXml(), cancellationToken);
                if (pair.TargetPath is not null)
                    await WriteAsync(targetDiffRoot, pair.RelativePath, diff.TargetDiff.ToXml(), cancellationToken);
                reviews.AddRange(diff.ManualReviews.Select(review => new OotbHopDiffManualReview(
                    pair.RelativePath, review.Code, review.SourcePath, review.TargetPath, review.Message)));
                sourceOnly += diff.Summary.SourceOnlyDeleted;
                targetOnly += diff.Summary.TargetOnlyRetained;
                equal += diff.Summary.EqualPairsDeleted;
                different += diff.Summary.DifferentPairsRetained;
                processed++;
            }
            catch (Exception exception) when (exception is AmlParseException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add(new OotbHopDiffError(pair.RelativePath, exception.Message));
            }
        }

        var summary = new OotbHopDiffSummary(processed, sourceOnly, targetOnly, equal, different, errors.Count, reviews.Count);
        return new OotbHopDiffBuildResult(
            request.AttemptId,
            errors.Count == 0 && reviews.Count == 0 ? OotbHopDiffBuildStatus.ReadyToPackage : OotbHopDiffBuildStatus.Blocked,
            request.SourceVersion,
            request.TargetVersion,
            Path.GetFullPath(request.SourceRoot),
            Path.GetFullPath(request.TargetRoot),
            outputRoot,
            sourceDiffRoot,
            targetDiffRoot,
            request.RuleResolution.PinnedVersions.ToArray(),
            request.RuleResolution.EffectiveChecksum!,
            request.StartedAt,
            (clock ?? (() => DateTimeOffset.UtcNow))(),
            summary,
            errors,
            reviews);
    }

    private static void ValidateRequest(OotbHopDiffRequest request)
    {
        if (request.AttemptId == Guid.Empty) throw new ArgumentException("差異包嘗試識別不可為空。", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SourceVersion) || string.IsNullOrWhiteSpace(request.TargetVersion))
            throw new ArgumentException("來源與目標版本不可為空。", nameof(request));
        if (request.RuleResolution.Status != RuleResolutionStatus.Resolved || request.RuleResolution.Issues.Count > 0 ||
            request.RuleResolution.PinnedVersions.Count == 0 || string.IsNullOrWhiteSpace(request.RuleResolution.EffectiveChecksum))
            throw new InvalidOperationException("OOTB 跳點差異必須使用已成功解析並固定版本的 Rule 1 規則快照。 ");
        var ruleDraft = new RuleSetDraft(Guid.NewGuid(), Guid.NewGuid(), "resolved Rule 1", RuleSetKind.Rule1,
            RuleSetScope.Common, null, null, request.RuleResolution.Steps, request.StartedAt, "resolved-rule-snapshot");
        if (!RuleSetValidator.Validate(ruleDraft).IsValid)
            throw new InvalidOperationException("解析後的規則快照不是有效 Rule 1 規則。 ");
        var output = Path.GetFullPath(request.OutputRoot);
        if (Directory.Exists(output) || File.Exists(output))
            throw new InvalidOperationException("每次 Rule 1 執行必須使用不存在的新輸出路徑。 ");
        foreach (var input in new[] { Path.GetFullPath(request.SourceRoot), Path.GetFullPath(request.TargetRoot) })
        {
            if (Overlaps(input, output)) throw new InvalidOperationException("Rule 1 輸出不得與原始 OOTB Package 重疊。 ");
        }
    }

    private static bool Overlaps(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
        left.StartsWith(right.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        right.StartsWith(left.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static AmlDocument EmptyLike(AmlDocument document)
    {
        var root = document.Root.CloneSubtree();
        root.RemoveNodes();
        var xml = document.Declaration is null
            ? root.ToString(SaveOptions.DisableFormatting)
            : $"{document.Declaration}{Environment.NewLine}{root.ToString(SaveOptions.DisableFormatting)}";
        return AmlDocument.Parse(xml);
    }

    private static async Task WriteAsync(string root, string relativePath, string content, CancellationToken cancellationToken)
    {
        var destination = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("XML 相對路徑逸出差異包目錄。 ");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(destination, content, Utf8, cancellationToken);
    }
}
