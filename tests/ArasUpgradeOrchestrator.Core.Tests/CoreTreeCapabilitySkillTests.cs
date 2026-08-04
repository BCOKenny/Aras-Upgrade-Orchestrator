internal static class CoreTreeCapabilitySkillTests
{
    internal static void AssertPackage(string skillName, IReadOnlyList<string> caseIds)
    {
        var root = ProjectPath(".agents", "skills", skillName);
        var skill = File.ReadAllText(Path.Combine(root, "SKILL.md"));
        AssertSkillFrontmatter(skill, skillName);
        AssertAgentMetadata(Path.Combine(root, "agents", "openai.yaml"), skillName);

        foreach (var reference in new[] { "input-contract.md", "output-contract.md", "rules.md", "error-and-stop-conditions.md", "skill-test-evidence.md" })
            Require(File.Exists(Path.Combine(root, "references", reference)), $"{skillName} 缺少 {reference}。");

        foreach (var caseId in caseIds)
        {
            var caseRoot = Path.Combine(root, "assets", "acceptance-cases", caseId);
            Require(File.Exists(Path.Combine(caseRoot, "input.json")), $"{skillName}/{caseId} 缺少 input.json。");
            Require(File.Exists(Path.Combine(caseRoot, "expected", "result.json")), $"{skillName}/{caseId} 缺少 expected/result.json。");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string ProjectPath(params string[] segments)
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ArasUpgradeOrchestrator.sln")))
            current = current.Parent;
        if (current is null) throw new DirectoryNotFoundException("找不到包含方案檔的專案根目錄。");
        return segments.Aggregate(current.FullName, Path.Combine);
    }

    private static void AssertSkillFrontmatter(string content, string expectedName)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.Equal("---", lines[0]);
        var closing = Array.IndexOf(lines, "---", 1);
        Require(closing > 2, "Skill frontmatter 不完整。");
        var fields = lines[1..closing].Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Equal(2, fields.Length);
        Assert.Equal($"name: {expectedName}", fields[0]);
        Require(fields[1].StartsWith("description: ", StringComparison.Ordinal) && fields[1].Length > "description: ".Length + 20,
            "Skill description 必須是足夠長的單行描述。");
        Require(expectedName.Length <= 64 && expectedName.All(character => char.IsAsciiLetterLower(character) || char.IsDigit(character) || character == '-'),
            "Skill name 必須是 64 字元內的小寫英文、數字或連字號。");
        var description = fields[1]["description: ".Length..];
        Require(description.Length <= 1024 && !description.Contains('<') && !description.Contains('>'),
            "Skill description 不可包含角括號且長度不得超過 1024 字元。");
    }

    private static void AssertAgentMetadata(string path, string skillName)
    {
        var content = File.ReadAllText(path);
        Require(content.Contains("display_name: \"", StringComparison.Ordinal), "Agent metadata 缺少 display_name。");
        Require(content.Contains("short_description: \"", StringComparison.Ordinal), "Agent metadata 缺少 short_description。");
        Require(content.Contains($"default_prompt: \"請使用 ${skillName}", StringComparison.Ordinal), "Agent metadata 缺少 Skill 預設提示。");
    }
}
