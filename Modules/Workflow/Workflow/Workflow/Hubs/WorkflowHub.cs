using Microsoft.AspNetCore.SignalR;

namespace Workflow.Workflow.Hubs;

public class WorkflowHub : Hub
{
    public async Task JoinWorkflowGroup(string workflowInstanceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"workflow-{workflowInstanceId}");
    }

    public async Task LeaveWorkflowGroup(string workflowInstanceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workflow-{workflowInstanceId}");
    }

    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }

    public async Task LeaveUserGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
    }

    public async Task JoinPoolGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"pool-{groupName}");
    }

    public async Task LeavePoolGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"pool-{groupName}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up any group memberships on disconnect
        await base.OnDisconnectedAsync(exception);
    }
}