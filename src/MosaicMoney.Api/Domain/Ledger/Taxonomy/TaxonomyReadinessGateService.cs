using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Domain.Ledger.Taxonomy;

public static class TaxonomyReadinessReasonCodes
{
    public const string Ready = "taxonomy_readiness_ready";
    public const string GateDisabled = "taxonomy_readiness_gate_disabled";
    public const string MissingSubcategoryCoverage = "taxonomy_readiness_missing_subcategory_coverage";
    public const string FillRateBelowThreshold = "taxonomy_readiness_fill_rate_below_threshold";
}

public enum TaxonomyReadinessLane
{
    Classification = 1,
    Ingestion = 2,
}

public sealed class TaxonomyReadinessOptions
{
    public const string SectionName = "TaxonomyReadiness";

    public bool EnableClassificationGate { get; init; } = true;

    public bool EnableIngestionGate { get; init; } = true;

    public int MinimumPlatformSubcategoryCount { get; init; } = 1;

    public int MinimumTotalSubcategoryCount { get; init; } = 3;

    public int MinimumExpenseSampleCount { get; init; } = 20;

    public decimal MinimumExpenseFillRate { get; init; } = 0.7000m;
}

public sealed record TaxonomyReadinessSnapshot(
    Guid HouseholdId,
    int PlatformSubcategoryCount,
    int HouseholdSharedSubcategoryCount,
    int UserScopedSubcategoryCount,
    int TotalEligibleSubcategoryCount,
    int ExpenseTransactionCount,
    int CategorizedExpenseTransactionCount,
    decimal ExpenseFillRate);

public sealed record TaxonomyReadinessEvaluation(
    bool IsReady,
    string ReasonCode,
    string Rationale,
    TaxonomyReadinessSnapshot Snapshot);

public interface ITaxonomyReadinessGate
{
    Task<TaxonomyReadinessEvaluation> EvaluateAsync(
        Guid householdId,
        TaxonomyReadinessLane lane,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default);
}

