using System.Data;
using System.Text;
using Dapper;
using OnFlight.Core.Models;

namespace OnFlight.Core.Data.Repositories;

public class TodoItemRepository : ITodoItemRepository
{
    private readonly DbConnectionFactory _factory;

    public TodoItemRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<TodoItem?> GetByIdAsync(Guid id)
    {
        using var db = _factory.CreateConnection();
        return await db.QueryFirstOrDefaultAsync<TodoItem>(
            "SELECT * FROM todo_items WHERE Id = @Id AND IsDeleted = 0", new { Id = id });
    }

    public async Task<IEnumerable<TodoItem>> GetByListIdAsync(Guid listId)
    {
        using var db = _factory.CreateConnection();
        return await db.QueryAsync<TodoItem>(
            "SELECT * FROM todo_items WHERE ParentListId = @ListId AND IsDeleted = 0 ORDER BY SortOrder",
            new { ListId = listId });
    }

    public async Task InsertAsync(TodoItem item)
    {
        using var db = _factory.CreateConnection();
        await db.ExecuteAsync(@"
            INSERT INTO todo_items (Id, Title, Description, Status, SortOrder, ParentListId, SubListId, NodeType, FlowConfigJson, CreatedAt, UpdatedAt, DeviceId, IsDeleted)
            VALUES (@Id, @Title, @Description, @Status, @SortOrder, @ParentListId, @SubListId, @NodeType, @FlowConfigJson, @CreatedAt, @UpdatedAt, @DeviceId, @IsDeleted)",
            new { item.Id, item.Title, item.Description, Status = (int)item.Status,
                  item.SortOrder, item.ParentListId,
                  item.SubListId, NodeType = (int)item.NodeType,
                  item.FlowConfigJson, item.CreatedAt,
                  item.UpdatedAt, item.DeviceId,
                  IsDeleted = item.IsDeleted ? 1 : 0 });
    }

    public async Task UpdateAsync(TodoItem item, IDbConnection? conn = null, IDbTransaction? tx = null)
    {
        bool ownConnection = conn == null;
        var db = conn ?? _factory.CreateConnection();
        try
        {
            await db.ExecuteAsync(@"
                UPDATE todo_items SET Title = @Title, Description = @Description, Status = @Status,
                SortOrder = @SortOrder, SubListId = @SubListId, NodeType = @NodeType,
                FlowConfigJson = @FlowConfigJson, UpdatedAt = @UpdatedAt, DeviceId = @DeviceId,
                IsDeleted = @IsDeleted WHERE Id = @Id",
                new { item.Id, item.Title, item.Description, Status = (int)item.Status,
                      item.SortOrder, item.SubListId,
                      NodeType = (int)item.NodeType, item.FlowConfigJson,
                      item.UpdatedAt, item.DeviceId,
                      IsDeleted = item.IsDeleted ? 1 : 0 }, transaction: tx);
        }
        finally
        {
            if (ownConnection) db.Dispose();
        }
    }

    public async Task SoftDeleteAsync(Guid id, IDbConnection? conn = null, IDbTransaction? tx = null)
    {
        bool ownConnection = conn == null;
        var db = conn ?? _factory.CreateConnection();
        try
        {
            await db.ExecuteAsync(
                "UPDATE todo_items SET IsDeleted = 1, UpdatedAt = @Now WHERE Id = @Id",
                new { Id = id, Now = DateTime.UtcNow }, transaction: tx);
        }
        finally
        {
            if (ownConnection) db.Dispose();
        }
    }

    public async Task<int> GetMaxSortOrderAsync(Guid listId)
    {
        using var db = _factory.CreateConnection();
        return await db.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(SortOrder), -1) FROM todo_items WHERE ParentListId = @ListId AND IsDeleted = 0",
            new { ListId = listId });
    }

    public async Task BatchUpdateSortOrderAsync(Guid listId, List<(Guid id, int order)> items)
    {
        if (items.Count == 0) return;
        using var db = _factory.CreateConnection();

        var sb = new StringBuilder("UPDATE todo_items SET SortOrder = CASE Id ");
        var parameters = new DynamicParameters();
        parameters.Add("ListId", listId);
        parameters.Add("Now", DateTime.UtcNow);

        for (int i = 0; i < items.Count; i++)
        {
            sb.Append($"WHEN @Id{i} THEN @Order{i} ");
            parameters.Add($"Id{i}", items[i].id);
            parameters.Add($"Order{i}", items[i].order);
        }

        sb.Append("END, UpdatedAt = @Now WHERE ParentListId = @ListId AND Id IN (");
        sb.AppendJoin(", ", Enumerable.Range(0, items.Count).Select(i => $"@Id{i}"));
        sb.Append(')');

        await db.ExecuteAsync(sb.ToString(), parameters);
    }

    public async Task SoftDeleteByListIdAsync(Guid listId, IDbConnection? conn = null, IDbTransaction? tx = null)
    {
        bool ownConnection = conn == null;
        var db = conn ?? _factory.CreateConnection();
        try
        {
            await db.ExecuteAsync(
                "UPDATE todo_items SET IsDeleted = 1, UpdatedAt = @Now WHERE ParentListId = @ListId AND IsDeleted = 0",
                new { ListId = listId, Now = DateTime.UtcNow }, transaction: tx);
        }
        finally
        {
            if (ownConnection) db.Dispose();
        }
    }
}
