using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

public static class AgentOrchestrationEndpoints
{
    private const string AgentCommandQueue = "runtime-agent-message-posted";
    private const string AgentCommandType = "agent_message_posted";
    private const string AgentApprovalCommandType = "agent_approval_submitted";

    public static RouteGroupBuilder MapAgentOrchestrationEndpoints(this RouteGroupBuilder group)
    {
        var agentGroup = group.MapGroup("/agent/conversations");

        agentGroup.MapPost("/{conversationId:guid}/messages", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            [FromServices] ServiceBusClient serviceBusClient,
            Guid conversationId,
            AgentConversationMessageRequest request,
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
                "The household member is not active and cannot post agent messages.",
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
                    "The household member is not active and cannot post agent messages.");
            }

            var now = DateTime.UtcNow;
            var commandId = Guid.NewGuid();
            var correlationId = $"agent:{householdId.Value:N}:{conversationId:N}:{commandId:N}";
            var policyDisposition = DetermineMessagePolicyDisposition(request.Message);

            var commandEnvelope = new AgentRuntimeCommandEnvelope(
                commandId,
                correlationId,
                AgentCommandType,
                now,
                request.ClientMessageId,
                new AgentMessagePostedCommand(
                    householdId.Value,
                    conversationId,
                    accessScope.HouseholdUserId,
                    request.Message.Trim(),
                    request.UserNote,
                    policyDisposition));

            await using var sender = serviceBusClient.CreateSender(AgentCommandQueue);
            var message = new ServiceBusMessage(JsonSerializer.Serialize(commandEnvelope))
            {
                MessageId = commandId.ToString("N"),
                CorrelationId = correlationId,
                Subject = AgentCommandType,
                ContentType = "application/json",
            };

            message.ApplicationProperties["conversationId"] = conversationId.ToString("D");
            message.ApplicationProperties["householdId"] = householdId.Value.ToString("D");
            message.ApplicationProperties["householdUserId"] = accessScope.HouseholdUserId.ToString("D");
            message.ApplicationProperties["policyDisposition"] = policyDisposition;

            await sender.SendMessageAsync(message, cancellationToken);

