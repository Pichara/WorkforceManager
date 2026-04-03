using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Services;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IElevatorRepository _elevatorRepository;
    private readonly IWorkerRepository _workerRepository;
    private readonly IUserContext _userContext;

    public TicketService(
        ITicketRepository ticketRepository,
        IElevatorRepository elevatorRepository,
        IWorkerRepository workerRepository,
        IUserContext userContext)
    {
        _ticketRepository = ticketRepository ?? throw new ArgumentNullException(nameof(ticketRepository));
        _elevatorRepository = elevatorRepository ?? throw new ArgumentNullException(nameof(elevatorRepository));
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    public async Task<IReadOnlyList<Ticket>> GetActiveAsync()
    {
        var tickets = await _ticketRepository.GetActiveAsync();
        return tickets
            .OrderByDescending(ticket => ticket.RequestedDate)
            .ToList();
    }

    public async Task<Ticket> CreateAsync(Guid elevatorId, string description, TicketIssueType issueType, TicketPriority priority, DateTime requestedDate)
    {
        var elevator = await _elevatorRepository.GetByIdAsync(elevatorId);
        if (elevator == null || elevator.DeletedAt.HasValue)
        {
            throw new KeyNotFoundException($"Elevator '{elevatorId}' was not found.");
        }

        var changedAtUtc = DateTime.UtcNow;
        var changedBy = _userContext.GetCurrentUser();
        var trimmedDescription = ValidateDescription(description);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            ElevatorId = elevatorId,
            Description = trimmedDescription,
            IssueType = issueType,
            Priority = priority,
            RequestedDate = requestedDate,
            Status = TicketStatus.Pending,
            CreatedAt = changedAtUtc,
            UpdatedAt = changedAtUtc,
            DeletedAt = null,
            History =
            [
                new TicketAuditEntry
                {
                    OccurredAtUtc = changedAtUtc,
                    ChangedBy = changedBy,
                    EntryType = TicketAuditEntryType.Created,
                    ToStatus = TicketStatus.Pending,
                    Message = "Created ticket."
                }
            ]
        };

        await _ticketRepository.AddAsync(ticket);
        return ticket;
    }

    public async Task<Ticket> UpdateDetailsAsync(Guid ticketId, string description, TicketIssueType issueType, TicketPriority priority, DateTime requestedDate)
    {
        var ticket = await RequireTicketAsync(ticketId);
        if (!TicketWorkflowPolicy.CanEditDetails(ticket.Status))
        {
            throw new InvalidOperationException($"Ticket '{ticketId}' cannot be edited while {ticket.Status}.");
        }

        var updated = await _ticketRepository.UpdateDetailsAsync(
            ticketId,
            ValidateDescription(description),
            issueType,
            priority,
            requestedDate,
            DateTime.UtcNow,
            _userContext.GetCurrentUser());

        return updated ?? throw new InvalidOperationException($"Ticket '{ticketId}' could not be updated.");
    }

    public async Task<Ticket> AssignWorkerAsync(Guid ticketId, Guid workerId)
    {
        var ticket = await RequireTicketAsync(ticketId);
        var worker = await _workerRepository.GetByIdAsync(workerId);

        if (worker == null || worker.DeletedAt.HasValue)
        {
            throw new KeyNotFoundException($"Worker '{workerId}' was not found.");
        }

        if (worker.AvailabilityStatus == WorkerAvailabilityStatus.Unavailable)
        {
            throw new InvalidOperationException($"Worker '{worker.FullName}' is unavailable.");
        }

        if (!TicketWorkflowPolicy.CanMove(ticket.Status, TicketStatus.Assigned))
        {
            throw new InvalidOperationException($"Cannot assign a worker while ticket is {ticket.Status}.");
        }

        var updated = await _ticketRepository.AssignWorkerAsync(
            ticketId,
            workerId,
            DateTime.UtcNow,
            _userContext.GetCurrentUser());

        return updated ?? throw new InvalidOperationException($"Ticket '{ticketId}' could not be assigned.");
    }

    public async Task<Ticket> UnassignWorkerAsync(Guid ticketId)
    {
        var ticket = await RequireTicketAsync(ticketId);

        if (ticket.AssignedWorkerId == null)
        {
            throw new InvalidOperationException($"Ticket '{ticketId}' has no assigned worker.");
        }

        if (!TicketWorkflowPolicy.CanMove(ticket.Status, TicketStatus.Pending))
        {
            throw new InvalidOperationException($"Cannot unassign a worker while ticket is {ticket.Status}.");
        }

        var updated = await _ticketRepository.UnassignWorkerAsync(
            ticketId,
            DateTime.UtcNow,
            _userContext.GetCurrentUser());

        return updated ?? throw new InvalidOperationException($"Ticket '{ticketId}' could not be unassigned.");
    }

    public async Task<Ticket> ChangeStatusAsync(Guid ticketId, TicketStatus nextStatus)
    {
        var ticket = await RequireTicketAsync(ticketId);

        if (ticket.Status == nextStatus)
        {
            throw new InvalidOperationException($"Ticket is already {nextStatus}.");
        }

        if (RequiresAssignedWorker(nextStatus) && ticket.AssignedWorkerId == null)
        {
            throw new InvalidOperationException($"Ticket '{ticketId}' requires an assigned worker before moving to {nextStatus}.");
        }

        if (!TicketWorkflowPolicy.CanMove(ticket.Status, nextStatus))
        {
            throw new InvalidOperationException($"Cannot move ticket from {ticket.Status} to {nextStatus}.");
        }

        var updated = await _ticketRepository.ChangeStatusAsync(
            ticketId,
            ticket.Status,
            nextStatus,
            DateTime.UtcNow,
            _userContext.GetCurrentUser());

        return updated ?? throw new InvalidOperationException($"Ticket '{ticketId}' could not move to {nextStatus}.");
    }

    public Task<Ticket> CancelAsync(Guid ticketId)
    {
        return ChangeStatusAsync(ticketId, TicketStatus.Canceled);
    }

    public async Task DeleteCanceledAsync(Guid ticketId)
    {
        var ticket = await RequireTicketAsync(ticketId);
        if (!TicketWorkflowPolicy.CanDelete(ticket.Status))
        {
            throw new InvalidOperationException("Only canceled tickets can be deleted.");
        }

        var deleted = await _ticketRepository.DeleteCanceledAsync(ticketId);
        if (!deleted)
        {
            throw new InvalidOperationException($"Canceled ticket '{ticketId}' could not be deleted.");
        }
    }

    private async Task<Ticket> RequireTicketAsync(Guid ticketId)
    {
        return await _ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");
    }

    private static string ValidateDescription(string description)
    {
        var trimmed = description.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        return trimmed;
    }

    private static bool RequiresAssignedWorker(TicketStatus status)
    {
        return status is TicketStatus.Assigned or TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Closed;
    }
}