public sealed class TaxonomyReadinessGateService(
    MosaicMoneyDbContext dbContext,
    IOptions<TaxonomyReadinessOptions> options,
    ILogger<TaxonomyReadinessGateService> logger) : ITaxonomyReadinessGate
{
    public async Task<TaxonomyReadinessEvaluation> EvaluateAsync(
        Guid householdId,
        TaxonomyReadinessLane lane,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = options.Value;
        if (!IsLaneEnabled(lane, resolvedOptions))
        {
            var bypassSnapshot = await BuildSnapshotAsync(householdId, ownerUserId, cancellationToken);
            return new TaxonomyReadinessEvaluation(
                IsReady: true,
                ReasonCode: TaxonomyReadinessReasonCodes.GateDisabled,
                Rationale: $"Taxonomy readiness gate for lane '{lane}' is disabled by configuration.",
                Snapshot: bypassSnapshot);
        }

        var minimumPlatformSubcategoryCount = Math.Max(resolvedOptions.MinimumPlatformSubcategoryCount, 0);
        var minimumTotalSubcategoryCount = Math.Max(resolvedOptions.MinimumTotalSubcategoryCount, 0);
        var minimumExpenseSampleCount = Math.Max(resolvedOptions.MinimumExpenseSampleCount, 0);
        var minimumExpenseFillRate = decimal.Clamp(resolvedOptions.MinimumExpenseFillRate, 0m, 1m);

        var snapshot = await BuildSnapshotAsync(householdId, ownerUserId, cancellationToken);

        if (snapshot.PlatformSubcategoryCount < minimumPlatformSubcategoryCount
            || snapshot.TotalEligibleSubcategoryCount < minimumTotalSubcategoryCount)
        {
            var rationale = $"Taxonomy readiness blocked because available subcategory coverage is below threshold. "
                + $"Platform={snapshot.PlatformSubcategoryCount}/{minimumPlatformSubcategoryCount}, "
                + $"TotalEligible={snapshot.TotalEligibleSubcategoryCount}/{minimumTotalSubcategoryCount}.";

            logger.LogInformation(
                "Taxonomy readiness gate blocked lane {Lane} for household {HouseholdId}: {Rationale}",
                lane,
                householdId,
                rationale);

            return new TaxonomyReadinessEvaluation(
                IsReady: false,
                ReasonCode: TaxonomyReadinessReasonCodes.MissingSubcategoryCoverage,
                Rationale: rationale,
                Snapshot: snapshot);
        }

        if (snapshot.ExpenseTransactionCount >= minimumExpenseSampleCount
            && snapshot.ExpenseFillRate < minimumExpenseFillRate)
        {
            var rationale = $"Taxonomy readiness blocked because expense fill-rate is below threshold. "
                + $"FillRate={snapshot.ExpenseFillRate:F4}/{minimumExpenseFillRate:F4}, "
                + $"Sample={snapshot.ExpenseTransactionCount}.";

            logger.LogInformation(
                "Taxonomy readiness gate blocked lane {Lane} for household {HouseholdId}: {Rationale}",
                lane,
                householdId,
                rationale);

            return new TaxonomyReadinessEvaluation(
                IsReady: false,
                ReasonCode: TaxonomyReadinessReasonCodes.FillRateBelowThreshold,
                Rationale: rationale,
                Snapshot: snapshot);
        }

        return new TaxonomyReadinessEvaluation(
            IsReady: true,
            ReasonCode: TaxonomyReadinessReasonCodes.Ready,
            Rationale: "Taxonomy readiness thresholds passed for lane execution.",
            Snapshot: snapshot);
    }

    private static bool IsLaneEnabled(TaxonomyReadinessLane lane, TaxonomyReadinessOptions options)
    {
        return lane switch
        {
            TaxonomyReadinessLane.Classification => options.EnableClassificationGate,
            TaxonomyReadinessLane.Ingestion => options.EnableIngestionGate,
            _ => false,
        };
    }

    private async Task<TaxonomyReadinessSnapshot> BuildSnapshotAsync(
        Guid householdId,
        Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        var subcategoryCounts = await dbContext.Subcategories
            .AsNoTracking()
            .Where(x =>
                !x.IsArchived
                && !x.Category.IsArchived
                && (
                    x.Category.OwnerType == CategoryOwnerType.Platform
                    || (x.Category.OwnerType == CategoryOwnerType.HouseholdShared
                        && x.Category.HouseholdId == householdId)
                    || (ownerUserId.HasValue
                        && x.Category.OwnerType == CategoryOwnerType.User
                        && x.Category.HouseholdId == householdId
                        && x.Category.OwnerUserId == ownerUserId.Value)))
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Platform = group.Count(x => x.Category.OwnerType == CategoryOwnerType.Platform),
                Household = group.Count(x => x.Category.OwnerType == CategoryOwnerType.HouseholdShared),
                UserScoped = group.Count(x => x.Category.OwnerType == CategoryOwnerType.User),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var expenseCounts = await dbContext.EnrichedTransactions
            .AsNoTracking()
            .Where(x => x.Account.HouseholdId == householdId && x.Amount < 0m)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Categorized = group.Count(x => x.SubcategoryId.HasValue),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var platformCount = subcategoryCounts?.Platform ?? 0;
        var householdCount = subcategoryCounts?.Household ?? 0;
        var userScopedCount = subcategoryCounts?.UserScoped ?? 0;
        var totalEligibleCount = platformCount + householdCount + userScopedCount;

        var expenseCount = expenseCounts?.Total ?? 0;
        var categorizedExpenseCount = expenseCounts?.Categorized ?? 0;
        var expenseFillRate = expenseCount == 0
            ? 1m
            : decimal.Round(categorizedExpenseCount / (decimal)expenseCount, 4, MidpointRounding.AwayFromZero);

        return new TaxonomyReadinessSnapshot(
            HouseholdId: householdId,
            PlatformSubcategoryCount: platformCount,
            HouseholdSharedSubcategoryCount: householdCount,
            UserScopedSubcategoryCount: userScopedCount,
            TotalEligibleSubcategoryCount: totalEligibleCount,
            ExpenseTransactionCount: expenseCount,
            CategorizedExpenseTransactionCount: categorizedExpenseCount,
            ExpenseFillRate: expenseFillRate);
    }
}

public sealed class AllowAllTaxonomyReadinessGate : ITaxonomyReadinessGate
{
    public static readonly AllowAllTaxonomyReadinessGate Instance = new();

    public Task<TaxonomyReadinessEvaluation> EvaluateAsync(
        Guid householdId,
        TaxonomyReadinessLane lane,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TaxonomyReadinessEvaluation(
            IsReady: true,
            ReasonCode: TaxonomyReadinessReasonCodes.Ready,
            Rationale: "Taxonomy readiness gate bypassed by allow-all implementation.",
            Snapshot: new TaxonomyReadinessSnapshot(
                HouseholdId: householdId,
                PlatformSubcategoryCount: 0,
                HouseholdSharedSubcategoryCount: 0,
                UserScopedSubcategoryCount: 0,
                TotalEligibleSubcategoryCount: 0,
                ExpenseTransactionCount: 0,
                CategorizedExpenseTransactionCount: 0,
                ExpenseFillRate: 1m)));
    }
}