using AiAgents.ContentModerationAgent.Domain.Entities;

namespace AiAgents.ContentModerationAgent.Application.Services;

public interface IContextService
{
    Task<Context> CalculateContextAsync(Content content, CancellationToken cancellationToken = default);
    Task<double> CalculateContextMultiplierAsync(Context context, CancellationToken cancellationToken = default);
}
