using CommunityToolkit.Mvvm.ComponentModel;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Infrastructure.Commands;
using ElevatorMaintenanceSystem.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ElevatorMaintenanceSystem.ViewModels;

/// <summary>
/// ViewModel for the map visualization workspace
/// </summary>
public partial class MapViewModel : ViewModelBase
{
    private readonly IMapDataService _mapDataService;
    private readonly MapSettings _mapSettings;
    private readonly ILogger<MapViewModel> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

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

    public AsyncRelayCommand RefreshMapCommand { get; }

    public MapViewModel(
        IMapDataService mapDataService,
        MapSettings mapSettings,
        ILogger<MapViewModel> logger)
    {
        _mapDataService = mapDataService ?? throw new ArgumentNullException(nameof(mapDataService));
        _mapSettings = mapSettings ?? throw new ArgumentNullException(nameof(mapSettings));
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
}
