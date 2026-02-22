using MosaicMoney.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<MosaicMoneyDbContext>(connectionName: "mosaicmoneydb");

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/health", () => new { Status = "ok", Timestamp = DateTime.UtcNow });

app.Run();
