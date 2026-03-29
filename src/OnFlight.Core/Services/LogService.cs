using OnFlight.Contracts.Enums;
using OnFlight.Core.Data.Repositories;
using OnFlight.Core.Models;

namespace OnFlight.Core.Services;

public class LogService : ILogService
{
    private readonly IOperationLogRepository _logRepo;

    public LogService(IOperationLogRepository logRepo) => _logRepo = logRepo;

    public async Task LogAsync(Guid listId, OperationType operationType, string? detail = null)
    {
        if (listId == Guid.Empty) throw new ArgumentException("Value cannot be empty.", nameof(listId));
        var log = new OperationLog
        {
            ListId = listId,
            OperationType = operationType,
            Detail = detail
        };
        await _logRepo.InsertAsync(log);
    }

    public Task<IEnumerable<OperationLog>> GetLogsAsync(Guid listId, int? limit = null)
    {
        if (listId == Guid.Empty) throw new ArgumentException("Value cannot be empty.", nameof(listId));
        return _logRepo.GetByListIdAsync(listId, limit);
    }

    public Task<IEnumerable<OperationLog>> SearchLogsAsync(Guid listId, string? keyword = null, DateTime? from = null, DateTime? to = null)
    {
        if (listId == Guid.Empty) throw new ArgumentException("Value cannot be empty.", nameof(listId));
        return _logRepo.SearchAsync(listId, keyword, from, to);
    }
}
