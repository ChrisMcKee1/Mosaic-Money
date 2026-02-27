using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

public sealed class RuntimeMessagingBackboneOptions
{
    public const string SectionName = "RuntimeMessaging";

    public static readonly IReadOnlyList<string> RequiredAspireConnectionNames =
    [
        "runtime-ingestion-completed",
        "runtime-assistant-message-posted",
        "runtime-nightly-anomaly-sweep",
        "runtime-telemetry-stream",
    ];

    public bool Enabled { get; init; }

    public RuntimeMessagingEventGridOptions EventGrid { get; init; } = new();

    public IReadOnlyList<string> GetMissingRequiredValues(IConfiguration configuration)
    {
        var missing = new List<string>();

        foreach (var connectionName in RequiredAspireConnectionNames)
        {
            AddIfMissing(missing, $"ConnectionStrings:{connectionName}", configuration.GetConnectionString(connectionName));
        }

        AddIfMissing(missing, "RuntimeMessaging:EventGrid:PublishEndpoint", EventGrid.PublishEndpoint);
        AddIfMissing(missing, "RuntimeMessaging:EventGrid:PublishAccessKey", EventGrid.PublishAccessKey);
        AddIfMissing(missing, "RuntimeMessaging:EventGrid:TopicName", EventGrid.TopicName);

        return missing;
    }

    private static void AddIfMissing(List<string> missing, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(key);
        }
    }
}

public sealed class RuntimeMessagingEventGridOptions
{
    public string? PublishEndpoint { get; init; }

    public string? PublishAccessKey { get; init; }

    public string? TopicName { get; init; }
}

public static class RuntimeMessagingBackboneExtensions
{
    public static TBuilder AddRuntimeMessagingBackboneChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var options = builder.Configuration
            .GetSection(RuntimeMessagingBackboneOptions.SectionName)
            .Get<RuntimeMessagingBackboneOptions>()
            ?? new RuntimeMessagingBackboneOptions();

        if (!options.Enabled)
        {
            return builder;
        }

        var missing = options.GetMissingRequiredValues(builder.Configuration);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Runtime messaging backbone is enabled, but required configuration keys are missing: {string.Join(", ", missing)}. " +
            "Provide Aspire WithReference(...) connection wiring and Event Grid runtime configuration values.");
        }

        return builder;
    }
}