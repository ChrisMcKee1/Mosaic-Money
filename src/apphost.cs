#:sdk Aspire.AppHost.Sdk@13.3.0-preview.1.26121.1
#:property UserSecretsId=270f7f5f-f938-4fde-be8f-247819628151
#:package Aspire.Hosting.JavaScript@13.3.0-preview.1.26121.1
#:project ./MosaicMoney.Api/MosaicMoney.Api.csproj
#:project ./MosaicMoney.Worker/MosaicMoney.Worker.csproj

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MosaicMoney_Api>("api");

builder
	.AddProject<Projects.MosaicMoney_Worker>("worker")
	.WithReference(api)
	.WaitFor(api);

builder
	.AddJavaScriptApp("web", "./MosaicMoney.Web")
	.WithHttpEndpoint(env: "PORT")
	.WithExternalHttpEndpoints()
	.WithReference(api)
	.WaitFor(api);

builder.Build().Run();
