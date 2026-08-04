namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public static class CoreTreeInputValidator
{
    public static void Validate(CoreTreeComparisonRequest request)
    {
        ValidateInputs(request);
        var output = Path.GetFullPath(request.OutputRoot);
        if (Directory.Exists(output) || File.Exists(output))
            throw new CoreTreeValidationException("OutputAttemptAlreadyExists", "每次 Core Tree 執行必須使用不存在的新輸出路徑。 ");
        foreach (var input in new[] { request.Customer.RootPath, request.SourceOotb.RootPath, request.TargetOotb.RootPath })
            if (Overlaps(Path.GetFullPath(input), output))
                throw new CoreTreeValidationException("InputOutputOverlap", "Core Tree 輸出不得與任一輸入目錄重疊。 ");
    }

    internal static void ValidateInputs(CoreTreeComparisonRequest request)
    {
        if (request is null)
            throw new CoreTreeValidationException("InvalidRequest", "Core Tree request is required.");
        if (request.AttemptId == Guid.Empty)
            throw new CoreTreeValidationException("InvalidRequest", "Core Tree 嘗試識別不可為空。");
        if (string.IsNullOrWhiteSpace(request.SourceVersion) || string.IsNullOrWhiteSpace(request.TargetVersion))
            throw new CoreTreeValidationException("InvalidRequest", "案件來源與目標版本不可為空。");
        var inputs = new[] { request.Customer, request.SourceOotb, request.TargetOotb };
        if (inputs.Any(input => input is null || string.IsNullOrWhiteSpace(input.RootPath)))
            throw new CoreTreeValidationException("InputDirectoryMissing", "Core Tree input root is required.");
        if (!string.Equals(request.Customer.InnovatorVersion, request.SourceVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.SourceOotb.InnovatorVersion, request.SourceVersion, StringComparison.OrdinalIgnoreCase))
            throw new CoreTreeValidationException("VersionEvidenceMismatch", "客戶來源與來源 OOTB 的版本證據必須符合案件來源版本。 ");
        if (!string.Equals(request.TargetOotb.InnovatorVersion, request.TargetVersion, StringComparison.OrdinalIgnoreCase))
            throw new CoreTreeValidationException("VersionEvidenceMismatch", "目標 OOTB 的版本證據必須符合案件目標版本。 ");

        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input.EvidenceReference))
                throw new CoreTreeValidationException("VersionEvidenceMismatch", "三份 Core Tree 輸入都必須有版本證據。 ");
            var root = Path.GetFullPath(input.RootPath);
            if (!Directory.Exists(root))
                throw new CoreTreeValidationException("InputDirectoryMissing", "Core Tree 輸入目錄不存在。 ");
            foreach (var side in new[] { "Client", "Server" })
                if (!Directory.Exists(Path.Combine(root, "Innovator", side)))
                    throw new CoreTreeValidationException("RequiredTreeStructureMissing", $"Core Tree 輸入缺少 Innovator\\{side}：{root}");
        }

        var roots = inputs.Select(input => Path.GetFullPath(input.RootPath)).ToArray();
        for (var left = 0; left < roots.Length; left++)
            for (var right = left + 1; right < roots.Length; right++)
                if (Overlaps(roots[left], roots[right]))
                    throw new CoreTreeValidationException("InputDirectoryOverlap", "三份 Core Tree 輸入必須是互不重疊的獨立目錄。 ");

        if (request.ServerTextRules is null || string.IsNullOrWhiteSpace(request.ServerTextRules.Version) ||
            string.IsNullOrWhiteSpace(request.ServerTextRules.Checksum) || request.ServerTextRules.RelativePaths is null)
            throw new CoreTreeValidationException("InvalidServerRuleSet", "Server 文字比較規則必須固定版本與 Checksum。 ");
        if (request.ServerTextRules.RelativePaths.Any(path => !CoreTreeServerTextRuleSet.IsCanonicalServerRelativePath(path)))
            throw new CoreTreeValidationException("InvalidServerRuleSet", "Server text rule paths must be canonical relative Server paths.");
        var expectedChecksum = CoreTreeServerTextRuleSet.CalculateChecksum(request.ServerTextRules.Version, request.ServerTextRules.RelativePaths);
        if (!string.Equals(expectedChecksum, request.ServerTextRules.Checksum, StringComparison.OrdinalIgnoreCase))
            throw new CoreTreeValidationException("RuleChecksumMismatch", "Server 文字比較規則 Checksum 與固定內容不符。 ");
        if (request.ServerTextRules.RelativePaths.Count != request.ServerTextRules.RelativePaths
                .Select(CoreTreeContentComparer.NormalizeRelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new CoreTreeValidationException("InvalidServerRuleSet", "Server 文字比較規則不得包含重複相對路徑。 ");
        foreach (var rulePath in request.ServerTextRules.RelativePaths)
        {
            var normalized = CoreTreeContentComparer.NormalizeRelativePath(rulePath);
            if (!normalized.StartsWith("Server/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("../", StringComparison.Ordinal))
                throw new CoreTreeValidationException("InvalidServerRuleSet", "Server 文字比較規則只能包含 Server 下的安全相對路徑。 ");
        }
    }

    private static bool Overlaps(string left, string right) =>
        IsSameOrDescendant(left, right) || IsSameOrDescendant(right, left);

    private static bool IsSameOrDescendant(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
}
