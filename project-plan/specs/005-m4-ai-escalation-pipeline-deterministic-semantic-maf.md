# Spec 005: M4 AI Escalation Pipeline (Deterministic -> Semantic -> MAF)

## Status
- Drafted: 2026-02-20
- Milestone: M4
- Depends on:
- `project-plan/specs/001-mvp-foundation-task-breakdown.md`
- `project-plan/specs/002-m1-platform-and-contract-foundation.md`
- `project-plan/specs/003-m2-ledger-truth-and-review-core.md`
- `project-plan/specs/004-m3-ingestion-recurring-reimbursements-projections.md`

## Subagent Inputs
- `mosaic-money-ai`: Stage orchestration, confidence routing, MAF fallback boundaries, and `AgentNote` summarization policy.
- `mosaic-money-backend`: Embeddings queue, semantic retrieval contracts, DB/index/query performance, and review-safe persistence behavior.
- `mosaic-money-devops`: Aspire-native service wiring, startup/readiness, and observability checks for staged AI execution.
- `mosaic-money-frontend` and `mosaic-money-mobile`: Consumption constraints only (render final rationale/review status, no client-side AI execution).

## Objective
Deliver a bounded, auditable AI escalation pipeline that executes in strict order: deterministic rules first, PostgreSQL semantic retrieval second, and Microsoft Agent Framework (MAF) graph fallback last. Ensure ambiguous outcomes fail closed to `NeedsReview`, preserve single-entry ledger truth, and enforce a hard stop on autonomous external messaging.

## Gap Analysis vs Specs 002-004
- Specs 002-003 intentionally deferred all staged AI escalation details and did not define deterministic-to-semantic-to-MAF handoff thresholds.
- Spec 004 defined anti-leakage constraints for M3 but did not define M4 execution contracts, stage budgets, or fallback trigger policy.
- Existing specs do not yet define an implementation-ready confidence fusion model that prioritizes deterministic outcomes and routes conflicts to `NeedsReview`.
- Existing specs do not yet define embeddings queue SLO/throughput, retry/dead-letter, and non-blocking write guarantees under load.
- Existing specs do not yet define semantic retrieval provenance requirements (feature vectors, score normalization, candidate reason codes).
- Existing specs do not yet define MAF call budgets (timeout/token/attempt limits), output schema validation, and safe failure behavior.
- Existing specs do not yet define enforceable hard-stop controls for external messaging attempts across deterministic, semantic, and MAF stages.
- Existing specs do not yet define evaluation datasets and release checks for routing correctness, ambiguity handling, and concise `AgentNote` quality.

## In Scope
- Deterministic classification rules execution (`MM-AI-03`) and ambiguity gate (`MM-AI-04`).
- Asynchronous embeddings generation and queue processing (`MM-BE-10`) with non-blocking ingest behavior.
- PostgreSQL semantic retrieval and scoring (`MM-AI-05`) using `azure_ai` and `pgvector`.
- Stage confidence fusion policy (`MM-AI-06`) with explicit deterministic precedence.
- MAF fallback workflow (`MM-AI-07`) only when stage 1 and stage 2 are insufficient.
- Hard-stop external messaging controls (`MM-AI-08`) with auditable deny events.
- `AgentNote` summarization enforcement (`MM-AI-09`) and transcript suppression.
- End-to-end staged orchestration (`MM-AI-10`) producing final classification or `NeedsReview` with rationale.

## Out of Scope
- New client-side AI inference logic in web/mobile apps.
- Autonomous transaction approval, reimbursement finalization, or outbound message sending.
- Changes to M3 recurring/reimbursement deterministic policy logic except where consumed as retrieval context.
- Any mutation of ledger truth amounts/dates as part of AI decisioning.

## Guardrails
- Single-entry ledger semantics remain authoritative and immutable.
- `UserNote` and `AgentNote` remain separate persisted fields and separate UI render lanes.
- Stage execution order is mandatory: deterministic -> semantic -> MAF; no bypass unless explicitly denied to `NeedsReview`.
- Ambiguous, conflicting, or low-confidence outcomes must route to `NeedsReview`.
- External messaging execution is denied in all stages; draft-only output is allowed.
- In-database retrieval and deterministic methods are required before any LLM/MAF fallback.
- Model invocation budgets must be explicit (timeouts, retries, token limits) and auditable.

