using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace MosaicMoney.Api.Data;

public sealed class MosaicMoneyDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MosaicMoneyDbContext>
{
    private const string DesignTimeConnectionStringEnvVar = "MOSAIC_MONEY_EF_DESIGNTIME_CONNECTION";

    public MosaicMoneyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DesignTimeConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Port=5432;Database=mosaicmoneydb;Username=postgres;Password=postgres";
        }

        var optionsBuilder = new DbContextOptionsBuilder<MosaicMoneyDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.UseVector());

        return new MosaicMoneyDbContext(optionsBuilder.Options);
    }
}
