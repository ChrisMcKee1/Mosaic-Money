#:sdk Aspire.AppHost.Sdk@13.3.0-preview.1.26121.1
#:property UserSecretsId=270f7f5f-f938-4fde-be8f-247819628151
#:package Aspire.Hosting.JavaScript@13.3.0-preview.1.26121.1
#:package Aspire.Hosting.Azure.PostgreSQL@13.3.0-preview.1.26123.9
#:package Aspire.Hosting.Azure.ServiceBus@13.3.0-preview.1.26121.1
#:package Aspire.Hosting.Azure.EventHubs@13.3.0-preview.1.26121.1
#:project ./MosaicMoney.Api/MosaicMoney.Api.csproj
#:project ./MosaicMoney.Worker/MosaicMoney.Worker.csproj

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<IResourceWithConnectionString> ledgerDb;
var externalLedgerConnection = builder.Configuration.GetConnectionString("mosaicmoneydb");
if (!string.IsNullOrWhiteSpace(externalLedgerConnection))
{
	// Use pre-provisioned Azure PostgreSQL when an explicit connection string is provided.
	ledgerDb = builder.AddConnectionString("mosaicmoneydb");
}
else
{
	var postgresAdminUsername = builder.AddParameter("mosaic-postgres-admin-username", secret: true);
	var postgresAdminPassword = builder.AddParameter("mosaic-postgres-admin-password", secret: true);
	var postgres = builder
		.AddAzurePostgresFlexibleServer("mosaic-postgres")
		.WithPasswordAuthentication(postgresAdminUsername, postgresAdminPassword)
		.RunAsContainer(container =>
		{
			// Local image pin keeps pgvector extension behavior consistent with current test/dev baseline.
			container.WithImage("pgvector/pgvector");
			container.WithImageTag("pg17");
		});

	ledgerDb = postgres.AddDatabase("mosaicmoneydb");
}
var plaidClientId = builder.AddParameter("plaid-client-id", secret: true);
var plaidSecret = builder.AddParameter("plaid-secret", secret: true);
var clerkPublishableKey = builder.AddParameter("clerk-publishable-key");
var clerkSecretKey = builder.AddParameter("clerk-secret-key", secret: true);
var clerkIssuer = builder.AddParameter("clerk-issuer");
var taxonomyOperatorApiKey = builder.AddParameter("taxonomy-operator-api-key", secret: true);
var taxonomyOperatorAllowedSubjects = builder.AddParameter("taxonomy-operator-allowed-subjects");
var azureOpenAiEndpoint = builder.AddParameter("azure-openai-endpoint");
var azureOpenAiApiKey = builder.AddParameter("azure-openai-api-key", secret: true);
var azureOpenAiEmbeddingDeployment = builder.AddParameter("azure-openai-embedding-deployment");
var azureOpenAiChatDeployment = builder.AddParameter("azure-openai-chat-deployment");
var foundryClassificationEnabled = builder.AddParameter("foundry-classification-enabled");
var foundryProjectEndpoint = builder.AddParameter("foundry-project-endpoint");
var foundryProjectApiKey = builder.AddParameter("foundry-project-api-key", secret: true);
var foundryClassificationDeployment = builder.AddParameter("foundry-classification-deployment");
var foundryAgentEnabled = builder.AddParameter("foundry-agent-enabled");
var foundryAgentEndpoint = builder.AddParameter("foundry-agent-endpoint");
var foundryAgentApiKey = builder.AddParameter("foundry-agent-api-key", secret: true);
var foundryAgentDeployment = builder.AddParameter("foundry-agent-deployment");
var foundryAgentMcpDatabaseToolName = builder.AddParameter("foundry-agent-mcp-database-tool-name");
var foundryAgentMcpDatabaseToolEndpoint = builder.AddParameter("foundry-agent-mcp-database-tool-endpoint");
var foundryAgentMcpDatabaseConnectionId = builder.AddParameter("foundry-agent-mcp-database-project-connection-id");
var foundryAgentMcpDatabaseAllowedToolsCsv = builder.AddParameter("foundry-agent-mcp-database-allowed-tools-csv");
var foundryAgentMcpDatabaseRequireApproval = builder.AddParameter("foundry-agent-mcp-database-require-approval");
var foundryAgentMcpApiToolName = builder.AddParameter("foundry-agent-mcp-api-tool-name");
var foundryAgentMcpApiToolEndpoint = builder.AddParameter("foundry-agent-mcp-api-tool-endpoint");
var foundryAgentMcpApiConnectionId = builder.AddParameter("foundry-agent-mcp-api-project-connection-id");
var foundryAgentMcpApiAllowedToolsCsv = builder.AddParameter("foundry-agent-mcp-api-allowed-tools-csv");
var foundryAgentMcpApiRequireApproval = builder.AddParameter("foundry-agent-mcp-api-require-approval");
var foundryAgentKnowledgeBaseLabel = builder.AddParameter("foundry-agent-knowledge-base-label");
var foundryAgentKnowledgeBaseEndpoint = builder.AddParameter("foundry-agent-knowledge-base-endpoint");
var foundryAgentKnowledgeBaseConnectionId = builder.AddParameter("foundry-agent-knowledge-base-project-connection-id");
var foundryAgentKnowledgeBaseAllowedToolsCsv = builder.AddParameter("foundry-agent-knowledge-base-allowed-tools-csv");
var foundryAgentKnowledgeBaseRequireApproval = builder.AddParameter("foundry-agent-knowledge-base-require-approval");

