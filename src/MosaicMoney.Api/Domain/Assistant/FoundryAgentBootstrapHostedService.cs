using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Assistant;

public sealed class FoundryAgentBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<FoundryAgentOptions> options,
    ILogger<FoundryAgentBootstrapHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var assistantOptions = options.Value;
        if (!assistantOptions.Enabled)
        {
            logger.LogInformation("Foundry agent bootstrap skipped because the runtime is disabled.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IFoundryAgentRuntimeService>();

        var bootstrapResult = await runtime.EnsureAgentAsync(stoppingToken);
        if (bootstrapResult.Succeeded)
        {
            logger.LogInformation(
                "Foundry agent bootstrap succeeded. AgentName={AgentName}, AgentSource={AgentSource}, Created={Created}, UsedFallbackPayload={UsedFallbackPayload}",
                bootstrapResult.AgentName,
                bootstrapResult.AgentSource,
                bootstrapResult.Created,
                bootstrapResult.UsedFallbackPayload);
            return;
        }

        logger.LogWarning(
            "Foundry agent bootstrap did not complete. AgentName={AgentName}, OutcomeCode={OutcomeCode}, Summary={Summary}",
            bootstrapResult.AgentName,
            bootstrapResult.OutcomeCode,
            bootstrapResult.Summary);
    }
}
