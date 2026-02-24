using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
builder.Services.AddHttpClient<PlaidHttpTokenProvider>();
builder.Services.AddScoped<PlaidDeltaIngestionService>();
builder.Services.AddScoped<DeterministicPlaidTokenProvider>();
builder.Services.AddScoped<IPlaidTokenProvider>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<PlaidOptions>>().Value;
    return options.UseDeterministicProvider
        ? serviceProvider.GetRequiredService<DeterministicPlaidTokenProvider>()
        : serviceProvider.GetRequiredService<PlaidHttpTokenProvider>();
});
builder.Services.AddScoped<PlaidAccessTokenProtector>();
builder.Services.AddScoped<PlaidLinkLifecycleService>();
builder.Services.AddScoped<PlaidItemSyncStateService>();
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

await ApplyMigrationsAsync(app);

app.MapDefaultEndpoints();
app.MapMosaicMoneyApi();

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();
    await dbContext.Database.MigrateAsync();
}

public partial class Program;
