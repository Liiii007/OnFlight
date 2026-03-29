namespace OnFlight.Contracts.Sync;

public class SyncResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ItemsSynced { get; set; }
    public DateTime SyncedAt { get; set; }
}
