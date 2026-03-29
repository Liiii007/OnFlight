using OnFlight.Contracts.Sync;

namespace OnFlight.Core.Services;

public class NoOpSyncProvider : ISyncProvider
{
    public Task<SyncResult> PushChangesAsync(SyncPayload payload, CancellationToken ct = default)
        => Task.FromResult(new SyncResult { Success = true, SyncedAt = DateTime.UtcNow });

    public Task<SyncPayload> PullChangesAsync(DateTime since, CancellationToken ct = default)
        => Task.FromResult(new SyncPayload { Timestamp = DateTime.UtcNow });

    public Task<ConflictResolution> ResolveConflictAsync(SyncConflict conflict, CancellationToken ct = default)
        => Task.FromResult(new ConflictResolution { Strategy = ConflictStrategy.LastWriteWins });
}
