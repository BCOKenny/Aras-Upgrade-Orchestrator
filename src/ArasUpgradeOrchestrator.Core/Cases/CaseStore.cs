using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Cases;

public sealed class CaseStore
{
    public const string ManifestFileName = "aras-upgrade-case.json";
    public const string ToolDataDirectoryName = ".orchestrator";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _caseRoot;

    public CaseStore(string caseRoot)
    {
        if (string.IsNullOrWhiteSpace(caseRoot)) throw new ArgumentException("案件根目錄不可為空。", nameof(caseRoot));
        _caseRoot = Path.GetFullPath(caseRoot);
    }

    public string CaseRoot => _caseRoot;
    public string ManifestPath => Path.Combine(_caseRoot, ManifestFileName);
    public string ToolDataPath => Path.Combine(_caseRoot, ToolDataDirectoryName);

    public async Task CreateAsync(CaseManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (File.Exists(ManifestPath)) throw new InvalidOperationException("案件根目錄已存在案件清單。 ");
        if (File.Exists(Path.Combine(ToolDataPath, "history.jsonl")))
            throw new InvalidOperationException("案件根目錄已存在正式執行歷程，拒絕建立新案件清單。 ");
        Directory.CreateDirectory(_caseRoot);
        Directory.CreateDirectory(ToolDataPath);
        await WriteAtomicallyAsync(manifest, cancellationToken);
    }

