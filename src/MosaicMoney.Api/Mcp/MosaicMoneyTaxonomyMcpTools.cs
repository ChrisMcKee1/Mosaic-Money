using System.ComponentModel;
using ModelContextProtocol.Server;
using MosaicMoney.Api.Domain.Ledger.Taxonomy;

namespace MosaicMoney.Api.Mcp;

[McpServerToolType]
public sealed class MosaicMoneyTaxonomyMcpTools(IMcpTaxonomyBusinessService taxonomyBusinessService)
{
    [McpServerTool, Description("Lists transaction subcategories within a specific ownership scope. Scope must be Platform, HouseholdShared, or User.")]
    public async Task<IReadOnlyList<McpScopedSubcategoryDto>> ListScopedSubcategoriesAsync(
        [Description("Category scope: Platform, HouseholdShared, or User.")] string scope,
        [Description("Household ID required for HouseholdShared/User scope.")] Guid? householdId = null,
        [Description("Owner user ID required for User scope.")] Guid? ownerUserId = null,
        [Description("Include archived categories and subcategories in results.")] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        return await taxonomyBusinessService.ListScopedSubcategoriesAsync(
            scope,
            householdId,
            ownerUserId,
            includeArchived,
            cancellationToken);
    }

    [McpServerTool, Description("Creates a new category in HouseholdShared or User scope. Platform scope mutation is not allowed.")]
    public async Task<McpCategoryMutationResult> CreateCategoryAsync(
        [Description("Category scope for creation: HouseholdShared or User.")] string scope,
        [Description("Category display name.")] string name,
        [Description("Target household identifier.")] Guid householdId,
        [Description("Owner user ID (required when scope is User).")]
        Guid? ownerUserId,
        [Description("Active household user ID that confirms human approval for this mutation.")]
        Guid approvedByHouseholdUserId,
        CancellationToken cancellationToken = default)
    {
        return await taxonomyBusinessService.CreateCategoryAsync(
            scope,
            name,
            householdId,
            ownerUserId,
            approvedByHouseholdUserId,
            cancellationToken);
    }

    [McpServerTool, Description("Applies a classification update to a transaction with explicit human approval and records provenance metadata.")]
    public async Task<McpClassificationMutationResult> ApplyTransactionClassificationAsync(
        [Description("Target transaction identifier.")] Guid transactionId,
        [Description("Subcategory identifier to assign.")] Guid subcategoryId,
        [Description("Active household user ID that confirms human approval for classification update.")]
        Guid approvedByHouseholdUserId,
        [Description("Optional human rationale note stored with the classification outcome.")]
        string? rationale = null,
        CancellationToken cancellationToken = default)
    {
        return await taxonomyBusinessService.ApplyClassificationAsync(
            transactionId,
            subcategoryId,
            approvedByHouseholdUserId,
            rationale,
            cancellationToken);
    }
}
