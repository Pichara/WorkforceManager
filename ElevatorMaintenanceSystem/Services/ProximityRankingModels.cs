using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Services;

public record ProximityRankRequest(
    Guid SelectedTicketId,
    Guid ElevatorId,
    double ElevatorLatitude,
    double ElevatorLongitude,
    IReadOnlyList<WorkerProximityCandidate> Candidates);

public record WorkerProximityCandidate(
    Guid WorkerId,
    string DisplayName,
    WorkerAvailabilityStatus Availability,
    double WorkerLatitude,
    double WorkerLongitude);

public record WorkerProximitySuggestion(
    Guid WorkerId,
    string DisplayName,
    WorkerAvailabilityStatus Availability,
    double DistanceKm,
    int Rank);
