using System.Security.Cryptography;

namespace ArasUpgradeOrchestrator.Core.Rules;

public sealed record CustomerPatchCodexExecutionRequest(
    string CaseRoot,
    string RuleStoreRelativePath,
    string ApprovalEvidenceRelativePath,
    string ApprovalReceiptRelativePath,
    string ApprovedBy,
    byte[] RequestBytes);

public sealed record CustomerPatchCodexExecutionResult(
    Guid CaseId,
    string RuleStorePath,
    string ApprovalEvidencePath,
    string ApprovalReceiptPath,
    Guid DraftId,
    PublishedRuleSet PublishedRuleSet,
    string ApprovedBy,
    string ExecutedBy,
    string RequestChecksum);

public sealed class CustomerPatchCodexExecutionCommand
{
    public async Task<CustomerPatchCodexExecutionResult> ExecuteAsync(
        CustomerPatchCodexExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ApprovedBy))
            throw new ArgumentException("Codex 受控發布必須指定具名核准人。", nameof(request));
        if (request.RequestBytes is null || request.RequestBytes.Length == 0)
            throw new ArgumentException("Codex 受控發布缺少 request 原始內容。", nameof(request));

        var preflight = await new CustomerPatchCommonRulePublicationPreflightCommand().ExecuteAsync(
            new CustomerPatchCommonRulePublicationPreflightRequest(
                request.CaseRoot,
                request.RuleStoreRelativePath,
                request.ApprovalEvidenceRelativePath,
                request.ApprovalReceiptRelativePath,
                request.ApprovedBy),
            cancellationToken);

        var ruleStore = new RuleSetStore(preflight.RuleStorePath);
        var existing = await ruleStore.ListPublishedAsync(cancellationToken);
        if (existing.Any(item => item.Kind == RuleSetKind.CustomerPatchComparison && item.Scope == RuleSetScope.Common))
            throw new InvalidOperationException("Customer-Patch 共用規則已發布；不得建立第二份共同規則。 ");

        var approvalActor = new RuleActor(request.ApprovedBy, RuleActorKind.Human);
        var draft = DefaultUpgradeRuleSets.CreateCustomerPatchComparisonDraft(approvalActor.Name, DateTimeOffset.UtcNow);
        var validation = RuleSetValidator.Validate(draft);
        if (!validation.IsValid)
            throw new InvalidOperationException("Customer-Patch 規則草稿未通過驗證：" + string.Join("；", validation.Errors.Select(error => error.Message)));

        var requestChecksum = Convert.ToHexString(SHA256.HashData(request.RequestBytes));
        var executionAudit = new RuleExecutionAudit(
            request.ApprovedBy,
            "CodexAgent",
            request.ApprovalReceiptRelativePath,
            requestChecksum);
        await ruleStore.SaveDraftAsync(draft, approvalActor, cancellationToken);
        var published = await ruleStore.PublishAsync(
            draft.DraftId,
            new RulePublicationApproval(approvalActor, request.ApprovalEvidenceRelativePath),
            executionAudit,
            cancellationToken);
        return new CustomerPatchCodexExecutionResult(
            preflight.CaseId,
            preflight.RuleStorePath,
            preflight.ApprovalEvidencePath,
            preflight.ApprovalReceiptPath,
            draft.DraftId,
            published,
            request.ApprovedBy,
            executionAudit.ExecutedBy,
            requestChecksum);
    }
}
