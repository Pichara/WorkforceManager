using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.Data;

public interface ITicketRepository : IMongoRepository<Ticket>
{
    Task<IEnumerable<Ticket>> GetActiveAsync();

    Task<Ticket?> UpdateDetailsAsync(
        Guid ticketId,
        string description,
        TicketIssueType issueType,
        TicketPriority priority,
        DateTime requestedDate,
        DateTime changedAtUtc,
        string changedBy);

    Task<Ticket?> AssignWorkerAsync(Guid ticketId, Guid workerId, DateTime changedAtUtc, string changedBy);

    Task<Ticket?> UnassignWorkerAsync(Guid ticketId, DateTime changedAtUtc, string changedBy);

    Task<Ticket?> ChangeStatusAsync(Guid ticketId, TicketStatus fromStatus, TicketStatus toStatus, DateTime changedAtUtc, string changedBy);

    Task<bool> DeleteCanceledAsync(Guid ticketId);
}