## Task Breakdown
| ID | Domain | Task | Dependencies | Deliverable | Status |
|---|---|---|---|---|---|
| MM-AI-03 | AI | Deterministic classification rules engine | MM-AI-01, MM-AI-02, MM-BE-06 | Stage-1 rules execute first and produce class, confidence, and reason code set. | Done |
| MM-AI-04 | AI | Ambiguity policy gate to `NeedsReview` | MM-AI-03, MM-BE-05 | Explicit fail-closed gate routes low-confidence/conflict outcomes to review with rationale. | Done |
| MM-BE-10 | Backend | Async embeddings queue pipeline | MM-BE-03, MM-BE-06 | Queue-backed embedding generation is non-blocking for writes; retries and dead-letter behavior are defined. | Done |
| MM-AI-05 | AI | PostgreSQL semantic retrieval layer | MM-BE-10, MM-AI-02 | In-database retrieval returns bounded candidates with normalized scores and provenance fields. | Done |
| MM-AI-06 | AI | Confidence fusion policy | MM-AI-03, MM-AI-04, MM-AI-05 | Deterministic precedence, semantic fallback thresholds, and conflict-to-review behavior are encoded. | Done |
| MM-AI-07 | AI | MAF fallback graph execution | MM-AI-06 | MAF graph runs only after stage insufficiency and returns schema-validated proposals with bounded cost/latency. | Done |
| MM-AI-08 | AI | External messaging hard-stop guardrail | MM-AI-07 | Send operations are denied and logged; draft content only may be produced for user review. | Done |
| MM-AI-09 | AI | `AgentNote` summarization enforcement | MM-AI-01, MM-AI-07 | Concise summary notes are persisted; raw transcripts/tool dumps are not stored as `AgentNote`. | Done |
| MM-AI-10 | AI | End-to-end orchestration flow | MM-AI-04, MM-AI-06, MM-AI-07, MM-AI-08, MM-AI-09 | Pipeline emits final classification or `NeedsReview` with traceable stage-by-stage rationale. | Done |
| MM-AI-12 | AI | Official evaluator stack adoption + research replay pack | MM-AI-11 | Add .NET evaluator libraries and Foundry evaluator/graders workflow with rerunnable documentation-backed instructions, dataset mappings, and CI evidence artifacts. | In Progress |

Update note (2026-02-26): Planner resumed `MM-AI-12` in `In Progress` for final evidence consolidation in the active completion wave.

Implementation note (2026-02-23): `MM-AI-05` now includes an advisory PostgreSQL semantic retrieval contract that reads existing transaction embeddings (`pgvector` cosine distance), returns bounded candidates with normalized `[0..1]` scores, and emits explicit provenance (`ProvenanceSource`, `ProvenanceReference`, `ProvenancePayloadJson`). Deterministic stage remains first and authoritative for decisioning; semantic stage evidence is appended only after deterministic `NeedsReview` routing and does not auto-categorize independently.

Implementation note (2026-02-23): `MM-AI-07` now adds an explicit eligibility gate and bounded fallback execution contract for MAF stage calls. Stage-3 runs only after deterministic and semantic insufficiency, validates proposal schema before persistence, and fails closed to `NeedsReview` on timeout/execution/schema errors.

Implementation note (2026-02-24): `MM-AI-10` orchestration now persists stage-level rationale that includes escalation policy context (deterministic gate + semantic fusion decisions) and promotes stage-3 MAF rationale into the final persisted outcome when fallback executes. Guardrail denials and fallback failures remain fail-closed to `NeedsReview` with auditable reason codes.

Implementation note (2026-02-24): `MM-AI-08` and `MM-AI-09` are promoted to `Done` after planner review with focused verification (`MafFallbackGraphServiceTests`, `AgentNoteSummaryPolicyTests`, and `AgenticEvalReleaseGateTests`) passing on .NET 10.