    public async Task<CaseManifest> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ManifestPath)) throw new FileNotFoundException("案件清單不存在，受控動作必須阻擋。", ManifestPath);
        await using var stream = new FileStream(ManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var manifest = await JsonSerializer.DeserializeAsync<CaseManifest>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("案件清單內容為空。 ");
        Validate(manifest);
        return manifest;
    }

    public async Task SavePlanningUpdateAsync(CaseManifest manifest, CancellationToken cancellationToken = default)
    {
        var existing = await LoadAsync(cancellationToken);
        if (existing.CaseId != manifest.CaseId) throw new InvalidOperationException("不得用其他案件覆寫案件清單。 ");
        Validate(manifest);
        foreach (var existingRoute in existing.Routes)
        {
            var candidate = manifest.Routes.SingleOrDefault(route => route.Version == existingRoute.Version);
            if (candidate is null || JsonSerializer.Serialize(candidate, JsonOptions) != JsonSerializer.Serialize(existingRoute, JsonOptions))
                throw new InvalidOperationException($"既有升級路徑版本 {existingRoute.Version} 不得修改或刪除；請建立新版路徑。 ");
        }
        foreach (var existingPreparation in existing.PackageComparisonPreparations)
        {
            var candidate = manifest.PackageComparisonPreparations.SingleOrDefault(item => item.PreparationId == existingPreparation.PreparationId);
            if (candidate is null || JsonSerializer.Serialize(candidate, JsonOptions) != JsonSerializer.Serialize(existingPreparation, JsonOptions))
                throw new InvalidOperationException($"既有 Package 比較計畫 {existingPreparation.PreparationId} 不得修改或刪除；請新增新的比較計畫。 ");
        }
        await WriteAtomicallyAsync(manifest, cancellationToken);
    }

    public async Task<CaseManifest> AddPackageComparisonPreparationAsync(
        PackageComparisonPreparation preparation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        var existing = await LoadAsync(cancellationToken);
        if (existing.PackageComparisonPreparations.Any(item => string.Equals(item.PreparationId, preparation.PreparationId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Package 比較計畫 {preparation.PreparationId} 已存在。 ");
        var updated = existing with { PackageComparisonPreparations = [.. existing.PackageComparisonPreparations, preparation] };
        await SavePlanningUpdateAsync(updated, cancellationToken);
        return updated;
    }

    private async Task WriteAtomicallyAsync(CaseManifest manifest, CancellationToken cancellationToken)
    {
        Validate(manifest);
        var temporaryPath = Path.Combine(_caseRoot, $".{ManifestFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, ManifestPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void Validate(CaseManifest manifest)
    {
        if (manifest.SchemaVersion != CaseManifest.CurrentSchemaVersion)
            throw new InvalidDataException($"不支援案件清單版本 {manifest.SchemaVersion}。 ");
        if (manifest.CaseId == Guid.Empty || string.IsNullOrWhiteSpace(manifest.CustomerCode) ||
            string.IsNullOrWhiteSpace(manifest.SourceVersion) || string.IsNullOrWhiteSpace(manifest.TargetVersion))
            throw new InvalidDataException("案件清單缺少必要的案件識別或版本。 ");
        if (manifest.Routes.Count == 0)
        {
            if (manifest.CurrentRouteVersion != 0)
                throw new InvalidDataException("未建立 Package／DB 工作流時目前升級路徑版本必須為 0。 ");
            if (manifest.CoreTreeComparison is null && manifest.PackageComparisonPreparations.Count == 0)
                throw new InvalidDataException("沒有升級路徑的案件必須包含 Core Tree 工作流設定或 Package 比較計畫。 ");
            manifest.CoreTreeComparison?.Validate();
        }
        if (manifest.Routes.Count > 0 && manifest.Routes.All(route => route.Version != manifest.CurrentRouteVersion))
            throw new InvalidDataException("案件清單沒有有效的目前升級路徑。 ");
        if (manifest.Routes.Select(route => route.Version).Distinct().Count() != manifest.Routes.Count)
            throw new InvalidDataException("案件清單包含重複的升級路徑版本。 ");
        manifest.CoreTreeComparison?.Validate();
        ValidatePreparations(manifest.PackageComparisonPreparations);
        foreach (var route in manifest.Routes)
        {
            var validatedRoute = UpgradeRoute.Create(route.Version, route.Hops, route.CreatedAt);
            _ = CaseManifest.Create(manifest.CaseId, manifest.CustomerCode, manifest.SourceVersion, manifest.TargetVersion, validatedRoute, manifest.CreatedAt, manifest.ArtifactLocations, manifest.CoreTreeComparison);
        }
    }

    private static void ValidatePreparations(IReadOnlyList<PackageComparisonPreparation> preparations)
    {
        if (preparations.Select(item => item.PreparationId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != preparations.Count)
            throw new InvalidDataException("案件清單包含重複的 Package 比較計畫識別。 ");
        foreach (var preparation in preparations)
        {
            if (string.IsNullOrWhiteSpace(preparation.PreparationId) || string.IsNullOrWhiteSpace(preparation.SourceVersion) ||
                string.IsNullOrWhiteSpace(preparation.TargetVersion) || string.IsNullOrWhiteSpace(preparation.CreatedBy))
                throw new InvalidDataException("Package 比較計畫缺少必要識別、版本或建立者。 ");
            if (string.Equals(preparation.SourceVersion, preparation.TargetVersion, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Package 比較計畫的來源與目標版本不可相同。 ");
            var paths = new[]
            {
                preparation.OotbSourceSolutionsRelativePath, preparation.OotbTargetSolutionsRelativePath,
                preparation.PatchSupportInputRelativePath, preparation.PreparationAttemptRelativePath
            };
            if (paths.Any(path => !IsSafeRelativePath(path)))
                throw new InvalidDataException("Package 比較計畫路徑必須是案件根目錄下的安全相對路徑。 ");
            if (paths.Take(3).Any(input => PathsOverlap(input, preparation.PreparationAttemptRelativePath)))
                throw new InvalidDataException("Package 比較 attempt 輸出不得與任何輸入目錄重疊。 ");
        }
    }

    private static bool IsSafeRelativePath(string? path) => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) &&
        !path.StartsWith('\\') && !path.StartsWith('/') && path.Split(new[] { '/', '\\' }, StringSplitOptions.None)
            .All(segment => segment is not "" and not "." and not "..");

    private static bool PathsOverlap(string left, string right)
    {
        var normalizedLeft = left.Replace('/', '\\').TrimEnd('\\');
        var normalizedRight = right.Replace('/', '\\').TrimEnd('\\');
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase) ||
               normalizedLeft.StartsWith(normalizedRight + "\\", StringComparison.OrdinalIgnoreCase) ||
               normalizedRight.StartsWith(normalizedLeft + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
