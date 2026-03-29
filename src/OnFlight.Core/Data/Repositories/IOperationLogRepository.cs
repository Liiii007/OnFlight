using OnFlight.Core.Models;

namespace OnFlight.Core.Data.Repositories;

public interface IOperationLogRepository
{
    Task<IEnumerable<OperationLog>> GetByListIdAsync(Guid listId, int? limit = null);
    Task<IEnumerable<OperationLog>> SearchAsync(Guid listId, string? keyword, DateTime? from, DateTime? to);
    Task InsertAsync(OperationLog log);
}
