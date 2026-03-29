using OnFlight.Contracts.Models;

namespace OnFlight.Core.Services;

public interface IRunningTaskService
{
    Task<RunningInstanceDto> CreateInstanceAsync(Guid listId);
    Task<IEnumerable<RunningInstanceDto>> GetAllInstancesAsync();
    Task SaveInstanceAsync(Guid instanceId, RunningInstanceDto dto);
    Task DeleteInstanceAsync(Guid instanceId);
}
