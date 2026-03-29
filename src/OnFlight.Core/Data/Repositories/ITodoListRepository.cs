using System.Data;
using OnFlight.Core.Models;

namespace OnFlight.Core.Data.Repositories;

public interface ITodoListRepository
{
    Task<TodoList?> GetByIdAsync(Guid id);
    Task<IEnumerable<TodoList>> GetRootListsAsync();
    Task InsertAsync(TodoList list, IDbConnection? conn = null, IDbTransaction? tx = null);
    Task UpdateAsync(TodoList list);
    Task SoftDeleteAsync(Guid id, IDbConnection? conn = null, IDbTransaction? tx = null);
}
