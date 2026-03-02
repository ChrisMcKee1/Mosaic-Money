using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Data;

namespace MosaicMoney.Api.Mcp;

public interface IMcpAuthenticatedContextAccessor
{
    Task<Guid> GetRequiredHouseholdUserIdAsync(Guid? householdId = null, CancellationToken cancellationToken = default);
}

public sealed class McpAuthenticatedContextAccessor(
    IHttpContextAccessor httpContextAccessor,
    MosaicMoneyDbContext dbContext) : IMcpAuthenticatedContextAccessor
{
    public async Task<Guid> GetRequiredHouseholdUserIdAsync(Guid? householdId = null, CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("MCP request is missing HttpContext. HTTP transport is required for authenticated user context resolution.");

        var scope = await HouseholdMemberContextResolver.ResolveAsync(
            httpContext,
            dbContext,
            householdId,
            "The authenticated user is not an active household member for this MCP operation.",
            cancellationToken);

        if (scope.ErrorResult is not null)
        {
            throw new UnauthorizedAccessException("Unable to resolve an authorized household member context for this MCP request.");
        }

        return scope.HouseholdUserId;
    }
}
