namespace ElevatorMaintenanceSystem.Infrastructure;

/// <summary>
/// Configuration settings for map visualization including default view and tile providers
/// </summary>
public class MapSettings
{
    public const string SectionName = "MapSettings";

    /// <summary>
    /// Default center latitude for the map (Waterloo/Kitchener area)
    /// </summary>
    public double DefaultCenterLatitude { get; set; } = 43.4516;

    /// <summary>
    /// Default center longitude for the map (Waterloo/Kitchener area)
    /// </summary>
    public double DefaultCenterLongitude { get; set; } = -80.4925;

    /// <summary>
    /// Default zoom level when the map first loads
    /// </summary>
    public int DefaultZoom { get; set; } = 10;

    /// <summary>
    /// Default base layer to display ("satellite")
    /// </summary>
    public string DefaultBaseLayer { get; set; } = "satellite";

    /// <summary>
    /// Standard (street) map tile configuration
    /// </summary>
    public TileProviderSettings StandardTiles { get; set; } = new();

    /// <summary>
    /// Satellite imagery tile configuration
    /// </summary>
    public TileProviderSettings SatelliteTiles { get; set; } = new();
}

/// <summary>
/// Tile provider configuration for map base layers
/// </summary>
public class TileProviderSettings
{
    /// <summary>
    /// Tile provider name (e.g., "OpenStreetMap", "ArcGIS")
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// URL template for tile requests (e.g., "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png")
    /// </summary>
    public string UrlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Attribution text to display on the map
    /// </summary>
    public string Attribution { get; set; } = string.Empty;

    /// <summary>
    /// Maximum zoom level supported by the tile provider
    /// </summary>
    public int MaxZoom { get; set; } = 19;

    /// <summary>
    /// API key required for the tile provider (optional for OpenStreetMap, required for ArcGIS)
    /// </summary>
    public string? ApiKey { get; set; }
}
