using OnFlight.Contracts.Enums;
using OnFlight.Core.Models;

namespace OnFlight.Core.Services;

public interface ILogService
{
    Task LogAsync(Guid listId, OperationType operationType, string? detail = null);
    Task<IEnumerable<OperationLog>> GetLogsAsync(Guid listId, int? limit = null);
    Task<IEnumerable<OperationLog>> SearchLogsAsync(Guid listId, string? keyword = null, DateTime? from = null, DateTime? to = null);
}
