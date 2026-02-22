using MosaicMoney.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddAiWorkflowIntegrationChecks();
builder.AddNpgsqlDataSource(connectionName: "mosaicmoneydb");

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
