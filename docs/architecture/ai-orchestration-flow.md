# AI Orchestration Flow

## Escalation Ladder
1. Deterministic classifier/rules execute first.
2. Semantic retrieval and confidence fusion execute next.
3. MAF fallback executes only when confidence is below policy threshold.
4. Foundry-assisted classification is an explicit operator-invoked path, not an autonomous bypass.
5. Ambiguous/high-impact outcomes route to `NeedsReview` with human approval required.

```mermaid
flowchart TD
    Ingest[Transaction Ingested] --> Deterministic[Deterministic Classification]
    Deterministic -->|High confidence| Finalized[Categorized]
    Deterministic -->|Low confidence| Semantic[Semantic Retrieval + Fusion]
    Semantic -->|High confidence| Finalized
    Semantic -->|Low confidence| MAF[MAF Fallback]
    MAF -->|Confident and allowed| Suggest[Proposed Classification]
    MAF -->|Ambiguous/high impact| Review[NeedsReview Queue]
    Review -->|Operator invokes Foundry endpoint| Foundry[Foundry Classification Endpoint]
    Foundry -->|Confidence below threshold or ambiguous| Review
    Foundry -->|Confident and policy-allowed| Suggest
    Suggest --> Human[Human Approval]
    Review --> Human
    Human --> Finalized
```

## Foundry Classification Guardrails
- Foundry classification decisions are fail-closed and must emit parseable JSON; invalid output routes back to `NeedsReview`.
- Auto-assignment is gated by minimum confidence and allowed subcategory scope.
- Assignment provenance is persisted (`IsAiAssigned`, `AssignmentSource`, `AssignedByAgent`) along with `ClassificationInsights` for auditability.

## Policy Requirements
- Never bypass `NeedsReview` when policy marks an action ambiguous or high impact.
- Never auto-send external communications; agent may draft only.
- Release evidence is governed by MM-AI-11 and MM-AI-12 tasks before production promotion.
