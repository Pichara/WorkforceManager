using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Data;

public interface IWorkerRepository : IMongoRepository<Worker>
{
    Task<IEnumerable<Worker>> GetActiveAsync();
}
