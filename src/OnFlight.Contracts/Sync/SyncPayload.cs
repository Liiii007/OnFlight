using OnFlight.Contracts.Models;

namespace OnFlight.Contracts.Sync;

public class SyncPayload
{
    public DateTime Timestamp { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public List<TodoListDto> Lists { get; set; } = new();
    public List<TodoItemDto> Items { get; set; } = new();
}
