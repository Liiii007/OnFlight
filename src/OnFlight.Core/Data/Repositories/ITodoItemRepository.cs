using System.Data;
using OnFlight.Core.Models;

namespace OnFlight.Core.Data.Repositories;

public interface ITodoItemRepository
{
    Task<TodoItem?> GetByIdAsync(Guid id);
    Task<IEnumerable<TodoItem>> GetByListIdAsync(Guid listId);
    Task InsertAsync(TodoItem item);
    Task UpdateAsync(TodoItem item, IDbConnection? conn = null, IDbTransaction? tx = null);
    Task SoftDeleteAsync(Guid id, IDbConnection? conn = null, IDbTransaction? tx = null);
    Task<int> GetMaxSortOrderAsync(Guid listId);
    Task BatchUpdateSortOrderAsync(Guid listId, List<(Guid id, int order)> items);
    Task SoftDeleteByListIdAsync(Guid listId, IDbConnection? conn = null, IDbTransaction? tx = null);
}
