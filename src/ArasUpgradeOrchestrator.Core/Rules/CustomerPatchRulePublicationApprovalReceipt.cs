using System.Security.Cryptography;
using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Rules;

public sealed record CustomerPatchRulePublicationApprovalReceipt(
    string Actor,
    string Decision,
    string ApprovalDraftRelativePath,
    string ApprovalDraftChecksum);

internal sealed record CustomerPatchRulePublicationApprovalReceiptDocument(
    string? Actor,
    string? Decision,
    string? ApprovalDraftRelativePath,
    string? ApprovalDraftChecksum,
    string? ApprovalEvidenceRelativePath,
    string? ApprovalEvidenceSha256);

public static class CustomerPatchRulePublicationApprovalReceiptStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<CustomerPatchRulePublicationApprovalReceipt> LoadAndValidateAsync(
        string caseRoot,
        string receiptRelativePath,
        string expectedActor,
        string expectedApprovalDraftRelativePath,
        CancellationToken cancellationToken = default)
    {
        var receiptPath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseRoot, receiptRelativePath);
        var expectedApprovalPath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseRoot, expectedApprovalDraftRelativePath);
        if (!File.Exists(receiptPath))
            throw new FileNotFoundException("找不到 Customer-Patch 核准收據。", receiptPath);
        if (!File.Exists(expectedApprovalPath))
            throw new FileNotFoundException("找不到 Customer-Patch 人工核准證據。", expectedApprovalPath);

        var content = await File.ReadAllTextAsync(receiptPath, cancellationToken);
        var document = JsonSerializer.Deserialize<CustomerPatchRulePublicationApprovalReceiptDocument>(content, Json)
            ?? throw new InvalidDataException("Customer-Patch 核准收據不可為空白。");
        var receipt = new CustomerPatchRulePublicationApprovalReceipt(
            document.Actor ?? string.Empty,
            document.Decision ?? string.Empty,
            ResolveCompatibleValue(
                document.ApprovalDraftRelativePath,
                document.ApprovalEvidenceRelativePath,
                "核准證據相對路徑"),
            ResolveCompatibleValue(
                document.ApprovalDraftChecksum,
                document.ApprovalEvidenceSha256,
                "核准證據 Checksum"));
        if (!string.Equals(receipt.Actor, expectedActor, StringComparison.Ordinal))
            throw new InvalidDataException("Customer-Patch 核准收據的操作者與發布 request 不一致。");
        if (!string.Equals(receipt.Decision, "Approved", StringComparison.Ordinal))
            throw new InvalidDataException("Customer-Patch 核准收據未記錄 Approved 決定。");

        var receiptApprovalPath = CustomerPatchRulePublicationPaths.ResolveCasePath(caseRoot, receipt.ApprovalDraftRelativePath);
        if (!string.Equals(receiptApprovalPath, expectedApprovalPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Customer-Patch 核准收據指向的核准證據與發布 request 不一致。");

        await using var stream = File.OpenRead(expectedApprovalPath);
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!string.Equals(receipt.ApprovalDraftChecksum, checksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Customer-Patch 人工核准證據的 Checksum 與核准收據不一致。");
        return receipt;
    }

    private static string ResolveCompatibleValue(string? approvalDraftValue, string? approvalEvidenceValue, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(approvalDraftValue) &&
            !string.IsNullOrWhiteSpace(approvalEvidenceValue) &&
            !string.Equals(approvalDraftValue, approvalEvidenceValue, StringComparison.Ordinal))
            throw new InvalidDataException($"Customer-Patch 核准收據的{fieldName}欄位不一致。");

        return !string.IsNullOrWhiteSpace(approvalEvidenceValue)
            ? approvalEvidenceValue
            : approvalDraftValue ?? throw new InvalidDataException($"Customer-Patch 核准收據缺少{fieldName}。");
    }
}

internal static class CustomerPatchRulePublicationPaths
{
    public static string ResolveCasePath(string caseRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(caseRoot))
            throw new ArgumentException("案件根目錄不可空白。", nameof(caseRoot));
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment == ".."))
            throw new InvalidDataException("案件內相對路徑無效。");

        var fullRoot = Path.GetFullPath(caseRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("案件內相對路徑逸出案件根目錄。");
        return path;
    }

    public static bool IsWithin(string parentPath, string childPath)
    {
        var fullParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullChild = Path.GetFullPath(childPath);
        return fullChild.StartsWith(fullParent, StringComparison.OrdinalIgnoreCase);
    }
}
