using System.Data;
using Dapper;
using OnFlight.Core.Models;

namespace OnFlight.Core.Data.Repositories;

public class TodoListRepository : ITodoListRepository
{
    private readonly DbConnectionFactory _factory;

    public TodoListRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<TodoList?> GetByIdAsync(Guid id)
    {
        using var db = _factory.CreateConnection();
        return await db.QueryFirstOrDefaultAsync<TodoList>(
            "SELECT * FROM todo_lists WHERE Id = @Id AND IsDeleted = 0", new { Id = id });
    }

    public async Task<IEnumerable<TodoList>> GetRootListsAsync()
    {
        using var db = _factory.CreateConnection();
        return await db.QueryAsync<TodoList>(
            "SELECT * FROM todo_lists WHERE ParentItemId IS NULL AND IsDeleted = 0 ORDER BY CreatedAt");
    }

    public async Task InsertAsync(TodoList list, IDbConnection? conn = null, IDbTransaction? tx = null)
    {
        bool ownConnection = conn == null;
        var db = conn ?? _factory.CreateConnection();
        try
        {
            await db.ExecuteAsync(@"
                INSERT INTO todo_lists (Id, Name, ParentItemId, CreatedAt, UpdatedAt, DeviceId, IsDeleted)
                VALUES (@Id, @Name, @ParentItemId, @CreatedAt, @UpdatedAt, @DeviceId, @IsDeleted)",
                new { list.Id, list.Name, list.ParentItemId,
                      list.CreatedAt, list.UpdatedAt,
                      list.DeviceId, IsDeleted = list.IsDeleted ? 1 : 0 }, transaction: tx);
        }
        finally
        {
            if (ownConnection) db.Dispose();
        }
    }

    public async Task UpdateAsync(TodoList list)
    {
        using var db = _factory.CreateConnection();
        await db.ExecuteAsync(@"
            UPDATE todo_lists SET Name = @Name, ParentItemId = @ParentItemId,
            UpdatedAt = @UpdatedAt, DeviceId = @DeviceId, IsDeleted = @IsDeleted WHERE Id = @Id",
            new { list.Id, list.Name, list.ParentItemId,
                  list.UpdatedAt, list.DeviceId, IsDeleted = list.IsDeleted ? 1 : 0 });
    }

    public async Task SoftDeleteAsync(Guid id, IDbConnection? conn = null, IDbTransaction? tx = null)
    {
        bool ownConnection = conn == null;
        var db = conn ?? _factory.CreateConnection();
        try
        {
            await db.ExecuteAsync(
                "UPDATE todo_lists SET IsDeleted = 1, UpdatedAt = @Now WHERE Id = @Id",
                new { Id = id, Now = DateTime.UtcNow }, transaction: tx);
        }
        finally
        {
            if (ownConnection) db.Dispose();
        }
    }
}
