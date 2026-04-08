using CommunityToolkit.Mvvm.ComponentModel;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Infrastructure.Commands;
using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace ElevatorMaintenanceSystem.ViewModels;

/// <summary>
/// ViewModel for the map visualization workspace
/// </summary>
public partial class MapViewModel : ViewModelBase
{
    private readonly IMapDataService _mapDataService;
    private readonly IMapDispatchService _mapDispatchService;
    private readonly IProximityRankingService _proximityRankingService;
    private readonly MapSettings _mapSettings;
    private readonly ILogger<MapViewModel> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private IReadOnlyList<MapMarkerSnapshot> _latestMarkers = [];

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string _selectedItemTitle = string.Empty;

    [ObservableProperty]
    private string _selectedItemDetail = string.Empty;

    [ObservableProperty]
    private Guid? _selectedElevatorId;

    [ObservableProperty]
    private Guid? _selectedTicketId;

    [ObservableProperty]
    private string _selectedBaseLayer = string.Empty;

    [ObservableProperty]
    private double _currentCenterLatitude;

    [ObservableProperty]
    private double _currentCenterLongitude;

    [ObservableProperty]
    private int _currentZoom;

    [ObservableProperty]
    private string _lastSnapshotJson = string.Empty;

    [ObservableProperty]
    private string _mapErrorMessage = string.Empty;

    public ObservableCollection<ElevatorTicketSummary> SelectedElevatorTickets { get; } = [];
    public ObservableCollection<WorkerSuggestionRow> WorkerSuggestions { get; } = [];

    public AsyncRelayCommand RefreshMapCommand { get; }

    public MapViewModel(
        IMapDataService mapDataService,
        MapSettings mapSettings,
        ILogger<MapViewModel> logger)
        : this(mapDataService, mapSettings, new NoOpMapDispatchService(), new NoOpProximityRankingService(), logger)
    {
    }

    public MapViewModel(
        IMapDataService mapDataService,
        MapSettings mapSettings,
        IMapDispatchService mapDispatchService,
        ILogger<MapViewModel> logger)
        : this(mapDataService, mapSettings, mapDispatchService, new NoOpProximityRankingService(), logger)
    {
    }

