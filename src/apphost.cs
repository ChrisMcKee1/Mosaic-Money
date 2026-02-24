#:sdk Aspire.AppHost.Sdk@13.3.0-preview.1.26121.1
#:property UserSecretsId=270f7f5f-f938-4fde-be8f-247819628151
#:package Aspire.Hosting.JavaScript@13.3.0-preview.1.26121.1
#:package Aspire.Hosting.PostgreSQL@13.3.0-preview.1.26121.1
#:project ./MosaicMoney.Api/MosaicMoney.Api.csproj
#:project ./MosaicMoney.Worker/MosaicMoney.Worker.csproj

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
	.AddPostgres("postgres")
	.WithImage("pgvector/pgvector")
	.WithImageTag("pg17");
var ledgerDb = postgres.AddDatabase("mosaicmoneydb");
var plaidClientId = builder.AddParameter("plaid-client-id", secret: true);
var plaidSecret = builder.AddParameter("plaid-secret", secret: true);

var api = builder
	.AddProject<Projects.MosaicMoney_Api>("api")
	.WithEnvironment("Plaid__ClientId", plaidClientId)
	.WithEnvironment("Plaid__Secret", plaidSecret)
	.WithReference(ledgerDb)
	.WaitFor(ledgerDb);

builder
	.AddProject<Projects.MosaicMoney_Worker>("worker")
	.WithEnvironment("Plaid__ClientId", plaidClientId)
	.WithEnvironment("Plaid__Secret", plaidSecret)
	.WithReference(ledgerDb)
	.WithReference(api)
	.WaitFor(ledgerDb)
	.WaitFor(api);

builder
	.AddJavaScriptApp("web", "./MosaicMoney.Web")
	.WithHttpEndpoint(port: 53832, env: "PORT", isProxied: false)
	.WithReference(api)
	.WaitFor(api);

builder.Build().Run();
