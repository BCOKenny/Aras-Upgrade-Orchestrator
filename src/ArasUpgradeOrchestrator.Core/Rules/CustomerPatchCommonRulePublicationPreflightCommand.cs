using ArasUpgradeOrchestrator.Core.Cases;

namespace ArasUpgradeOrchestrator.Core.Rules;

public enum CustomerPatchCommonRulePublicationPreflightStatus
{
    Ready
}

public sealed record CustomerPatchCommonRulePublicationPreflightRequest(
    string CaseRoot,
    string RuleStoreRelativePath,
    string ApprovalEvidenceRelativePath,
    string ApprovalReceiptRelativePath,
    string Actor);

public sealed record CustomerPatchCommonRulePublicationPreflightResult(
    CustomerPatchCommonRulePublicationPreflightStatus Status,
    Guid CaseId,
    string RuleStorePath,
    string ApprovalEvidencePath,
    string ApprovalReceiptPath,
    RuleSetKind RuleSetKind,
    RuleSetScope RuleSetScope,
    string StepId);

public sealed class CustomerPatchCommonRulePublicationPreflightCommand
{
    public async Task<CustomerPatchCommonRulePublicationPreflightResult> ExecuteAsync(
        CustomerPatchCommonRulePublicationPreflightRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Actor))
            throw new ArgumentException("發布規則必須指定具名人工操作員。", nameof(request));

        var caseStore = new CaseStore(request.CaseRoot);
        var manifest = await caseStore.LoadAsync(cancellationToken);
        var ruleStorePath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseStore.CaseRoot, request.RuleStoreRelativePath);
        var approvalEvidencePath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseStore.CaseRoot, request.ApprovalEvidenceRelativePath);
        var approvalReceiptPath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseStore.CaseRoot, request.ApprovalReceiptRelativePath);
        if (!Directory.Exists(ruleStorePath))
            throw new DirectoryNotFoundException("找不到 Customer-Patch 規則儲存位置。");
        if (!CustomerPatchRulePublicationPaths.IsWithin(ruleStorePath, approvalEvidencePath) ||
            !CustomerPatchRulePublicationPaths.IsWithin(ruleStorePath, approvalReceiptPath))
            throw new InvalidDataException("Customer-Patch 核准資料必須位於規則儲存位置內。");

        await CustomerPatchRulePublicationApprovalReceiptStore.LoadAndValidateAsync(
            caseStore.CaseRoot, request.ApprovalReceiptRelativePath, request.Actor,
            request.ApprovalEvidenceRelativePath, cancellationToken);

        var existing = await new RuleSetStore(ruleStorePath).ListPublishedAsync(cancellationToken);
        if (existing.Any(item => item.Kind == RuleSetKind.CustomerPatchComparison && item.Scope == RuleSetScope.Common))
            throw new InvalidOperationException("Customer-Patch 共用規則已發布；不得建立第二份共同規則。");

        return new(CustomerPatchCommonRulePublicationPreflightStatus.Ready, manifest.CaseId,
            ruleStorePath, approvalEvidencePath, approvalReceiptPath,
            RuleSetKind.CustomerPatchComparison, RuleSetScope.Common, "customer-patch-retain-items");
    }
}
