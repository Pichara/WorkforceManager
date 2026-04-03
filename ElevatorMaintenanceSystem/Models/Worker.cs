using MongoDB.Driver.GeoJsonObjectModel;

namespace ElevatorMaintenanceSystem.Models;

public class Worker : BaseDocument
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public List<string> Skills { get; set; } = [];

    public WorkerAvailabilityStatus AvailabilityStatus { get; set; } = WorkerAvailabilityStatus.Available;

    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = null!;
}
