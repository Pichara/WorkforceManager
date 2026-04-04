using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Models;
using MongoDB.Driver;

namespace ElevatorMaintenanceSystem.Services;

/// <summary>
/// Service for building map data snapshots containing elevator and worker markers
/// </summary>
public class MapDataService : IMapDataService
{
    private readonly IElevatorRepository _elevatorRepository;
    private readonly IWorkerRepository _workerRepository;
    private readonly MapSettings _settings;

    public MapDataService(
        IElevatorRepository elevatorRepository,
        IWorkerRepository workerRepository,
        MapSettings settings)
    {
        _elevatorRepository = elevatorRepository;
        _workerRepository = workerRepository;
        _settings = settings;
    }

    /// <summary>
    /// Build a snapshot of all active elevators and workers for map visualization
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Map data snapshot with markers and tile settings</returns>
    public async Task<MapDataSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var activeFilter = Builders<Elevator>.Filter.Eq(e => e.DeletedAt, null);
        var elevators = await _elevatorRepository.FindAsync(activeFilter);

        var activeWorkerFilter = Builders<Worker>.Filter.Eq(w => w.DeletedAt, null);
        var workers = await _workerRepository.FindAsync(activeWorkerFilter);

        var markers = new List<MapMarkerSnapshot>();

        // Project elevators into MAP-01 markers
        foreach (var elevator in elevators.Where(e => e.Location != null && e.Location.Coordinates != null))
        {
            markers.Add(new MapMarkerSnapshot(
                elevator.Id.ToString(),
                MapMarkerKind.Elevator,
                elevator.Location.Coordinates.Latitude,
                elevator.Location.Coordinates.Longitude,
                elevator.Name,
                [
                    elevator.Address,
                    $"{elevator.BuildingName} / {elevator.FloorLabel}"
                ]));
        }

        // Project workers into MAP-02 markers (MAP-02: AvailableWorker vs UnavailableWorker)
        foreach (var worker in workers.Where(w => w.Location != null && w.Location.Coordinates != null))
        {
            var markerKind = worker.AvailabilityStatus == WorkerAvailabilityStatus.Available
                ? MapMarkerKind.AvailableWorker
                : MapMarkerKind.UnavailableWorker;

            // Take top three skills for popup
            var topSkills = worker.Skills.Take(3).ToList();

            markers.Add(new MapMarkerSnapshot(
                worker.Id.ToString(),
                markerKind,
                worker.Location.Coordinates.Latitude,
                worker.Location.Coordinates.Longitude,
                worker.FullName,
                [
                    $"Status: {worker.AvailabilityStatus}",
                    $"Skills: {string.Join(", ", topSkills)}"
                ]));
        }

        // Sort markers by title for predictable rendering
        markers = markers.OrderBy(m => m.Title).ToList();

        return new MapDataSnapshot(
            _settings.DefaultCenterLatitude,
            _settings.DefaultCenterLongitude,
            _settings.DefaultZoom,
            markers,
            new TileProviderSnapshot(
                _settings.StandardTiles.Provider,
                _settings.StandardTiles.UrlTemplate,
                _settings.StandardTiles.Attribution,
                _settings.StandardTiles.MaxZoom,
                _settings.StandardTiles.ApiKey),
            new TileProviderSnapshot(
                _settings.SatelliteTiles.Provider,
                _settings.SatelliteTiles.UrlTemplate,
                _settings.SatelliteTiles.Attribution,
                _settings.SatelliteTiles.MaxZoom,
                _settings.SatelliteTiles.ApiKey));
    }
}
