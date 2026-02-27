using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

public static class AssistantOrchestrationEndpoints
{
    private const string AssistantCommandQueue = "runtime-assistant-message-posted";
    private const string AssistantCommandType = "assistant_message_posted";
    private const string AssistantApprovalCommandType = "assistant_approval_submitted";

    public static RouteGroupBuilder MapAssistantOrchestrationEndpoints(this RouteGroupBuilder group)
    {
        var assistantGroup = group.MapGroup("/assistant/conversations");

        assistantGroup.MapPost("/{conversationId:guid}/messages", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            [FromKeyedServices(AssistantCommandQueue)] ServiceBusClient serviceBusClient,
            Guid conversationId,
            AssistantConversationMessageRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                errors.Add(new ApiValidationError(nameof(request.Message), "Message is required."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var accessScope = await HouseholdMemberContextResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The household member is not active and cannot post assistant messages.",
                cancellationToken);

            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var householdId = await ResolveHouseholdIdAsync(dbContext, accessScope.HouseholdUserId, cancellationToken);
            if (householdId is null)
            {
                return ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    "The household member is not active and cannot post assistant messages.");
            }

            var now = DateTime.UtcNow;
            var commandId = Guid.NewGuid();
            var correlationId = $"assistant:{householdId.Value:N}:{conversationId:N}:{commandId:N}";
            var policyDisposition = DetermineMessagePolicyDisposition(request.Message);

            var commandEnvelope = new AssistantRuntimeCommandEnvelope(
                commandId,
                correlationId,
                AssistantCommandType,
                now,
                request.ClientMessageId,
                new AssistantMessagePostedCommand(
                    householdId.Value,
                    conversationId,
                    accessScope.HouseholdUserId,
                    request.Message.Trim(),
                    request.UserNote,
                    policyDisposition));

            await using var sender = serviceBusClient.CreateSender(AssistantCommandQueue);
            var message = new ServiceBusMessage(JsonSerializer.Serialize(commandEnvelope))
            {
                MessageId = commandId.ToString("N"),
                CorrelationId = correlationId,
                Subject = AssistantCommandType,
                ContentType = "application/json",
            };

            message.ApplicationProperties["conversationId"] = conversationId.ToString("D");
            message.ApplicationProperties["householdId"] = householdId.Value.ToString("D");
            message.ApplicationProperties["householdUserId"] = accessScope.HouseholdUserId.ToString("D");
            message.ApplicationProperties["policyDisposition"] = policyDisposition;

            await sender.SendMessageAsync(message, cancellationToken);

            return Results.Accepted(
                $"/api/v1/assistant/conversations/{conversationId}/stream",
                new AssistantCommandAcceptedDto(
                    commandId,
                    correlationId,
                    conversationId,
                    AssistantCommandType,
                    AssistantCommandQueue,
                    policyDisposition,
                    now,
                    "queued"));
        });

        assistantGroup.MapPost("/{conversationId:guid}/approvals/{approvalId:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            [FromKeyedServices(AssistantCommandQueue)] ServiceBusClient serviceBusClient,
            Guid conversationId,
            Guid approvalId,
            AssistantApprovalRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (!ApiEndpointHelpers.TryParseEnum<AssistantApprovalDecision>(request.Decision, out var parsedDecision))
            {
                errors.Add(new ApiValidationError(nameof(request.Decision), "Decision must be one of: Approve, Reject."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var accessScope = await HouseholdMemberContextResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The household member is not active and cannot submit assistant approvals.",
                cancellationToken);

            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var householdId = await ResolveHouseholdIdAsync(dbContext, accessScope.HouseholdUserId, cancellationToken);
            if (householdId is null)
            {
                return ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    "The household member is not active and cannot submit assistant approvals.");
            }

            var now = DateTime.UtcNow;
            var commandId = Guid.NewGuid();
            var correlationId = $"assistant:{householdId.Value:N}:{conversationId:N}:{commandId:N}";
            var policyDisposition = parsedDecision == AssistantApprovalDecision.Approve ? "approved_by_human" : "rejected_by_human";

            var commandEnvelope = new AssistantRuntimeCommandEnvelope(
                commandId,
                correlationId,
                AssistantApprovalCommandType,
                now,
                request.ClientApprovalId,
                new AssistantApprovalSubmittedCommand(
                    householdId.Value,
                    conversationId,
                    approvalId,
                    accessScope.HouseholdUserId,
                    parsedDecision.ToString(),
                    request.Rationale,
                    policyDisposition));

            await using var sender = serviceBusClient.CreateSender(AssistantCommandQueue);
            var message = new ServiceBusMessage(JsonSerializer.Serialize(commandEnvelope))
            {
                MessageId = commandId.ToString("N"),
                CorrelationId = correlationId,
                Subject = AssistantApprovalCommandType,
                ContentType = "application/json",
            };

            message.ApplicationProperties["conversationId"] = conversationId.ToString("D");
            message.ApplicationProperties["approvalId"] = approvalId.ToString("D");
            message.ApplicationProperties["householdId"] = householdId.Value.ToString("D");
            message.ApplicationProperties["householdUserId"] = accessScope.HouseholdUserId.ToString("D");
            message.ApplicationProperties["policyDisposition"] = policyDisposition;

            await sender.SendMessageAsync(message, cancellationToken);

            return Results.Accepted(
                $"/api/v1/assistant/conversations/{conversationId}/stream",
                new AssistantCommandAcceptedDto(
                    commandId,
                    correlationId,
                    conversationId,
                    AssistantApprovalCommandType,
                    AssistantCommandQueue,
                    policyDisposition,
                    now,
                    "queued"));
        });

