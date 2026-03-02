using MosaicMoney.Api.Contracts.V1;

namespace MosaicMoney.Api.Apis;

public static class ApiRouteGroups
{
    public static WebApplication MapMosaicMoneyApi(this WebApplication app)
    {
        app.MapGet("/", () => "Mosaic Money API").AllowAnonymous();
        app.MapGet("/api/health", () => new { Status = "ok", Timestamp = DateTime.UtcNow }).AllowAnonymous();

        var v1 = app.MapGroup("/api/v1").RequireAuthorization();
        v1.MapTransactionEndpoints();
        v1.MapPlaidIngestionEndpoints();
        v1.MapPlaidLinkLifecycleEndpoints();
        v1.MapPlaidLiabilitiesEndpoints();
        v1.MapHouseholdEndpoints();
        v1.MapCategoryLifecycleEndpoints();
        v1.MapClassificationOutcomeEndpoints();
        v1.MapRecurringEndpoints();
        v1.MapReviewActionEndpoints();
        v1.MapReimbursementEndpoints();
        v1.MapPlaidInvestmentsEndpoints();
        v1.MapNetWorthHistoryEndpoints();
        v1.MapSearchEndpoints();
        v1.MapAgentOrchestrationEndpoints();
        v1.MapAgentPromptEndpoints();

        return app;
    }
}
