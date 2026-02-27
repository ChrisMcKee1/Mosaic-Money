using MosaicMoney.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddAiWorkflowIntegrationChecks();
builder.AddRuntimeMessagingBackboneChecks();
builder.AddAzureServiceBusClient(connectionName: "runtime-ingestion-completed");
builder.AddAzureEventHubProducerClient(connectionName: "runtime-telemetry-stream");
builder.AddNpgsqlDataSource(connectionName: "mosaicmoneydb");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
