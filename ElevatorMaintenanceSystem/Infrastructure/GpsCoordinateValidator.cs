using MongoDB.Driver.GeoJsonObjectModel;

namespace ElevatorMaintenanceSystem.Infrastructure;

public class GpsCoordinateValidator
{
    public void Validate(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90.");
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180.");
        }
    }

    public GeoJsonPoint<GeoJson2DGeographicCoordinates> CreatePoint(double latitude, double longitude)
    {
        Validate(latitude, longitude);

        return new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
            new GeoJson2DGeographicCoordinates(longitude, latitude));
    }
}