    public MapViewModel(
        IMapDataService mapDataService,
        MapSettings mapSettings,
        IMapDispatchService mapDispatchService,
        IProximityRankingService proximityRankingService,
        ILogger<MapViewModel> logger)
    {
        _mapDataService = mapDataService ?? throw new ArgumentNullException(nameof(mapDataService));
        _mapSettings = mapSettings ?? throw new ArgumentNullException(nameof(mapSettings));
        _mapDispatchService = mapDispatchService ?? throw new ArgumentNullException(nameof(mapDispatchService));
        _proximityRankingService = proximityRankingService ?? throw new ArgumentNullException(nameof(proximityRankingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Initialize with default settings
        CurrentCenterLatitude = _mapSettings.DefaultCenterLatitude;
        CurrentCenterLongitude = _mapSettings.DefaultCenterLongitude;
        CurrentZoom = _mapSettings.DefaultZoom;
        SelectedBaseLayer = _mapSettings.DefaultBaseLayer;

        RefreshMapCommand = new AsyncRelayCommand(RefreshMapAsync, () => !IsBusy);
    }

    partial void OnIsBusyChanged(bool value) => RefreshMapCommand.RaiseCanExecuteChanged();
    partial void OnSelectedTicketIdChanged(Guid? value) => RefreshWorkerSuggestions();

    /// <summary>
    /// Load the map for the first time using Waterloo/Kitchener defaults
    /// </summary>
    public async Task LoadMapAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            StatusMessage = "Loading map data...";
            MapErrorMessage = string.Empty;

            var snapshot = await _mapDataService.BuildSnapshotAsync();

            // First load: use Waterloo/Kitchener defaults from MapSettings
            CurrentCenterLatitude = _mapSettings.DefaultCenterLatitude;
            CurrentCenterLongitude = _mapSettings.DefaultCenterLongitude;
            CurrentZoom = _mapSettings.DefaultZoom;
            SelectedBaseLayer = _mapSettings.DefaultBaseLayer;

            LastSnapshotJson = JsonSerializer.Serialize(new
            {
                centerLat = CurrentCenterLatitude,
                centerLng = CurrentCenterLongitude,
                zoom = CurrentZoom,
                defaultBaseLayer = SelectedBaseLayer,
                markers = snapshot.Markers,
                standardTiles = snapshot.StandardTiles,
                satelliteTiles = snapshot.SatelliteTiles
            }, _jsonOptions);
            _latestMarkers = snapshot.Markers;
            RefreshWorkerSuggestions();

            IsInitialized = true;
            StatusMessage = "Map loaded.";
        }, "Loading map data failed.");
    }

    /// <summary>
    /// Refresh map data while preserving current center, zoom, and selected base layer
    /// </summary>
    public async Task RefreshMapAsync()
    {
        if (!IsInitialized)
        {
            await LoadMapAsync();
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            StatusMessage = "Refreshing map data...";
            MapErrorMessage = string.Empty;

            var snapshot = await _mapDataService.BuildSnapshotAsync();

            // Preserve current map state
            LastSnapshotJson = JsonSerializer.Serialize(new
            {
                centerLat = CurrentCenterLatitude,
                centerLng = CurrentCenterLongitude,
                zoom = CurrentZoom,
                defaultBaseLayer = SelectedBaseLayer,
                markers = snapshot.Markers,
                standardTiles = snapshot.StandardTiles,
                satelliteTiles = snapshot.SatelliteTiles
            }, _jsonOptions);
            _latestMarkers = snapshot.Markers;
            RefreshWorkerSuggestions();

            StatusMessage = "Map refreshed.";
        }, "Refreshing map data failed.");
    }

    /// <summary>
    /// Apply map state updates from the browser (center, zoom, base layer)
    /// </summary>
    public void ApplyMapState(double centerLat, double centerLng, int zoom, string baseLayer)
    {
        CurrentCenterLatitude = centerLat;
        CurrentCenterLongitude = centerLng;
        CurrentZoom = zoom;
        SelectedBaseLayer = baseLayer;
    }

    /// <summary>
    /// Set the selected item from a marker click
    /// </summary>
    public void SetSelectedItem(string title, string detail)
    {
        SelectedItemTitle = title;
        SelectedItemDetail = detail;
    }

    /// <summary>
    /// Clear the selected item (map background click)
    /// </summary>
    public void ClearSelectedItem()
    {
        SelectedItemTitle = string.Empty;
        SelectedItemDetail = string.Empty;
    }

    public async Task HandleElevatorFocusedAsync(
        Guid elevatorId,
        string? elevatorTitle = null,
        CancellationToken cancellationToken = default)
    {
        await RunBusyOperationAsync(async () =>
        {
            StatusMessage = "Loading elevator ticket context...";
            MapErrorMessage = string.Empty;

            var context = await _mapDispatchService.LoadElevatorTicketContextAsync(elevatorId, cancellationToken);

            SelectedElevatorId = context.ElevatorId;
            ReplaceCollection(SelectedElevatorTickets, context.ActiveTickets);

            if (SelectedTicketId.HasValue && !SelectedElevatorTickets.Any(ticket => ticket.TicketId == SelectedTicketId.Value))
            {
                SelectedTicketId = null;
            }
            RefreshWorkerSuggestions();

            if (!string.IsNullOrWhiteSpace(elevatorTitle))
            {
                SelectedItemTitle = elevatorTitle;
            }

            StatusMessage = SelectedElevatorTickets.Count == 0
                ? "No active tickets found for the selected elevator."
                : $"Loaded {SelectedElevatorTickets.Count} active ticket(s) for the selected elevator.";
        }, "Loading elevator ticket context failed.");
    }

    public async Task HandleWorkerDroppedOnElevatorAsync(
        Guid workerId,
        Guid elevatorId,
        string? workerTitle = null,
        string? elevatorTitle = null,
        CancellationToken cancellationToken = default)
    {
        if (!SelectedTicketId.HasValue)
        {
            const string missingTicketMessage = "Select an elevator ticket before assigning a worker.";
            MapErrorMessage = missingTicketMessage;
            StatusMessage = missingTicketMessage;
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            StatusMessage = "Assigning worker to selected ticket...";
            MapErrorMessage = string.Empty;

            var assignmentResult = await _mapDispatchService.AssignWorkerToTicketAsync(
                SelectedTicketId.Value,
                workerId,
                cancellationToken);

            if (!assignmentResult.Success)
            {
                MapErrorMessage = assignmentResult.ErrorMessage ?? "Worker assignment failed.";
                StatusMessage = assignmentResult.StatusMessage;
                return;
            }

            var refreshedContext = await _mapDispatchService.LoadElevatorTicketContextAsync(elevatorId, cancellationToken);
            SelectedElevatorId = refreshedContext.ElevatorId;
            ReplaceCollection(SelectedElevatorTickets, refreshedContext.ActiveTickets);

            if (!SelectedElevatorTickets.Any(ticket => ticket.TicketId == assignmentResult.TicketId))
            {
                SelectedTicketId = null;
            }
            RefreshWorkerSuggestions();

            if (!string.IsNullOrWhiteSpace(elevatorTitle))
            {
                SelectedItemTitle = elevatorTitle;
            }

            if (!string.IsNullOrWhiteSpace(workerTitle))
            {
                SelectedItemDetail = $"Assigned worker: {workerTitle}";
            }

            MapErrorMessage = string.Empty;
            StatusMessage = assignmentResult.StatusMessage;
        }, "Assigning worker from map drop failed.");
    }

    private void RefreshWorkerSuggestions()
    {
        if (!SelectedTicketId.HasValue || !SelectedElevatorId.HasValue)
        {
            ReplaceCollection(WorkerSuggestions, []);
            return;
        }

        if (!SelectedElevatorTickets.Any(ticket => ticket.TicketId == SelectedTicketId.Value))
        {
            ReplaceCollection(WorkerSuggestions, []);
            return;
        }

        var elevatorIdText = SelectedElevatorId.Value.ToString();
        var elevatorMarker = _latestMarkers.FirstOrDefault(marker =>
            marker.Kind == MapMarkerKind.Elevator &&
            string.Equals(marker.Id, elevatorIdText, StringComparison.OrdinalIgnoreCase));

        if (elevatorMarker is null)
        {
            ReplaceCollection(WorkerSuggestions, []);
            return;
        }

        var candidates = _latestMarkers
            .Select(CreateWorkerCandidate)
            .Where(candidate => candidate != null)
            .Cast<WorkerProximityCandidate>()
            .ToList();

        if (candidates.Count == 0)
        {
            ReplaceCollection(WorkerSuggestions, []);
            return;
        }

        var rankedSuggestions = _proximityRankingService.RankWorkers(
            new ProximityRankRequest(
                SelectedTicketId.Value,
                SelectedElevatorId.Value,
                elevatorMarker.Latitude,
                elevatorMarker.Longitude,
                candidates));

        var rows = rankedSuggestions.Select(suggestion => new WorkerSuggestionRow(
            suggestion.Rank,
            suggestion.DisplayName,
            ToAvailabilityLabel(suggestion.Availability),
            $"{suggestion.DistanceKm:F2} km",
            suggestion.WorkerId,
            suggestion.DistanceKm));

        ReplaceCollection(WorkerSuggestions, rows);
    }

    private static WorkerProximityCandidate? CreateWorkerCandidate(MapMarkerSnapshot marker)
    {
        if (!Guid.TryParse(marker.Id, out var workerId))
        {
            return null;
        }

        var availability = marker.Kind switch
        {
            MapMarkerKind.AvailableWorker or MapMarkerKind.AssignedAvailableWorker => WorkerAvailabilityStatus.Available,
            MapMarkerKind.UnavailableWorker or MapMarkerKind.AssignedUnavailableWorker => WorkerAvailabilityStatus.Unavailable,
            _ => (WorkerAvailabilityStatus?)null
        };

        if (availability is null)
        {
            return null;
        }

        return new WorkerProximityCandidate(
            workerId,
            marker.Title,
            availability.Value,
            marker.Latitude,
            marker.Longitude);
    }

    private static string ToAvailabilityLabel(WorkerAvailabilityStatus availability)
    {
        return availability == WorkerAvailabilityStatus.Available ? "Available" : "Unavailable";
    }

    private async Task RunBusyOperationAsync(Func<Task> action, string failureMessage)
    {
        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = "Map error occurred.";
            MapErrorMessage = ex.Message;
            _logger.LogError(ex, failureMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    public sealed record WorkerSuggestionRow(
        int Rank,
        string DisplayName,
        string Availability,
        string DistanceText,
        Guid WorkerId,
        double DistanceKm);

    private sealed class NoOpMapDispatchService : IMapDispatchService
    {
        public Task<ElevatorTicketContext> LoadElevatorTicketContextAsync(Guid elevatorId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ElevatorTicketContext(elevatorId, []));
        }

        public Task<MapAssignmentResult> AssignWorkerToTicketAsync(Guid ticketId, Guid workerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MapAssignmentResult(
                Success: false,
                TicketId: ticketId,
                WorkerId: workerId,
                StatusMessage: "Map dispatch service is not configured.",
                ErrorMessage: "Map dispatch service is not configured."));
        }
    }

    private sealed class NoOpProximityRankingService : IProximityRankingService
    {
        public IReadOnlyList<WorkerProximitySuggestion> RankWorkers(ProximityRankRequest request, int maxResults = 10)
        {
            return [];
        }
    }
}
