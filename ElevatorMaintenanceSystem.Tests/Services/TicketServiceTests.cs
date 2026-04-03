using ElevatorMaintenanceSystem.Data;
using ElevatorMaintenanceSystem.Infrastructure;
using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using Xunit;

namespace ElevatorMaintenanceSystem.Tests.Services;

public class TicketServiceTests
{
    [Fact]
    public async Task TICK_01_CreateAsync_CreatesPendingTicketWithCreatedAudit()
    {
        var harness = CreateHarness();

        var created = await harness.Service.CreateAsync(
            harness.Elevator.Id,
            "  Door stuck open  ",
            TicketIssueType.Mechanical,
            TicketPriority.High,
            new DateTime(2026, 4, 2));

        Assert.Equal(TicketStatus.Pending, created.Status);
        Assert.Equal("Door stuck open", created.Description);
        Assert.Equal(harness.Elevator.Id, created.ElevatorId);
        Assert.Single(created.History);
        Assert.Equal(TicketAuditEntryType.Created, created.History[0].EntryType);
        Assert.Equal(harness.UserContext.CurrentUser, created.History[0].ChangedBy);
        Assert.NotEqual(default, created.History[0].OccurredAtUtc);
    }

    [Fact]
    public async Task TICK_02_UpdateDetailsAsync_AllowsEditsWhileAssignedButRejectsInProgress()
    {
        var harness = CreateHarness();
        var ticket = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, harness.Worker.Id, TicketStatus.Assigned));

        var updated = await harness.Service.UpdateDetailsAsync(
            ticket.Id,
            "Replace brake assembly",
            TicketIssueType.Safety,
            TicketPriority.Critical,
            new DateTime(2026, 4, 3));

        Assert.Equal("Replace brake assembly", updated.Description);
        Assert.Equal(TicketIssueType.Safety, updated.IssueType);
        Assert.Equal(TicketPriority.Critical, updated.Priority);

        harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, harness.Worker.Id, TicketStatus.InProgress, Guid.NewGuid()));
        var inProgressTicket = harness.Tickets.Items.Last();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.UpdateDetailsAsync(
            inProgressTicket.Id,
            "No longer editable",
            TicketIssueType.Other,
            TicketPriority.Low,
            DateTime.UtcNow));
    }

    [Fact]
    public async Task TICK_03_DeleteCanceledAsync_DeletesOnlyCanceledTickets()
    {
        var harness = CreateHarness();
        var canceled = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, harness.Worker.Id, TicketStatus.Canceled));
        var pending = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, null, TicketStatus.Pending, Guid.NewGuid()));

        await harness.Service.DeleteCanceledAsync(canceled.Id);

        Assert.DoesNotContain(harness.Tickets.Items, ticket => ticket.Id == canceled.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.DeleteCanceledAsync(pending.Id));
    }

    [Fact]
    public async Task TICK_04_AssignWorkerAsync_SetsAssignedWorkerAndAssignedStatus()
    {
        var harness = CreateHarness();
        var ticket = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, null, TicketStatus.Pending));

        var updated = await harness.Service.AssignWorkerAsync(ticket.Id, harness.Worker.Id);

        Assert.Equal(TicketStatus.Assigned, updated.Status);
        Assert.Equal(harness.Worker.Id, updated.AssignedWorkerId);
        Assert.Equal(TicketAuditEntryType.WorkerAssigned, updated.History.Last().EntryType);
    }

    [Fact]
    public async Task TICK_05_UnassignWorkerAsync_ClearsWorkerAndReturnsPending()
    {
        var harness = CreateHarness();
        var ticket = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, harness.Worker.Id, TicketStatus.Assigned));

        var updated = await harness.Service.UnassignWorkerAsync(ticket.Id);

        Assert.Null(updated.AssignedWorkerId);
        Assert.Equal(TicketStatus.Pending, updated.Status);
        Assert.Equal(TicketAuditEntryType.WorkerUnassigned, updated.History.Last().EntryType);
    }

    [Fact]
    public async Task TICK_06_ChangeStatusAsync_AdvancesThroughAllowedWorkflow()
    {
        var harness = CreateHarness();
        var ticket = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, harness.Worker.Id, TicketStatus.Assigned));

        var inProgress = await harness.Service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress);
        var inProgressStatus = inProgress.Status;
        var resolved = await harness.Service.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved);
        var resolvedStatus = resolved.Status;
        var closed = await harness.Service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed);
        var closedStatus = closed.Status;

        Assert.Equal(TicketStatus.InProgress, inProgressStatus);
        Assert.Equal(TicketStatus.Resolved, resolvedStatus);
        Assert.Equal(TicketStatus.Closed, closedStatus);
    }

    [Fact]
    public async Task TICK_07_ChangeStatusAsync_AllowsManualStatusUpdatesWhenPolicyAllows()
    {
        var harness = CreateHarness();
        var ticket = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, harness.Worker.Id, TicketStatus.Assigned));

        var canceled = await harness.Service.CancelAsync(ticket.Id);

        Assert.Equal(TicketStatus.Canceled, canceled.Status);
        Assert.Equal(TicketAuditEntryType.Canceled, canceled.History.Last().EntryType);
    }

    [Fact]
    public async Task TICK_08_ChangeStatusAsync_RejectsInvalidTransitions()
    {
        var harness = CreateHarness();
        var ticket = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, null, TicketStatus.Pending));

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed));
    }

    [Fact]
    public async Task TICK_09_StatusChanges_WriteChangedByAndOccurredAtUtc()
    {
        var harness = CreateHarness();
        var ticket = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, harness.Worker.Id, TicketStatus.Assigned));

        var updated = await harness.Service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress);
        var auditEntry = updated.History.Last();

        Assert.Equal(harness.UserContext.CurrentUser, auditEntry.ChangedBy);
        Assert.NotEqual(default, auditEntry.OccurredAtUtc);
    }

    [Fact]
    public async Task TICK_10_AssignmentChanges_WriteChangedByAndOccurredAtUtc()
    {
        var harness = CreateHarness();
        var ticket = harness.Tickets.Seed(CreateTicket(harness.Elevator.Id, null, TicketStatus.Pending));

        var assigned = await harness.Service.AssignWorkerAsync(ticket.Id, harness.Worker.Id);
        var auditEntry = assigned.History.Last();

        Assert.Equal(harness.UserContext.CurrentUser, auditEntry.ChangedBy);
        Assert.NotEqual(default, auditEntry.OccurredAtUtc);
        Assert.Equal(harness.Worker.Id, auditEntry.ToWorkerId);
    }

    private static TicketServiceHarness CreateHarness()
    {
        var elevators = new InMemoryElevatorRepository();
        var workers = new InMemoryWorkerRepository();
        var tickets = new InMemoryTicketRepository();
        var userContext = new StubUserContext();
        var elevator = elevators.Seed(new Elevator
        {
            Id = Guid.NewGuid(),
            Name = "Tower A",
            Address = "100 Main St",
            BuildingName = "Main Tower",
            FloorLabel = "L1",
            Manufacturer = "Otis",
            InstallationDate = new DateTime(2020, 1, 1),
            IsActive = true,
            Location = CreatePoint(40.7128, -74.0060)
        });
        var worker = workers.Seed(new Worker
        {
            Id = Guid.NewGuid(),
            FullName = "Ava Stone",
            Email = "ava@example.com",
            PhoneNumber = "555-0100",
            Skills = ["Repair"],
            AvailabilityStatus = WorkerAvailabilityStatus.Available,
            Location = CreatePoint(40.7138, -74.0050)
        });

        return new TicketServiceHarness(
            new TicketService(tickets, elevators, workers, userContext),
            elevators,
            workers,
            tickets,
            userContext,
            elevator,
            worker);
    }

    private static Ticket CreateTicket(Guid elevatorId, Guid? workerId, TicketStatus status, Guid? id = null)
    {
        return new Ticket
        {
            Id = id ?? Guid.NewGuid(),
            ElevatorId = elevatorId,
            AssignedWorkerId = workerId,
            Description = "Initial ticket",
            IssueType = TicketIssueType.Mechanical,
            Priority = TicketPriority.Medium,
            RequestedDate = new DateTime(2026, 4, 2),
            Status = status,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            History =
            [
                new TicketAuditEntry
                {
                    OccurredAtUtc = DateTime.UtcNow.AddMinutes(-10),
                    ChangedBy = "seed",
                    EntryType = TicketAuditEntryType.Created,
                    ToStatus = status,
                    ToWorkerId = workerId,
                    Message = "Seeded ticket."
                }
            ]
        };
    }

    private static GeoJsonPoint<GeoJson2DGeographicCoordinates> CreatePoint(double latitude, double longitude)
    {
        return new GeoJsonPoint<GeoJson2DGeographicCoordinates>(new GeoJson2DGeographicCoordinates(longitude, latitude));
    }

    private sealed record TicketServiceHarness(
        TicketService Service,
        InMemoryElevatorRepository Elevators,
        InMemoryWorkerRepository Workers,
        InMemoryTicketRepository Tickets,
        StubUserContext UserContext,
        Elevator Elevator,
        Worker Worker);

    private sealed class StubUserContext : IUserContext
    {
        public string CurrentUser { get; } = "dispatch.user";

        public string GetCurrentUser() => CurrentUser;
    }

    private sealed class InMemoryElevatorRepository : IElevatorRepository
    {
        public List<Elevator> Items { get; } = new();

        public Elevator Seed(Elevator elevator)
        {
            Items.Add(elevator);
            return elevator;
        }

        public Task<Elevator?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(elevator => elevator.Id == id));
        public Task<IEnumerable<Elevator>> GetAllAsync() => Task.FromResult<IEnumerable<Elevator>>(Items.ToList());
        public Task<IEnumerable<Elevator>> FindAsync(FilterDefinition<Elevator> filter) => throw new NotSupportedException();
        public Task AddAsync(Elevator entity) => throw new NotSupportedException();
        public Task UpdateAsync(Elevator entity) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
        public Task<long> CountAsync(FilterDefinition<Elevator>? filter = null) => Task.FromResult((long)Items.Count);
        public Task<IEnumerable<Elevator>> GetActiveAsync() => Task.FromResult<IEnumerable<Elevator>>(Items.Where(elevator => elevator.IsActive && elevator.DeletedAt == null).ToList());
    }

    private sealed class InMemoryWorkerRepository : IWorkerRepository
    {
        public List<Worker> Items { get; } = new();

        public Worker Seed(Worker worker)
        {
            Items.Add(worker);
            return worker;
        }

        public Task<Worker?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(worker => worker.Id == id));
        public Task<IEnumerable<Worker>> GetAllAsync() => Task.FromResult<IEnumerable<Worker>>(Items.ToList());
        public Task<IEnumerable<Worker>> FindAsync(FilterDefinition<Worker> filter) => throw new NotSupportedException();
        public Task AddAsync(Worker entity) => throw new NotSupportedException();
        public Task UpdateAsync(Worker entity) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
        public Task<long> CountAsync(FilterDefinition<Worker>? filter = null) => Task.FromResult((long)Items.Count);
        public Task<IEnumerable<Worker>> GetActiveAsync() => Task.FromResult<IEnumerable<Worker>>(Items.Where(worker => worker.DeletedAt == null).ToList());
    }

    private sealed class InMemoryTicketRepository : ITicketRepository
    {
        public List<Ticket> Items { get; } = new();

        public Ticket Seed(Ticket ticket)
        {
            Items.Add(ticket);
            return ticket;
        }

        public Task<Ticket?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(ticket => ticket.Id == id));
        public Task<IEnumerable<Ticket>> GetAllAsync() => Task.FromResult<IEnumerable<Ticket>>(Items.ToList());
        public Task<IEnumerable<Ticket>> FindAsync(FilterDefinition<Ticket> filter) => throw new NotSupportedException();

        public Task AddAsync(Ticket entity)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Ticket entity)
        {
            var index = Items.FindIndex(ticket => ticket.Id == entity.Id);
            if (index >= 0)
            {
                Items[index] = entity;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id)
        {
            Items.RemoveAll(ticket => ticket.Id == id);
            return Task.CompletedTask;
        }

        public Task<long> CountAsync(FilterDefinition<Ticket>? filter = null) => Task.FromResult((long)Items.Count);
        public Task<IEnumerable<Ticket>> GetActiveAsync() => Task.FromResult<IEnumerable<Ticket>>(Items.Where(ticket => ticket.DeletedAt == null).ToList());

        public Task<Ticket?> UpdateDetailsAsync(Guid ticketId, string description, TicketIssueType issueType, TicketPriority priority, DateTime requestedDate, DateTime changedAtUtc, string changedBy)
        {
            var ticket = Items.FirstOrDefault(item => item.Id == ticketId);
            if (ticket == null)
            {
                return Task.FromResult<Ticket?>(null);
            }

            ticket.Description = description;
            ticket.IssueType = issueType;
            ticket.Priority = priority;
            ticket.RequestedDate = requestedDate;
            ticket.UpdatedAt = changedAtUtc;
            ticket.History.Add(new TicketAuditEntry
            {
                OccurredAtUtc = changedAtUtc,
                ChangedBy = changedBy,
                EntryType = TicketAuditEntryType.DetailsUpdated,
                Message = "Updated ticket details."
            });

            return Task.FromResult<Ticket?>(ticket);
        }

        public Task<Ticket?> AssignWorkerAsync(Guid ticketId, Guid workerId, DateTime changedAtUtc, string changedBy)
        {
            var ticket = Items.FirstOrDefault(item => item.Id == ticketId);
            if (ticket == null)
            {
                return Task.FromResult<Ticket?>(null);
            }

            ticket.AssignedWorkerId = workerId;
            ticket.Status = TicketStatus.Assigned;
            ticket.UpdatedAt = changedAtUtc;
            ticket.History.Add(new TicketAuditEntry
            {
                OccurredAtUtc = changedAtUtc,
                ChangedBy = changedBy,
                EntryType = TicketAuditEntryType.WorkerAssigned,
                FromStatus = TicketStatus.Pending,
                ToStatus = TicketStatus.Assigned,
                ToWorkerId = workerId,
                Message = "Assigned worker to ticket."
            });

            return Task.FromResult<Ticket?>(ticket);
        }

        public Task<Ticket?> UnassignWorkerAsync(Guid ticketId, DateTime changedAtUtc, string changedBy)
        {
            var ticket = Items.FirstOrDefault(item => item.Id == ticketId);
            if (ticket == null)
            {
                return Task.FromResult<Ticket?>(null);
            }

            var priorWorkerId = ticket.AssignedWorkerId;
            ticket.AssignedWorkerId = null;
            ticket.Status = TicketStatus.Pending;
            ticket.UpdatedAt = changedAtUtc;
            ticket.History.Add(new TicketAuditEntry
            {
                OccurredAtUtc = changedAtUtc,
                ChangedBy = changedBy,
                EntryType = TicketAuditEntryType.WorkerUnassigned,
                FromStatus = TicketStatus.Assigned,
                ToStatus = TicketStatus.Pending,
                FromWorkerId = priorWorkerId,
                Message = "Removed worker assignment from ticket."
            });

            return Task.FromResult<Ticket?>(ticket);
        }

        public Task<Ticket?> ChangeStatusAsync(Guid ticketId, TicketStatus fromStatus, TicketStatus toStatus, DateTime changedAtUtc, string changedBy)
        {
            var ticket = Items.FirstOrDefault(item => item.Id == ticketId && item.Status == fromStatus);
            if (ticket == null)
            {
                return Task.FromResult<Ticket?>(null);
            }

            ticket.Status = toStatus;
            ticket.UpdatedAt = changedAtUtc;
            ticket.History.Add(new TicketAuditEntry
            {
                OccurredAtUtc = changedAtUtc,
                ChangedBy = changedBy,
                EntryType = toStatus == TicketStatus.Canceled ? TicketAuditEntryType.Canceled : TicketAuditEntryType.StatusChanged,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ToWorkerId = ticket.AssignedWorkerId,
                Message = $"Changed status from {fromStatus} to {toStatus}."
            });

            return Task.FromResult<Ticket?>(ticket);
        }

        public Task<bool> DeleteCanceledAsync(Guid ticketId)
        {
            var deleted = Items.RemoveAll(ticket => ticket.Id == ticketId && ticket.Status == TicketStatus.Canceled) > 0;
            return Task.FromResult(deleted);
        }
    }
}
