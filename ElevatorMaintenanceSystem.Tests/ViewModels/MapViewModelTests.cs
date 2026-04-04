using CommunityToolkit.Mvvm.Input;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Services;
using ElevatorMaintenanceSystem.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ElevatorMaintenanceSystem.Tests.ViewModels;

/// <summary>
/// Tests for MapViewModel manual refresh, map-state preservation, and error handling
/// </summary>
public class MapViewModelTests
{
    private readonly FakeMapDataService _fakeMapDataService;
    private readonly NullLogger<MapViewModel> _logger;
    private readonly MapSettings _mapSettings;

    public MapViewModelTests()
    {
        _fakeMapDataService = new FakeMapDataService();
        _logger = new NullLogger<MapViewModel>();
        _mapSettings = new MapSettings
        {
            DefaultCenterLatitude = 43.4516,
            DefaultCenterLongitude = -80.4925,
            DefaultZoom = 10,
            DefaultBaseLayer = "standard"
        };
    }

    [Fact]
    public async Task LoadMapAsync_UsesWaterlooKitchenerDefaultsOnFirstLoad()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _fakeMapDataService.SnapshotToReturn = snapshot;

        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);

        // Act
        await viewModel.LoadMapAsync();

        // Assert
        Assert.True(viewModel.IsInitialized);
        Assert.Equal(43.4516, viewModel.CurrentCenterLatitude);
        Assert.Equal(-80.4925, viewModel.CurrentCenterLongitude);
        Assert.Equal(10, viewModel.CurrentZoom);
        Assert.Equal("standard", viewModel.SelectedBaseLayer);
        Assert.Equal("Map loaded.", viewModel.StatusMessage);
        Assert.False(string.IsNullOrEmpty(viewModel.LastSnapshotJson));
    }

    [Fact]
    public async Task LoadMapAsync_DisplaysLoadingStatus()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _fakeMapDataService.SnapshotToReturn = snapshot;

        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);

        // Act
        var loadTask = viewModel.LoadMapAsync();
        Assert.Equal("Loading map data...", viewModel.StatusMessage);
        Assert.True(viewModel.IsBusy);

        await loadTask;

        // Assert
        Assert.False(viewModel.IsBusy);
        Assert.Equal("Map loaded.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshMapAsync_PreservesCurrentStateWhenInitialized()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _fakeMapDataService.SnapshotToReturn = snapshot;

        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);
        await viewModel.LoadMapAsync();

        // Change state after initial load
        viewModel.ApplyMapState(43.5, -80.5, 12, "satellite");

        // Act
        await viewModel.RefreshMapAsync();

        // Assert
        Assert.Equal(43.5, viewModel.CurrentCenterLatitude);
        Assert.Equal(-80.5, viewModel.CurrentCenterLongitude);
        Assert.Equal(12, viewModel.CurrentZoom);
        Assert.Equal("satellite", viewModel.SelectedBaseLayer);
        Assert.Equal("Map refreshed.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshMapAsync_UsesDefaultsWhenNotInitialized()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _fakeMapDataService.SnapshotToReturn = snapshot;

        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);
        Assert.False(viewModel.IsInitialized);

        // Act
        await viewModel.RefreshMapAsync();

        // Assert
        Assert.True(viewModel.IsInitialized);
        Assert.Equal(43.4516, viewModel.CurrentCenterLatitude);
        Assert.Equal(-80.4925, viewModel.CurrentCenterLongitude);
    }

    [Fact]
    public async Task RefreshMapAsync_DisplaysRefreshingStatus()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _fakeMapDataService.SnapshotToReturn = snapshot;

        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);
        await viewModel.LoadMapAsync();

        // Act
        var refreshTask = viewModel.RefreshMapAsync();
        Assert.Equal("Refreshing map data...", viewModel.StatusMessage);
        Assert.True(viewModel.IsBusy);

        await refreshTask;

        // Assert
        Assert.False(viewModel.IsBusy);
        Assert.Equal("Map refreshed.", viewModel.StatusMessage);
    }

    [Fact]
    public void SetSelectedItem_UpdatesSelectedItemProperties()
    {
        // Arrange
        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);

        // Act
        viewModel.SetSelectedItem("Test Elevator", "123 Main St\nBuilding A, Floor 5");

        // Assert
        Assert.Equal("Test Elevator", viewModel.SelectedItemTitle);
        Assert.Equal("123 Main St\nBuilding A, Floor 5", viewModel.SelectedItemDetail);
    }

    [Fact]
    public void ClearSelectedItem_ClearsSelectedItemProperties()
    {
        // Arrange
        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);
        viewModel.SetSelectedItem("Test Elevator", "123 Main St");

        // Act
        viewModel.ClearSelectedItem();

        // Assert
        Assert.Equal(string.Empty, viewModel.SelectedItemTitle);
        Assert.Equal(string.Empty, viewModel.SelectedItemDetail);
    }

    [Fact]
    public void ApplyMapState_UpdatesMapState()
    {
        // Arrange
        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);

        // Act
        viewModel.ApplyMapState(43.5, -80.5, 15, "satellite");

        // Assert
        Assert.Equal(43.5, viewModel.CurrentCenterLatitude);
        Assert.Equal(-80.5, viewModel.CurrentCenterLongitude);
        Assert.Equal(15, viewModel.CurrentZoom);
        Assert.Equal("satellite", viewModel.SelectedBaseLayer);
    }

    [Fact]
    public void RefreshMapCommand_CanExecuteWhenNotBusy()
    {
        // Arrange
        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);

        // Act & Assert
        Assert.True(viewModel.RefreshMapCommand.CanExecute(null));
    }

    [Fact]
    public async Task RefreshMapCommand_CannotExecuteWhenBusy()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _fakeMapDataService.SnapshotToReturn = snapshot;

        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);

        // Act
        var loadTask = viewModel.LoadMapAsync();

        // Assert
        Assert.False(viewModel.RefreshMapCommand.CanExecute(null));

        await loadTask;
        Assert.True(viewModel.RefreshMapCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadMapAsync_SurfacesExceptionInStatusMessageAndMapErrorMessage()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Failed to load map data");
        _fakeMapDataService.ExceptionToThrow = expectedException;

        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);

        // Act
        await viewModel.LoadMapAsync();

        // Assert
        Assert.Equal("Map error occurred.", viewModel.StatusMessage);
        Assert.Equal(expectedException.Message, viewModel.MapErrorMessage);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.IsInitialized);
    }

    [Fact]
    public async Task RefreshMapAsync_SurfacesExceptionInStatusMessageAndMapErrorMessage()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        _fakeMapDataService.SnapshotToReturn = snapshot;

        var viewModel = new MapViewModel(_fakeMapDataService, _mapSettings, _logger);
        await viewModel.LoadMapAsync();

        // Now set an exception for the next call
        var expectedException = new InvalidOperationException("Network error");
        _fakeMapDataService.ExceptionToThrow = expectedException;

        // Act
        await viewModel.RefreshMapAsync();

        // Assert
        Assert.Equal("Map error occurred.", viewModel.StatusMessage);
        Assert.Equal("Network error", viewModel.MapErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    private static MapDataSnapshot CreateTestSnapshot()
    {
        return new MapDataSnapshot(
            CenterLatitude: 43.4516,
            CenterLongitude: -80.4925,
            Zoom: 10,
            Markers: new List<MapMarkerSnapshot>
            {
                new MapMarkerSnapshot(
                    Id: "elevator-1",
                    Kind: MapMarkerKind.Elevator,
                    Latitude: 43.4516,
                    Longitude: -80.4925,
                    Title: "Test Elevator",
                    DetailLines: new List<string> { "123 Main St", "Building A, Floor 5" }
                )
            },
            StandardTiles: new TileProviderSnapshot(
                Provider: "OpenStreetMap",
                UrlTemplate: "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
                Attribution: "© OpenStreetMap contributors",
                MaxZoom: 19,
                ApiKey: null
            ),
            SatelliteTiles: new TileProviderSnapshot(
                Provider: "ArcGIS",
                UrlTemplate: "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
                Attribution: "© Esri",
                MaxZoom: 19,
                ApiKey: "test-key"
            )
        );
    }

    private sealed class FakeMapDataService : IMapDataService
    {
        public MapDataSnapshot? SnapshotToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public int BuildSnapshotCallCount { get; private set; }

        public Task<MapDataSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken = default)
        {
            BuildSnapshotCallCount++;
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }
            if (SnapshotToReturn == null)
            {
                throw new InvalidOperationException("No snapshot configured");
            }
            return Task.FromResult(SnapshotToReturn);
        }
    }
}
