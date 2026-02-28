using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger.Classification;

namespace MosaicMoney.Api.Domain.Ledger.Taxonomy;

public sealed record McpScopedSubcategoryDto(
    Guid SubcategoryId,
    string SubcategoryName,
    Guid CategoryId,
    string CategoryName,
    string Scope,
    Guid? HouseholdId,
    Guid? OwnerUserId,
    bool IsArchived);

public sealed record McpCategoryMutationResult(
    Guid CategoryId,
    string Name,
    string Scope,
    Guid? HouseholdId,
    Guid? OwnerUserId,
    DateTime CreatedAtUtc);

public sealed record McpClassificationMutationResult(
    Guid TransactionId,
    Guid SubcategoryId,
    string ReviewStatus,
    string AssignmentSource,
    DateTime UpdatedAtUtc);

public interface IMcpTaxonomyBusinessService
{
    Task<IReadOnlyList<McpScopedSubcategoryDto>> ListScopedSubcategoriesAsync(
        string scope,
        Guid? householdId,
        Guid? ownerUserId,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<McpCategoryMutationResult> CreateCategoryAsync(
        string scope,
        string name,
        Guid householdId,
        Guid? ownerUserId,
        Guid approvedByHouseholdUserId,
        CancellationToken cancellationToken = default);

    Task<McpClassificationMutationResult> ApplyClassificationAsync(
        Guid transactionId,
        Guid subcategoryId,
        Guid approvedByHouseholdUserId,
        string? rationale,
        CancellationToken cancellationToken = default);
}

public sealed class McpTaxonomyBusinessService(
    MosaicMoneyDbContext dbContext,
    ICategoryLifecycleAuditTrail auditTrail,
    IClassificationInsightWriter insightWriter) : IMcpTaxonomyBusinessService
{
    public async Task<IReadOnlyList<McpScopedSubcategoryDto>> ListScopedSubcategoriesAsync(
        string scope,
        Guid? householdId,
        Guid? ownerUserId,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var resolvedScope = ResolveScope(scope);

        var query = dbContext.Subcategories
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => includeArchived || (!x.IsArchived && !x.Category.IsArchived));

        query = ApplyScope(query, resolvedScope, householdId, ownerUserId);

        return await query
            .OrderBy(x => x.Category.DisplayOrder)
            .ThenBy(x => x.DisplayOrder)
            .Select(x => new McpScopedSubcategoryDto(
                x.Id,
                x.Name,
                x.CategoryId,
                x.Category.Name,
                x.Category.OwnerType.ToString(),
                x.Category.HouseholdId,
                x.Category.OwnerUserId,
                x.IsArchived || x.Category.IsArchived))
            .ToListAsync(cancellationToken);
    }

    public async Task<McpCategoryMutationResult> CreateCategoryAsync(
        string scope,
        string name,
        Guid householdId,
        Guid? ownerUserId,
        Guid approvedByHouseholdUserId,
        CancellationToken cancellationToken = default)
    {
        var resolvedScope = ResolveScope(scope);
        if (resolvedScope == CategoryOwnerType.Platform)
        {
            throw new InvalidOperationException("Platform scope categories cannot be created through MCP mutation tools.");
        }

        if (resolvedScope == CategoryOwnerType.User && !ownerUserId.HasValue)
        {
            throw new InvalidOperationException("OwnerUserId is required for User scope category creation.");
        }

        await ValidateApproverInHouseholdAsync(approvedByHouseholdUserId, householdId, cancellationToken);

        var duplicateExists = await dbContext.Categories
            .AsNoTracking()
            .Where(x => !x.IsArchived && x.OwnerType == resolvedScope)
            .Where(x => resolvedScope == CategoryOwnerType.Platform
                || x.HouseholdId == householdId)
            .Where(x => resolvedScope != CategoryOwnerType.User
                || x.OwnerUserId == ownerUserId)
            .AnyAsync(x => x.Name == name.Trim(), cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("An active category with the same name already exists in the requested scope.");
        }

        var displayOrder = await dbContext.Categories
            .AsNoTracking()
            .Where(x => !x.IsArchived && x.OwnerType == resolvedScope)
            .Where(x => resolvedScope == CategoryOwnerType.Platform
                || x.HouseholdId == householdId)
            .Where(x => resolvedScope != CategoryOwnerType.User
                || x.OwnerUserId == ownerUserId)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var now = DateTime.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            DisplayOrder = displayOrder + 1,
            IsSystem = false,
            OwnerType = resolvedScope,
            HouseholdId = householdId,
            OwnerUserId = resolvedScope == CategoryOwnerType.User ? ownerUserId : null,
            CreatedAtUtc = now,
            LastModifiedAtUtc = now,
        };

        dbContext.Categories.Add(category);

