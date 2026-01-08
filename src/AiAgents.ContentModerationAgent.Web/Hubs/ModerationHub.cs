using Microsoft.AspNetCore.SignalR;

namespace AiAgents.ContentModerationAgent.Web.Hubs;

public class ModerationHub : Hub
{
    public async Task SendModerationResult(string contentId, string decision, double score, string status)
    {
        await Clients.All.SendAsync("ModerationResult", contentId, decision, score, status);
    }
}