// M10 MM-ASP-12 runtime messaging backbone (Aspire-native resources and references).
var runtimeServiceBus = builder
	.AddAzureServiceBus("runtime-messaging")
	.RunAsEmulator();

var runtimeLaneIngestionCompleted = runtimeServiceBus.AddServiceBusQueue("runtime-ingestion-completed");
var runtimeLaneAssistantMessagePosted = runtimeServiceBus.AddServiceBusQueue("runtime-assistant-message-posted");
var runtimeLaneNightlyAnomalySweep = runtimeServiceBus.AddServiceBusQueue("runtime-nightly-anomaly-sweep");

var runtimeEventHubs = builder
	.AddAzureEventHubs("runtime-telemetry")
	.RunAsEmulator();

var runtimeTelemetryStream = runtimeEventHubs.AddHub("runtime-telemetry-stream");
var runtimeTelemetryConsumer = runtimeTelemetryStream.AddConsumerGroup("mosaic-money-runtime");

// Event Grid remains explicit config until a first-class Aspire Event Grid integration is available.
var runtimeEventGridPublishEndpoint = builder.AddParameter("runtime-eventgrid-publish-endpoint");
var runtimeEventGridPublishAccessKey = builder.AddParameter("runtime-eventgrid-publish-access-key", secret: true);
var runtimeEventGridTopicName = builder.AddParameter("runtime-eventgrid-topic-name");

