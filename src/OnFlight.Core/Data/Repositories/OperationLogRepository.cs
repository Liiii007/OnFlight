using Dapper;
using OnFlight.Core.Models;

namespace OnFlight.Core.Data.Repositories;

public class OperationLogRepository : IOperationLogRepository
{
    private readonly DbConnectionFactory _factory;

    public OperationLogRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<OperationLog>> GetByListIdAsync(Guid listId, int? limit = null)
    {
        using var db = _factory.CreateConnection();
        var sql = "SELECT * FROM operation_logs WHERE ListId = @ListId ORDER BY Timestamp DESC";
        if (limit.HasValue) sql += $" LIMIT {limit.Value}";
        return await db.QueryAsync<OperationLog>(sql, new { ListId = listId });
    }

    public async Task<IEnumerable<OperationLog>> SearchAsync(Guid listId, string? keyword, DateTime? from, DateTime? to)
    {
        using var db = _factory.CreateConnection();
        var sql = "SELECT * FROM operation_logs WHERE ListId = @ListId";
        if (!string.IsNullOrEmpty(keyword))
            sql += " AND Detail LIKE @Keyword";
        if (from.HasValue)
            sql += " AND Timestamp >= @From";
        if (to.HasValue)
            sql += " AND Timestamp <= @To";
        sql += " ORDER BY Timestamp DESC";
        return await db.QueryAsync<OperationLog>(sql, new
        {
            ListId = listId,
            Keyword = keyword != null ? $"%{keyword}%" : null,
            From = from,
            To = to
        });
    }

    public async Task InsertAsync(OperationLog log)
    {
        using var db = _factory.CreateConnection();
        await db.ExecuteAsync(@"
            INSERT INTO operation_logs (Id, ListId, OperationType, Detail, Timestamp, DeviceId)
            VALUES (@Id, @ListId, @OperationType, @Detail, @Timestamp, @DeviceId)",
            new { log.Id, log.ListId,
                  OperationType = (int)log.OperationType, log.Detail,
                  log.Timestamp, log.DeviceId });
    }
}
