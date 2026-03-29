using OnFlight.Contracts.Enums;

namespace OnFlight.Core.Models;

public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TodoStatus Status { get; set; } = TodoStatus.Pending;
    public int SortOrder { get; set; }
    public Guid ParentListId { get; set; }
    public Guid? SubListId { get; set; }
    public FlowNodeType NodeType { get; set; } = FlowNodeType.Task;
    public string? FlowConfigJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string DeviceId { get; set; } = Environment.MachineName;
    public bool IsDeleted { get; set; }

    public TodoList? SubList { get; set; }
    public TodoList? ParentList { get; set; }
}
