using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;
using ElevatorMaintenanceSystem.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver.GeoJsonObjectModel;
using Xunit;

namespace ElevatorMaintenanceSystem.Tests.ViewModels;

public class TicketManagementViewModelTests
{
    [Fact]
    public async Task LoadTicketsAsync_PopulatesTicketsElevatorsAndWorkers()
    {
        var fixture = CreateFixture();

        await fixture.ViewModel.LoadTicketsAsync();

        Assert.Equal(3, fixture.ViewModel.Tickets.Count);
        Assert.Single(fixture.ViewModel.Elevators);
        Assert.Single(fixture.ViewModel.Workers);
        Assert.Equal("Second ticket", fixture.ViewModel.TicketRows[0].Description);
    }

    [Fact]
    public async Task SelectedTicketRow_SynchronizesSelectedTicketAndAuditTrail()
    {
        var fixture = CreateFixture();
        await fixture.ViewModel.LoadTicketsAsync();

        fixture.ViewModel.SelectedTicketRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.AssignedTicket.Id);

        Assert.NotNull(fixture.ViewModel.SelectedTicket);
        Assert.Equal(fixture.AssignedTicket.Id, fixture.ViewModel.SelectedTicket!.Id);
        Assert.Equal(fixture.AssignedTicket.Description, fixture.ViewModel.Description);
        Assert.Equal(fixture.Worker.Id, fixture.ViewModel.SelectedWorkerId);
        Assert.Single(fixture.ViewModel.AuditTrail);
    }

    [Fact]
    public async Task UpdateDetailsCommand_DisabledWhenStatusIsInProgressOrLater()
    {
        var fixture = CreateFixture();
        await fixture.ViewModel.LoadTicketsAsync();

        fixture.ViewModel.SelectedTicketRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.AssignedTicket.Id);
        Assert.True(fixture.ViewModel.UpdateDetailsCommand.CanExecute(null));

        fixture.ViewModel.SelectedTicketRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.InProgressTicket.Id);
        Assert.False(fixture.ViewModel.UpdateDetailsCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteCanceledCommand_EnabledOnlyForCanceledTickets()
    {
        var fixture = CreateFixture();
        await fixture.ViewModel.LoadTicketsAsync();

        fixture.ViewModel.SelectedTicketRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.AssignedTicket.Id);
        Assert.False(fixture.ViewModel.DeleteCanceledCommand.CanExecute(null));

        var canceled = await fixture.Tickets.CancelAsync(fixture.AssignedTicket.Id);
        fixture.Tickets.Replace(canceled);
        await fixture.ViewModel.LoadTicketsAsync();
        fixture.ViewModel.SelectedTicketRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.AssignedTicket.Id);

        Assert.True(fixture.ViewModel.DeleteCanceledCommand.CanExecute(null));
    }

    [Fact]
    public async Task AssignAndUnassignCommands_CallTicketServiceAndRefreshStatusMessage()
    {
        var fixture = CreateFixture();
        await fixture.ViewModel.LoadTicketsAsync();

        fixture.ViewModel.SelectedTicketRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.PendingTicket.Id);
        fixture.ViewModel.SelectedWorkerId = fixture.Worker.Id;
        await fixture.ViewModel.AssignWorkerCommand.ExecuteAsync();

        Assert.Equal(1, fixture.Tickets.AssignCalls);
        var assignedRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.PendingTicket.Id);
        Assert.Equal("Ava Stone", assignedRow.AssignedWorkerDisplay);
        Assert.Contains("Assigned worker", fixture.ViewModel.StatusMessage);

        await fixture.ViewModel.UnassignWorkerCommand.ExecuteAsync();

        Assert.Equal(1, fixture.Tickets.UnassignCalls);
        var unassignedRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.PendingTicket.Id);
        Assert.Equal("Unassigned", unassignedRow.AssignedWorkerDisplay);
        Assert.Contains("Unassigned worker", fixture.ViewModel.StatusMessage);
    }

    [Fact]
    public async Task ChangeStatusCommand_SurfacesInvalidTransitionErrors()
    {
        var fixture = CreateFixture();
        await fixture.ViewModel.LoadTicketsAsync();

        fixture.ViewModel.SelectedTicketRow = fixture.ViewModel.TicketRows.First(row => row.TicketId == fixture.PendingTicket.Id);
        fixture.ViewModel.NextStatus = TicketStatus.Closed;
        await fixture.ViewModel.ChangeStatusCommand.ExecuteAsync();

        Assert.Contains("Cannot move ticket", fixture.ViewModel.StatusMessage);
    }

    private static TicketManagementFixture CreateFixture()
    {
        var elevator = new Elevator
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
        };

        var worker = new Worker
        {
            Id = Guid.NewGuid(),
            FullName = "Ava Stone",
            Email = "ava@example.com",
            PhoneNumber = "555-0100",
            Skills = ["Repair"],
            AvailabilityStatus = WorkerAvailabilityStatus.Available,
            Location = CreatePoint(40.7138, -74.0050)
        };

        var pendingTicket = CreateTicket(elevator.Id, null, TicketStatus.Pending, "First ticket", new DateTime(2026, 4, 2));
        var assignedTicket = CreateTicket(elevator.Id, worker.Id, TicketStatus.Assigned, "Second ticket", new DateTime(2026, 4, 3));
        var inProgressTicket = CreateTicket(elevator.Id, worker.Id, TicketStatus.InProgress, "Third ticket", new DateTime(2026, 4, 1));

        var ticketService = new FakeTicketService(pendingTicket, assignedTicket, inProgressTicket);
        var elevatorService = new FakeElevatorService(elevator);
        var workerService = new FakeWorkerService(worker);
        var viewModel = new TicketManagementViewModel(
            ticketService,
            elevatorService,
            workerService,
            NullLogger<TicketManagementViewModel>.Instance);

        return new TicketManagementFixture(viewModel, ticketService, worker, pendingTicket, assignedTicket, inProgressTicket);
    }

    private static Ticket CreateTicket(Guid elevatorId, Guid? workerId, TicketStatus status, string description, DateTime requestedDate)
    {
        return new Ticket
        {
            Id = Guid.NewGuid(),
            ElevatorId = elevatorId,
            AssignedWorkerId = workerId,
            Description = description,
            IssueType = TicketIssueType.Mechanical,
            Priority = TicketPriority.Medium,
            RequestedDate = requestedDate,
            Status = status,
            History =
            [
                new TicketAuditEntry
                {
                    OccurredAtUtc = requestedDate.AddHours(1),
                    ChangedBy = "dispatch.user",
                    EntryType = TicketAuditEntryType.Created,
                    ToStatus = status,
                    ToWorkerId = workerId,
                    Message = "Created ticket."
                }
            ]
        };
    }

    private static GeoJsonPoint<GeoJson2DGeographicCoordinates> CreatePoint(double latitude, double longitude)
    {
        return new GeoJsonPoint<GeoJson2DGeographicCoordinates>(new GeoJson2DGeographicCoordinates(longitude, latitude));
    }

    private sealed record TicketManagementFixture(
        TicketManagementViewModel ViewModel,
        FakeTicketService Tickets,
        Worker Worker,
        Ticket PendingTicket,
        Ticket AssignedTicket,
        Ticket InProgressTicket);

    private sealed class FakeElevatorService : IElevatorService
    {
        private readonly Elevator _elevator;

        public FakeElevatorService(Elevator elevator)
        {
            _elevator = elevator;
        }

        public Task<IReadOnlyList<Elevator>> GetActiveAsync() => Task.FromResult<IReadOnlyList<Elevator>>([_elevator]);
        public Task<Elevator> CreateAsync(Elevator elevator, double latitude, double longitude) => throw new NotSupportedException();
        public Task<Elevator> UpdateAsync(Elevator elevator, double latitude, double longitude) => throw new NotSupportedException();
        public Task DeleteInactiveAsync(Guid id) => throw new NotSupportedException();
    }

    private sealed class FakeWorkerService : IWorkerService
    {
        private readonly Worker _worker;

        public FakeWorkerService(Worker worker)
        {
            _worker = worker;
        }

        public Task<IReadOnlyList<Worker>> GetActiveAsync() => Task.FromResult<IReadOnlyList<Worker>>([_worker]);
        public Task<Worker> CreateAsync(Worker worker, double latitude, double longitude) => throw new NotSupportedException();
        public Task<Worker> UpdateAsync(Worker worker, double latitude, double longitude) => throw new NotSupportedException();
        public Task<Worker> DeactivateAsync(Guid id) => throw new NotSupportedException();
        public Task<Worker> UpdateLocationAsync(Guid id, double latitude, double longitude) => throw new NotSupportedException();
    }

    private sealed class FakeTicketService : ITicketService
    {
        private readonly List<Ticket> _items;

        public FakeTicketService(params Ticket[] items)
        {
            _items = items.ToList();
        }

        public int AssignCalls { get; private set; }

        public int UnassignCalls { get; private set; }

        public Task<IReadOnlyList<Ticket>> GetActiveAsync()
        {
            return Task.FromResult<IReadOnlyList<Ticket>>(_items.OrderByDescending(ticket => ticket.RequestedDate).ToList());
        }

        public Task<Ticket> CreateAsync(Guid elevatorId, string description, TicketIssueType issueType, TicketPriority priority, DateTime requestedDate)
        {
            var created = CreateTicket(elevatorId, null, TicketStatus.Pending, description.Trim(), requestedDate);
            _items.Add(created);
            return Task.FromResult(created);
        }

        public Task<Ticket> UpdateDetailsAsync(Guid ticketId, string description, TicketIssueType issueType, TicketPriority priority, DateTime requestedDate)
        {
            var ticket = RequireTicket(ticketId);
            if (!TicketWorkflowPolicy.CanEditDetails(ticket.Status))
            {
                throw new InvalidOperationException($"Ticket '{ticketId}' cannot be edited while {ticket.Status}.");
            }

            ticket.Description = description;
            ticket.IssueType = issueType;
            ticket.Priority = priority;
            ticket.RequestedDate = requestedDate;
            return Task.FromResult(ticket);
        }

        public Task<Ticket> AssignWorkerAsync(Guid ticketId, Guid workerId)
        {
            var ticket = RequireTicket(ticketId);
            AssignCalls++;
            ticket.AssignedWorkerId = workerId;
            ticket.Status = TicketStatus.Assigned;
            return Task.FromResult(ticket);
        }

        public Task<Ticket> UnassignWorkerAsync(Guid ticketId)
        {
            var ticket = RequireTicket(ticketId);
            UnassignCalls++;
            ticket.AssignedWorkerId = null;
            ticket.Status = TicketStatus.Pending;
            return Task.FromResult(ticket);
        }

        public Task<Ticket> ChangeStatusAsync(Guid ticketId, TicketStatus nextStatus)
        {
            var ticket = RequireTicket(ticketId);
            if (!TicketWorkflowPolicy.CanMove(ticket.Status, nextStatus))
            {
                throw new InvalidOperationException($"Cannot move ticket from {ticket.Status} to {nextStatus}.");
            }

            ticket.Status = nextStatus;
            return Task.FromResult(ticket);
        }

        public Task<Ticket> CancelAsync(Guid ticketId)
        {
            return ChangeStatusAsync(ticketId, TicketStatus.Canceled);
        }

        public Task DeleteCanceledAsync(Guid ticketId)
        {
            var deleted = _items.RemoveAll(ticket => ticket.Id == ticketId && ticket.Status == TicketStatus.Canceled);
            if (deleted == 0)
            {
                throw new InvalidOperationException("Only canceled tickets can be deleted.");
            }

            return Task.CompletedTask;
        }

        public void Replace(Ticket ticket)
        {
            var index = _items.FindIndex(item => item.Id == ticket.Id);
            if (index >= 0)
            {
                _items[index] = ticket;
            }
        }

        private Ticket RequireTicket(Guid ticketId)
        {
            return _items.First(ticket => ticket.Id == ticketId);
        }
    }
}
