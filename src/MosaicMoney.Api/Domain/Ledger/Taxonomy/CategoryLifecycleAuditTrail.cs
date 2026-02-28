using System.Text.Json;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Taxonomy;

public interface ICategoryLifecycleAuditTrail
{
    void Record(
        MosaicMoneyDbContext dbContext,
        string entityType,
        Guid entityId,
        string operation,
        CategoryOwnerType scopeOwnerType,
        Guid? householdId,
        Guid? ownerUserId,
        Guid performedByHouseholdUserId,
        object? metadata = null);
}

public sealed class CategoryLifecycleAuditTrail(TimeProvider timeProvider) : ICategoryLifecycleAuditTrail
{
    public void Record(
        MosaicMoneyDbContext dbContext,
        string entityType,
        Guid entityId,
        string operation,
        CategoryOwnerType scopeOwnerType,
        Guid? householdId,
        Guid? ownerUserId,
        Guid performedByHouseholdUserId,
        object? metadata = null)
    {
        var metadataJson = metadata is null
            ? null
            : JsonSerializer.Serialize(metadata);

        dbContext.TaxonomyLifecycleAuditEntries.Add(new TaxonomyLifecycleAuditEntry
        {
            Id = Guid.CreateVersion7(),
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            ScopeOwnerType = scopeOwnerType,
            HouseholdId = householdId,
            OwnerUserId = ownerUserId,
            PerformedByHouseholdUserId = performedByHouseholdUserId,
            PerformedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            MetadataJson = metadataJson,
        });
    }
}
