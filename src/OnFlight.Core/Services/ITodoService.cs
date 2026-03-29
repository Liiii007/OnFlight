using OnFlight.Contracts.Enums;
using OnFlight.Core.Models;

namespace OnFlight.Core.Services;

public interface ITodoService
{
    Task<TodoList> CreateListAsync(string name, Guid? parentItemId = null);
    Task<TodoList?> GetListAsync(Guid listId);
    Task<IEnumerable<TodoList>> GetRootListsAsync();
    Task UpdateListAsync(TodoList list);
    Task DeleteListAsync(Guid listId);
    Task<TodoItem> AddItemAsync(Guid listId, string title, FlowNodeType nodeType = FlowNodeType.Task);
    Task<TodoItem?> GetItemAsync(Guid itemId);
    Task UpdateItemAsync(TodoItem item);
    Task DeleteItemAsync(Guid itemId);
    Task SetItemStatusAsync(Guid itemId, TodoStatus status);
    Task ReorderItemsAsync(Guid listId, List<Guid> orderedIds);
    Task<TodoList> CreateSubListAsync(Guid parentItemId, string name);

    /// <summary>
    /// Check whether setting a fork node in <paramref name="sourceListId"/> to target
    /// <paramref name="targetListId"/> would create a cycle in the fork dependency graph.
    /// Returns true if a cycle would be formed.
    /// </summary>
    Task<bool> WouldCreateCycleAsync(Guid sourceListId, Guid targetListId);
}
