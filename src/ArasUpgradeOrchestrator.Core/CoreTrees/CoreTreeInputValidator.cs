namespace ArasUpgradeOrchestrator.Core.CoreTrees;

public static class CoreTreeInputValidator
{
    public static void Validate(CoreTreeComparisonRequest request)
    {
        ValidateInputs(request);
        var output = Path.GetFullPath(request.OutputRoot);
        if (Directory.Exists(output) || File.Exists(output))
            throw new CoreTreeValidationException("OutputAttemptAlreadyExists", "Core Tree output attempt already exists.");
        if (Directory.Exists(output) || File.Exists(output))
            throw new InvalidOperationException("每次 Core Tree 執行必須使用不存在的新輸出路徑。 ");
        foreach (var input in new[] { request.Customer.RootPath, request.SourceOotb.RootPath, request.TargetOotb.RootPath })
        {
            if (Overlaps(Path.GetFullPath(input), output))
                throw new CoreTreeValidationException("InputOutputOverlap", "Core Tree output overlaps an input.");
            if (Overlaps(Path.GetFullPath(input), output))
                throw new InvalidOperationException("Core Tree 輸出不得與任一輸入目錄重疊。 ");
        }
    }

    internal static void ValidateInputs(CoreTreeComparisonRequest request)
    {
        if (request is null) throw new CoreTreeValidationException("InvalidRequest", "Core Tree request is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (request.AttemptId == Guid.Empty) throw new CoreTreeValidationException("InvalidRequest", "Core Tree attempt id is required.");
        if (request.AttemptId == Guid.Empty) throw new ArgumentException("Core Tree 嘗試識別不可為空。", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SourceVersion) || string.IsNullOrWhiteSpace(request.TargetVersion))
            throw new CoreTreeValidationException("InvalidRequest", "Core Tree source and target versions are required.");
        if (string.IsNullOrWhiteSpace(request.SourceVersion) || string.IsNullOrWhiteSpace(request.TargetVersion))
            throw new ArgumentException("案件來源與目標版本不可為空。", nameof(request));
        if (!string.Equals(request.Customer.InnovatorVersion, request.SourceVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.SourceOotb.InnovatorVersion, request.SourceVersion, StringComparison.OrdinalIgnoreCase))
            throw new CoreTreeValidationException("VersionEvidenceMismatch", "Core Tree source version evidence does not match.");
        if (!string.Equals(request.Customer.InnovatorVersion, request.SourceVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.SourceOotb.InnovatorVersion, request.SourceVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("客戶來源與來源 OOTB 的版本證據必須符合案件來源版本。 ");
        if (!string.Equals(request.TargetOotb.InnovatorVersion, request.TargetVersion, StringComparison.OrdinalIgnoreCase))
            throw new CoreTreeValidationException("VersionEvidenceMismatch", "Core Tree target version evidence does not match.");
        if (!string.Equals(request.TargetOotb.InnovatorVersion, request.TargetVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("目標 OOTB 的版本證據必須符合案件目標版本。 ");

        foreach (var input in new[] { request.Customer, request.SourceOotb, request.TargetOotb })
        {
            if (string.IsNullOrWhiteSpace(input.EvidenceReference))
                throw new CoreTreeValidationException("VersionEvidenceMismatch", "Core Tree version evidence is required.");
            if (string.IsNullOrWhiteSpace(input.EvidenceReference))
                throw new InvalidOperationException("三份 Core Tree 輸入都必須有版本證據。 ");
            var root = Path.GetFullPath(input.RootPath);
            if (!Directory.Exists(root))
                throw new CoreTreeValidationException("InputDirectoryMissing", "Core Tree input directory is missing.");
            foreach (var side in new[] { "Client", "Server" })
            {
                if (!Directory.Exists(Path.Combine(root, "Innovator", side)))
                    throw new CoreTreeValidationException("RequiredTreeStructureMissing", "Core Tree required Client or Server directory is missing.");
                if (!Directory.Exists(Path.Combine(root, "Innovator", side)))
                    throw new InvalidOperationException($"Core Tree 輸入缺少 Innovator\\{side}：{root}");
            }
        }

        var roots = new[] { request.Customer.RootPath, request.SourceOotb.RootPath, request.TargetOotb.RootPath }
            .Select(Path.GetFullPath).ToArray();
        for (var left = 0; left < roots.Length; left++)
        {
            for (var right = left + 1; right < roots.Length; right++)
            {
                if (Overlaps(roots[left], roots[right]))
                    throw new CoreTreeValidationException("InputDirectoryOverlap", "Core Tree input directories overlap.");
                if (Overlaps(roots[left], roots[right]))
                    throw new InvalidOperationException("三份 Core Tree 輸入必須是互不重疊的獨立目錄。 ");

            }
        }

        if (string.IsNullOrWhiteSpace(request.ServerTextRules.Version) ||
            string.IsNullOrWhiteSpace(request.ServerTextRules.Checksum))
            throw new CoreTreeValidationException("InvalidServerRuleSet", "Core Tree Server text rule metadata is invalid.");
        if (string.IsNullOrWhiteSpace(request.ServerTextRules.Version) ||
            string.IsNullOrWhiteSpace(request.ServerTextRules.Checksum))
            throw new InvalidOperationException("Server 文字比較規則必須固定版本與 Checksum。 ");
        var expectedChecksum = CoreTreeServerTextRuleSet.CalculateChecksum(
            request.ServerTextRules.Version, request.ServerTextRules.RelativePaths);
        if (!string.Equals(expectedChecksum, request.ServerTextRules.Checksum, StringComparison.OrdinalIgnoreCase))
            throw new CoreTreeValidationException("RuleChecksumMismatch", "Core Tree Server text rule checksum does not match.");
        if (!string.Equals(expectedChecksum, request.ServerTextRules.Checksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Server 文字比較規則 Checksum 與固定內容不符。 ");
        if (request.ServerTextRules.RelativePaths.Count != request.ServerTextRules.RelativePaths
                .Select(CoreTreeContentComparer.NormalizeRelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new CoreTreeValidationException("InvalidServerRuleSet", "Core Tree Server text rule paths are duplicated.");
        if (request.ServerTextRules.RelativePaths.Count != request.ServerTextRules.RelativePaths
                .Select(CoreTreeContentComparer.NormalizeRelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidOperationException("Server 文字比較規則不得包含重複相對路徑。 ");
        foreach (var rulePath in request.ServerTextRules.RelativePaths)
        {
            var normalized = CoreTreeContentComparer.NormalizeRelativePath(rulePath);
            if (!normalized.StartsWith("Server/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("../", StringComparison.Ordinal))
                throw new CoreTreeValidationException("InvalidServerRuleSet", "Core Tree Server text rule path is unsafe.");
            if (!normalized.StartsWith("Server/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("../", StringComparison.Ordinal))
                throw new InvalidOperationException("Server 文字比較規則只能包含 Server 下的安全相對路徑。 ");
        }
    }

    private static bool Overlaps(string left, string right) =>
        IsSameOrDescendant(left, right) || IsSameOrDescendant(right, left);

    private static bool IsSameOrDescendant(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
}
