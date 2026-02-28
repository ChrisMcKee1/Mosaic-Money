using MosaicMoney.Worker;
using MosaicMoney.Api.Domain.Assistant;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddAiWorkflowIntegrationChecks();
builder.AddRuntimeMessagingBackboneChecks();
builder.AddAzureServiceBusClient(connectionName: "runtime-ingestion-completed");
builder.AddAzureEventHubProducerClient(connectionName: "runtime-telemetry-stream");
builder.AddNpgsqlDataSource(connectionName: "mosaicmoneydb");
var foundryAgentSection = builder.Configuration.GetSection(FoundryAgentOptions.SectionName);
builder.Services.Configure<FoundryAgentOptions>(
	foundryAgentSection.Exists()
		? foundryAgentSection
		: builder.Configuration.GetSection(FoundryAgentOptions.LegacySectionName));
builder.Services.AddHttpClient<IFoundryAgentRuntimeService, FoundryAgentRuntimeService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
