using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ElevatorMaintenanceSystem.Infrastructure;

/// <summary>
/// Hosted service to initialize database and create indexes on startup
/// </summary>
public class DatabaseInitializer : IHostedService
{
    private readonly IMongoDbContext _context;
    private readonly MongoDbSettings _settings;
    private readonly GpsCoordinateValidator _gpsCoordinateValidator;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IMongoDbContext context,
        IOptions<MongoDbSettings> settings,
        GpsCoordinateValidator gpsCoordinateValidator,
        ILogger<DatabaseInitializer> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _gpsCoordinateValidator = gpsCoordinateValidator ?? throw new ArgumentNullException(nameof(gpsCoordinateValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing MongoDB database: {DatabaseName}", _settings.DatabaseName);

            // Test connection
            var command = new JsonCommand<BsonDocument>("{ ping: 1 }");
            await _context.Database.RunCommandAsync(command);
            _logger.LogInformation("Successfully connected to MongoDB");

            // Create collections if they don't exist
            await CreateCollectionsAsync(cancellationToken);

            // Create indexes
            await CreateIndexesAsync(cancellationToken);

            await SeedMockElevatorsAsync(cancellationToken);

            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MongoDB database");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task CreateCollectionsAsync(CancellationToken cancellationToken)
    {
        var cursor = await _context.Database.ListCollectionNamesAsync(cancellationToken: cancellationToken);
        var collectionNames = await cursor.ToListAsync(cancellationToken);

        // Define collection names - these will be created when documents are first inserted
        // We pre-create them here to set up indexes
        var requiredCollections = new[]
        {
            "elevators",
            "workers",
            "tickets"
        };

        foreach (var collectionName in requiredCollections)
        {
            if (!collectionNames.Contains(collectionName))
            {
                await _context.Database.CreateCollectionAsync(collectionName, cancellationToken: cancellationToken);
                _logger.LogInformation("Created collection: {CollectionName}", collectionName);
            }
        }
    }

    private async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        // Create 2dsphere index for GPS coordinates on Elevators collection
        var elevatorsCollection = _context.Database.GetCollection<Elevator>("elevators");
        var elevatorsIndexModel = new CreateIndexModel<Elevator>(
            Builders<Elevator>.IndexKeys.Geo2DSphere(x => x.Location),
            new CreateIndexOptions { Name = "location_2dsphere" });

        await elevatorsCollection.Indexes.CreateOneAsync(elevatorsIndexModel);
        _logger.LogInformation("Created 2dsphere index on Elevators.Location");

        // Create 2dsphere index for GPS coordinates on Workers collection
        var workersCollection = _context.Database.GetCollection<Worker>("workers");
        var workersIndexModel = new CreateIndexModel<Worker>(
            Builders<Worker>.IndexKeys.Geo2DSphere(x => x.Location),
            new CreateIndexOptions { Name = "location_2dsphere" });

        await workersCollection.Indexes.CreateOneAsync(workersIndexModel);
        _logger.LogInformation("Created 2dsphere index on Workers.Location");

        // Create standard indexes for common queries
        await CreateStandardIndexesAsync(cancellationToken);
    }

    private async Task CreateStandardIndexesAsync(CancellationToken cancellationToken)
    {
        // Index for soft-delete queries
        var elevatorsCollection = _context.Database.GetCollection<Elevator>("elevators");
        var deletedAtIndex = Builders<Elevator>.IndexKeys.Ascending(x => x.DeletedAt);
        await elevatorsCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<Elevator>(deletedAtIndex, new CreateIndexOptions { Name = "deleted_at_idx" }));

        var workersCollection = _context.Database.GetCollection<Worker>("workers");
        await workersCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<Worker>(Builders<Worker>.IndexKeys.Ascending(x => x.DeletedAt),
                new CreateIndexOptions { Name = "deleted_at_idx" }));

        var ticketsCollection = _context.Database.GetCollection<BsonDocument>("tickets");
        await ticketsCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("DeletedAt"),
                new CreateIndexOptions { Name = "deleted_at_idx" }),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created standard indexes for queries");
    }

    private async Task SeedMockElevatorsAsync(CancellationToken cancellationToken)
    {
        var elevatorsCollection = _context.Database.GetCollection<Elevator>("elevators");
        var elevatorCount = await elevatorsCollection.CountDocumentsAsync(
            Builders<Elevator>.Filter.Empty,
            cancellationToken: cancellationToken);

        if (elevatorCount > 0)
        {
            _logger.LogInformation("Skipping mock elevator seed because collection already contains data");
            return;
        }

        var mockElevators = new[]
        {
            new Elevator
            {
                Name = "North Tower Passenger Lift A",
                Address = "350 5th Ave, New York, NY 10118",
                BuildingName = "North Tower",
                FloorLabel = "Lobby-25",
                Manufacturer = "Otis",
                InstallationDate = new DateTime(2018, 5, 12, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                Location = _gpsCoordinateValidator.CreatePoint(40.7484, -73.9857)
            },
            new Elevator
            {
                Name = "West Annex Service Lift 2",
                Address = "11 Madison Ave, New York, NY 10010",
                BuildingName = "West Annex",
                FloorLabel = "B2-18",
                Manufacturer = "KONE",
                InstallationDate = new DateTime(2020, 9, 3, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                Location = _gpsCoordinateValidator.CreatePoint(40.7411, -73.9872)
            },
            new Elevator
            {
                Name = "River Plaza Freight Lift",
                Address = "200 Vesey St, New York, NY 10281",
                BuildingName = "River Plaza",
                FloorLabel = "Dock-12",
                Manufacturer = "Schindler",
                InstallationDate = new DateTime(2016, 2, 18, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                Location = _gpsCoordinateValidator.CreatePoint(40.7148, -74.0153)
            }
        };

        await elevatorsCollection.InsertManyAsync(mockElevators, cancellationToken: cancellationToken);
        _logger.LogInformation("Seeded {ElevatorCount} mock elevators", mockElevators.Length);
    }
}
