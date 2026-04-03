namespace ElevatorMaintenanceSystem.Models;

public class Ticket : BaseDocument
{
    public Guid ElevatorId { get; set; }

    public Guid? AssignedWorkerId { get; set; }

    public string Description { get; set; } = string.Empty;

    public TicketIssueType IssueType { get; set; }

    public TicketPriority Priority { get; set; }

    public DateTime RequestedDate { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Pending;

    public List<TicketAuditEntry> History { get; set; } = [];
}