var api = builder
	.AddProject<Projects.MosaicMoney_Api>("api")
	.WithEnvironment("Plaid__ClientId", plaidClientId)
	.WithEnvironment("Plaid__Secret", plaidSecret)
	.WithEnvironment("Authentication__Clerk__Issuer", clerkIssuer)
	.WithEnvironment("Authentication__Clerk__SecretKey", clerkSecretKey)
	.WithEnvironment("TaxonomyOperator__ApiKey", taxonomyOperatorApiKey)
	.WithEnvironment("TaxonomyOperator__AllowedAuthSubjectsCsv", taxonomyOperatorAllowedSubjects)
	.WithEnvironment("AiWorkflow__Embeddings__AzureOpenAI__Endpoint", azureOpenAiEndpoint)
	.WithEnvironment("AiWorkflow__Embeddings__AzureOpenAI__ApiKey", azureOpenAiApiKey)
	.WithEnvironment("AiWorkflow__Embeddings__AzureOpenAI__Deployment", azureOpenAiEmbeddingDeployment)
	.WithEnvironment("AiWorkflow__Chat__AzureOpenAI__Endpoint", azureOpenAiEndpoint)
	.WithEnvironment("AiWorkflow__Chat__AzureOpenAI__ApiKey", azureOpenAiApiKey)
	.WithEnvironment("AiWorkflow__Chat__AzureOpenAI__Deployment", azureOpenAiChatDeployment)
	.WithEnvironment("AiWorkflow__Classification__Foundry__Enabled", foundryClassificationEnabled)
	.WithEnvironment("AiWorkflow__Classification__Foundry__Endpoint", foundryProjectEndpoint)
	.WithEnvironment("AiWorkflow__Classification__Foundry__ApiKey", foundryProjectApiKey)
	.WithEnvironment("AiWorkflow__Classification__Foundry__Deployment", foundryClassificationDeployment)
	.WithEnvironment("AiWorkflow__Agent__Foundry__Enabled", foundryAgentEnabled)
	.WithEnvironment("AiWorkflow__Agent__Foundry__Endpoint", foundryAgentEndpoint)
	.WithEnvironment("AiWorkflow__Agent__Foundry__ApiKey", foundryAgentApiKey)
	.WithEnvironment("AiWorkflow__Agent__Foundry__Deployment", foundryAgentDeployment)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseToolName", foundryAgentMcpDatabaseToolName)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseToolEndpoint", foundryAgentMcpDatabaseToolEndpoint)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseToolProjectConnectionId", foundryAgentMcpDatabaseConnectionId)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseAllowedToolsCsv", foundryAgentMcpDatabaseAllowedToolsCsv)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseRequireApproval", foundryAgentMcpDatabaseRequireApproval)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiToolName", foundryAgentMcpApiToolName)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiToolEndpoint", foundryAgentMcpApiToolEndpoint)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiToolProjectConnectionId", foundryAgentMcpApiConnectionId)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiAllowedToolsCsv", foundryAgentMcpApiAllowedToolsCsv)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiRequireApproval", foundryAgentMcpApiRequireApproval)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseMcpServerLabel", foundryAgentKnowledgeBaseLabel)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseMcpEndpoint", foundryAgentKnowledgeBaseEndpoint)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseProjectConnectionId", foundryAgentKnowledgeBaseConnectionId)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseAllowedToolsCsv", foundryAgentKnowledgeBaseAllowedToolsCsv)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseRequireApproval", foundryAgentKnowledgeBaseRequireApproval)
	.WithEnvironment("RuntimeMessaging__Enabled", "false")
	.WithEnvironment("RuntimeMessaging__EventGrid__PublishEndpoint", runtimeEventGridPublishEndpoint)
	.WithEnvironment("RuntimeMessaging__EventGrid__PublishAccessKey", runtimeEventGridPublishAccessKey)
	.WithEnvironment("RuntimeMessaging__EventGrid__TopicName", runtimeEventGridTopicName)
	.WithReference(runtimeServiceBus)
	.WithReference(runtimeLaneIngestionCompleted)
	.WithReference(runtimeLaneAssistantMessagePosted)
	.WithReference(runtimeLaneNightlyAnomalySweep)
	.WithReference(runtimeEventHubs)
	.WithReference(runtimeTelemetryStream)
	.WithReference(runtimeTelemetryConsumer)
	.WithReference(ledgerDb)
	.WaitFor(ledgerDb);

