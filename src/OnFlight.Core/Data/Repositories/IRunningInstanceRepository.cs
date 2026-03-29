using OnFlight.Core.Models;

namespace OnFlight.Core.Data.Repositories;

public interface IRunningInstanceRepository
{
    Task<RunningInstance?> GetByIdAsync(Guid id);
    Task<IEnumerable<RunningInstance>> GetAllAsync();
    Task InsertAsync(RunningInstance instance);
    Task UpdateAsync(RunningInstance instance);
    Task DeleteAsync(Guid id);
}
