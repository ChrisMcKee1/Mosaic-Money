using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Classification;
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using MosaicMoney.Api.Domain.Ledger.Ingestion;
using MosaicMoney.Api.Domain.Ledger.Plaid;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddAiWorkflowIntegrationChecks();
builder.AddNpgsqlDbContext<MosaicMoneyDbContext>(
    connectionName: "mosaicmoneydb",
    configureDbContextOptions: options => options.UseNpgsql(o => o.UseVector()));
builder.Services.AddDataProtection();
builder.Services.Configure<PlaidOptions>(builder.Configuration.GetSection(PlaidOptions.SectionName));
builder.Services.AddScoped<PlaidDeltaIngestionService>();
builder.Services.AddScoped<IPlaidTokenProvider, DeterministicPlaidTokenProvider>();
builder.Services.AddScoped<PlaidAccessTokenProtector>();
builder.Services.AddScoped<PlaidLinkLifecycleService>();
builder.Services.AddScoped<TransactionProjectionMetadataQueryService>();
builder.Services.AddScoped<IDeterministicClassificationEngine, DeterministicClassificationEngine>();
builder.Services.AddScoped<IClassificationAmbiguityPolicyGate, ClassificationAmbiguityPolicyGate>();
builder.Services.AddScoped<IClassificationConfidenceFusionPolicy, ClassificationConfidenceFusionPolicy>();
builder.Services.AddScoped<IPostgresSemanticNeighborQuery, PostgresSemanticNeighborQuery>();
builder.Services.AddScoped<IPostgresSemanticRetrievalService, PostgresSemanticRetrievalService>();
builder.Services.Configure<MafFallbackGraphOptions>(builder.Configuration.GetSection(MafFallbackGraphOptions.SectionName));
builder.Services.AddScoped<IMafFallbackEligibilityGate, MafFallbackEligibilityGate>();
builder.Services.AddSingleton<IMafFallbackGraphExecutor, NoOpMafFallbackGraphExecutor>();
builder.Services.AddScoped<IMafFallbackGraphService, MafFallbackGraphService>();
builder.Services.AddScoped<IDeterministicClassificationOrchestrator, DeterministicClassificationOrchestrator>();
builder.Services.AddScoped<ITransactionEmbeddingGenerator, DeterministicTransactionEmbeddingGenerator>();
builder.Services.AddScoped<ITransactionEmbeddingQueueService, TransactionEmbeddingQueueService>();
builder.Services.AddScoped<ITransactionEmbeddingQueueProcessor, TransactionEmbeddingQueueProcessor>();
builder.Services.AddHostedService<TransactionEmbeddingQueueBackgroundService>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapMosaicMoneyApi();

app.Run();

public partial class Program;