        assistantGroup.MapGet("/{conversationId:guid}/stream", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid conversationId,
            DateTime? sinceUtc,
            CancellationToken cancellationToken) =>
        {
            var accessScope = await HouseholdMemberContextResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The household member is not active and cannot access assistant stream updates.",
                cancellationToken);

            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var householdId = await ResolveHouseholdIdAsync(dbContext, accessScope.HouseholdUserId, cancellationToken);
            if (householdId is null)
            {
                return ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    "The household member is not active and cannot access assistant stream updates.");
            }

            var correlationPrefix = $"assistant:{householdId.Value:N}:{conversationId:N}:";
            var query = dbContext.AgentRuns
                .AsNoTracking()
                .Where(x => x.HouseholdId == householdId.Value)
                .Where(x => x.CorrelationId.StartsWith(correlationPrefix));

            if (sinceUtc.HasValue)
            {
                query = query.Where(x => x.CreatedAtUtc >= sinceUtc.Value);
            }

            var runs = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(50)
                .Select(x => new AssistantConversationRunStatusDto(
                    x.Id,
                    x.CorrelationId,
                    x.Status.ToString(),
                    x.TriggerSource,
                    x.FailureCode,
                    x.FailureRationale,
                    x.CreatedAtUtc,
                    x.LastModifiedAtUtc,
                    x.CompletedAtUtc))
                .ToListAsync(cancellationToken);

            return Results.Ok(new AssistantConversationStreamDto(conversationId, runs));
        });

        return group;
    }

    private static string DetermineMessagePolicyDisposition(string message)
    {
        if (message.Contains("send", StringComparison.OrdinalIgnoreCase)
            || message.Contains("email", StringComparison.OrdinalIgnoreCase)
            || message.Contains("text", StringComparison.OrdinalIgnoreCase)
            || message.Contains("wire", StringComparison.OrdinalIgnoreCase))
        {
            return "approval_required";
        }

        return "advisory_only";
    }

    private static async Task<Guid?> ResolveHouseholdIdAsync(
        MosaicMoneyDbContext dbContext,
        Guid householdUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.HouseholdUsers
            .AsNoTracking()
            .Where(x => x.Id == householdUserId && x.MembershipStatus == HouseholdMembershipStatus.Active)
            .Select(x => (Guid?)x.HouseholdId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private sealed record AssistantRuntimeCommandEnvelope(
        Guid CommandId,
        string CorrelationId,
        string CommandType,
        DateTime CreatedAtUtc,
        string? ClientReferenceId,
        object Payload);

    private sealed record AssistantMessagePostedCommand(
        Guid HouseholdId,
        Guid ConversationId,
        Guid HouseholdUserId,
        string Message,
        string? UserNote,
        string PolicyDisposition);

    private sealed record AssistantApprovalSubmittedCommand(
        Guid HouseholdId,
        Guid ConversationId,
        Guid ApprovalId,
        Guid HouseholdUserId,
        string Decision,
        string? Rationale,
        string PolicyDisposition);
}
