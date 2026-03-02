using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using MosaicMoney.Api.Domain.Ledger.Taxonomy;

namespace MosaicMoney.Api.Mcp;

[Authorize]
[McpServerToolType]
public sealed class MosaicMoneyTaxonomyMcpTools(
    IMcpAuthenticatedContextAccessor contextAccessor,
    IMcpTaxonomyBusinessService taxonomyBusinessService)
{
    [McpServerTool, Description("Lists transaction subcategories within a specific ownership scope visible to the authenticated user. Scope must be Platform, HouseholdShared, or User.")]
    public async Task<IReadOnlyList<McpScopedSubcategoryDto>> ListScopedSubcategoriesAsync(
        [Description("Category scope: Platform, HouseholdShared, or User.")] string scope,
        [Description("Include archived categories and subcategories in results.")] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var householdUserId = await contextAccessor.GetRequiredHouseholdUserIdAsync(
            householdId: null,
            cancellationToken);

        return await taxonomyBusinessService.ListScopedSubcategoriesAsync(
            scope,
            householdUserId,
            includeArchived,
            cancellationToken);
    }

    [McpServerTool, Description("Creates a new category in HouseholdShared or User scope for the authenticated user context. Platform scope mutation is not allowed.")]
    public async Task<McpCategoryMutationResult> CreateCategoryAsync(
        [Description("Category scope for creation: HouseholdShared or User.")] string scope,
        [Description("Category display name.")] string name,
        CancellationToken cancellationToken = default)
    {
        var householdUserId = await contextAccessor.GetRequiredHouseholdUserIdAsync(
            householdId: null,
            cancellationToken);

        return await taxonomyBusinessService.CreateCategoryAsync(
            scope,
            name,
            householdUserId,
            cancellationToken);
    }

    [McpServerTool, Description("Applies a classification update to a transaction in the authenticated user context and records provenance metadata.")]
    public async Task<McpClassificationMutationResult> ApplyTransactionClassificationAsync(
        [Description("Target transaction identifier.")] Guid transactionId,
        [Description("Subcategory identifier to assign.")] Guid subcategoryId,
        [Description("Optional human rationale note stored with the classification outcome.")]
        string? rationale = null,
        CancellationToken cancellationToken = default)
    {
        var householdUserId = await contextAccessor.GetRequiredHouseholdUserIdAsync(
            householdId: null,
            cancellationToken);

        return await taxonomyBusinessService.ApplyClassificationAsync(
            transactionId,
            subcategoryId,
            householdUserId,
            rationale,
            cancellationToken);
    }
}
