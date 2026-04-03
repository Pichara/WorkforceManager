using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Services;

public interface IWorkerService
{
    Task<IReadOnlyList<Worker>> GetActiveAsync();

    Task<Worker> CreateAsync(Worker worker, double latitude, double longitude);

    Task<Worker> UpdateAsync(Worker worker, double latitude, double longitude);

    Task<Worker> DeactivateAsync(Guid id);

    Task<Worker> UpdateLocationAsync(Guid id, double latitude, double longitude);
}