            return Results.Accepted(
                $"/api/v1/agent/conversations/{conversationId}/stream",
                new AgentCommandAcceptedDto(
                    commandId,
                    correlationId,
                    conversationId,
                    AgentCommandType,
                    AgentCommandQueue,
                    policyDisposition,
                    now,
                    "queued"));
        });

        agentGroup.MapPost("/{conversationId:guid}/approvals/{approvalId:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            [FromServices] ServiceBusClient serviceBusClient,
            Guid conversationId,
            Guid approvalId,
            AgentApprovalRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (!ApiEndpointHelpers.TryParseEnum<AgentApprovalDecision>(request.Decision, out var parsedDecision))
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
                "The household member is not active and cannot submit agent approvals.",
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
                    "The household member is not active and cannot submit agent approvals.");
            }

            var now = DateTime.UtcNow;
            var commandId = Guid.NewGuid();
            var correlationId = $"agent:{householdId.Value:N}:{conversationId:N}:{commandId:N}";
            var policyDisposition = parsedDecision == AgentApprovalDecision.Approve ? "approved_by_human" : "rejected_by_human";

            var commandEnvelope = new AgentRuntimeCommandEnvelope(
                commandId,
                correlationId,
                AgentApprovalCommandType,
                now,
                request.ClientApprovalId,
                new AgentApprovalSubmittedCommand(
                    householdId.Value,
                    conversationId,
                    approvalId,
                    accessScope.HouseholdUserId,
                    parsedDecision.ToString(),
                    request.Rationale,
                    policyDisposition));

            await using var sender = serviceBusClient.CreateSender(AgentCommandQueue);
            var message = new ServiceBusMessage(JsonSerializer.Serialize(commandEnvelope))
            {
                MessageId = commandId.ToString("N"),
                CorrelationId = correlationId,
                Subject = AgentApprovalCommandType,
                ContentType = "application/json",
            };

            message.ApplicationProperties["conversationId"] = conversationId.ToString("D");
            message.ApplicationProperties["approvalId"] = approvalId.ToString("D");
            message.ApplicationProperties["householdId"] = householdId.Value.ToString("D");
            message.ApplicationProperties["householdUserId"] = accessScope.HouseholdUserId.ToString("D");
            message.ApplicationProperties["policyDisposition"] = policyDisposition;

            await sender.SendMessageAsync(message, cancellationToken);

            return Results.Accepted(
                $"/api/v1/agent/conversations/{conversationId}/stream",
                new AgentCommandAcceptedDto(
                    commandId,
                    correlationId,
                    conversationId,
                    AgentApprovalCommandType,
                    AgentCommandQueue,
                    policyDisposition,
                    now,
                    "queued"));
        });

        agentGroup.MapGet("/{conversationId:guid}/stream", async (
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
                "The household member is not active and cannot access agent stream updates.",
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
                    "The household member is not active and cannot access agent stream updates.");
            }

            var correlationPrefix = $"agent:{householdId.Value:N}:{conversationId:N}:";
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
                .Select(x => new
                {
                    x.Id,
                    x.CorrelationId,
                    Status = x.Status.ToString(),
                    x.TriggerSource,
                    x.FailureCode,
                    x.FailureRationale,
                    x.CreatedAtUtc,
                    x.LastModifiedAtUtc,
                    x.CompletedAtUtc,
                    x.WorkflowName,
                    LatestStageExecutor = x.Stages
                        .OrderByDescending(stage => stage.StageOrder)
                        .ThenByDescending(stage => stage.CreatedAtUtc)
                        .Select(stage => stage.Executor)
                        .FirstOrDefault(),
                    LatestStageOutcomeRationale = x.Stages
                        .OrderByDescending(stage => stage.StageOrder)
                        .ThenByDescending(stage => stage.CreatedAtUtc)
                        .Select(stage => stage.OutcomeRationale)
                        .FirstOrDefault(),
                    LatestStageAgentNoteSummary = x.Stages
                        .OrderByDescending(stage => stage.StageOrder)
                        .ThenByDescending(stage => stage.CreatedAtUtc)
                        .Select(stage => stage.AgentNoteSummary)
                        .FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            var mappedRuns = runs
                .Select(static run =>
                {
                    var (latestStageOutcomeSummary, assignmentHint) = ParseStageOutcomeRationale(run.LatestStageOutcomeRationale);
                    var (agentSource, agentName) = ResolveAgentProvenance(run.WorkflowName, run.LatestStageExecutor);

                    return new AgentConversationRunStatusDto(
                        run.Id,
                        run.CorrelationId,
                        run.Status,
                        run.TriggerSource,
                        run.FailureCode,
                        run.FailureRationale,
                        run.CreatedAtUtc,
                        run.LastModifiedAtUtc,
                        run.CompletedAtUtc,
                        AgentName: agentName,
                        AgentSource: agentSource,
                        AgentNoteSummary: run.LatestStageAgentNoteSummary,
                        LatestStageOutcomeSummary: latestStageOutcomeSummary,
                        AssignmentHint: assignmentHint);
                })
                .ToList();

            return Results.Ok(new AgentConversationStreamDto(conversationId, mappedRuns));
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

    private static (string? Summary, string? AssignmentHint) ParseStageOutcomeRationale(string? outcomeRationale)
    {
        if (string.IsNullOrWhiteSpace(outcomeRationale))
        {
            return (null, null);
        }

        const string assignmentHintPrefix = "assignment_hint=";
        if (outcomeRationale.StartsWith(assignmentHintPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var delimiterIndex = outcomeRationale.IndexOf(';');
            if (delimiterIndex > assignmentHintPrefix.Length)
            {
                var assignmentHint = outcomeRationale[assignmentHintPrefix.Length..delimiterIndex].Trim();
                var summary = outcomeRationale[(delimiterIndex + 1)..].Trim();

                return (
                    string.IsNullOrWhiteSpace(summary) ? null : summary,
                    string.IsNullOrWhiteSpace(assignmentHint) ? null : assignmentHint);
            }
        }

        return (outcomeRationale, null);
    }

    private static (string? AgentSource, string? AgentName) ResolveAgentProvenance(string workflowName, string? latestStageExecutor)
    {
        var parsed = ParseAgentExecutor(latestStageExecutor);
        if (!string.IsNullOrWhiteSpace(parsed.AgentSource) && !string.IsNullOrWhiteSpace(parsed.AgentName))
        {
            return parsed;
        }

        if (string.Equals(workflowName, AgentCommandType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(workflowName, AgentApprovalCommandType, StringComparison.OrdinalIgnoreCase))
        {
            return ("foundry", "Mosaic");
        }

        return (null, null);
    }

    private static (string? AgentSource, string? AgentName) ParseAgentExecutor(string? latestStageExecutor)
    {
        if (string.IsNullOrWhiteSpace(latestStageExecutor))
        {
            return (null, null);
        }

        var separatorIndex = latestStageExecutor.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= latestStageExecutor.Length - 1)
        {
            return (null, null);
        }

        var agentSource = latestStageExecutor[..separatorIndex].Trim();
        var agentName = latestStageExecutor[(separatorIndex + 1)..].Trim();

        return string.IsNullOrWhiteSpace(agentSource) || string.IsNullOrWhiteSpace(agentName)
            ? (null, null)
            : (agentSource, agentName);
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

    private sealed record AgentRuntimeCommandEnvelope(
        Guid CommandId,
        string CorrelationId,
        string CommandType,
        DateTime CreatedAtUtc,
        string? ClientReferenceId,
        object Payload);

    private sealed record AgentMessagePostedCommand(
        Guid HouseholdId,
        Guid ConversationId,
        Guid HouseholdUserId,
        string Message,
        string? UserNote,
        string PolicyDisposition);

    private sealed record AgentApprovalSubmittedCommand(
        Guid HouseholdId,
        Guid ConversationId,
        Guid ApprovalId,
        Guid HouseholdUserId,
        string Decision,
        string? Rationale,
        string PolicyDisposition);
}