Implementation note (2026-02-25): `MM-AI-12` implementation is in review with official evaluator stack integration points added to the existing `MM-AI-11` release-gate workflow.
- `.NET` evaluator package anchors are now wired in test infrastructure (`Microsoft.Extensions.AI.Evaluation`, `Quality`, `Safety`, `Reporting`) with fail-closed behavior when Foundry cloud prerequisites are unavailable.
- Release-gate evidence now emits an official evaluator replay artifact at `artifacts/release-gates/mm-ai-12/latest.json` through `.github/scripts/run-mm-ai-11-release-gate.ps1` (`-OfficialEvaluatorOutputPath` override supported).
- Mapping coverage includes all `MM-AI-11` criteria and reusable dataset fields (`query`, `response`, `tool_definitions`, `actions`, `expected_actions`, `ground_truth`) for rerunnable replay scenarios.
- Task status is now `In Review` after focused offline validation (`AgenticEvalOfficialEvaluatorStackTests`, `AgenticEvalOfficialEvaluatorArtifactsTests`, `AgenticEvalReleaseGateTests`) and release-gate script replay; cloud-run evidence is still required before `Done`.

Implementation note (2026-02-27): Planner-led kickoff audit plus AI specialist review found that official evaluator replay-pack artifact generation is still incomplete (`artifacts/release-gates/mm-ai-12/criteria-dataset-mapping.json` and `artifacts/release-gates/mm-ai-12/replay-pack.md` missing in latest run evidence), and cloud evaluator execution evidence is not yet captured. `MM-AI-12` remains `In Progress` pending those artifacts and validation reruns.

## MM-AI-12 Documentation Replay Pack
Use this runbook verbatim when restarting evaluator modernization work after a pause. Do not mark `MM-AI-12` done without producing all artifacts listed below.

Primary documentation links (must be re-read each iteration):
- `https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries`
- `https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/evaluation-evaluators/azure-openai-graders?view=foundry`
- `https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/evaluation-evaluators/agent-evaluators?view=foundry`
- `https://learn.microsoft.com/en-us/azure/ai-foundry/concepts/built-in-evaluators?view=foundry`

Supporting implementation docs (recommended in the same pass):
- `https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/evaluate-sdk?view=foundry`
- `https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/cloud-evaluation?view=foundry`
- `https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/agent-evaluate-sdk?view=foundry`

Detailed execution checklist:
1. Re-read all primary links and log date/time, reviewer, and key doc changes since the last run.
2. Build an evaluator mapping matrix for Mosaic Money criteria (`routing_correctness`, `ambiguity_fail_closed_to_needs_review`, `external_messaging_hard_stop`, `agentnote_explainability_summary_policy`) to:
	- `.NET` evaluator library components (`Microsoft.Extensions.AI.Evaluation`, `Quality`, `Safety`, `Reporting`).
	- Foundry agent evaluators (`task_adherence`, `task_completion`, `intent_resolution`, tool-call evaluators as applicable).
	- Azure OpenAI graders (`score_model`, `label_model`, `string_check`, `text_similarity`) for deterministic pass/fail checks.
3. Define dataset schema and mappings (`query`, `response`, `tool_definitions`, `actions`, `expected_actions`, `ground_truth`) and store reusable fixtures under test assets.
4. Implement evaluation runners and config with explicit thresholds, pass/fail interpretation, and fail-closed behavior for unavailable preview evaluators.
5. Produce evidence artifacts per run:
	- Test output and pass/fail summary.
	- Serialized evaluator results (local and/or cloud run).
	- Threshold comparison report.
	- Decision log on whether release gate status changes are warranted.
6. Update this spec and `project-plan/specs/001-mvp-foundation-task-breakdown.md` with outcomes and any threshold or evaluator-selection changes.

Completion criteria for `MM-AI-12`:
- Official evaluator stack is integrated and executable in CI.
- Research replay instructions remain current and source-linked.
- Evidence artifacts are reproducible and attached to the corresponding issue/PR.

## Dependency Sequence (Implementation Order)
1. `MM-BE-10`: establish non-blocking embedding generation before semantic stage rollout.
2. `MM-AI-03` and `MM-AI-04`: lock stage-1 deterministic behavior and fail-closed gate.
3. `MM-AI-05`: enable semantic retrieval with bounded candidate set and provenance.
4. `MM-AI-06`: codify confidence fusion and escalation thresholds.
5. `MM-AI-07`: enable bounded MAF fallback workflow.
6. `MM-AI-08` and `MM-AI-09`: apply safety and persistence controls.
7. `MM-AI-10`: wire full orchestration and audit trace.

