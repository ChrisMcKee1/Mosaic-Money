using Microsoft.EntityFrameworkCore;

namespace MosaicMoney.Api.Data;

public sealed class MosaicMoneyDbContext : DbContext
{
    public MosaicMoneyDbContext(DbContextOptions<MosaicMoneyDbContext> options)
        : base(options)
    {
    }
}
