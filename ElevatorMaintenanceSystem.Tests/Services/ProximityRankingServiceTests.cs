using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;

namespace ElevatorMaintenanceSystem.Tests.Services;

public class ProximityRankingServiceTests
{
    private readonly IProximityRankingService _sut = new PlaceholderProximityRankingService();

    [Fact]
    public void RankWorkers_MAP_08_D_06_ComputesDistanceInKilometersForEachCandidate()
    {
        // D_06: Straight-line distance from worker location to selected ticket elevator location.
        var request = new ProximityRankRequest(
            SelectedTicketId: Guid.NewGuid(),
            ElevatorId: Guid.NewGuid(),
            ElevatorLatitude: 0.0,
            ElevatorLongitude: 0.0,
            Candidates:
            [
                new WorkerProximityCandidate(
                    WorkerId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    DisplayName: "Alex",
                    Availability: WorkerAvailabilityStatus.Available,
                    WorkerLatitude: 0.0,
                    WorkerLongitude: 1.0)
            ]);

        var ranked = _sut.RankWorkers(request);

        Assert.Single(ranked);
        Assert.Equal("Alex", ranked[0].DisplayName);
        Assert.InRange(ranked[0].DistanceKm, 111.19, 111.20);
        Assert.Equal(1, ranked[0].Rank);
    }

    [Fact]
    public void RankWorkers_MAP_09_D_04_ReturnsNearestAvailableWorkersFirstForSelectedTicketContext()
    {
        var request = new ProximityRankRequest(
            SelectedTicketId: Guid.NewGuid(),
            ElevatorId: Guid.NewGuid(),
            ElevatorLatitude: 0.0,
            ElevatorLongitude: 0.0,
            Candidates:
            [
                new WorkerProximityCandidate(
                    WorkerId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    DisplayName: "Far",
                    Availability: WorkerAvailabilityStatus.Available,
                    WorkerLatitude: 0.0,
                    WorkerLongitude: 2.0),
                new WorkerProximityCandidate(
                    WorkerId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    DisplayName: "Near",
                    Availability: WorkerAvailabilityStatus.Available,
                    WorkerLatitude: 0.0,
                    WorkerLongitude: 0.5),
                new WorkerProximityCandidate(
                    WorkerId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    DisplayName: "Middle",
                    Availability: WorkerAvailabilityStatus.Available,
                    WorkerLatitude: 0.0,
                    WorkerLongitude: 1.0)
            ]);

        var ranked = _sut.RankWorkers(request);

        Assert.Equal(3, ranked.Count);
        Assert.Equal("Near", ranked[0].DisplayName);
        Assert.Equal("Middle", ranked[1].DisplayName);
        Assert.Equal("Far", ranked[2].DisplayName);
    }

    [Fact]
    public void RankWorkers_MAP_10_D_07_OrdersByDistanceThenAvailabilityThenNameThenId()
    {
        // D_07: deterministic tie-break order after distance.
        var request = new ProximityRankRequest(
            SelectedTicketId: Guid.NewGuid(),
            ElevatorId: Guid.NewGuid(),
            ElevatorLatitude: 0.0,
            ElevatorLongitude: 0.0,
            Candidates:
            [
                new WorkerProximityCandidate(
                    WorkerId: Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    DisplayName: "Bob",
                    Availability: WorkerAvailabilityStatus.Available,
                    WorkerLatitude: 0.0,
                    WorkerLongitude: 1.0),
                new WorkerProximityCandidate(
                    WorkerId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    DisplayName: "Bob",
                    Availability: WorkerAvailabilityStatus.Available,
                    WorkerLatitude: 0.0,
                    WorkerLongitude: 1.0),
                new WorkerProximityCandidate(
                    WorkerId: Guid.Parse("00000000-0000-0000-0000-000000000010"),
                    DisplayName: "Zed",
                    Availability: WorkerAvailabilityStatus.Available,
                    WorkerLatitude: 0.0,
                    WorkerLongitude: 1.0),
                new WorkerProximityCandidate(
                    WorkerId: Guid.Parse("00000000-0000-0000-0000-000000000011"),
                    DisplayName: "Amy",
                    Availability: WorkerAvailabilityStatus.Unavailable,
                    WorkerLatitude: 0.0,
                    WorkerLongitude: 1.0)
            ]);

        var ranked = _sut.RankWorkers(request, maxResults: 10);

        Assert.Equal(4, ranked.Count);
        Assert.Equal("Bob", ranked[0].DisplayName);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), ranked[0].WorkerId);
        Assert.Equal("Bob", ranked[1].DisplayName);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), ranked[1].WorkerId);
        Assert.Equal("Zed", ranked[2].DisplayName);
        Assert.Equal("Amy", ranked[3].DisplayName);
        Assert.Equal(WorkerAvailabilityStatus.Unavailable, ranked[3].Availability);
        Assert.Equal(new[] { 1, 2, 3, 4 }, ranked.Select(x => x.Rank).ToArray());
    }

    private sealed class PlaceholderProximityRankingService : IProximityRankingService
    {
        public IReadOnlyList<WorkerProximitySuggestion> RankWorkers(ProximityRankRequest request, int maxResults = 10)
        {
            return [];
        }
    }
}
