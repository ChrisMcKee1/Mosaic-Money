using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.ServiceBus;
using MosaicMoney.Api.Domain.Agent;
using Npgsql;

namespace MosaicMoney.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    ServiceBusClient serviceBusClient,
    EventHubProducerClient eventHubProducerClient,
    NpgsqlDataSource npgsqlDataSource,
    IFoundryAgentRuntimeService foundryAgentRuntimeService) : BackgroundService
{
    private const string PolicyVersion = "m10-worker-orchestration-v1";
    private static readonly JsonSerializerOptions PayloadJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ingestionProcessor = serviceBusClient.CreateProcessor(
            RuntimeMessagingQueues.IngestionCompleted,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 2,
            });

        var agentCommandProcessor = serviceBusClient.CreateProcessor(
            RuntimeMessagingQueues.AgentMessagePosted,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 2,
            });

        var nightlySweepProcessor = serviceBusClient.CreateProcessor(
            RuntimeMessagingQueues.NightlyAnomalySweep,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1,
            });

        ingestionProcessor.ProcessMessageAsync += args => ProcessMessageAsync(args, RuntimeMessagingQueues.IngestionCompleted, stoppingToken);
        agentCommandProcessor.ProcessMessageAsync += args => ProcessMessageAsync(args, RuntimeMessagingQueues.AgentMessagePosted, stoppingToken);
        nightlySweepProcessor.ProcessMessageAsync += args => ProcessMessageAsync(args, RuntimeMessagingQueues.NightlyAnomalySweep, stoppingToken);

        ingestionProcessor.ProcessErrorAsync += args => ProcessErrorAsync(args, RuntimeMessagingQueues.IngestionCompleted);
        agentCommandProcessor.ProcessErrorAsync += args => ProcessErrorAsync(args, RuntimeMessagingQueues.AgentMessagePosted);
        nightlySweepProcessor.ProcessErrorAsync += args => ProcessErrorAsync(args, RuntimeMessagingQueues.NightlyAnomalySweep);

        await ingestionProcessor.StartProcessingAsync(stoppingToken);
        await agentCommandProcessor.StartProcessingAsync(stoppingToken);
        await nightlySweepProcessor.StartProcessingAsync(stoppingToken);

        logger.LogInformation(
            "Worker command processors started for queues: {IngestionQueue}, {AgentQueue}, {NightlyQueue}",
            RuntimeMessagingQueues.IngestionCompleted,
            RuntimeMessagingQueues.AgentMessagePosted,
            RuntimeMessagingQueues.NightlyAnomalySweep);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        finally
        {
            await ingestionProcessor.StopProcessingAsync(CancellationToken.None);
            await agentCommandProcessor.StopProcessingAsync(CancellationToken.None);
            await nightlySweepProcessor.StopProcessingAsync(CancellationToken.None);

            await ingestionProcessor.DisposeAsync();
            await agentCommandProcessor.DisposeAsync();
            await nightlySweepProcessor.DisposeAsync();
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args, string queueName, CancellationToken cancellationToken)
    {
        var body = args.Message.Body.ToString();
        RuntimeCommandEnvelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<RuntimeCommandEnvelope>(body);
        }
        catch (JsonException jsonException)
        {
            logger.LogWarning(jsonException, "Received invalid JSON payload on queue {QueueName}. Dead-lettering message {MessageId}.", queueName, args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "invalid_json", "Message payload is not valid JSON.", cancellationToken);
            return;
        }

        if (envelope is null || envelope.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.CorrelationId) || string.IsNullOrWhiteSpace(envelope.CommandType))
        {
            logger.LogWarning("Received invalid command envelope on queue {QueueName}. Dead-lettering message {MessageId}.", queueName, args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "invalid_envelope", "Runtime command envelope is missing required fields.", cancellationToken);
            return;
        }

        var idempotencyKeyValue = !string.IsNullOrWhiteSpace(args.Message.MessageId)
            ? args.Message.MessageId
            : envelope.CommandId.ToString("N");
        var requestHash = ComputeSha256(body);
        var reservationResult = await TryReserveIdempotencyKeyAsync(queueName, idempotencyKeyValue, requestHash, cancellationToken);

        if (reservationResult == IdempotencyReservationResult.AlreadyCompleted)
        {
            logger.LogInformation("Skipping duplicate runtime command {CommandId} on queue {QueueName} with idempotency key {IdempotencyKey}.", envelope.CommandId, queueName, idempotencyKeyValue);
            await args.CompleteMessageAsync(args.Message, cancellationToken);
            return;
        }

        if (reservationResult == IdempotencyReservationResult.HashMismatch)
        {
            logger.LogWarning(
                "Idempotency hash mismatch for queue {QueueName} and key {IdempotencyKey}. Dead-lettering command {CommandId}.",
                queueName,
                idempotencyKeyValue,
                envelope.CommandId);
            await args.DeadLetterMessageAsync(args.Message, "idempotency_hash_mismatch", "Idempotency key was reused with a different payload hash.", cancellationToken);
            return;
        }

        if (reservationResult == IdempotencyReservationResult.AlreadyRejected)
        {
            logger.LogWarning(
                "Skipping rejected runtime command {CommandId} on queue {QueueName} with idempotency key {IdempotencyKey}.",
                envelope.CommandId,
                queueName,
                idempotencyKeyValue);
            await args.DeadLetterMessageAsync(args.Message, "idempotency_rejected", "Command idempotency key is already finalized as rejected.", cancellationToken);
            return;
        }

        var householdId = TryResolveHouseholdId(envelope.Payload);
        var runId = await CreateAgentRunAsync(envelope, queueName, householdId, cancellationToken);

        try
        {
            var executionResult = await ExecuteCommandAsync(runId, envelope, queueName, cancellationToken);

            if (executionResult.NeedsReview)
            {
                await MarkRunNeedsReviewAsync(runId, executionResult.OutcomeCode, executionResult.OutcomeRationale, cancellationToken);
                await AddNeedsReviewSignalAsync(runId, executionResult, cancellationToken);
                await FinalizeIdempotencyKeyAsync(
                    runId,
                    queueName,
                    idempotencyKeyValue,
                    requestHash,
                    status: 3,
                    "needs_review",
                    executionResult.OutcomeRationale,
                    cancellationToken);
                await EmitTelemetryAsync(envelope, queueName, "needs_review", cancellationToken);
            }
            else
            {
                await MarkRunCompletedAsync(runId, cancellationToken);
                await FinalizeIdempotencyKeyAsync(runId, queueName, idempotencyKeyValue, requestHash, status: 2, "completed", "Command processed successfully.", cancellationToken);
                await EmitTelemetryAsync(envelope, queueName, "completed", cancellationToken);
            }

            await args.CompleteMessageAsync(args.Message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Runtime command {CommandId} failed on queue {QueueName} (delivery {DeliveryCount}).", envelope.CommandId, queueName, args.Message.DeliveryCount);

            await MarkRunNeedsReviewAsync(runId, "worker_command_failed", exception.Message, cancellationToken);
            await AddFailureSignalAsync(runId, exception, cancellationToken);
            await EmitTelemetryAsync(envelope, queueName, "needs_review", cancellationToken);

            if (args.Message.DeliveryCount >= 5)
            {
                await FinalizeIdempotencyKeyAsync(runId, queueName, idempotencyKeyValue, requestHash, status: 3, "failed", exception.Message, cancellationToken);
                await args.DeadLetterMessageAsync(args.Message, "processing_failed", exception.Message, cancellationToken);
                return;
            }

            await args.AbandonMessageAsync(args.Message, cancellationToken: cancellationToken);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args, string queueName)
    {
        logger.LogError(
            args.Exception,
            "Runtime queue processor error for queue {QueueName}. Source={ErrorSource}, EntityPath={EntityPath}, Namespace={FullyQualifiedNamespace}",
            queueName,
            args.ErrorSource,
            args.EntityPath,
            args.FullyQualifiedNamespace);

        return Task.CompletedTask;
    }

    private async Task<StageExecutionResult> ExecuteCommandAsync(Guid runId, RuntimeCommandEnvelope envelope, string queueName, CancellationToken cancellationToken)
    {
        var executionResult = envelope.CommandType switch
        {
            RuntimeCommandTypes.IngestionCompleted => new StageExecutionResult(
                NeedsReview: false,
                Executor: "MosaicMoney.Worker",
                StageStatus: 3,
                Confidence: 1.0000m,
                OutcomeCode: "ingestion_trigger_processed",
                OutcomeRationale: "Worker processed ingestion trigger and preserved fail-closed routing requirements.",
                AgentNoteSummary: "Ingestion command acknowledged."),
            RuntimeCommandTypes.AgentMessagePosted => await ExecuteAgentMessageCommandAsync(envelope.Payload, cancellationToken),
            RuntimeCommandTypes.AgentApprovalSubmitted => await ExecuteAgentApprovalCommandAsync(envelope.Payload, cancellationToken),
            RuntimeCommandTypes.NightlyAnomalySweep => new StageExecutionResult(
                NeedsReview: false,
                Executor: "MosaicMoney.Worker",
                StageStatus: 3,
                Confidence: 1.0000m,
                OutcomeCode: "nightly_sweep_trigger_processed",
                OutcomeRationale: "Worker processed nightly anomaly sweep trigger and preserved fail-closed routing requirements.",
                AgentNoteSummary: "Nightly sweep command acknowledged."),
            _ => throw new InvalidOperationException($"Unsupported runtime command type '{envelope.CommandType}'."),
        };

        await PersistStageResultAsync(runId, envelope.CommandType, executionResult, cancellationToken);
        logger.LogInformation(
            "Runtime command {CommandType} on queue {QueueName} persisted with outcome {OutcomeCode} and needsReview={NeedsReview}.",
            envelope.CommandType,
            queueName,
            executionResult.OutcomeCode,
            executionResult.NeedsReview);

        return executionResult;
    }

    private async Task PersistStageResultAsync(
        Guid runId,
        string stageName,
        StageExecutionResult executionResult,
        CancellationToken cancellationToken)
    {

        const string sql =
            """
            INSERT INTO "AgentRunStages" (
                "Id",
                "AgentRunId",
                "StageName",
                "StageOrder",
                "Executor",
                "Status",
                "Confidence",
                "OutcomeCode",
                "OutcomeRationale",
                "AgentNoteSummary",
                "CreatedAtUtc",
                "LastModifiedAtUtc",
                "CompletedAtUtc")
            VALUES (
                @id,
                @agentRunId,
                @stageName,
                @stageOrder,
                @executor,
                @status,
                @confidence,
                @outcomeCode,
                @outcomeRationale,
                @agentNoteSummary,
                @createdAtUtc,
                @lastModifiedAtUtc,
                @completedAtUtc);
            """;

        await using var connection = await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("agentRunId", runId);
        command.Parameters.AddWithValue("stageName", stageName);
        command.Parameters.AddWithValue("stageOrder", 1);
        command.Parameters.AddWithValue("executor", executionResult.Executor);
        command.Parameters.AddWithValue("status", executionResult.StageStatus);
        command.Parameters.AddWithValue("confidence", executionResult.Confidence);
        command.Parameters.AddWithValue("outcomeCode", executionResult.OutcomeCode);
        command.Parameters.AddWithValue("outcomeRationale", Truncate(executionResult.OutcomeRationale, 500));
        command.Parameters.AddWithValue("agentNoteSummary", Truncate(executionResult.AgentNoteSummary, 600));
        command.Parameters.AddWithValue("createdAtUtc", now);
        command.Parameters.AddWithValue("lastModifiedAtUtc", now);
        command.Parameters.AddWithValue("completedAtUtc", now);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<StageExecutionResult> ExecuteAgentMessageCommandAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var command = DeserializeAgentMessageCommand(payload);
        var invocationRequest = new FoundryAgentInvocationRequest(
            command.HouseholdId,
            command.ConversationId,
            command.HouseholdUserId,
            RuntimeCommandTypes.AgentMessagePosted,
            command.Message,
            command.UserNote,
            command.PolicyDisposition,
            ApprovalId: null,
            ApprovalDecision: null,
            ApprovalRationale: null);

        var invocation = await foundryAgentRuntimeService.InvokeAsync(invocationRequest, cancellationToken);
        return CreateAgentStageResult(invocation);
    }

    private async Task<StageExecutionResult> ExecuteAgentApprovalCommandAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var command = DeserializeAgentApprovalCommand(payload);
        var syntheticMessage = $"Approval decision '{command.Decision}' submitted for approval '{command.ApprovalId:D}'.";

        var invocationRequest = new FoundryAgentInvocationRequest(
            command.HouseholdId,
            command.ConversationId,
            command.HouseholdUserId,
            RuntimeCommandTypes.AgentApprovalSubmitted,
            syntheticMessage,
            command.Rationale,
            command.PolicyDisposition,
            command.ApprovalId,
            command.Decision,
            command.Rationale);

        var invocation = await foundryAgentRuntimeService.InvokeAsync(invocationRequest, cancellationToken);
        return CreateAgentStageResult(invocation);
    }

    private static StageExecutionResult CreateAgentStageResult(FoundryAgentInvocationResult invocation)
    {
        var assignmentHint = string.IsNullOrWhiteSpace(invocation.AssignmentHint)
            ? "needs_review"
            : invocation.AssignmentHint.Trim();
        var rationale = BuildRationaleWithAssignmentHint(assignmentHint, invocation.Summary);
        var executor = BuildAgentExecutor(invocation.AgentSource, invocation.AgentName);
        var noteSummary = string.IsNullOrWhiteSpace(invocation.ResponseSummary)
            ? invocation.Summary
            : invocation.ResponseSummary;

        return new StageExecutionResult(
            NeedsReview: invocation.NeedsReview,
            Executor: executor,
            StageStatus: invocation.NeedsReview
                ? 5
                : 3,
            Confidence: invocation.NeedsReview ? 0.0000m : 1.0000m,
            OutcomeCode: invocation.OutcomeCode,
            OutcomeRationale: rationale,
            AgentNoteSummary: noteSummary);
    }

    private static string BuildAgentExecutor(string? agentSource, string? agentName)
    {
        if (string.IsNullOrWhiteSpace(agentSource) || string.IsNullOrWhiteSpace(agentName))
        {
            return "MosaicMoney.Worker";
        }

        return $"{agentSource.Trim()}:{agentName.Trim()}";
    }

    private static string BuildRationaleWithAssignmentHint(string assignmentHint, string summary)
    {
        return $"assignment_hint={assignmentHint}; {summary}";
    }

    private static AgentMessagePostedCommand DeserializeAgentMessageCommand(JsonElement payload)
    {
        var command = payload.Deserialize<AgentMessagePostedCommand>(PayloadJsonSerializerOptions);
        if (command is null
            || command.HouseholdId == Guid.Empty
            || command.ConversationId == Guid.Empty
            || command.HouseholdUserId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.Message)
            || string.IsNullOrWhiteSpace(command.PolicyDisposition))
        {
            throw new InvalidOperationException("Agent message payload is missing required fields.");
        }

        return command;
    }

    private static AgentApprovalSubmittedCommand DeserializeAgentApprovalCommand(JsonElement payload)
    {
        var command = payload.Deserialize<AgentApprovalSubmittedCommand>(PayloadJsonSerializerOptions);
        if (command is null
            || command.HouseholdId == Guid.Empty
            || command.ConversationId == Guid.Empty
            || command.ApprovalId == Guid.Empty
            || command.HouseholdUserId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.Decision)
            || string.IsNullOrWhiteSpace(command.PolicyDisposition))
        {
            throw new InvalidOperationException("Agent approval payload is missing required fields.");
        }

        return command;
    }

    private async Task<Guid> CreateAgentRunAsync(
        RuntimeCommandEnvelope envelope,
        string queueName,
        Guid? householdId,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO "AgentRuns" (
                "Id",
                "HouseholdId",
                "CorrelationId",
                "WorkflowName",
                "TriggerSource",
                "PolicyVersion",
                "Status",
                "FailureCode",
                "FailureRationale",
                "CreatedAtUtc",
                "LastModifiedAtUtc",
                "CompletedAtUtc")
            VALUES (
                @id,
                @householdId,
                @correlationId,
                @workflowName,
                @triggerSource,
                @policyVersion,
                @status,
                NULL,
                NULL,
                @createdAtUtc,
                @lastModifiedAtUtc,
                NULL);
            """;

        var runId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var connection = await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", runId);
        command.Parameters.AddWithValue("householdId", householdId is Guid value ? value : DBNull.Value);
        command.Parameters.AddWithValue("correlationId", envelope.CorrelationId);
        command.Parameters.AddWithValue("workflowName", envelope.CommandType);
        command.Parameters.AddWithValue("triggerSource", queueName);
        command.Parameters.AddWithValue("policyVersion", PolicyVersion);
        command.Parameters.AddWithValue("status", 2);
        command.Parameters.AddWithValue("createdAtUtc", now);
        command.Parameters.AddWithValue("lastModifiedAtUtc", now);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return runId;
    }

    private async Task MarkRunCompletedAsync(Guid runId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE "AgentRuns"
            SET
                "Status" = 3,
                "LastModifiedAtUtc" = @now,
                "CompletedAtUtc" = @now,
                "FailureCode" = NULL,
                "FailureRationale" = NULL
            WHERE "Id" = @id;
            """;

        await using var connection = await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var now = DateTime.UtcNow;

        command.Parameters.AddWithValue("id", runId);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkRunNeedsReviewAsync(
        Guid runId,
        string failureCode,
        string failureRationale,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE "AgentRuns"
            SET
                "Status" = 5,
                "LastModifiedAtUtc" = @now,
                "CompletedAtUtc" = @now,
                "FailureCode" = @failureCode,
                "FailureRationale" = @failureRationale
            WHERE "Id" = @id;
            """;

        await using var connection = await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var now = DateTime.UtcNow;

        command.Parameters.AddWithValue("id", runId);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("failureCode", Truncate(failureCode, 120));
        command.Parameters.AddWithValue("failureRationale", Truncate(failureRationale, 500));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AddNeedsReviewSignalAsync(Guid runId, StageExecutionResult executionResult, CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO "AgentSignals" (
                "Id",
                "AgentRunId",
                "AgentRunStageId",
                "SignalCode",
                "Summary",
                "Severity",
                "RequiresHumanReview",
                "IsResolved",
                "RaisedAtUtc",
                "ResolvedAtUtc",
                "PayloadJson")
            VALUES (
                @id,
                @agentRunId,
                NULL,
                @signalCode,
                @summary,
                @severity,
                TRUE,
                FALSE,
                @raisedAtUtc,
                NULL,
                @payloadJson);
            """;

        await using var connection = await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("agentRunId", runId);
        command.Parameters.AddWithValue("signalCode", Truncate(executionResult.OutcomeCode, 120));
        command.Parameters.AddWithValue("summary", Truncate(executionResult.OutcomeRationale, 200));
        command.Parameters.AddWithValue("severity", 2);
        command.Parameters.AddWithValue("raisedAtUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("payloadJson", JsonSerializer.Serialize(new
        {
            source = "agent_fail_closed",
            executionResult.NeedsReview,
        }));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task AddFailureSignalAsync(Guid runId, Exception exception, CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO "AgentSignals" (
                "Id",
                "AgentRunId",
                "AgentRunStageId",
                "SignalCode",
                "Summary",
                "Severity",
                "RequiresHumanReview",
                "IsResolved",
                "RaisedAtUtc",
                "ResolvedAtUtc",
                "PayloadJson")
            VALUES (
                @id,
                @agentRunId,
                NULL,
                @signalCode,
                @summary,
                @severity,
                TRUE,
                FALSE,
                @raisedAtUtc,
                NULL,
                @payloadJson);
            """;

        await using var connection = await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("agentRunId", runId);
        command.Parameters.AddWithValue("signalCode", "worker_command_failed");
        command.Parameters.AddWithValue("summary", Truncate(exception.Message, 200));
        command.Parameters.AddWithValue("severity", 3);
        command.Parameters.AddWithValue("raisedAtUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("payloadJson", JsonSerializer.Serialize(new { exception = exception.GetType().Name }));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IdempotencyReservationResult> TryReserveIdempotencyKeyAsync(
        string scope,
        string keyValue,
        string requestHash,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO "IdempotencyKeys" (
                "Id",
                "Scope",
                "KeyValue",
                "RequestHash",
                "Status",
                "AgentRunId",
                "CreatedAtUtc",
                "ExpiresAtUtc",
                "FinalizedAtUtc",
                "ResolutionCode",
                "ResolutionRationale")
            VALUES (
                @id,
                @scope,
                @keyValue,
                @requestHash,
                1,
                NULL,
                @createdAtUtc,
                @expiresAtUtc,
                NULL,
                NULL,
                NULL)
            ON CONFLICT ("Scope", "KeyValue") DO NOTHING
            RETURNING "Id";
            """;

        await using var connection = await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("scope", scope);
        command.Parameters.AddWithValue("keyValue", keyValue);
        command.Parameters.AddWithValue("requestHash", requestHash);
        command.Parameters.AddWithValue("createdAtUtc", now);
        command.Parameters.AddWithValue("expiresAtUtc", now.AddDays(7));

        var reservedId = await command.ExecuteScalarAsync(cancellationToken);
        if (reservedId is Guid)
        {
            return IdempotencyReservationResult.Reserved;
        }

        const string selectSql =
            """
            SELECT "Status", "RequestHash"
            FROM "IdempotencyKeys"
            WHERE "Scope" = @scope AND "KeyValue" = @keyValue
            LIMIT 1;
            """;

        await using var selectCommand = new NpgsqlCommand(selectSql, connection);
        selectCommand.Parameters.AddWithValue("scope", scope);
        selectCommand.Parameters.AddWithValue("keyValue", keyValue);

        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return IdempotencyReservationResult.Reserved;
        }

        var existingStatus = reader.GetInt32(0);
        var existingHash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        if (!string.Equals(existingHash, requestHash, StringComparison.OrdinalIgnoreCase))
        {
            return IdempotencyReservationResult.HashMismatch;
        }

        return existingStatus switch
        {
            1 => IdempotencyReservationResult.Reserved,
            2 => IdempotencyReservationResult.AlreadyCompleted,
            3 => IdempotencyReservationResult.AlreadyRejected,
            4 => IdempotencyReservationResult.AlreadyRejected,
            _ => IdempotencyReservationResult.AlreadyRejected,
        };
    }

    private async Task FinalizeIdempotencyKeyAsync(
        Guid runId,
        string scope,
        string keyValue,
        string requestHash,
        int status,
        string resolutionCode,
        string resolutionRationale,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE "IdempotencyKeys"
            SET
                "AgentRunId" = @agentRunId,
                "Status" = @status,
                "RequestHash" = @requestHash,
                "FinalizedAtUtc" = @finalizedAtUtc,
                "ResolutionCode" = @resolutionCode,
                "ResolutionRationale" = @resolutionRationale
            WHERE "Scope" = @scope AND "KeyValue" = @keyValue;
            """;

        await using var connection = await npgsqlDataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("agentRunId", runId);
        command.Parameters.AddWithValue("scope", scope);
        command.Parameters.AddWithValue("keyValue", keyValue);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("requestHash", requestHash);
        command.Parameters.AddWithValue("finalizedAtUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("resolutionCode", resolutionCode);
        command.Parameters.AddWithValue("resolutionRationale", Truncate(resolutionRationale, 500));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EmitTelemetryAsync(RuntimeCommandEnvelope envelope, string queueName, string state, CancellationToken cancellationToken)
    {
        try
        {
            using var batch = await eventHubProducerClient.CreateBatchAsync(cancellationToken);
            var payload = JsonSerializer.Serialize(new
            {
                envelope.CommandId,
                envelope.CorrelationId,
                envelope.CommandType,
                Queue = queueName,
                State = state,
                OccurredAtUtc = DateTime.UtcNow,
            });

            if (batch.TryAdd(new EventData(Encoding.UTF8.GetBytes(payload))))
            {
                await eventHubProducerClient.SendAsync(batch, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Telemetry backpressure/errors must not block business command processing.
            logger.LogWarning(exception, "Failed to emit runtime telemetry for command {CommandId}.", envelope.CommandId);
        }
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static Guid? TryResolveHouseholdId(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryReadGuidProperty(payload, "HouseholdId")
            ?? TryReadGuidProperty(payload, "householdId");
    }

    private static Guid? TryReadGuidProperty(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String
            && Guid.TryParse(value.GetString(), out var parsedGuid))
        {
            return parsedGuid;
        }

        return null;
    }

    private enum IdempotencyReservationResult
    {
        Reserved = 1,
        AlreadyCompleted = 2,
        AlreadyRejected = 3,
        HashMismatch = 4,
    }

    private static class RuntimeMessagingQueues
    {
        public const string IngestionCompleted = "runtime-ingestion-completed";
        public const string AgentMessagePosted = "runtime-agent-message-posted";
        public const string NightlyAnomalySweep = "runtime-nightly-anomaly-sweep";
    }

    private static class RuntimeCommandTypes
    {
        public const string IngestionCompleted = "ingestion_completed";
        public const string AgentMessagePosted = "agent_message_posted";
        public const string AgentApprovalSubmitted = "agent_approval_submitted";
        public const string NightlyAnomalySweep = "nightly_anomaly_sweep";
    }

    private sealed record RuntimeCommandEnvelope(
        Guid CommandId,
        string CorrelationId,
        string CommandType,
        DateTime CreatedAtUtc,
        string? ClientReferenceId,
        JsonElement Payload);

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

    private sealed record StageExecutionResult(
        bool NeedsReview,
        string Executor,
        int StageStatus,
        decimal Confidence,
        string OutcomeCode,
        string OutcomeRationale,
        string AgentNoteSummary);
}
