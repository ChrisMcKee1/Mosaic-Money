using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger.Classification;

namespace MosaicMoney.Api.Domain.Ledger.Taxonomy;

public static class TaxonomyBackfillReasonCodes
{
    public const string NoRuleMatch = "taxonomy_backfill_no_rule_match";
    public const string ConflictingRules = "taxonomy_backfill_conflicting_rules";
    public const string MissingDescription = "taxonomy_backfill_missing_description";
    public const string LowConfidence = "taxonomy_backfill_low_confidence";
}

public sealed class TaxonomyBackfillOptions
{
    public const string SectionName = "TaxonomyBackfill";

    public bool Enabled { get; init; } = true;

    public decimal DeterministicConfidenceThreshold { get; init; } = 0.7000m;

    public int MaxTransactionsPerRun { get; init; } = 10000;
}

public sealed record TaxonomyNullMetric(int TotalCount, int NullCount, decimal NullPercent);

public sealed record TaxonomyDiscrepancySnapshot(
    TaxonomyNullMetric TransactionSubcategory,
    TaxonomyNullMetric OutcomeProposedSubcategory,
    TaxonomyNullMetric StageOutputProposedSubcategory);

public sealed record TaxonomyBootstrapBackfillResult(
    bool Enabled,
    int CategoriesInserted,
    int CategoriesUpdated,
    int SubcategoriesInserted,
    int SubcategoriesUpdated,
    int EligibleTransactionsProcessed,
    int BackfilledTransactions,
    int NeedsReviewRouted,
    TaxonomyDiscrepancySnapshot BeforeSnapshot,
    TaxonomyDiscrepancySnapshot AfterSnapshot);

