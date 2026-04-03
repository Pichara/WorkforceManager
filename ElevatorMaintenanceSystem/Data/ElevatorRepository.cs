using ElevatorMaintenanceSystem.Models;
using MongoDB.Driver;

namespace ElevatorMaintenanceSystem.Data;

public class ElevatorRepository : MongoRepository<Elevator>, IElevatorRepository
{
    private readonly IMongoCollection<Elevator> _collection;
    private readonly FilterDefinitionBuilder<Elevator> _filterBuilder = Builders<Elevator>.Filter;

    public ElevatorRepository(IMongoDbContext context)
        : base(context, "elevators")
    {
        _collection = context.Database.GetCollection<Elevator>("elevators");
    }

    public async Task<IEnumerable<Elevator>> GetActiveAsync()
    {
        var filter = _filterBuilder.And(
            _filterBuilder.Eq(x => x.IsActive, true),
            _filterBuilder.Eq(x => x.DeletedAt, null as DateTime?));

        return await _collection.Find(filter).ToListAsync();
    }
}
