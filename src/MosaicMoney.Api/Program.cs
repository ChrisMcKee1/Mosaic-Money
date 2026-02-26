using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Authentication;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.AccessPolicy;
using MosaicMoney.Api.Domain.Ledger.Classification;
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using MosaicMoney.Api.Domain.Ledger.Ingestion;
using MosaicMoney.Api.Domain.Ledger.Plaid;
using Npgsql;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddAiWorkflowIntegrationChecks();
builder.AddNpgsqlDbContext<MosaicMoneyDbContext>(
    connectionName: "mosaicmoneydb",
    configureDbContextOptions: options => options.UseNpgsql(o => o.UseVector()));
builder.Services.AddDataProtection();
builder.Services.AddClerkJwtAuthentication(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AccountAccessPolicyBackfillService>();
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
builder.Services.AddScoped<PlaidTransactionsSyncProcessor>();
builder.Services.AddScoped<PlaidLiabilitiesIngestionService>();
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
builder.Services.Configure<TransactionEmbeddingProviderOptions>(
    builder.Configuration.GetSection(TransactionEmbeddingProviderOptions.SectionName));
builder.Services.AddHttpClient<AzureOpenAiTransactionEmbeddingGenerator>();
builder.Services.AddScoped<DeterministicTransactionEmbeddingGenerator>();
builder.Services.AddScoped<ITransactionEmbeddingGenerator>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<TransactionEmbeddingProviderOptions>>()
        .Value;

    return options.ShouldUseAzureOpenAi()
        ? serviceProvider.GetRequiredService<AzureOpenAiTransactionEmbeddingGenerator>()
        : serviceProvider.GetRequiredService<DeterministicTransactionEmbeddingGenerator>();
});
builder.Services.AddScoped<ITransactionEmbeddingQueueService, TransactionEmbeddingQueueService>();
builder.Services.AddScoped<ITransactionEmbeddingQueueProcessor, TransactionEmbeddingQueueProcessor>();
builder.Services.AddHostedService<TransactionEmbeddingQueueBackgroundService>();
builder.Services.AddHostedService<PlaidTransactionsSyncBackgroundService>();

var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedCorsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy
                .WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

await ApplyMigrationsAsync(app);

if (allowedCorsOrigins.Length > 0)
{
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapMosaicMoneyApi();

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();

    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupMigrations");

    var rawConnectionString = dbContext.Database.GetConnectionString() ?? string.Empty;
    var connection = new NpgsqlConnectionStringBuilder(rawConnectionString);
    logger.LogInformation(
        "Applying migrations against host '{Host}', database '{Database}', user '{User}'.",
        connection.Host,
        connection.Database,
        connection.Username);

    var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
    var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();

    logger.LogInformation(
        "Migration state before apply: {AppliedCount} applied, {PendingCount} pending.",
        appliedMigrations.Count(),
        pendingMigrations.Count());

    await dbContext.Database.MigrateAsync();

    var backfillService = scope.ServiceProvider.GetRequiredService<AccountAccessPolicyBackfillService>();
    await backfillService.ExecuteAsync();

    var appliedAfter = await dbContext.Database.GetAppliedMigrationsAsync();
    logger.LogInformation("Migration state after apply: {AppliedCount} applied.", appliedAfter.Count());
}

public partial class Program;
