using Dapper;
using OnFlight.Core.Models;

namespace OnFlight.Core.Data.Repositories;

public class RunningInstanceRepository : IRunningInstanceRepository
{
    private readonly DbConnectionFactory _factory;

    public RunningInstanceRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<RunningInstance?> GetByIdAsync(Guid id)
    {
        using var db = _factory.CreateConnection();
        return await db.QueryFirstOrDefaultAsync<RunningInstance>(
            "SELECT * FROM running_instances WHERE Id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<RunningInstance>> GetAllAsync()
    {
        using var db = _factory.CreateConnection();
        return await db.QueryAsync<RunningInstance>(
            "SELECT * FROM running_instances ORDER BY CreatedAt DESC");
    }

    public async Task InsertAsync(RunningInstance instance)
    {
        using var db = _factory.CreateConnection();
        await db.ExecuteAsync(@"
            INSERT INTO running_instances (Id, SourceListId, ListName, StateJson, CreatedAt, UpdatedAt, DeviceId)
            VALUES (@Id, @SourceListId, @ListName, @StateJson, @CreatedAt, @UpdatedAt, @DeviceId)",
            new
            {
                instance.Id,
                instance.SourceListId,
                instance.ListName,
                instance.StateJson,
                instance.CreatedAt,
                instance.UpdatedAt,
                instance.DeviceId
            });
    }

    public async Task UpdateAsync(RunningInstance instance)
    {
        using var db = _factory.CreateConnection();
        await db.ExecuteAsync(@"
            UPDATE running_instances SET StateJson = @StateJson, UpdatedAt = @UpdatedAt
            WHERE Id = @Id",
            new
            {
                instance.Id,
                instance.StateJson,
                instance.UpdatedAt
            });
    }

    public async Task DeleteAsync(Guid id)
    {
        using var db = _factory.CreateConnection();
        await db.ExecuteAsync("DELETE FROM running_instances WHERE Id = @Id", new { Id = id });
    }
}
