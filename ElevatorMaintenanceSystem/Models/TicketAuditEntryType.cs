namespace ElevatorMaintenanceSystem.Models;

public enum TicketAuditEntryType
{
    Created,
    DetailsUpdated,
    WorkerAssigned,
    WorkerUnassigned,
    StatusChanged,
    Canceled
}
