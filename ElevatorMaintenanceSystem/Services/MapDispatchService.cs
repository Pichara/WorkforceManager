using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Services;

public sealed class MapDispatchService : IMapDispatchService
{
    private static readonly TicketStatus[] ActiveStatuses =
    [
        TicketStatus.Pending,
        TicketStatus.Assigned,
        TicketStatus.InProgress
    ];

    private readonly ITicketService _ticketService;

    public MapDispatchService(ITicketService ticketService)
    {
        _ticketService = ticketService ?? throw new ArgumentNullException(nameof(ticketService));
    }

    public async Task<ElevatorTicketContext> LoadElevatorTicketContextAsync(Guid elevatorId, CancellationToken cancellationToken = default)
    {
        var tickets = await _ticketService.GetByElevatorAsync(elevatorId);

        var activeTickets = tickets
            .Where(ticket => ActiveStatuses.Contains(ticket.Status))
            .OrderByDescending(ticket => ticket.Priority)
            .ThenBy(ticket => ticket.RequestedDate)
            .ThenBy(ticket => ticket.Id)
            .Select(ticket => new ElevatorTicketSummary(
                ticket.Id,
                ticket.Description,
                ticket.Priority,
                ticket.Status,
                ticket.RequestedDate,
                ticket.AssignedWorkerId))
            .ToList();

        return new ElevatorTicketContext(elevatorId, activeTickets);
    }

    public async Task<MapAssignmentResult> AssignWorkerToTicketAsync(Guid ticketId, Guid workerId, CancellationToken cancellationToken = default)
    {
        await _ticketService.AssignWorkerAsync(ticketId, workerId);

        return new MapAssignmentResult(
            Success: true,
            TicketId: ticketId,
            WorkerId: workerId,
            StatusMessage: "Worker assigned to selected ticket.");
    }
}
