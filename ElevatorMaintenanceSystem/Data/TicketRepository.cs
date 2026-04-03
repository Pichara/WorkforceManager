using ElevatorMaintenanceSystem.Models;
using MongoDB.Driver;

namespace ElevatorMaintenanceSystem.Data;

public class TicketRepository : MongoRepository<Ticket>, ITicketRepository
{
    private readonly IMongoCollection<Ticket> _collection;
    private readonly FilterDefinitionBuilder<Ticket> _filterBuilder = Builders<Ticket>.Filter;
    private readonly UpdateDefinitionBuilder<Ticket> _updateBuilder = Builders<Ticket>.Update;

    public TicketRepository(IMongoDbContext context)
        : base(context, "tickets")
    {
        _collection = context.Database.GetCollection<Ticket>("tickets");
    }

    public async Task<IEnumerable<Ticket>> GetActiveAsync()
    {
        var filter = _filterBuilder.Eq(x => x.DeletedAt, null as DateTime?);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<Ticket?> UpdateDetailsAsync(
        Guid ticketId,
        string description,
        TicketIssueType issueType,
        TicketPriority priority,
        DateTime requestedDate,
        DateTime changedAtUtc,
        string changedBy)
    {
        var existing = await GetByIdAsync(ticketId);
        if (existing == null)
        {
            return null;
        }

        var auditEntry = new TicketAuditEntry
        {
            OccurredAtUtc = changedAtUtc,
            ChangedBy = changedBy,
            EntryType = TicketAuditEntryType.DetailsUpdated,
            Message = "Updated ticket details."
        };

        var update = _updateBuilder
            .Set(x => x.Description, description)
            .Set(x => x.IssueType, issueType)
            .Set(x => x.Priority, priority)
            .Set(x => x.RequestedDate, requestedDate)
            .Set(x => x.UpdatedAt, changedAtUtc)
            .Push(x => x.History, auditEntry);

        return await _collection.FindOneAndUpdateAsync(
            _filterBuilder.Eq(x => x.Id, ticketId),
            update,
            new FindOneAndUpdateOptions<Ticket> { ReturnDocument = ReturnDocument.After });
    }

    public async Task<Ticket?> AssignWorkerAsync(Guid ticketId, Guid workerId, DateTime changedAtUtc, string changedBy)
    {
        var existing = await GetByIdAsync(ticketId);
        if (existing == null)
        {
            return null;
        }

        var auditEntry = new TicketAuditEntry
        {
            OccurredAtUtc = changedAtUtc,
            ChangedBy = changedBy,
            EntryType = TicketAuditEntryType.WorkerAssigned,
            FromStatus = existing.Status,
            ToStatus = TicketStatus.Assigned,
            FromWorkerId = existing.AssignedWorkerId,
            ToWorkerId = workerId,
            Message = "Assigned worker to ticket."
        };

        var update = _updateBuilder
            .Set(x => x.AssignedWorkerId, workerId)
            .Set(x => x.Status, TicketStatus.Assigned)
            .Set(x => x.UpdatedAt, changedAtUtc)
            .Push(x => x.History, auditEntry);

        return await _collection.FindOneAndUpdateAsync(
            _filterBuilder.Eq(x => x.Id, ticketId),
            update,
            new FindOneAndUpdateOptions<Ticket> { ReturnDocument = ReturnDocument.After });
    }

    public async Task<Ticket?> UnassignWorkerAsync(Guid ticketId, DateTime changedAtUtc, string changedBy)
    {
        var existing = await GetByIdAsync(ticketId);
        if (existing == null)
        {
            return null;
        }

        var auditEntry = new TicketAuditEntry
        {
            OccurredAtUtc = changedAtUtc,
            ChangedBy = changedBy,
            EntryType = TicketAuditEntryType.WorkerUnassigned,
            FromStatus = existing.Status,
            ToStatus = TicketStatus.Pending,
            FromWorkerId = existing.AssignedWorkerId,
            ToWorkerId = null,
            Message = "Removed worker assignment from ticket."
        };

        var update = _updateBuilder
            .Set(x => x.AssignedWorkerId, null)
            .Set(x => x.Status, TicketStatus.Pending)
            .Set(x => x.UpdatedAt, changedAtUtc)
            .Push(x => x.History, auditEntry);

        return await _collection.FindOneAndUpdateAsync(
            _filterBuilder.Eq(x => x.Id, ticketId),
            update,
            new FindOneAndUpdateOptions<Ticket> { ReturnDocument = ReturnDocument.After });
    }

    public async Task<Ticket?> ChangeStatusAsync(Guid ticketId, TicketStatus fromStatus, TicketStatus toStatus, DateTime changedAtUtc, string changedBy)
    {
        var existing = await GetByIdAsync(ticketId);
        if (existing == null)
        {
            return null;
        }

        var entryType = toStatus == TicketStatus.Canceled
            ? TicketAuditEntryType.Canceled
            : TicketAuditEntryType.StatusChanged;

        var auditEntry = new TicketAuditEntry
        {
            OccurredAtUtc = changedAtUtc,
            ChangedBy = changedBy,
            EntryType = entryType,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            FromWorkerId = existing.AssignedWorkerId,
            ToWorkerId = existing.AssignedWorkerId,
            Message = toStatus == TicketStatus.Canceled
                ? "Canceled ticket."
                : $"Changed status from {fromStatus} to {toStatus}."
        };

        var update = _updateBuilder
            .Set(x => x.Status, toStatus)
            .Set(x => x.UpdatedAt, changedAtUtc)
            .Push(x => x.History, auditEntry);

        return await _collection.FindOneAndUpdateAsync(
            _filterBuilder.And(
                _filterBuilder.Eq(x => x.Id, ticketId),
                _filterBuilder.Eq(x => x.Status, fromStatus)),
            update,
            new FindOneAndUpdateOptions<Ticket> { ReturnDocument = ReturnDocument.After });
    }

    public async Task<bool> DeleteCanceledAsync(Guid ticketId)
    {
        var result = await _collection.DeleteOneAsync(
            _filterBuilder.And(
                _filterBuilder.Eq(x => x.Id, ticketId),
                _filterBuilder.Eq(x => x.Status, TicketStatus.Canceled)));

        return result.DeletedCount > 0;
    }
}
