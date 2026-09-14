using ArasUpgradeOrchestrator.Core.Cases;

namespace ArasUpgradeOrchestrator.Core.Rules;

public sealed record CustomerPatchCommonRulePublicationRequest(
    string CaseRoot,
    string RuleStoreRelativePath,
    string ApprovalEvidenceRelativePath,
    string ApprovalReceiptRelativePath,
    string Actor);

public sealed record CustomerPatchCommonRulePublicationResult(
    Guid CaseId,
    string RuleStorePath,
    string ApprovalEvidencePath,
    Guid DraftId,
    PublishedRuleSet PublishedRuleSet);

public sealed class CustomerPatchCommonRulePublicationCommand
{
    public async Task<CustomerPatchCommonRulePublicationResult> ExecuteAsync(
        CustomerPatchCommonRulePublicationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preflight = await new CustomerPatchCommonRulePublicationPreflightCommand().ExecuteAsync(
            new(request.CaseRoot, request.RuleStoreRelativePath, request.ApprovalEvidenceRelativePath,
                request.ApprovalReceiptRelativePath, request.Actor), cancellationToken);

        var ruleStore = new RuleSetStore(preflight.RuleStorePath);
        var existing = await ruleStore.ListPublishedAsync(cancellationToken);
        if (existing.Any(item => item.Kind == RuleSetKind.CustomerPatchComparison && item.Scope == RuleSetScope.Common))
            throw new InvalidOperationException("Customer-Patch 共用規則已發布；不得建立第二份共同規則。 ");

        var actor = new RuleActor(request.Actor, RuleActorKind.Human);
        var draft = DefaultUpgradeRuleSets.CreateCustomerPatchComparisonDraft(actor.Name, DateTimeOffset.UtcNow);
        var validation = RuleSetValidator.Validate(draft);
        if (!validation.IsValid)
            throw new InvalidOperationException("Customer-Patch 規則草稿未通過驗證：" + string.Join("；", validation.Errors.Select(error => error.Message)));

        await ruleStore.SaveDraftAsync(draft, actor, cancellationToken);
        var published = await ruleStore.PublishAsync(draft.DraftId,
            new RulePublicationApproval(actor, request.ApprovalEvidenceRelativePath), cancellationToken);
        return new(preflight.CaseId, preflight.RuleStorePath, preflight.ApprovalEvidencePath, draft.DraftId, published);
    }
}