var worker = builder
	.AddProject<Projects.MosaicMoney_Worker>("worker")
	.WithEnvironment("Plaid__ClientId", plaidClientId)
	.WithEnvironment("Plaid__Secret", plaidSecret)
	.WithEnvironment("AiWorkflow__Embeddings__AzureOpenAI__Endpoint", azureOpenAiEndpoint)
	.WithEnvironment("AiWorkflow__Embeddings__AzureOpenAI__ApiKey", azureOpenAiApiKey)
	.WithEnvironment("AiWorkflow__Embeddings__AzureOpenAI__Deployment", azureOpenAiEmbeddingDeployment)
	.WithEnvironment("AiWorkflow__Chat__AzureOpenAI__Endpoint", azureOpenAiEndpoint)
	.WithEnvironment("AiWorkflow__Chat__AzureOpenAI__ApiKey", azureOpenAiApiKey)
	.WithEnvironment("AiWorkflow__Chat__AzureOpenAI__Deployment", azureOpenAiChatDeployment)
	.WithEnvironment("AiWorkflow__Classification__Foundry__Enabled", foundryClassificationEnabled)
	.WithEnvironment("AiWorkflow__Classification__Foundry__Endpoint", foundryProjectEndpoint)
	.WithEnvironment("AiWorkflow__Classification__Foundry__ApiKey", foundryProjectApiKey)
	.WithEnvironment("AiWorkflow__Classification__Foundry__Deployment", foundryClassificationDeployment)
	.WithEnvironment("AiWorkflow__Agent__Foundry__Enabled", foundryAgentEnabled)
	.WithEnvironment("AiWorkflow__Agent__Foundry__Endpoint", foundryAgentEndpoint)
	.WithEnvironment("AiWorkflow__Agent__Foundry__ApiKey", foundryAgentApiKey)
	.WithEnvironment("AiWorkflow__Agent__Foundry__Deployment", foundryAgentDeployment)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseToolName", foundryAgentMcpDatabaseToolName)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseToolEndpoint", foundryAgentMcpDatabaseToolEndpoint)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseToolProjectConnectionId", foundryAgentMcpDatabaseConnectionId)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseAllowedToolsCsv", foundryAgentMcpDatabaseAllowedToolsCsv)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpDatabaseRequireApproval", foundryAgentMcpDatabaseRequireApproval)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiToolName", foundryAgentMcpApiToolName)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiToolEndpoint", foundryAgentMcpApiToolEndpoint)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiToolProjectConnectionId", foundryAgentMcpApiConnectionId)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiAllowedToolsCsv", foundryAgentMcpApiAllowedToolsCsv)
	.WithEnvironment("AiWorkflow__Agent__Foundry__McpApiRequireApproval", foundryAgentMcpApiRequireApproval)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseMcpServerLabel", foundryAgentKnowledgeBaseLabel)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseMcpEndpoint", foundryAgentKnowledgeBaseEndpoint)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseProjectConnectionId", foundryAgentKnowledgeBaseConnectionId)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseAllowedToolsCsv", foundryAgentKnowledgeBaseAllowedToolsCsv)
	.WithEnvironment("AiWorkflow__Agent__Foundry__KnowledgeBaseRequireApproval", foundryAgentKnowledgeBaseRequireApproval)
	.WithEnvironment("RuntimeMessaging__Enabled", "false")
	.WithEnvironment("RuntimeMessaging__EventGrid__PublishEndpoint", runtimeEventGridPublishEndpoint)
	.WithEnvironment("RuntimeMessaging__EventGrid__PublishAccessKey", runtimeEventGridPublishAccessKey)
	.WithEnvironment("RuntimeMessaging__EventGrid__TopicName", runtimeEventGridTopicName)
	.WithReference(runtimeServiceBus)
	.WithReference(runtimeLaneIngestionCompleted)
	.WithReference(runtimeLaneAssistantMessagePosted)
	.WithReference(runtimeLaneNightlyAnomalySweep)
	.WithReference(runtimeEventHubs)
	.WithReference(runtimeTelemetryStream)
	.WithReference(runtimeTelemetryConsumer)
	.WithReference(ledgerDb)
	.WithReference(api)
	.WaitFor(ledgerDb)
	.WaitFor(api);

builder
	.AddJavaScriptApp("web", "./MosaicMoney.Web")
	.WithHttpEndpoint(port: 53832, env: "PORT", isProxied: false)
	.WithEnvironment("CLERK_PUBLISHABLE_KEY", clerkPublishableKey)
	.WithEnvironment("NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY", clerkPublishableKey)
	.WithEnvironment("CLERK_SECRET_KEY", clerkSecretKey)
	.WithReference(api)
	.WaitFor(api);

// Mobile is not launched by AppHost today; keep Clerk contract in src/MosaicMoney.Mobile/.env.example.

builder.Build().Run();
