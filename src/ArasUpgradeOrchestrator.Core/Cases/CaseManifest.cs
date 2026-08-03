namespace ArasUpgradeOrchestrator.Core.Cases;

public sealed record UpgradeHop(string SourceVersion, string TargetVersion, string SupportDirectory)
{
    public string Key => $"{SourceVersion}->{TargetVersion}";
}

public sealed record UpgradeRoute(int Version, IReadOnlyList<UpgradeHop> Hops, DateTimeOffset CreatedAt)
{
    public static UpgradeRoute Create(int version, IEnumerable<UpgradeHop> hops, DateTimeOffset createdAt)
    {
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        var items = hops.ToArray();
        if (items.Length == 0) throw new ArgumentException("升級路徑至少需要一個跳點。", nameof(hops));

        for (var index = 0; index < items.Length; index++)
        {
            var hop = items[index];
            if (string.IsNullOrWhiteSpace(hop.SourceVersion) || string.IsNullOrWhiteSpace(hop.TargetVersion))
                throw new ArgumentException("每個跳點都必須有來源與目標版本。", nameof(hops));
            if (string.Equals(hop.SourceVersion, hop.TargetVersion, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("跳點的來源與目標版本不可相同。", nameof(hops));
            if (index > 0 && !string.Equals(items[index - 1].TargetVersion, hop.SourceVersion, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"升級路徑在 {items[index - 1].TargetVersion} 與 {hop.SourceVersion} 之間不連續。", nameof(hops));
        }

        return new UpgradeRoute(version, items, createdAt);
    }
}

public sealed record ArtifactLocation(string Kind, string Path, string? HopKey = null);

public sealed record CaseManifest(
    int SchemaVersion,
    Guid CaseId,
    string CustomerCode,
    string SourceVersion,
    string TargetVersion,
    DateTimeOffset CreatedAt,
    int CurrentRouteVersion,
    IReadOnlyList<UpgradeRoute> Routes,
    IReadOnlyList<ArtifactLocation> ArtifactLocations)
{
    public const int CurrentSchemaVersion = 1;

    public UpgradeRoute CurrentRoute => Routes.Single(route => route.Version == CurrentRouteVersion);

    public static CaseManifest Create(
        Guid caseId,
        string customerCode,
        string sourceVersion,
        string targetVersion,
        UpgradeRoute route,
        DateTimeOffset createdAt,
        IEnumerable<ArtifactLocation>? artifactLocations = null)
    {
        if (caseId == Guid.Empty) throw new ArgumentException("案件識別不可為空。", nameof(caseId));
        if (string.IsNullOrWhiteSpace(customerCode)) throw new ArgumentException("客戶代號不可為空。", nameof(customerCode));
        if (!string.Equals(route.Hops[0].SourceVersion, sourceVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(route.Hops[^1].TargetVersion, targetVersion, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("升級路徑的起訖版本必須與案件相符。", nameof(route));

        return new CaseManifest(
            CurrentSchemaVersion,
            caseId,
            customerCode.Trim(),
            sourceVersion.Trim(),
            targetVersion.Trim(),
            createdAt,
            route.Version,
            [route],
            artifactLocations?.ToArray() ?? []);
    }

    public CaseManifest AddRouteVersion(UpgradeRoute route)
    {
        if (Routes.Any(existing => existing.Version == route.Version))
            throw new InvalidOperationException($"升級路徑版本 {route.Version} 已存在。 ");
        if (route.Version <= Routes.Max(existing => existing.Version))
            throw new InvalidOperationException("新升級路徑版本必須大於所有既有版本。 ");
        if (!string.Equals(route.Hops[0].SourceVersion, SourceVersion, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(route.Hops[^1].TargetVersion, TargetVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("新版升級路徑的起訖版本必須與案件相符。 ");

        return this with { CurrentRouteVersion = route.Version, Routes = [.. Routes, route] };
    }
}
