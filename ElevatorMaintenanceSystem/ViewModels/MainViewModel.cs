using CommunityToolkit.Mvvm.ComponentModel;
using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Infrastructure;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ElevatorMaintenanceSystem.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IMongoDbContext _context;
    private readonly MongoDbSettings _settings;
    private readonly ILogger<MainViewModel> _logger;

    public ElevatorManagementViewModel ElevatorManagement { get; }

    public WorkerManagementViewModel WorkerManagement { get; }

    public TicketManagementViewModel TicketManagement { get; }

    [ObservableProperty]
    private string _connectionStatus = "Connecting...";

    public MainViewModel(
        IMongoDbContext context,
        MongoDbSettings settings,
        ElevatorManagementViewModel elevatorManagement,
        WorkerManagementViewModel workerManagement,
        TicketManagementViewModel ticketManagement,
        ILogger<MainViewModel> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ElevatorManagement = elevatorManagement ?? throw new ArgumentNullException(nameof(elevatorManagement));
        WorkerManagement = workerManagement ?? throw new ArgumentNullException(nameof(workerManagement));
        TicketManagement = ticketManagement ?? throw new ArgumentNullException(nameof(ticketManagement));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _ = TestConnectionAsync();
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            _logger.LogInformation("Testing MongoDB connection...");

            // Run ping command to verify connection
            var command = new JsonCommand<BsonDocument>("{ ping: 1 }");
            var result = await _context.Database.RunCommandAsync(command);

            if (result != null)
            {
                ConnectionStatus = $"Connected to MongoDB: {_settings.DatabaseName}";
                _logger.LogInformation("Successfully connected to MongoDB database: {DatabaseName}", _settings.DatabaseName);

                await ElevatorManagement.LoadElevatorsAsync();
                await WorkerManagement.LoadWorkersAsync();
                await TicketManagement.LoadTicketsAsync();
            }
            else
            {
                ConnectionStatus = "Failed to connect to MongoDB";
                _logger.LogWarning("MongoDB ping returned null result");
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Connection Error: {ex.Message}";
            _logger.LogError(ex, "Failed to connect to MongoDB");
        }
    }
}