public sealed class TaxonomyBootstrapBackfillService(
    MosaicMoneyDbContext dbContext,
    IDeterministicClassificationEngine deterministicClassificationEngine,
    TimeProvider timeProvider,
    IOptions<TaxonomyBackfillOptions> options,
    ILogger<TaxonomyBootstrapBackfillService> logger)
{
    public async Task<TaxonomyBootstrapBackfillResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var beforeSnapshot = await CaptureSnapshotAsync(cancellationToken);

        var resolvedOptions = options.Value;
        if (!resolvedOptions.Enabled)
        {
            return new TaxonomyBootstrapBackfillResult(
                Enabled: false,
                CategoriesInserted: 0,
                CategoriesUpdated: 0,
                SubcategoriesInserted: 0,
                SubcategoriesUpdated: 0,
                EligibleTransactionsProcessed: 0,
                BackfilledTransactions: 0,
                NeedsReviewRouted: 0,
                BeforeSnapshot: beforeSnapshot,
                AfterSnapshot: beforeSnapshot);
        }

        var (categoriesInserted, categoriesUpdated, subcategoriesInserted, subcategoriesUpdated) =
            await SeedBaselineTaxonomyAsync(cancellationToken);

        // Persist seed upserts first so deterministic backfill can query newly inserted subcategories.
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var threshold = decimal.Clamp(resolvedOptions.DeterministicConfidenceThreshold, 0m, 1m);
        var maxTransactionsPerRun = Math.Clamp(resolvedOptions.MaxTransactionsPerRun, 1, 100000);

        var availableSubcategories = await dbContext.Subcategories
            .AsNoTracking()
            .Where(x =>
                !x.IsArchived
                && !x.Category.IsArchived
                && x.Category.OwnerType == CategoryOwnerType.Platform)
            .Select(x => new DeterministicClassificationSubcategory(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        var eligibleTransactions = await dbContext.EnrichedTransactions
            .Where(x => x.SubcategoryId == null && x.ReviewStatus == TransactionReviewStatus.None && x.Amount < 0m)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.Id)
            .Take(maxTransactionsPerRun)
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var backfilledTransactions = 0;
        var needsReviewRouted = 0;

        foreach (var transaction in eligibleTransactions)
        {
            var stageResult = deterministicClassificationEngine.Execute(new DeterministicClassificationRequest(
                transaction.Id,
                transaction.Description,
                transaction.Amount,
                transaction.TransactionDate,
                availableSubcategories));

            var canAssign = stageResult.ProposedSubcategoryId.HasValue
                && !stageResult.HasConflict
                && stageResult.Confidence >= threshold;

            if (canAssign)
            {
                transaction.SubcategoryId = stageResult.ProposedSubcategoryId;
                transaction.LastModifiedAtUtc = now;
                backfilledTransactions++;
                continue;
            }

            var reviewReason = ResolveReviewReason(stageResult, threshold);
            if (TransactionReviewStateMachine.TryTransition(
                    transaction.ReviewStatus,
                    TransactionReviewAction.RouteToNeedsReview,
                    out var nextReviewStatus))
            {
                transaction.ReviewStatus = nextReviewStatus;
            }
            else
            {
                transaction.ReviewStatus = TransactionReviewStatus.NeedsReview;
            }

            transaction.ReviewReason = reviewReason;
            transaction.LastModifiedAtUtc = now;
            needsReviewRouted++;
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var afterSnapshot = await CaptureSnapshotAsync(cancellationToken);

        var result = new TaxonomyBootstrapBackfillResult(
            Enabled: true,
            CategoriesInserted: categoriesInserted,
            CategoriesUpdated: categoriesUpdated,
            SubcategoriesInserted: subcategoriesInserted,
            SubcategoriesUpdated: subcategoriesUpdated,
            EligibleTransactionsProcessed: eligibleTransactions.Count,
            BackfilledTransactions: backfilledTransactions,
            NeedsReviewRouted: needsReviewRouted,
            BeforeSnapshot: beforeSnapshot,
            AfterSnapshot: afterSnapshot);

        logger.LogInformation(
            "Taxonomy bootstrap/backfill complete. CategoriesInserted={CategoriesInserted}, CategoriesUpdated={CategoriesUpdated}, SubcategoriesInserted={SubcategoriesInserted}, SubcategoriesUpdated={SubcategoriesUpdated}, EligibleTransactions={EligibleTransactions}, BackfilledTransactions={BackfilledTransactions}, NeedsReviewRouted={NeedsReviewRouted}, TxSubcategoryNullBefore={TxNullBefore}/{TxTotalBefore} ({TxPctBefore:F2}%), TxSubcategoryNullAfter={TxNullAfter}/{TxTotalAfter} ({TxPctAfter:F2}%), OutcomeNullBefore={OutcomeNullBefore}/{OutcomeTotalBefore} ({OutcomePctBefore:F2}%), OutcomeNullAfter={OutcomeNullAfter}/{OutcomeTotalAfter} ({OutcomePctAfter:F2}%), StageNullBefore={StageNullBefore}/{StageTotalBefore} ({StagePctBefore:F2}%), StageNullAfter={StageNullAfter}/{StageTotalAfter} ({StagePctAfter:F2}%).",
            result.CategoriesInserted,
            result.CategoriesUpdated,
            result.SubcategoriesInserted,
            result.SubcategoriesUpdated,
            result.EligibleTransactionsProcessed,
            result.BackfilledTransactions,
            result.NeedsReviewRouted,
            result.BeforeSnapshot.TransactionSubcategory.NullCount,
            result.BeforeSnapshot.TransactionSubcategory.TotalCount,
            result.BeforeSnapshot.TransactionSubcategory.NullPercent,
            result.AfterSnapshot.TransactionSubcategory.NullCount,
            result.AfterSnapshot.TransactionSubcategory.TotalCount,
            result.AfterSnapshot.TransactionSubcategory.NullPercent,
            result.BeforeSnapshot.OutcomeProposedSubcategory.NullCount,
            result.BeforeSnapshot.OutcomeProposedSubcategory.TotalCount,
            result.BeforeSnapshot.OutcomeProposedSubcategory.NullPercent,
            result.AfterSnapshot.OutcomeProposedSubcategory.NullCount,
            result.AfterSnapshot.OutcomeProposedSubcategory.TotalCount,
            result.AfterSnapshot.OutcomeProposedSubcategory.NullPercent,
            result.BeforeSnapshot.StageOutputProposedSubcategory.NullCount,
            result.BeforeSnapshot.StageOutputProposedSubcategory.TotalCount,
            result.BeforeSnapshot.StageOutputProposedSubcategory.NullPercent,
            result.AfterSnapshot.StageOutputProposedSubcategory.NullCount,
            result.AfterSnapshot.StageOutputProposedSubcategory.TotalCount,
            result.AfterSnapshot.StageOutputProposedSubcategory.NullPercent);

        return result;
    }

    private async Task<(int CategoriesInserted, int CategoriesUpdated, int SubcategoriesInserted, int SubcategoriesUpdated)> SeedBaselineTaxonomyAsync(
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .Include(x => x.Subcategories)
            .Where(x => x.OwnerType == CategoryOwnerType.Platform)
            .ToListAsync(cancellationToken);

        var categoriesByName = categories
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderBy(candidate => candidate.IsArchived)
                    .ThenBy(candidate => candidate.CreatedAtUtc)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var categoriesInserted = 0;
        var categoriesUpdated = 0;
        var subcategoriesInserted = 0;
        var subcategoriesUpdated = 0;
        var seedCategoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var seedCategory in SystemTaxonomySeedManifest.Categories.OrderBy(x => x.DisplayOrder))
        {
            seedCategoryNames.Add(seedCategory.Name);

            if (!categoriesByName.TryGetValue(seedCategory.Name, out var category))
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = seedCategory.Name,
                    DisplayOrder = seedCategory.DisplayOrder,
                    IsSystem = true,
                    IsArchived = false,
                    ArchivedAtUtc = null,
                    OwnerType = CategoryOwnerType.Platform,
                    HouseholdId = null,
                    OwnerUserId = null,
                    CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                    LastModifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                };

                dbContext.Categories.Add(category);
                categories.Add(category);
                categoriesByName[seedCategory.Name] = category;
                categoriesInserted++;
            }
            else
            {
                var changed = false;
                if (!string.Equals(category.Name, seedCategory.Name, StringComparison.Ordinal))
                {
                    category.Name = seedCategory.Name;
                    changed = true;
                }

                if (category.DisplayOrder != seedCategory.DisplayOrder)
                {
                    category.DisplayOrder = seedCategory.DisplayOrder;
                    changed = true;
                }

                if (!category.IsSystem)
                {
                    category.IsSystem = true;
                    changed = true;
                }

                if (category.OwnerType != CategoryOwnerType.Platform)
                {
                    category.OwnerType = CategoryOwnerType.Platform;
                    changed = true;
                }

                if (category.HouseholdId.HasValue)
                {
                    category.HouseholdId = null;
                    changed = true;
                }

                if (category.OwnerUserId.HasValue)
                {
                    category.OwnerUserId = null;
                    changed = true;
                }

                if (category.IsArchived)
                {
                    category.IsArchived = false;
                    category.ArchivedAtUtc = null;
                    changed = true;
                }

                if (changed)
                {
                    category.LastModifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                    categoriesUpdated++;
                }
            }

            var seedSubcategoryNames = new HashSet<string>(
                seedCategory.Subcategories.Select(x => x.Name),
                StringComparer.OrdinalIgnoreCase);

            var subcategoriesByName = category.Subcategories
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderBy(candidate => candidate.IsArchived)
                        .ThenBy(candidate => candidate.CreatedAtUtc)
                        .First(),
                    StringComparer.OrdinalIgnoreCase);

            for (var seedIndex = 0; seedIndex < seedCategory.Subcategories.Count; seedIndex++)
            {
                var seedSubcategory = seedCategory.Subcategories[seedIndex];
                if (!subcategoriesByName.TryGetValue(seedSubcategory.Name, out var subcategory))
                {
                    subcategory = new Subcategory
                    {
                        Id = Guid.NewGuid(),
                        CategoryId = category.Id,
                        Name = seedSubcategory.Name,
                        DisplayOrder = seedIndex,
                        IsBusinessExpense = seedSubcategory.IsBusinessExpense,
                        IsArchived = false,
                        ArchivedAtUtc = null,
                        CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                        LastModifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
                    };

                    dbContext.Subcategories.Add(subcategory);
                    subcategoriesByName[seedSubcategory.Name] = subcategory;
                    subcategoriesInserted++;
                }
                else
                {
                    var changed = false;

                    if (!string.Equals(subcategory.Name, seedSubcategory.Name, StringComparison.Ordinal))
                    {
                        subcategory.Name = seedSubcategory.Name;
                        changed = true;
                    }

                    if (subcategory.CategoryId != category.Id)
                    {
                        subcategory.CategoryId = category.Id;
                        changed = true;
                    }

                    if (subcategory.IsBusinessExpense != seedSubcategory.IsBusinessExpense)
                    {
                        subcategory.IsBusinessExpense = seedSubcategory.IsBusinessExpense;
                        changed = true;
                    }

                    if (subcategory.DisplayOrder != seedIndex)
                    {
                        subcategory.DisplayOrder = seedIndex;
                        changed = true;
                    }

                    if (subcategory.IsArchived)
                    {
                        subcategory.IsArchived = false;
                        subcategory.ArchivedAtUtc = null;
                        changed = true;
                    }

                    if (changed)
                    {
                        subcategory.LastModifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                        subcategoriesUpdated++;
                    }
                }
            }

            foreach (var existingSubcategory in category.Subcategories)
            {
                if (seedSubcategoryNames.Contains(existingSubcategory.Name))
                {
                    continue;
                }

                if (existingSubcategory.IsArchived)
                {
                    continue;
                }

                existingSubcategory.IsArchived = true;
                existingSubcategory.ArchivedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                existingSubcategory.LastModifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                subcategoriesUpdated++;
            }
        }

        foreach (var existingCategory in categories.Where(x => x.OwnerType == CategoryOwnerType.Platform))
        {
            if (seedCategoryNames.Contains(existingCategory.Name))
            {
                continue;
            }

            if (!existingCategory.IsArchived)
            {
                existingCategory.IsArchived = true;
                existingCategory.ArchivedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                existingCategory.LastModifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                categoriesUpdated++;
            }

            foreach (var subcategory in existingCategory.Subcategories)
            {
                if (subcategory.IsArchived)
                {
                    continue;
                }

                subcategory.IsArchived = true;
                subcategory.ArchivedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                subcategory.LastModifiedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                subcategoriesUpdated++;
            }
        }

        return (categoriesInserted, categoriesUpdated, subcategoriesInserted, subcategoriesUpdated);
    }

    private async Task<TaxonomyDiscrepancySnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken)
    {
        var transactionTotal = await dbContext.EnrichedTransactions.CountAsync(cancellationToken);
        var transactionNulls = await dbContext.EnrichedTransactions
            .Where(x => x.SubcategoryId == null)
            .CountAsync(cancellationToken);

        var outcomeTotal = await dbContext.TransactionClassificationOutcomes.CountAsync(cancellationToken);
        var outcomeNulls = await dbContext.TransactionClassificationOutcomes
            .Where(x => x.ProposedSubcategoryId == null)
            .CountAsync(cancellationToken);

        var stageOutputTotal = await dbContext.ClassificationStageOutputs.CountAsync(cancellationToken);
        var stageOutputNulls = await dbContext.ClassificationStageOutputs
            .Where(x => x.ProposedSubcategoryId == null)
            .CountAsync(cancellationToken);

        return new TaxonomyDiscrepancySnapshot(
            TransactionSubcategory: BuildNullMetric(transactionTotal, transactionNulls),
            OutcomeProposedSubcategory: BuildNullMetric(outcomeTotal, outcomeNulls),
            StageOutputProposedSubcategory: BuildNullMetric(stageOutputTotal, stageOutputNulls));
    }

    private static TaxonomyNullMetric BuildNullMetric(int totalCount, int nullCount)
    {
        var nullPercent = totalCount == 0
            ? 0m
            : decimal.Round((nullCount * 100m) / totalCount, 2, MidpointRounding.AwayFromZero);

        return new TaxonomyNullMetric(totalCount, nullCount, nullPercent);
    }

    private static string ResolveReviewReason(DeterministicClassificationStageResult stageResult, decimal threshold)
    {
        if (stageResult.HasConflict)
        {
            return TaxonomyBackfillReasonCodes.ConflictingRules;
        }

        if (!stageResult.ProposedSubcategoryId.HasValue)
        {
            return stageResult.RationaleCode switch
            {
                DeterministicClassificationReasonCodes.MissingDescription => TaxonomyBackfillReasonCodes.MissingDescription,
                _ => TaxonomyBackfillReasonCodes.NoRuleMatch,
            };
        }

        if (stageResult.Confidence < threshold)
        {
            return TaxonomyBackfillReasonCodes.LowConfidence;
        }

        return TaxonomyBackfillReasonCodes.NoRuleMatch;
    }
}
