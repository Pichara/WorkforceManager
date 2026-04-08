using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;
using ElevatorMaintenanceSystem.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;

namespace ElevatorMaintenanceSystem.Tests.ViewModels;

public class MapViewModelDispatchTests
{
    [Fact]
    public async Task MAP_05_D_01_D_02_HandleWorkerDroppedOnElevatorAsync_WithoutSelectedTicket_ShowsUserErrorStatusAndSkipsAssignment()
    {
        var dispatchService = new StubMapDispatchService();
        var viewModel = CreateViewModel(dispatchService);

        await viewModel.HandleWorkerDroppedOnElevatorAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(0, dispatchService.AssignCallCount);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.MapErrorMessage));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        Assert.Contains("ticket", viewModel.MapErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ticket", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MAP_06_D_03_HandleElevatorFocusedAsync_LoadsSelectedElevatorTicketContext()
    {
        var elevatorId = Guid.NewGuid();
        var dispatchService = new StubMapDispatchService
        {
            ContextToReturn = new ElevatorTicketContext(
                elevatorId,
                [
                    new ElevatorTicketSummary(Guid.NewGuid(), "Door issue", Models.TicketPriority.Critical, Models.TicketStatus.Pending, new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), null)
                ])
        };
        var viewModel = CreateViewModel(dispatchService);

        await viewModel.HandleElevatorFocusedAsync(elevatorId);

        Assert.Equal(elevatorId, viewModel.SelectedElevatorId);
        Assert.Single(viewModel.SelectedElevatorTickets);
        Assert.Equal(1, dispatchService.LoadCallCount);
    }

    [Fact]
    public async Task MAP_08_MAP_09_MAP_10_SelectedTicketRefreshesWorkerSuggestionsWithRankingAndDistance()
    {
        var elevatorId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var workerA = Guid.NewGuid();
        var workerB = Guid.NewGuid();
        var dispatchService = new StubMapDispatchService
        {
            ContextToReturn = new ElevatorTicketContext(
                elevatorId,
                [
                    new ElevatorTicketSummary(ticketId, "Door issue", TicketPriority.High, TicketStatus.Pending, new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), null)
                ])
        };

        var mapDataService = new StubMapDataService
        {
            SnapshotToReturn = CreateMapSnapshot(elevatorId, workerA, workerB)
        };

        var proximityService = new StubProximityRankingService
        {
            ResultFactory = request =>
            [
                new WorkerProximitySuggestion(workerB, "Worker B", WorkerAvailabilityStatus.Available, 1.25, 1),
                new WorkerProximitySuggestion(workerA, "Worker A", WorkerAvailabilityStatus.Unavailable, 2.50, 2)
            ]
        };

        var viewModel = CreateViewModel(dispatchService, mapDataService, proximityService);
        await viewModel.LoadMapAsync();
        await viewModel.HandleElevatorFocusedAsync(elevatorId);

        viewModel.SelectedTicketId = ticketId;

        var suggestions = GetWorkerSuggestions(viewModel);

        Assert.Equal(2, suggestions.Count);
        Assert.Equal(1, GetIntProperty(suggestions[0], "Rank"));
        Assert.Equal(2, GetIntProperty(suggestions[1], "Rank"));
        Assert.Equal("Worker B", GetStringProperty(suggestions[0], "DisplayName", "WorkerName"));
        Assert.Equal("Worker A", GetStringProperty(suggestions[1], "DisplayName", "WorkerName"));
        Assert.Contains("km", GetStringProperty(suggestions[0], "DistanceText"));
        Assert.True(proximityService.Requests.Any(r => r.SelectedTicketId == ticketId));
    }

    [Fact]
    public async Task MAP_08_D_04_ChangingSelectedTicketScopesSuggestionsToCurrentTicket()
    {
        var elevatorId = Guid.NewGuid();
        var ticketA = Guid.NewGuid();
        var ticketB = Guid.NewGuid();
        var worker = Guid.NewGuid();
        var dispatchService = new StubMapDispatchService
        {
            ContextToReturn = new ElevatorTicketContext(
                elevatorId,
                [
                    new ElevatorTicketSummary(ticketA, "Ticket A", TicketPriority.High, TicketStatus.Pending, new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc), null),
                    new ElevatorTicketSummary(ticketB, "Ticket B", TicketPriority.Medium, TicketStatus.Pending, new DateTime(2026, 4, 3, 1, 0, 0, DateTimeKind.Utc), null)
                ])
        };

        var mapDataService = new StubMapDataService
        {
            SnapshotToReturn = CreateMapSnapshot(elevatorId, worker, Guid.NewGuid())
        };

        var proximityService = new StubProximityRankingService
        {
            ResultFactory = request =>
            {
                if (request.SelectedTicketId == ticketA)
                {
                    return [new WorkerProximitySuggestion(worker, "Ticket-A Worker", WorkerAvailabilityStatus.Available, 1.00, 1)];
                }

                return [new WorkerProximitySuggestion(worker, "Ticket-B Worker", WorkerAvailabilityStatus.Available, 2.00, 1)];
            }
        };

        var viewModel = CreateViewModel(dispatchService, mapDataService, proximityService);
        await viewModel.LoadMapAsync();
        await viewModel.HandleElevatorFocusedAsync(elevatorId);

        viewModel.SelectedTicketId = ticketA;
        viewModel.SelectedTicketId = ticketB;

        var suggestions = GetWorkerSuggestions(viewModel);

        Assert.Equal(2, proximityService.Requests.Count);
        Assert.Equal(ticketA, proximityService.Requests[0].SelectedTicketId);
        Assert.Equal(ticketB, proximityService.Requests[1].SelectedTicketId);
        Assert.Equal("Ticket-B Worker", GetStringProperty(suggestions.Single(), "DisplayName", "WorkerName"));
    }

    private static MapViewModel CreateViewModel(
        IMapDispatchService dispatchService,
        IMapDataService? mapDataService = null,
        IProximityRankingService? proximityService = null)
    {
        var dataService = mapDataService ?? new StubMapDataService();
        var settings = new MapSettings
        {
            DefaultCenterLatitude = 43.4516,
            DefaultCenterLongitude = -80.4925,
            DefaultZoom = 10,
            DefaultBaseLayer = "standard"
        };

        var rankingService = proximityService ?? new StubProximityRankingService();
        var mapViewModelType = typeof(MapViewModel);
        var constructor = mapViewModelType.GetConstructor(
        [
            typeof(IMapDataService),
            typeof(MapSettings),
            typeof(IMapDispatchService),
            typeof(IProximityRankingService),
            typeof(Microsoft.Extensions.Logging.ILogger<MapViewModel>)
        ]);

        if (constructor != null)
        {
            return (MapViewModel)constructor.Invoke(
            [
                dataService,
                settings,
                dispatchService,
                rankingService,
                NullLogger<MapViewModel>.Instance
            ]);
        }

        return new MapViewModel(dataService, settings, dispatchService, NullLogger<MapViewModel>.Instance);
    }

    private sealed class StubMapDispatchService : IMapDispatchService
    {
        public int AssignCallCount { get; private set; }
        public int LoadCallCount { get; private set; }
        public ElevatorTicketContext? ContextToReturn { get; set; }

        public Task<ElevatorTicketContext> LoadElevatorTicketContextAsync(Guid elevatorId, CancellationToken cancellationToken = default)
        {
            LoadCallCount++;
            return Task.FromResult(ContextToReturn ?? new ElevatorTicketContext(elevatorId, []));
        }

        public Task<MapAssignmentResult> AssignWorkerToTicketAsync(Guid ticketId, Guid workerId, CancellationToken cancellationToken = default)
        {
            AssignCallCount++;
            return Task.FromResult(new MapAssignmentResult(true, ticketId, workerId, "Worker assigned."));
        }
    }

    private static IReadOnlyList<object> GetWorkerSuggestions(MapViewModel viewModel)
    {
        var property = typeof(MapViewModel).GetProperty("WorkerSuggestions");
        Assert.NotNull(property);

        var value = property!.GetValue(viewModel);
        Assert.NotNull(value);
        Assert.IsAssignableFrom<IEnumerable>(value);

        return ((IEnumerable)value).Cast<object>().ToList();
    }

    private static string GetStringProperty(object value, params string[] names)
    {
        foreach (var name in names)
        {
            var property = value.GetType().GetProperty(name);
            if (property != null && property.PropertyType == typeof(string))
            {
                return (string)(property.GetValue(value) ?? string.Empty);
            }
        }

        throw new InvalidOperationException($"Expected one of string properties: {string.Join(", ", names)}");
    }

    private static int GetIntProperty(object value, string name)
    {
        var property = value.GetType().GetProperty(name);
        Assert.NotNull(property);
        Assert.Equal(typeof(int), property!.PropertyType);
        return (int)(property.GetValue(value) ?? 0);
    }

    private static MapDataSnapshot CreateMapSnapshot(Guid elevatorId, Guid workerA, Guid workerB)
    {
        return new MapDataSnapshot(
            CenterLatitude: 43.4516,
            CenterLongitude: -80.4925,
            Zoom: 10,
            Markers:
            [
                new MapMarkerSnapshot(
                    elevatorId.ToString(),
                    MapMarkerKind.Elevator,
                    43.4516,
                    -80.4925,
                    "Selected Elevator",
                    ["100 Main St", "Alpha / L1"]),
                new MapMarkerSnapshot(
                    workerA.ToString(),
                    MapMarkerKind.AvailableWorker,
                    43.4520,
                    -80.4922,
                    "Worker A",
                    ["Status: Available", "Skills: Repair"]),
                new MapMarkerSnapshot(
                    workerB.ToString(),
                    MapMarkerKind.UnavailableWorker,
                    43.4530,
                    -80.4919,
                    "Worker B",
                    ["Status: Unavailable", "Skills: Inspection"])
            ],
            StandardTiles: new TileProviderSnapshot("OpenStreetMap", "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", "© OpenStreetMap", 19, null),
            SatelliteTiles: new TileProviderSnapshot("ArcGIS", "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", "© Esri", 19, null));
    }

    private sealed class StubMapDataService : IMapDataService
    {
        public MapDataSnapshot? SnapshotToReturn { get; set; }

        public Task<MapDataSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SnapshotToReturn ?? new MapDataSnapshot(
                CenterLatitude: 43.4516,
                CenterLongitude: -80.4925,
                Zoom: 10,
                Markers: [],
                StandardTiles: new TileProviderSnapshot("OpenStreetMap", "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", "© OpenStreetMap", 19, null),
                SatelliteTiles: new TileProviderSnapshot("ArcGIS", "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", "© Esri", 19, null)));
        }
    }

    private sealed class StubProximityRankingService : IProximityRankingService
    {
        public List<ProximityRankRequest> Requests { get; } = [];
        public Func<ProximityRankRequest, IReadOnlyList<WorkerProximitySuggestion>> ResultFactory { get; set; } = _ => [];

        public IReadOnlyList<WorkerProximitySuggestion> RankWorkers(ProximityRankRequest request, int maxResults = 10)
        {
            Requests.Add(request);
            return ResultFactory(request);
        }
    }
}
