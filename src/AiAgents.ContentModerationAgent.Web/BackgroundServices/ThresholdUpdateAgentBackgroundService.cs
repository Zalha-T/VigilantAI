using AiAgents.ContentModerationAgent.Application.Runners;

namespace AiAgents.ContentModerationAgent.Web.BackgroundServices;

public class ThresholdUpdateAgentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ThresholdUpdateAgentBackgroundService> _logger;

    public ThresholdUpdateAgentBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ThresholdUpdateAgentBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ThresholdUpdateRunner>();

                // Execute one tick (checks if thresholds need updating)
                var updated = await runner.TickAsync(stoppingToken);

                if (updated)
                {
                    _logger.LogInformation("Thresholds updated based on feedback");
                }

                // Check less frequently (every hour)
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in threshold update agent background service");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
