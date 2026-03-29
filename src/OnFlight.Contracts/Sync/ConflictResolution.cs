namespace OnFlight.Contracts.Sync;

public enum ConflictStrategy
{
    LocalWins,
    RemoteWins,
    LastWriteWins,
    Manual
}

public class ConflictResolution
{
    public ConflictStrategy Strategy { get; set; }
    public string? ResolvedData { get; set; }
}

public class SyncConflict
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string LocalData { get; set; } = string.Empty;
    public string RemoteData { get; set; } = string.Empty;
    public DateTime LocalUpdatedAt { get; set; }
    public DateTime RemoteUpdatedAt { get; set; }
}
