using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace MosaicMoney.Api.Data;

public sealed class MosaicMoneyDbContextFactory : IDesignTimeDbContextFactory<MosaicMoneyDbContext>
{
    public MosaicMoneyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MosaicMoneyDbContext>();

        // Design-time only: enables migration scaffolding without Aspire runtime wiring.
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=mosaicmoney_design_time;Username=postgres;Password=postgres",
            options => options.UseVector());

        return new MosaicMoneyDbContext(optionsBuilder.Options);
    }
}
