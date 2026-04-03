using ElevatorMaintenanceSystem.Models;
using ElevatorMaintenanceSystem.Services;
using Xunit;

namespace ElevatorMaintenanceSystem.Tests.Services;

public class TicketWorkflowPolicyTests
{
    [Fact]
    public void CanMove_AllowsExpectedWorkflowTransitions()
    {
        Assert.True(TicketWorkflowPolicy.CanMove(TicketStatus.Pending, TicketStatus.Assigned));
        Assert.True(TicketWorkflowPolicy.CanMove(TicketStatus.Assigned, TicketStatus.Pending));
        Assert.True(TicketWorkflowPolicy.CanMove(TicketStatus.Assigned, TicketStatus.InProgress));
        Assert.True(TicketWorkflowPolicy.CanMove(TicketStatus.InProgress, TicketStatus.Resolved));
        Assert.True(TicketWorkflowPolicy.CanMove(TicketStatus.Resolved, TicketStatus.Closed));
    }

    [Fact]
    public void CanMove_BlocksInvalidJumpsAndCanceledOutboundTransitions()
    {
        Assert.False(TicketWorkflowPolicy.CanMove(TicketStatus.Pending, TicketStatus.Closed));
        Assert.False(TicketWorkflowPolicy.CanMove(TicketStatus.Canceled, TicketStatus.Pending));
        Assert.False(TicketWorkflowPolicy.CanMove(TicketStatus.Canceled, TicketStatus.Assigned));
    }

    [Fact]
    public void CanEditDetails_AllowsPendingAndAssignedOnly()
    {
        Assert.True(TicketWorkflowPolicy.CanEditDetails(TicketStatus.Pending));
        Assert.True(TicketWorkflowPolicy.CanEditDetails(TicketStatus.Assigned));
        Assert.False(TicketWorkflowPolicy.CanEditDetails(TicketStatus.InProgress));
        Assert.False(TicketWorkflowPolicy.CanEditDetails(TicketStatus.Resolved));
        Assert.False(TicketWorkflowPolicy.CanEditDetails(TicketStatus.Closed));
        Assert.False(TicketWorkflowPolicy.CanEditDetails(TicketStatus.Canceled));
    }
}
