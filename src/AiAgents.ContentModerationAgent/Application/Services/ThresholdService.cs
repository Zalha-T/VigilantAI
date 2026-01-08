using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class ThresholdService : IThresholdService
{
    private readonly ContentModerationDbContext _context;

    public ThresholdService(ContentModerationDbContext context)
    {
        _context = context;
    }

    public async Task<SystemSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        
        if (settings == null)
        {
            // Create default settings
            settings = new SystemSettings
            {
                Id = Guid.NewGuid(),
                AllowThreshold = 0.3,
                ReviewThreshold = 0.5,
                BlockThreshold = 0.7,
                RetrainThreshold = 10, // Changed from 100 to 10 for faster learning
                NewGoldSinceLastTrain = 0,
                RetrainingEnabled = true
            };
            _context.SystemSettings.Add(settings);
            await _context.SaveChangesAsync(cancellationToken);
        }
        // Note: We don't auto-update RetrainThreshold here anymore
        // Users can set it through the Settings UI

        return settings;
    }

    public async Task UpdateThresholdsAsync(double allowThreshold, double reviewThreshold, double blockThreshold, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        settings.AllowThreshold = allowThreshold;
        settings.ReviewThreshold = reviewThreshold;
        settings.BlockThreshold = blockThreshold;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRetrainThresholdAsync(int retrainThreshold, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        settings.RetrainThreshold = retrainThreshold;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
