namespace OnFlight.Core.Models;

public class TodoList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid? ParentItemId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string DeviceId { get; set; } = Environment.MachineName;
    public bool IsDeleted { get; set; }

    public List<TodoItem> Items { get; set; } = new();
    public TodoItem? ParentItem { get; set; }
}
