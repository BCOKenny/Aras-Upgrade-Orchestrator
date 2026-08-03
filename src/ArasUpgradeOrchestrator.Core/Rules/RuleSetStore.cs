using System.Collections.Concurrent;
using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Rules;

public sealed class RuleSetStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _root;
    private readonly Func<DateTimeOffset> _clock;

    public RuleSetStore(string root, Func<DateTimeOffset>? clock = null)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("規則儲存根目錄不可為空白。", nameof(root));
        _root = Path.GetFullPath(root);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task SaveDraftAsync(RuleSetDraft draft, RuleActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EnsureHuman(actor, "建立或修改規則草稿");
        if (!string.Equals(draft.CreatedBy, actor.Name, StringComparison.Ordinal))
            throw new InvalidOperationException("規則草稿的建立者必須與具名人工操作員一致。 ");
        Directory.CreateDirectory(DraftDirectory);
        var path = DraftPath(draft.DraftId);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(draft, JsonOptions), cancellationToken);
        File.Move(temporary, path, true);
    }

    public async Task<PublishedRuleSet> PublishAsync(
        Guid draftId,
        RulePublicationApproval approval,
        CancellationToken cancellationToken = default)
    {
        EnsureHuman(approval?.Actor, "發布規則版本");
        if (string.IsNullOrWhiteSpace(approval!.EvidenceReference))
            throw new InvalidOperationException("發布規則必須附人工核准證據。 ");
        var draft = await ReadDraftAsync(draftId, cancellationToken);
        var validation = RuleSetValidator.Validate(draft);
        if (!validation.IsValid)
            throw new InvalidOperationException("規則草稿未通過驗證：" + string.Join("; ", validation.Errors.Select(error => error.Message)));

        var gate = Locks.GetOrAdd(_root, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await ListPublishedAsync(cancellationToken);
            var nextVersion = existing.Where(item => item.RuleSetId == draft.RuleSetId).Select(item => item.Version).DefaultIfEmpty(0).Max() + 1;
            var published = PublishedRuleSet.Create(draft, nextVersion, _clock(), approval.Actor.Name, approval.EvidenceReference);
            var directory = Path.Combine(PublishedDirectory, draft.RuleSetId.ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{nextVersion:D8}.json");
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, published, JsonOptions, cancellationToken);
            return published;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<PublishedRuleSet> GetPublishedAsync(Guid ruleSetId, int version, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(PublishedDirectory, ruleSetId.ToString("N"), $"{version:D8}.json");
        if (!File.Exists(path)) throw new FileNotFoundException("找不到指定規則版本。", path);
        await using var stream = File.OpenRead(path);
        var published = await JsonSerializer.DeserializeAsync<PublishedRuleSet>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("規則版本內容無效。 ");
        VerifyChecksum(published);
        return published;
    }

    public async Task<IReadOnlyList<PublishedRuleSet>> ListPublishedAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(PublishedDirectory)) return [];
        var result = new List<PublishedRuleSet>();
        foreach (var path in Directory.EnumerateFiles(PublishedDirectory, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            await using var stream = File.OpenRead(path);
            var published = await JsonSerializer.DeserializeAsync<PublishedRuleSet>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException($"規則版本內容無效：{path}");
            VerifyChecksum(published);
            result.Add(published);
        }
        return result;
    }

    private async Task<RuleSetDraft> ReadDraftAsync(Guid draftId, CancellationToken cancellationToken)
    {
        var path = DraftPath(draftId);
        if (!File.Exists(path)) throw new FileNotFoundException("找不到規則草稿。", path);
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RuleSetDraft>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("規則草稿內容無效。 ");
    }

    private static void EnsureHuman(RuleActor? actor, string operation)
    {
        if (actor is null || actor.Kind != RuleActorKind.Human || string.IsNullOrWhiteSpace(actor.Name))
            throw new InvalidOperationException($"{operation}只能由具名人工操作員執行；AI 或自動化不得代行。 ");
    }

    private static void VerifyChecksum(PublishedRuleSet published)
    {
        var actual = RuleSetCanonicalizer.Checksum(published.DisplayName, published.Kind, published.Scope,
            published.SourceVersion, published.TargetVersion, published.Steps);
        if (!string.Equals(actual, published.ContentChecksum, StringComparison.Ordinal))
            throw new InvalidDataException($"規則集 {published.RuleSetId} 第 {published.Version} 版內容 Checksum 不相符。 ");
    }

    private string DraftDirectory => Path.Combine(_root, "drafts");
    private string PublishedDirectory => Path.Combine(_root, "published");
    private string DraftPath(Guid draftId) => Path.Combine(DraftDirectory, draftId.ToString("N") + ".json");
}
