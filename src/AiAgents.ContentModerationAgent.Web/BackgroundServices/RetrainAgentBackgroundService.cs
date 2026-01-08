using AiAgents.ContentModerationAgent.Application.Runners;
using AiAgents.ContentModerationAgent.Application.Services;

namespace AiAgents.ContentModerationAgent.Web.BackgroundServices;

public class RetrainAgentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetrainAgentBackgroundService> _logger;

    public RetrainAgentBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RetrainAgentBackgroundService> logger)
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
                var runner = scope.ServiceProvider.GetRequiredService<RetrainAgentRunner>();

                // Execute one tick (checks if retraining is needed)
                var retrained = await runner.TickAsync(stoppingToken);

                if (retrained)
                {
                    _logger.LogInformation("Model retraining completed successfully");
                }
                else
                {
                    // Log why retraining didn't happen (for debugging)
                    using var debugScope = _serviceProvider.CreateScope();
                    var thresholdService = debugScope.ServiceProvider.GetRequiredService<AiAgents.ContentModerationAgent.Application.Services.IThresholdService>();
                    var settings = await thresholdService.GetSettingsAsync(stoppingToken);
                    _logger.LogDebug($"Retraining check: NewGoldSinceLastTrain={settings.NewGoldSinceLastTrain}, RetrainThreshold={settings.RetrainThreshold}, RetrainingEnabled={settings.RetrainingEnabled}");
                }

                // Check less frequently (every 5 minutes)
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in retrain agent background service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
