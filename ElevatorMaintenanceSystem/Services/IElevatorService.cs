using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Services;

public interface IElevatorService
{
    Task<IReadOnlyList<Elevator>> GetActiveAsync();

    Task<Elevator> CreateAsync(Elevator elevator, double latitude, double longitude);

    Task<Elevator> UpdateAsync(Elevator elevator, double latitude, double longitude);

    Task DeleteInactiveAsync(Guid id);
}