## Acceptance Criteria
- Deterministic stage runs first for all eligible classification requests and emits reasoned outcomes.
- Low-confidence or conflicting deterministic and semantic outcomes consistently route to `NeedsReview`.
- Embeddings generation never blocks ingestion writes and supports retry/dead-letter recovery paths.
- Semantic retrieval returns candidate evidence with reproducible scores and source provenance.
- MAF is invoked only after deterministic and semantic insufficiency based on explicit thresholds.
- External messaging send attempts are denied in all code paths and recorded for audit.
- `AgentNote` entries are concise summaries and exclude raw LLM transcript content.
- End-to-end orchestration yields consistent final states with stage-level rationale for audit/review.

## Evaluation Checks (Agentic-Eval Gate Style)
- Criteria: Routing correctness across staged escalation.
- Result target: `>= 95%` expected route selection on a labeled regression set (deterministic, semantic, fallback, `NeedsReview`).
- Follow-up on fail: adjust thresholds/reason mapping, rerun labeled set, and re-baseline.

- Criteria: Ambiguity handling and fail-closed safety.
- Result target: `100%` of conflict/low-confidence scenarios land in `NeedsReview` with non-empty rationale codes.
- Follow-up on fail: treat as release blocker; patch gate logic and add missing negative tests.

- Criteria: Non-blocking ingestion under embedding load.
- Result target: ingestion write endpoints remain within agreed SLO and do not synchronously await embedding generation.
- Follow-up on fail: queue capacity tuning, batch controls, and worker backpressure fixes.

- Criteria: MAF bounded execution.
- Result target: all MAF calls enforce configured timeout/retry/token ceilings and return schema-valid outputs or safe fallback.
- Follow-up on fail: enforce stricter middleware guards and schema validation pre-persist.

- Criteria: Explainability quality in `AgentNote`.
- Result target: `>= 95%` of sampled notes are concise, action-oriented, and reference stage rationale without transcript leakage.
- Follow-up on fail: tighten summary template and add validation checks before persistence.

- Criteria: External messaging hard-stop.
- Result target: `100%` of outbound send execution attempts are denied; draft-only generation remains available.
- Follow-up on fail: block release, patch policy middleware, and add explicit deny-path integration tests.

## Verification
- Unit tests for deterministic rule outcomes, confidence thresholds, and ambiguity routing.
- Integration tests for queue-backed embeddings, semantic retrieval, and fusion-policy branching.
- Contract tests for stage output schema and persisted rationale fields.
- Negative tests proving outbound send operations are denied across all stages.
- End-to-end scenario tests validating deterministic-only, semantic-resolved, MAF-resolved, and `NeedsReview` outcomes.
- Observability checks for stage latency, fallback rate, denial events, and note-quality sampling metrics.

## Risks and Mitigations
- Risk: Over-escalation to MAF increases cost and latency.
- Mitigation: Deterministic precedence, semantic thresholds, and strict fallback budgets.

- Risk: Under-escalation causes incorrect auto-classification.
- Mitigation: Conservative confidence gating and mandatory `NeedsReview` on conflicts.

- Risk: Embedding backlog degrades semantic freshness.
- Mitigation: Queue monitoring, backpressure controls, retry limits, and dead-letter remediation workflow.

- Risk: Transcript leakage into persisted notes.
- Mitigation: `AgentNote` schema constraints, summary templates, and persistence-layer sanitization checks.

- Risk: Autonomous messaging regression via new tool or endpoint.
- Mitigation: Central deny-by-default outbound messaging policy with integration tests and audit alerts.

## Hard-Stop Policies
- No autonomous external messaging execution is permitted.
- No autonomous financial approval or finalization is permitted for ambiguous/high-impact decisions.
- Any unresolved conflict or low-confidence result must route to `NeedsReview`.
- Any policy violation in evaluation checks is a release blocker for M4.

## Governance Snapshot
- Risk: High (financial categorization and escalation safety).
- Decision: Allow with strict stage gates, bounded model usage, and mandatory human review for ambiguity/high-impact outcomes.
- Rationale: M4 introduces controlled AI escalation but preserves deterministic-first logic and fail-closed review routing.
- Escalation: Any attempt to bypass `NeedsReview`, exceed model guardrails, or execute outbound send actions requires immediate human review and rollback consideration.

## Exit Criteria
M4 exits when staged AI escalation is auditable, bounded, and review-safe; external messaging hard-stops are enforced; and agentic-eval checks pass for routing correctness, ambiguity handling, and explainability quality.
