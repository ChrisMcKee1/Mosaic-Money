# AI Feature Gap and Agent Trigger Architecture Audit (2026-02-27)

## Scope
- Daily kickoff cold-start audit executed from `mosaic-money-planner`.
- Inputs reviewed: PRD/architecture agent context, skills catalog, spec 001 + milestones 002-006, repo memory, and recent git history (`git log --oneline -20`).
- Specialist delegations completed: `mosaic-money-ai` (AI feature/evaluator audit) and `mosaic-money-devops` (agent architecture + trigger audit).

## Situational Awareness Snapshot
- Recent delivery focus includes M5/M7 progress, auth/access triage, charting migration, and evaluator replay groundwork.
- Active status snapshot from specs 001-006:
  - `In Progress`: `MM-AI-12`
  - `Blocked`: `MM-MOB-08`, `MM-MOB-13`, `MM-QA-04`, `MM-QA-01`
  - `In Review`: none after kickoff reconciliation
- Highest-priority unblocked `Not Started` tasks in 002-006: none currently identified.

## Today’s Selected Work
- Continue `MM-AI-12` to close release-evaluator modernization gaps.
- Keep blocked mobile/auth tasks unchanged until device-level OAuth evidence is available.

## AI Feature Audit (Implemented vs Gaps)

### Implemented (✅)
- Staged orchestration is active in backend: deterministic -> semantic -> MAF fallback.
- Ambiguity fail-closed gate to `NeedsReview` is implemented.
- Confidence fusion policy exists with deterministic precedence and conflict routing.
- External messaging hard-stop is enforced in fallback action parsing (`send_*` and explicit deny list).
- `AgentNote` summarization policy suppresses transcript/tool-dump leakage.
- Release-gate script emits evaluator artifacts at:
  - `artifacts/release-gates/mm-ai-11/latest.json`
  - `artifacts/release-gates/mm-ai-12/latest.json`

### Partial (🟡)
- Official evaluator integration is currently a readiness/mapping snapshot with fail-closed cloud-unavailable behavior.
- MM-AI-12 evidence is not yet a full replay pack despite baseline artifact generation.

### Missing (❌)
- Required MM-AI-12 replay outputs are not yet generated:
  - `artifacts/release-gates/mm-ai-12/criteria-dataset-mapping.json`
  - `artifacts/release-gates/mm-ai-12/replay-pack.md`
- Cloud evaluator execution evidence path is still absent in latest artifact evidence.

### Evidence Summary
- Targeted AI tests passed for orchestrator/policy/retrieval/queue paths.
- Official evaluator artifact test suite still reports missing replay-pack artifacts.

## Agent Architecture Audit (Have vs Should Have)

### Agents currently available
- `mosaic-money-planner`
- `mosaic-money-backend`
- `mosaic-money-frontend`
- `mosaic-money-mobile`
- `mosaic-money-ai`
- `mosaic-money-devops`
- `Microsoft Agent Framework .NET`
- `code-simplifier`

### Current architecture gaps
- Trigger boundaries between `mosaic-money-ai` and `Microsoft Agent Framework .NET` are under-specified.
- Backend/DevOps and Frontend/Mobile boundaries rely on human interpretation instead of explicit dispatch rules.
- Utility mode (`code-simplifier`) is available but not clearly constrained as opt-in only.

### Target architecture recommendation
- Keep planner + five domain specialists as primary operating model.
- Keep `Microsoft Agent Framework .NET`, but limit it to framework-specific implementation/migration work.
- Keep `mosaic-money-ai` focused on product AI behavior, confidence policy, and review-routing semantics.
- Retain `code-simplifier` as explicit opt-in maintenance mode (not default delegation).

## Trigger Matrix (Should Do)

| Agent | Should own | Trigger examples |
|---|---|---|
| `mosaic-money-planner` | Multi-domain orchestration, status authority, sequencing | “plan this milestone”, “daily kickoff”, “coordinate backend + frontend + devops” |
| `mosaic-money-backend` | API/domain/migrations/ingestion/EF | “add endpoint”, “update migration”, “worker ingestion bug”, changes under `src/MosaicMoney.Api/**` |
| `mosaic-money-frontend` | Next.js web UX/data-fetch/charting | “build web page”, “fix dashboard chart”, changes under `src/MosaicMoney.Web/**` |
| `mosaic-money-mobile` | Expo mobile flows/native UX | “mobile sign-in flow”, “Expo screen”, changes under `src/MosaicMoney.Mobile/**` |
| `mosaic-money-ai` | Business AI policy and routing semantics | “confidence thresholds”, “NeedsReview behavior”, “classification policy” |
| `Microsoft Agent Framework .NET` | MAF SDK/framework mechanics | “MAF graph/executor API update”, “migrate SK/AutoGen to MAF”, package/API compatibility |
| `mosaic-money-devops` | AppHost wiring, service discovery, runtime diagnostics | “apphost.cs wiring”, “WithReference issue”, “Aspire startup/telemetry failure” |
| `code-simplifier` | Post-change simplification only | “simplify recent diff”, “refactor last commit for readability” |

## Immediate Next Slice for MM-AI-12
1. Generate missing replay-pack artifacts (`criteria-dataset-mapping.json`, `replay-pack.md`) from current evaluator snapshot inputs.
2. Update release-gate script flow to validate those artifacts in the same run.
3. Add env-gated cloud evaluator execution path and evidence output.
4. Re-run focused tests and artifact checks before considering promotion back to `In Review`.

## Planner Decision
- `MM-AI-12` remains `In Progress`.
- No promotion to `In Review` or `Done` until replay-pack artifacts and cloud evidence path are validated.
