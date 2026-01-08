using AiAgents.ContentModerationAgent.Application.Runners;
using AiAgents.ContentModerationAgent.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AiAgents.ContentModerationAgent.Web.BackgroundServices;

public class ModerationAgentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<ModerationHub> _hubContext;
    private readonly ILogger<ModerationAgentBackgroundService> _logger;

    public ModerationAgentBackgroundService(
        IServiceProvider serviceProvider,
        IHubContext<ModerationHub> hubContext,
        ILogger<ModerationAgentBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ModerationAgentBackgroundService started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ModerationAgentRunner>();

                // Execute one tick
                var result = await runner.TickAsync(stoppingToken);

                if (result != null)
                {
                    // Emit SignalR event
                    await _hubContext.Clients.All.SendAsync(
                        "ModerationResult",
                        result.ContentId.ToString(),
                        result.Decision.ToString(),
                        result.FinalScore,
                        result.NewStatus.ToString(),
                        cancellationToken: stoppingToken);

                    _logger.LogInformation(
                        "✓ Processed content {ContentId}: {Decision} (Score: {Score}, Status: {Status})",
                        result.ContentId, result.Decision, result.FinalScore, result.NewStatus);
                }
                else
                {
                    // No work available, wait longer
                    _logger.LogDebug("No content in queue, waiting...");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                // Small delay between ticks
                await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ Error in moderation agent background service: {Message}\nStack: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                // Wait longer on error to avoid spamming logs
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
