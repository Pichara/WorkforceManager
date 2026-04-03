using CommunityToolkit.Mvvm.ComponentModel;
using ElevatorMaintenanceSystem.Infrastructure.Commands;
using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace ElevatorMaintenanceSystem.ViewModels;

public partial class TicketManagementViewModel : ViewModelBase
{
    private readonly ITicketService _ticketService;
    private readonly IElevatorService _elevatorService;
    private readonly IWorkerService _workerService;
    private readonly ILogger<TicketManagementViewModel> _logger;

    [ObservableProperty]
    private TicketRowViewModel? _selectedTicketRow;

    [ObservableProperty]
    private Ticket? _selectedTicket;

    [ObservableProperty]
    private Guid? _selectedElevatorId;

    [ObservableProperty]
    private Guid? _selectedWorkerId;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private TicketIssueType _issueType = TicketIssueType.Mechanical;

    [ObservableProperty]
    private TicketPriority _priority = TicketPriority.Medium;

    [ObservableProperty]
    private DateTime _requestedDate = DateTime.UtcNow.Date;

    [ObservableProperty]
    private TicketStatus _nextStatus = TicketStatus.Pending;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    private bool _isBusy;

    public TicketManagementViewModel(
        ITicketService ticketService,
        IElevatorService elevatorService,
        IWorkerService workerService,
        ILogger<TicketManagementViewModel> logger)
    {
        _ticketService = ticketService ?? throw new ArgumentNullException(nameof(ticketService));
        _elevatorService = elevatorService ?? throw new ArgumentNullException(nameof(elevatorService));
        _workerService = workerService ?? throw new ArgumentNullException(nameof(workerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadCommand = new AsyncRelayCommand(LoadTicketsAsync, () => !IsBusy);
        CreateCommand = new AsyncRelayCommand(CreateAsync, CanCreate);
        UpdateDetailsCommand = new AsyncRelayCommand(UpdateDetailsAsync, CanUpdateDetails);
        AssignWorkerCommand = new AsyncRelayCommand(AssignWorkerAsync, CanAssignWorker);
        UnassignWorkerCommand = new AsyncRelayCommand(UnassignWorkerAsync, CanUnassignWorker);
        ChangeStatusCommand = new AsyncRelayCommand(ChangeStatusAsync, CanChangeStatus);
        CancelCommand = new AsyncRelayCommand(CancelAsync, CanCancel);
        DeleteCanceledCommand = new AsyncRelayCommand(DeleteCanceledAsync, CanDeleteCanceled);
        ResetCommand = new AsyncRelayCommand(ResetAsync, () => !IsBusy);
    }

    public ObservableCollection<Ticket> Tickets { get; } = new();

    public ObservableCollection<TicketRowViewModel> TicketRows { get; } = new();

    public ObservableCollection<Elevator> Elevators { get; } = new();

    public ObservableCollection<Worker> Workers { get; } = new();

    public ObservableCollection<TicketAuditEntry> AuditTrail { get; } = new();

    public AsyncRelayCommand LoadCommand { get; }

    public AsyncRelayCommand CreateCommand { get; }

    public AsyncRelayCommand UpdateDetailsCommand { get; }

    public AsyncRelayCommand AssignWorkerCommand { get; }

    public AsyncRelayCommand UnassignWorkerCommand { get; }

    public AsyncRelayCommand ChangeStatusCommand { get; }

    public AsyncRelayCommand CancelCommand { get; }

    public AsyncRelayCommand DeleteCanceledCommand { get; }

    public AsyncRelayCommand ResetCommand { get; }

    public async Task LoadTicketsAsync()
    {
        await RunBusyOperationAsync(async () =>
        {
            var selectedTicketId = SelectedTicket?.Id;
            var tickets = await _ticketService.GetActiveAsync();
            var elevators = await _elevatorService.GetActiveAsync();
            var workers = await _workerService.GetActiveAsync();

            ReplaceCollection(Tickets, tickets.OrderByDescending(ticket => ticket.RequestedDate));
            ReplaceCollection(Elevators, elevators.OrderBy(elevator => elevator.Name));
            ReplaceCollection(Workers, workers.OrderBy(worker => worker.FullName));
            RebuildTicketRows();
            RestoreSelection(selectedTicketId);

            StatusMessage = $"Loaded {TicketRows.Count} tickets, {Elevators.Count} elevators, and {Workers.Count} workers.";
        }, "Loading tickets failed.");
    }

    partial void OnSelectedTicketRowChanged(TicketRowViewModel? value)
    {
        ApplySelection(value);
    }

    partial void OnSelectedElevatorIdChanged(Guid? value) => RefreshCommandStates();
    partial void OnSelectedWorkerIdChanged(Guid? value) => RefreshCommandStates();
    partial void OnDescriptionChanged(string value) => RefreshCommandStates();
    partial void OnIssueTypeChanged(TicketIssueType value) => RefreshCommandStates();
    partial void OnPriorityChanged(TicketPriority value) => RefreshCommandStates();
    partial void OnRequestedDateChanged(DateTime value) => RefreshCommandStates();
    partial void OnNextStatusChanged(TicketStatus value) => RefreshCommandStates();
    partial void OnIsBusyChanged(bool value) => RefreshCommandStates();

    private async Task CreateAsync()
    {
        if (!CanCreate())
        {
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            var created = await _ticketService.CreateAsync(
                SelectedElevatorId!.Value,
                Description,
                IssueType,
                Priority,
                RequestedDate);

            UpsertTicket(created);
            RestoreSelection(created.Id);
            StatusMessage = $"Created ticket '{created.Description}'.";
        }, "Creating ticket failed.");
    }

    private async Task UpdateDetailsAsync()
    {
        if (!CanUpdateDetails())
        {
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            var updated = await _ticketService.UpdateDetailsAsync(
                SelectedTicket!.Id,
                Description,
                IssueType,
                Priority,
                RequestedDate);

            UpsertTicket(updated);
            RestoreSelection(updated.Id);
            StatusMessage = $"Updated ticket '{updated.Description}'.";
        }, "Updating ticket details failed.");
    }

