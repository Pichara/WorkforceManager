using MongoDB.Driver.GeoJsonObjectModel;

namespace ElevatorMaintenanceSystem.Models;

public class Elevator : BaseDocument
{
    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string BuildingName { get; set; } = string.Empty;

    public string FloorLabel { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public DateTime InstallationDate { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = null!;
}
