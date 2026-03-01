using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class RuntimeMessagingBackboneOptionsTests
{
    [Fact]
    public void GetMissingRequiredValues_EmptyOptions_ReturnsAllRequiredKeys()
    {
        var options = new RuntimeMessagingBackboneOptions();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var missing = options.GetMissingRequiredValues(configuration);

        Assert.Equal(7, missing.Count);
        Assert.Contains("ConnectionStrings:runtime-ingestion-completed", missing);
        Assert.Contains("RuntimeMessaging:EventGrid:PublishAccessKey", missing);
        Assert.Contains("ConnectionStrings:runtime-telemetry-stream", missing);
    }

    [Fact]
    public void GetMissingRequiredValues_WithCompleteValues_ReturnsEmpty()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:runtime-ingestion-completed"] = "sb://localhost:5672",
                ["ConnectionStrings:runtime-agent-message-posted"] = "sb://localhost:5672",
                ["ConnectionStrings:runtime-nightly-anomaly-sweep"] = "sb://localhost:5672",
                ["ConnectionStrings:runtime-telemetry-stream"] = "sb://localhost:5672",
            })
            .Build();

        var options = new RuntimeMessagingBackboneOptions
        {
            Enabled = true,
            EventGrid = new RuntimeMessagingEventGridOptions
            {
                PublishEndpoint = "https://mm-runtime.centralus-1.eventgrid.azure.net/api/events",
                PublishAccessKey = "not-real",
                TopicName = "mm-runtime-events",
            },
        };

        var missing = options.GetMissingRequiredValues(configuration);

        Assert.Empty(missing);
    }

    [Fact]
    public void GetMissingRequiredValues_WithWhitespaceValue_TreatsAsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:runtime-ingestion-completed"] = "sb://localhost:5672",
                ["ConnectionStrings:runtime-agent-message-posted"] = " ",
                ["ConnectionStrings:runtime-nightly-anomaly-sweep"] = "sb://localhost:5672",
                ["ConnectionStrings:runtime-telemetry-stream"] = "sb://localhost:5672",
            })
            .Build();

        var options = new RuntimeMessagingBackboneOptions
        {
            EventGrid = new RuntimeMessagingEventGridOptions
            {
                PublishEndpoint = "https://mm-runtime.centralus-1.eventgrid.azure.net/api/events",
                PublishAccessKey = "not-real",
                TopicName = "mm-runtime-events",
            },
        };

        var missing = options.GetMissingRequiredValues(configuration);

        Assert.Contains("ConnectionStrings:runtime-agent-message-posted", missing);
    }
}
