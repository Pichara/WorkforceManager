using ElevatorMaintenanceSystem.Models;
using MongoDB.Driver;

namespace ElevatorMaintenanceSystem.Data;

public class WorkerRepository : MongoRepository<Worker>, IWorkerRepository
{
    private readonly IMongoCollection<Worker> _collection;
    private readonly FilterDefinitionBuilder<Worker> _filterBuilder = Builders<Worker>.Filter;

    public WorkerRepository(IMongoDbContext context)
        : base(context, "workers")
    {
        _collection = context.Database.GetCollection<Worker>("workers");
    }

    public async Task<IEnumerable<Worker>> GetActiveAsync()
    {
        var filter = _filterBuilder.Eq(x => x.DeletedAt, null as DateTime?);
        return await _collection.Find(filter).ToListAsync();
    }
}