    private async Task AssignWorkerAsync()
    {
        if (!CanAssignWorker())
        {
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            var updated = await _ticketService.AssignWorkerAsync(SelectedTicket!.Id, SelectedWorkerId!.Value);
            UpsertTicket(updated);
            RestoreSelection(updated.Id);
            StatusMessage = "Assigned worker to selected ticket.";
        }, "Assigning worker failed.");
    }

    private async Task UnassignWorkerAsync()
    {
        if (!CanUnassignWorker())
        {
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            var updated = await _ticketService.UnassignWorkerAsync(SelectedTicket!.Id);
            UpsertTicket(updated);
            RestoreSelection(updated.Id);
            StatusMessage = "Unassigned worker from selected ticket.";
        }, "Unassigning worker failed.");
    }

    private async Task ChangeStatusAsync()
    {
        if (!CanChangeStatus())
        {
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            var updated = await _ticketService.ChangeStatusAsync(SelectedTicket!.Id, NextStatus);
            UpsertTicket(updated);
            RestoreSelection(updated.Id);
            StatusMessage = $"Changed ticket status to {FormatStatus(updated.Status)}.";
        }, "Changing ticket status failed.");
    }

    private async Task CancelAsync()
    {
        if (!CanCancel())
        {
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            var updated = await _ticketService.CancelAsync(SelectedTicket!.Id);
            UpsertTicket(updated);
            RestoreSelection(updated.Id);
            StatusMessage = "Canceled selected ticket.";
        }, "Canceling ticket failed.");
    }

    private async Task DeleteCanceledAsync()
    {
        if (!CanDeleteCanceled())
        {
            return;
        }

        await RunBusyOperationAsync(async () =>
        {
            var ticketId = SelectedTicket!.Id;
            await _ticketService.DeleteCanceledAsync(ticketId);
            RemoveTicket(ticketId);
            ClearSelection(false);
            StatusMessage = "Deleted canceled ticket.";
        }, "Deleting canceled ticket failed.");
    }

    private Task ResetAsync()
    {
        ClearSelection();
        StatusMessage = "Ticket editor reset.";
        return Task.CompletedTask;
    }

    private bool CanCreate()
    {
        return !IsBusy
            && SelectedElevatorId.HasValue
            && !string.IsNullOrWhiteSpace(Description);
    }

    private bool CanUpdateDetails()
    {
        return !IsBusy
            && SelectedTicket != null
            && TicketWorkflowPolicy.CanEditDetails(SelectedTicket.Status)
            && !string.IsNullOrWhiteSpace(Description);
    }

    private bool CanAssignWorker()
    {
        return !IsBusy
            && SelectedTicket != null
            && SelectedWorkerId.HasValue
            && TicketWorkflowPolicy.CanMove(SelectedTicket.Status, TicketStatus.Assigned);
    }

    private bool CanUnassignWorker()
    {
        return !IsBusy
            && SelectedTicket?.AssignedWorkerId != null
            && TicketWorkflowPolicy.CanMove(SelectedTicket.Status, TicketStatus.Pending);
    }

    private bool CanChangeStatus()
    {
        return !IsBusy
            && SelectedTicket != null
            && SelectedTicket.Status != NextStatus;
    }

    private bool CanCancel()
    {
        return !IsBusy
            && SelectedTicket != null
            && TicketWorkflowPolicy.CanMove(SelectedTicket.Status, TicketStatus.Canceled);
    }

    private bool CanDeleteCanceled()
    {
        return !IsBusy
            && SelectedTicket != null
            && SelectedTicket.Status == TicketStatus.Canceled;
    }

    private void ApplySelection(TicketRowViewModel? row)
    {
        if (row == null)
        {
            SelectedTicket = null;
            RefreshAuditTrail(null);
            ResetEditorFields();
            return;
        }

        var ticket = Tickets.FirstOrDefault(item => item.Id == row.TicketId);
        if (ticket == null)
        {
            SelectedTicket = null;
            RefreshAuditTrail(null);
            ResetEditorFields();
            return;
        }

        SelectedTicket = ticket;
        SelectedElevatorId = ticket.ElevatorId;
        SelectedWorkerId = ticket.AssignedWorkerId;
        Description = ticket.Description;
        IssueType = ticket.IssueType;
        Priority = ticket.Priority;
        RequestedDate = ticket.RequestedDate;
        NextStatus = ticket.Status;
        RefreshAuditTrail(ticket);
        StatusMessage = $"Editing ticket '{ticket.Description}'.";
        RefreshCommandStates();
    }

    private void RefreshAuditTrail(Ticket? ticket)
    {
        AuditTrail.Clear();

        if (ticket == null)
        {
            return;
        }

        AddRange(
            AuditTrail,
            ticket.History
                .OrderByDescending(entry => entry.OccurredAtUtc)
                .ToList());
    }

    private void UpsertTicket(Ticket ticket)
    {
        var index = Tickets
            .Select((item, position) => new { item, position })
            .FirstOrDefault(entry => entry.item.Id == ticket.Id)?
            .position;

        if (index.HasValue)
        {
            Tickets[index.Value] = ticket;
        }
        else
        {
            Tickets.Add(ticket);
        }

        SortTickets();
        RebuildTicketRows();
    }

    private void RemoveTicket(Guid ticketId)
    {
        var ticket = Tickets.FirstOrDefault(item => item.Id == ticketId);
        if (ticket != null)
        {
            Tickets.Remove(ticket);
        }

        RebuildTicketRows();
    }

    private void SortTickets()
    {
        var sorted = Tickets.OrderByDescending(ticket => ticket.RequestedDate).ToList();
        ReplaceCollection(Tickets, sorted);
    }

    private void RebuildTicketRows()
    {
        TicketRows.Clear();
        AddRange(TicketRows, Tickets.Select(BuildRow));
    }

    private TicketRowViewModel BuildRow(Ticket ticket)
    {
        var assignedWorkerDisplay = "Unassigned";

        if (ticket.AssignedWorkerId.HasValue)
        {
            assignedWorkerDisplay = Workers
                .FirstOrDefault(worker => worker.Id == ticket.AssignedWorkerId.Value)
                ?.FullName
                ?? ticket.AssignedWorkerId.Value.ToString();
        }

        return new TicketRowViewModel
        {
            TicketId = ticket.Id,
            Description = ticket.Description,
            IssueType = ticket.IssueType,
            Priority = ticket.Priority,
            Status = ticket.Status,
            RequestedDate = ticket.RequestedDate,
            AssignedWorkerDisplay = assignedWorkerDisplay
        };
    }

    private void RestoreSelection(Guid? ticketId)
    {
        if (!ticketId.HasValue)
        {
            if (TicketRows.Count == 0)
            {
                ClearSelection(false);
            }

            return;
        }

        SelectedTicketRow = TicketRows.FirstOrDefault(row => row.TicketId == ticketId.Value);
    }

    private void ClearSelection(bool clearStatus = true)
    {
        SelectedTicket = null;
        SelectedTicketRow = null;
        RefreshAuditTrail(null);
        ResetEditorFields();

        if (clearStatus)
        {
            StatusMessage = "Ready.";
        }
    }

    private void ResetEditorFields()
    {
        SelectedElevatorId = Elevators.FirstOrDefault()?.Id;
        SelectedWorkerId = null;
        Description = string.Empty;
        IssueType = TicketIssueType.Mechanical;
        Priority = TicketPriority.Medium;
        RequestedDate = DateTime.UtcNow.Date;
        NextStatus = TicketStatus.Pending;
        RefreshCommandStates();
    }

    private void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        AddRange(collection, items);
    }

    private async Task RunBusyOperationAsync(Func<Task> action, string failureMessage)
    {
        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _logger.LogError(ex, failureMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCommandStates()
    {
        LoadCommand.RaiseCanExecuteChanged();
        CreateCommand.RaiseCanExecuteChanged();
        UpdateDetailsCommand.RaiseCanExecuteChanged();
        AssignWorkerCommand.RaiseCanExecuteChanged();
        UnassignWorkerCommand.RaiseCanExecuteChanged();
        ChangeStatusCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        DeleteCanceledCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
    }

    private static string FormatStatus(TicketStatus status)
    {
        return status == TicketStatus.InProgress ? "In Progress" : status.ToString();
    }
}
