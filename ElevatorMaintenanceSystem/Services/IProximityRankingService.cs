namespace ElevatorMaintenanceSystem.Services;

public interface IProximityRankingService
{
    IReadOnlyList<WorkerProximitySuggestion> RankWorkers(ProximityRankRequest request, int maxResults = 10);
}
