namespace ElevatorMaintenanceSystem.Services;

/// <summary>
/// Map marker kind distinguishing between elevators and worker availability states
/// </summary>
public enum MapMarkerKind
{
    /// <summary>
    /// Elevator marker
    /// </summary>
    Elevator,

    /// <summary>
    /// Worker with Available status
    /// </summary>
    AvailableWorker,

    /// <summary>
    /// Worker with Available status and active ticket assignment
    /// </summary>
    AssignedAvailableWorker,

    /// <summary>
    /// Worker with Unavailable status
    /// </summary>
    UnavailableWorker,

    /// <summary>
    /// Worker with Unavailable status and active ticket assignment
    /// </summary>
    AssignedUnavailableWorker
}

/// <summary>
/// Snapshot of all map data including markers and settings for the current view
/// </summary>
/// <param name="CenterLatitude">Current map center latitude</param>
/// <param name="CenterLongitude">Current map center longitude</param>
/// <param name="Zoom">Current zoom level</param>
/// <param name="Markers">All markers to display on the map</param>
/// <param name="StandardTiles">Standard map tile settings</param>
/// <param name="SatelliteTiles">Satellite map tile settings</param>
public record MapDataSnapshot(
    double CenterLatitude,
    double CenterLongitude,
    int Zoom,
    IReadOnlyList<MapMarkerSnapshot> Markers,
    TileProviderSnapshot StandardTiles,
    TileProviderSnapshot SatelliteTiles);

/// <summary>
/// Snapshot of a single map marker
/// </summary>
/// <param name="Id">Unique identifier for the marker</param>
/// <param name="Kind">Type of marker (elevator or worker availability)</param>
/// <param name="Latitude">GPS latitude coordinate</param>
/// <param name="Longitude">GPS longitude coordinate</param>
/// <param name="Title">Display title for the marker popup</param>
/// <param name="DetailLines">Detail lines to display in the popup (up to 3 lines)</param>
public record MapMarkerSnapshot(
    string Id,
    MapMarkerKind Kind,
    double Latitude,
    double Longitude,
    string Title,
    IReadOnlyList<string> DetailLines);

/// <summary>
/// Snapshot of tile provider settings for map rendering
/// </summary>
/// <param name="Provider">Provider name</param>
/// <param name="UrlTemplate">Tile URL template</param>
/// <param name="Attribution">Attribution text</param>
/// <param name="MaxZoom">Maximum zoom level</param>
/// <param name="ApiKey">Optional API key for provider</param>
public record TileProviderSnapshot(
    string Provider,
    string UrlTemplate,
    string Attribution,
    int MaxZoom,
    string? ApiKey = null);
