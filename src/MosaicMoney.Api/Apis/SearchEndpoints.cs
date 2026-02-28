using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace MosaicMoney.Api.Apis;

public static class SearchEndpoints
{
    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/search/transactions", async (
            MosaicMoneyDbContext dbContext,
            ITransactionEmbeddingGenerator embeddingGenerator,
            string query,
            int limit = 10,
            CancellationToken cancellationToken = default) =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.Ok(Array.Empty<TransactionDto>());
            }

            var embeddingArray = await embeddingGenerator.GenerateEmbeddingAsync(query, cancellationToken);
            var queryEmbedding = new Vector(embeddingArray);

            var boundedLimit = Math.Clamp(limit, 1, 50);

            var transactions = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Include(x => x.Splits)
                .Where(x => x.DescriptionEmbedding != null)
                .OrderBy(x => x.DescriptionEmbedding!.CosineDistance(queryEmbedding))
                .Take(boundedLimit)
                .ToListAsync(cancellationToken);

            return Results.Ok(transactions.Select(ApiEndpointHelpers.MapTransaction).ToList());
        });

        group.MapGet("/search/categories", async (
            MosaicMoneyDbContext dbContext,
            ITransactionEmbeddingGenerator embeddingGenerator,
            string query,
            CategoryOwnerType? ownerType,
            Guid? householdId,
            Guid? ownerUserId,
            int limit = 10,
            CancellationToken cancellationToken = default) =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.Ok(Array.Empty<object>());
            }

            var embeddingArray = await embeddingGenerator.GenerateEmbeddingAsync(query, cancellationToken);
            var queryEmbedding = new Vector(embeddingArray);

            var boundedLimit = Math.Clamp(limit, 1, 50);

            var scopedSubcategoryIdsQuery = ApplyCategoryOwnershipScope(
                    dbContext.Subcategories.AsNoTracking(),
                    ownerType,
                    householdId,
                    ownerUserId)
                .Select(x => x.Id);

            // Find subcategories from similar transactions
            var semanticSubcategoryIds = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Where(x =>
                    x.DescriptionEmbedding != null
                    && x.SubcategoryId != null
                    && scopedSubcategoryIdsQuery.Contains(x.SubcategoryId.Value))
                .OrderBy(x => x.DescriptionEmbedding!.CosineDistance(queryEmbedding))
                .Select(x => x.SubcategoryId!.Value)
                .Take(boundedLimit * 2)
                .ToListAsync(cancellationToken);

            // Also find subcategories by name
            var textMatchSubcategories = await ApplyCategoryOwnershipScope(
                    dbContext.Subcategories
                        .AsNoTracking()
                        .Include(x => x.Category),
                    ownerType,
                    householdId,
                    ownerUserId)
                .Where(x => EF.Functions.ILike(x.Name, $"%{query}%") || EF.Functions.ILike(x.Category.Name, $"%{query}%"))
                .Take(boundedLimit)
                .ToListAsync(cancellationToken);

            var combinedIds = semanticSubcategoryIds.Concat(textMatchSubcategories.Select(x => x.Id)).Distinct().Take(boundedLimit).ToList();

            var finalSubcategories = await ApplyCategoryOwnershipScope(
                    dbContext.Subcategories
                        .AsNoTracking()
                        .Include(x => x.Category),
                    ownerType,
                    householdId,
                    ownerUserId)
                .Where(x => combinedIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            // Sort to prioritize text matches, then semantic matches
            var sortedSubcategories = finalSubcategories
                .OrderByDescending(x => textMatchSubcategories.Any(t => t.Id == x.Id))
                .ThenBy(x => combinedIds.IndexOf(x.Id))
                .Select(x => new
                {
                    Id = x.Id,
                    Name = x.Name,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category.Name,
                    IsBusinessExpense = x.IsBusinessExpense
                })
                .ToList();

            return Results.Ok(sortedSubcategories);
        });

        return group;
    }

    private static IQueryable<Subcategory> ApplyCategoryOwnershipScope(
        IQueryable<Subcategory> query,
        CategoryOwnerType? ownerType,
        Guid? householdId,
        Guid? ownerUserId)
    {
        var resolvedOwnerType = ownerType ?? CategoryOwnerType.Platform;

        return resolvedOwnerType switch
        {
            CategoryOwnerType.Platform => query.Where(x => x.Category.OwnerType == CategoryOwnerType.Platform),
            CategoryOwnerType.HouseholdShared when householdId.HasValue => query.Where(x =>
                x.Category.OwnerType == CategoryOwnerType.HouseholdShared
                && x.Category.HouseholdId == householdId.Value),
            CategoryOwnerType.User when householdId.HasValue && ownerUserId.HasValue => query.Where(x =>
                x.Category.OwnerType == CategoryOwnerType.User
                && x.Category.HouseholdId == householdId.Value
                && x.Category.OwnerUserId == ownerUserId.Value),
            _ => query.Where(_ => false),
        };
    }
}
