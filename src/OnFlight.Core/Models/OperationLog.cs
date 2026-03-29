using OnFlight.Contracts.Enums;

namespace OnFlight.Core.Models;

public class OperationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListId { get; set; }
    public OperationType OperationType { get; set; }
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string DeviceId { get; set; } = Environment.MachineName;
}
