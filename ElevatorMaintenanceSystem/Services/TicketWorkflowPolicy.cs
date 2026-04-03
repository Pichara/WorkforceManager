using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Services;

public static class TicketWorkflowPolicy
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> AllowedTransitions =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.Pending] = [TicketStatus.Assigned, TicketStatus.Canceled],
            [TicketStatus.Assigned] = [TicketStatus.Pending, TicketStatus.InProgress, TicketStatus.Canceled],
            [TicketStatus.InProgress] = [TicketStatus.Resolved],
            [TicketStatus.Resolved] = [TicketStatus.Closed],
            [TicketStatus.Closed] = [],
            [TicketStatus.Canceled] = []
        };

    public static bool CanMove(TicketStatus from, TicketStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var nextStates) && nextStates.Contains(to);
    }

    public static bool CanEditDetails(TicketStatus status)
    {
        return status is TicketStatus.Pending or TicketStatus.Assigned;
    }

    public static bool CanDelete(TicketStatus status)
    {
        return status == TicketStatus.Canceled;
    }
}
