# Runtime Agentic Worker Runbook

This runbook covers day-2 operations for the runtime agentic orchestration lanes owned by `MosaicMoney.Worker`.

## Scope
- Command queues: `runtime-ingestion-completed`, `runtime-agent-message-posted`, `runtime-nightly-anomaly-sweep`.
- Telemetry stream: `runtime-telemetry-stream` Event Hubs lane.
- Lifecycle persistence: `AgentRuns`, `AgentRunStages`, `AgentSignals`, `IdempotencyKeys`.
- API contracts that enqueue commands:
   - `POST /api/v1/agent/conversations/{conversationId}/messages`
   - `POST /api/v1/agent/conversations/{conversationId}/approvals/{approvalId}`

## Guardrails
- Worker owns runtime orchestration command execution. API endpoints only enqueue commands and expose read models.
- Fail closed on command execution failures by setting run status to `NeedsReview` and raising an error signal.
- High-impact actions stay approval-only; no autonomous external messaging.
- Use service discovery and Aspire references for connection wiring. Do not hardcode endpoints or secrets.

## Prerequisites
- Start stack with Aspire task `Aspire: Start Stack`.
- Validate resource health:
  - `aspire resources --project src/apphost.cs`
  - `aspire wait worker --project src/apphost.cs --status up --timeout 180`
  - `aspire wait api --project src/apphost.cs --status healthy --timeout 180`

## Queue Retry And Dead-Letter Workflow
1. Inspect runtime status and delivery failures from worker logs:
   - `aspire logs worker --project src/apphost.cs --tail 200`
2. Inspect traces/log correlation for the failing command:
   - `aspire telemetry traces worker --project src/apphost.cs --limit 50`
   - `aspire telemetry logs worker --project src/apphost.cs --limit 100`
3. Verify persistence layer captured fail-closed state:
   - `AgentRuns.Status = NeedsReview` for failed command correlation id.
   - `AgentSignals` includes `worker_command_failed` with `RequiresHumanReview = true`.
   - `IdempotencyKeys.Status = Reserved` during retry attempts and `Rejected` only when retries are exhausted/dead-lettered.
4. If a message dead-letters repeatedly, do not bulk replay until root cause is fixed. Capture:
   - queue lane
   - command id / message id
   - correlation id
   - failure code + rationale

## Replay Drill (After Fix)
1. Confirm fix is deployed and worker healthy.
2. Re-enqueue from source of truth using original command semantics with a new command id.
3. Validate idempotency behavior:
   - duplicate command ids should complete without reprocessing.
   - fresh command id should create a new run record and complete.
4. Confirm run progression:
   - `AgentRuns`: `Running` -> `Completed`
   - `AgentRunStages`: terminal stage with outcome code/rationale populated.
   - `IdempotencyKeys`: `Reserved` -> `Completed` with `FinalizedAtUtc`.

## Trace Correlation Procedure
1. Start from command id or message id in API/worker logs.
2. Resolve correlation id (`agent:{householdId}:{conversationId}:{commandId}` for conversational agent paths).
3. Query lifecycle tables by correlation id to obtain run id and stage status timeline.
4. Use Event Hubs telemetry entries for `runtime-telemetry-stream` to verify cross-service visibility.

## Common Failure Modes
- Invalid JSON envelope: message dead-lettered with reason `invalid_json`.
- Missing required envelope fields: dead-lettered with reason `invalid_envelope`.
- Unsupported command type: run moved to `NeedsReview`, signal raised, retry then dead-letter.
- Telemetry publish failure: warning only; command completion must still proceed.

## Evidence Checklist For Release Gates
- Queue retry scenario with recovery proof.
- Dead-letter scenario with fail-closed status and signal proof.
- Replay scenario proving idempotent duplicate handling.
- Correlated trace evidence across API enqueue, worker handling, and lifecycle persistence.
