namespace OnFlight.Contracts.Sync;

public interface ISyncProvider
{
    Task<SyncResult> PushChangesAsync(SyncPayload payload, CancellationToken ct = default);
    Task<SyncPayload> PullChangesAsync(DateTime since, CancellationToken ct = default);
    Task<ConflictResolution> ResolveConflictAsync(SyncConflict conflict, CancellationToken ct = default);
}
