namespace OnFlight.Core.Models;

public class RunningInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceListId { get; set; }
    public string ListName { get; set; } = string.Empty;
    public string StateJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string DeviceId { get; set; } = Environment.MachineName;
}