        auditTrail.Record(
            dbContext,
            entityType: "Category",
            entityId: category.Id,
            operation: "mcp_create_category",
            scopeOwnerType: category.OwnerType,
            householdId: category.HouseholdId,
            ownerUserId: category.OwnerUserId,
            performedByHouseholdUserId: approvedByHouseholdUserId,
            metadata: new { source = "mcp_tool", scope = category.OwnerType.ToString() });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new McpCategoryMutationResult(
            category.Id,
            category.Name,
            category.OwnerType.ToString(),
            category.HouseholdId,
            category.OwnerUserId,
            category.CreatedAtUtc);
    }

    public async Task<McpClassificationMutationResult> ApplyClassificationAsync(
        Guid transactionId,
        Guid subcategoryId,
        Guid approvedByHouseholdUserId,
        string? rationale,
        CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.EnrichedTransactions
            .Include(x => x.Account)
            .FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken)
            ?? throw new InvalidOperationException("Transaction not found.");

        await ValidateApproverInHouseholdAsync(approvedByHouseholdUserId, transaction.Account.HouseholdId, cancellationToken);

        var subcategory = await dbContext.Subcategories
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == subcategoryId, cancellationToken)
            ?? throw new InvalidOperationException("Subcategory not found.");

        var scopeAccessible = subcategory.Category.OwnerType == CategoryOwnerType.Platform
            || (subcategory.Category.OwnerType == CategoryOwnerType.HouseholdShared
                && subcategory.Category.HouseholdId == transaction.Account.HouseholdId)
            || (subcategory.Category.OwnerType == CategoryOwnerType.User
                && subcategory.Category.HouseholdId == transaction.Account.HouseholdId
                && subcategory.Category.OwnerUserId == approvedByHouseholdUserId);

        if (!scopeAccessible)
        {
            throw new InvalidOperationException("The requested subcategory scope is not accessible for this transaction household.");
        }

        transaction.SubcategoryId = subcategoryId;
        transaction.ReviewStatus = TransactionReviewStatus.Reviewed;
        transaction.ReviewReason = null;
        transaction.NeedsReviewByUserId = null;
        transaction.LastModifiedAtUtc = DateTime.UtcNow;

        var summary = string.IsNullOrWhiteSpace(rationale)
            ? "Classification updated through MCP tool with human approval."
            : rationale.Trim();

        var outcome = new TransactionClassificationOutcome
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            ProposedSubcategoryId = subcategoryId,
            FinalConfidence = 1.0m,
            Decision = ClassificationDecision.Categorized,
            ReviewStatus = TransactionReviewStatus.Reviewed,
            DecisionReasonCode = "mcp_classification_update",
            DecisionRationale = summary,
            AgentNoteSummary = AgentNoteSummaryPolicy.Sanitize(summary),
            IsAiAssigned = false,
            AssignmentSource = "mcp_human_approved",
            AssignedByAgent = null,
            CreatedAtUtc = DateTime.UtcNow,
        };

        outcome.StageOutputs.Add(new ClassificationStageOutput
        {
            Id = Guid.NewGuid(),
            Stage = ClassificationStage.Deterministic,
            StageOrder = 1,
            ProposedSubcategoryId = subcategoryId,
            Confidence = 1.0m,
            RationaleCode = "mcp_human_approved",
            Rationale = summary,
            EscalatedToNextStage = false,
            ProducedAtUtc = DateTime.UtcNow,
        });

        insightWriter.RecordOutcomeInsight(transaction, outcome, summary);

        dbContext.TransactionClassificationOutcomes.Add(outcome);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new McpClassificationMutationResult(
            transaction.Id,
            subcategoryId,
            transaction.ReviewStatus.ToString(),
            outcome.AssignmentSource,
            transaction.LastModifiedAtUtc);
    }

    private static CategoryOwnerType ResolveScope(string scope)
    {
        if (!Enum.TryParse<CategoryOwnerType>(scope, ignoreCase: true, out var parsedScope)
            || !Enum.IsDefined(parsedScope))
        {
            throw new InvalidOperationException("Scope must be one of: Platform, HouseholdShared, User.");
        }

        return parsedScope;
    }

    private static IQueryable<Subcategory> ApplyScope(
        IQueryable<Subcategory> query,
        CategoryOwnerType scope,
        Guid? householdId,
        Guid? ownerUserId)
    {
        return scope switch
        {
            CategoryOwnerType.Platform => query.Where(x => x.Category.OwnerType == CategoryOwnerType.Platform),
            CategoryOwnerType.HouseholdShared => householdId.HasValue
                ? query.Where(x => x.Category.OwnerType == CategoryOwnerType.HouseholdShared && x.Category.HouseholdId == householdId)
                : throw new InvalidOperationException("HouseholdId is required for HouseholdShared scope."),
            CategoryOwnerType.User => householdId.HasValue && ownerUserId.HasValue
                ? query.Where(x =>
                    x.Category.OwnerType == CategoryOwnerType.User
                    && x.Category.HouseholdId == householdId
                    && x.Category.OwnerUserId == ownerUserId)
                : throw new InvalidOperationException("HouseholdId and OwnerUserId are required for User scope."),
            _ => throw new InvalidOperationException("Unsupported category scope."),
        };
    }

    private async Task ValidateApproverInHouseholdAsync(
        Guid householdUserId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.HouseholdUsers
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == householdUserId
                && x.HouseholdId == householdId
                && x.MembershipStatus == HouseholdMembershipStatus.Active,
                cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("ApprovedByHouseholdUserId is not an active member of the target household.");
        }
    }
}
