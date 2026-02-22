using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

public sealed class AiWorkflowIntegrationOptions
{
    public const string SectionName = "AiWorkflow";

    public string ConnectionName { get; init; } = "mosaicmoneydb";

    public string? DirectConnectionString { get; init; }
}

public static class AiWorkflowIntegrationExtensions
{
    private const string RequiredConnectionName = "mosaicmoneydb";

    public static TBuilder AddAiWorkflowIntegrationChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var options = builder.Configuration
            .GetSection(AiWorkflowIntegrationOptions.SectionName)
            .Get<AiWorkflowIntegrationOptions>()
            ?? new AiWorkflowIntegrationOptions();

        if (!string.Equals(options.ConnectionName, RequiredConnectionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"AiWorkflow:ConnectionName must be '{RequiredConnectionName}' so AI paths share Aspire reference-driven database wiring.");
        }

        if (!string.IsNullOrWhiteSpace(options.DirectConnectionString))
        {
            throw new InvalidOperationException(
                "AiWorkflow:DirectConnectionString must not be configured. Use AppHost WithReference(...) and ConnectionStrings:mosaicmoneydb injection only.");
        }

        var configuredConnection = builder.Configuration.GetConnectionString(RequiredConnectionName);
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{RequiredConnectionName} must be provided by Aspire orchestration before AI workflow startup.");
        }

        return builder;
    }
}
