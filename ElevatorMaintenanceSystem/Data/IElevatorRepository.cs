using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Data;

public interface IElevatorRepository : IMongoRepository<Elevator>
{
    Task<IEnumerable<Elevator>> GetActiveAsync();
}
