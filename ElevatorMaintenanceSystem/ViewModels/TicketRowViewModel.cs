using ElevatorMaintenanceSystem.Models;

namespace ElevatorMaintenanceSystem.ViewModels;

public class TicketRowViewModel
{
    public Guid TicketId { get; set; }

    public string Description { get; set; } = string.Empty;

    public TicketIssueType IssueType { get; set; }

    public TicketPriority Priority { get; set; }

    public TicketStatus Status { get; set; }

    public DateTime RequestedDate { get; set; }

    public string AssignedWorkerDisplay { get; set; } = string.Empty;
}
