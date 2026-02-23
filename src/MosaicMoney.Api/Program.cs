using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Ingestion;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddAiWorkflowIntegrationChecks();
builder.AddNpgsqlDbContext<MosaicMoneyDbContext>(
    connectionName: "mosaicmoneydb",
    configureDbContextOptions: options => options.UseNpgsql(o => o.UseVector()));
builder.Services.AddScoped<PlaidDeltaIngestionService>();
builder.Services.AddScoped<TransactionProjectionMetadataQueryService>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapMosaicMoneyApi();

app.Run();
