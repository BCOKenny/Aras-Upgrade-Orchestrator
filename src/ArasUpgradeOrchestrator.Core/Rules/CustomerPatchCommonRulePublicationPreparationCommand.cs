using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArasUpgradeOrchestrator.Core.Cases;

namespace ArasUpgradeOrchestrator.Core.Rules;

public sealed record CustomerPatchCommonRulePublicationPreparationRequest(
    string CaseRoot,
    string Actor,
    string HumanApprovalDecision);

public sealed record CustomerPatchCommonRulePublicationPreparationResult(
    string Status,
    Guid CaseId,
    string RuleStorePath,
    string ApprovalEvidencePath,
    string ApprovalEvidenceRelativePath,
    string ApprovalReceiptPath,
    string PendingPath,
    string PublicationRequestPath);

public sealed class CustomerPatchCommonRulePublicationPreparationCommand
{
    private const string RuleStoreRelativePath = "rule-sets";
    private const string ApprovalEvidenceRelativePath = "rule-sets\\approval-drafts\\customer-patch-common-v1-approval-draft.md";
    private const string ApprovalReceiptRelativePath = "rule-sets\\approval-drafts\\customer-patch-common-v1.approval.json";
    private const string PendingRelativePath = "rule-sets\\publication-requests\\customer-patch-common-v1.pending.md";
    private const string PublicationRequestRelativePath = "rule-sets\\publication-requests\\customer-patch-common-v1.request.json";
    private readonly Func<DateTimeOffset> _clock;

    public CustomerPatchCommonRulePublicationPreparationCommand(Func<DateTimeOffset>? clock = null) =>
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public async Task<CustomerPatchCommonRulePublicationPreparationResult> ExecuteAsync(
        CustomerPatchCommonRulePublicationPreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Actor))
            throw new ArgumentException("規則發布準備必須指定具名人工操作員。", nameof(request));
        if (request.HumanApprovalDecision is not ("同意" or "不同意"))
            throw new InvalidDataException("人工核准結論只能是「同意」或「不同意」。");

        var caseStore = new CaseStore(request.CaseRoot);
        var manifest = await caseStore.LoadAsync(cancellationToken);
        var ruleStorePath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseStore.CaseRoot, RuleStoreRelativePath);
        Directory.CreateDirectory(ruleStorePath);

        var approvalEvidencePath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseStore.CaseRoot, ApprovalEvidenceRelativePath);
        var approvalReceiptPath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseStore.CaseRoot, ApprovalReceiptRelativePath);
        var pendingPath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseStore.CaseRoot, PendingRelativePath);
        var publicationRequestPath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseStore.CaseRoot, PublicationRequestRelativePath);
        var outputPaths = new[] { approvalEvidencePath, approvalReceiptPath, pendingPath, publicationRequestPath };
        if (outputPaths.Any(File.Exists))
            throw new InvalidOperationException("Customer-Patch 規則發布準備資料已有檔案，禁止覆寫；請先依規格檢查既有資料。");

        var createdAt = _clock();
        var approvalDraft = $@"# Customer-Patch Comparison 共用規則核准草稿

規則名稱：客戶基準對 Patch／Support 比較共同準則
規則類型：CustomerPatchComparison
規則範圍：Common
規則步驟：customer-patch-retain-items
規則語意：不自動刪除任何左、右側既有對象；僅保留 Item 供後續正式比較依規則產生報告與人工確認。

具名人工操作者：{request.Actor}
人工核准結論：{request.HumanApprovalDecision}
產生時間（UTC）：{createdAt:O}

狀態：待受控發布；不是已發布規則。
";
        var pending = $@"# Customer-Patch Comparison 共用規則待發布資料

規則庫位置：{RuleStoreRelativePath}
核准草稿：{ApprovalEvidenceRelativePath}
規則類型：CustomerPatchComparison
規則範圍：Common
規則步驟：customer-patch-retain-items
人工核准結論：{request.HumanApprovalDecision}
產生時間（UTC）：{createdAt:O}

狀態：尚無 RuleSetId、版本號或 ContentChecksum；待受控發布。
";

        var approvalDirectory = Path.GetDirectoryName(approvalEvidencePath)!;
        var publicationDirectory = Path.GetDirectoryName(pendingPath)!;
        Directory.CreateDirectory(approvalDirectory);
        Directory.CreateDirectory(publicationDirectory);
        await CreateNewTextAsync(approvalEvidencePath, approvalDraft, cancellationToken);
        await CreateNewTextAsync(pendingPath, pending, cancellationToken);

        if (request.HumanApprovalDecision == "同意")
        {
            var checksum = await HashFileAsync(approvalEvidencePath, cancellationToken);
            var receipt = new
            {
                actor = request.Actor,
                decision = "Approved",
                approvalEvidenceRelativePath = ApprovalEvidenceRelativePath,
                approvalEvidenceSha256 = checksum
            };
            await CreateNewTextAsync(approvalReceiptPath, JsonSerializer.Serialize(receipt, JsonOptions), cancellationToken);
            var publicationRequest = new
            {
                caseRoot = caseStore.CaseRoot,
                ruleStoreRelativePath = RuleStoreRelativePath,
                approvalEvidenceRelativePath = ApprovalEvidenceRelativePath,
                approvalReceiptRelativePath = ApprovalReceiptRelativePath,
                approvedBy = request.Actor
            };
            await CreateNewTextAsync(publicationRequestPath, JsonSerializer.Serialize(publicationRequest, JsonOptions), cancellationToken);
        }

        return new(request.HumanApprovalDecision == "同意" ? "Created" : "Rejected", manifest.CaseId,
            ruleStorePath, approvalEvidencePath, ApprovalEvidenceRelativePath, approvalReceiptPath, pendingPath, publicationRequestPath);
    }

    private static async Task CreateNewTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
