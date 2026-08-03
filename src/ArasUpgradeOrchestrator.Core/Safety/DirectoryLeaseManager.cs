using System.Diagnostics;
using System.Text.Json;

namespace ArasUpgradeOrchestrator.Core.Safety;

public sealed record DirectoryLeaseRecord(Guid LeaseId, int ProcessId, DateTimeOffset AcquiredAt, IReadOnlyList<string> WriteScopes);

public sealed class DirectoryLeaseManager
{
    private readonly string _leaseDirectory;
    private readonly string _coordinatorPath;

    public DirectoryLeaseManager(string toolDataDirectory)
    {
        var root = Path.GetFullPath(toolDataDirectory);
        _leaseDirectory = Path.Combine(root, "locks");
        Directory.CreateDirectory(_leaseDirectory);
        _coordinatorPath = Path.Combine(_leaseDirectory, "coordinator.lock");
    }

    public async Task<DirectoryLease> AcquireAsync(IEnumerable<string> writeScopes, CancellationToken cancellationToken = default)
    {
        var scopes = writeScopes.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (scopes.Length == 0) throw new ArgumentException("至少需要一個寫入目錄。", nameof(writeScopes));

        await using var coordinator = OpenCoordinator();
        var active = await ReadAndCleanActiveLeasesAsync(cancellationToken);
        var conflict = active.FirstOrDefault(lease => lease.WriteScopes.Any(existing => scopes.Any(requested => Overlaps(existing, requested))));
        if (conflict is not null)
            throw new InvalidOperationException($"工作目錄與租約 {conflict.LeaseId} 重疊，已阻擋後開始者。 ");

        var record = new DirectoryLeaseRecord(Guid.NewGuid(), Environment.ProcessId, DateTimeOffset.UtcNow, scopes);
        var path = GetLeasePath(record.LeaseId);
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, record, cancellationToken: cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        return new DirectoryLease(this, record);
    }

    internal async ValueTask ReleaseAsync(Guid leaseId)
    {
        await using var coordinator = OpenCoordinator();
        var path = GetLeasePath(leaseId);
        if (File.Exists(path)) File.Delete(path);
    }

    private FileStream OpenCoordinator() => new(_coordinatorPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private async Task<IReadOnlyList<DirectoryLeaseRecord>> ReadAndCleanActiveLeasesAsync(CancellationToken cancellationToken)
    {
        var active = new List<DirectoryLeaseRecord>();
        foreach (var path in Directory.EnumerateFiles(_leaseDirectory, "*.lease.json"))
        {
            DirectoryLeaseRecord? record;
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                record = await JsonSerializer.DeserializeAsync<DirectoryLeaseRecord>(stream, cancellationToken: cancellationToken);

            if (record is null || !IsProcessAlive(record.ProcessId))
            {
                File.Delete(path);
                continue;
            }
            active.Add(record);
        }
        return active;
    }

    private string GetLeasePath(Guid leaseId) => Path.Combine(_leaseDirectory, $"{leaseId:N}.lease.json");
    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool Overlaps(string left, string right) =>
        IsSameOrDescendant(left, right) || IsSameOrDescendant(right, left);

    private static bool IsSameOrDescendant(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsProcessAlive(int processId)
    {
        try { return !Process.GetProcessById(processId).HasExited; }
        catch (ArgumentException) { return false; }
    }
}

public sealed class DirectoryLease : IAsyncDisposable
{
    private readonly DirectoryLeaseManager _manager;
    private int _released;

    internal DirectoryLease(DirectoryLeaseManager manager, DirectoryLeaseRecord record)
    {
        _manager = manager;
        Record = record;
    }

    public DirectoryLeaseRecord Record { get; }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
            await _manager.ReleaseAsync(Record.LeaseId);
    }
}
