namespace ElevatorMaintenanceSystem.Models;

public class TicketAuditEntry
{
    public DateTime OccurredAtUtc { get; set; }

    public string ChangedBy { get; set; } = string.Empty;

    public TicketAuditEntryType EntryType { get; set; }

    public TicketStatus? FromStatus { get; set; }

    public TicketStatus? ToStatus { get; set; }

    public Guid? FromWorkerId { get; set; }

    public Guid? ToWorkerId { get; set; }

    public string Message { get; set; } = string.Empty;
}
