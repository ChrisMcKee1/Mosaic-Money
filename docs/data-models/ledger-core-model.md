# Ledger Core Model

## Core Entities
- `EnrichedTransaction`: canonical single-entry transaction row.
- `TransactionSplit`: optional child allocations for a parent transaction.
- `RecurringItem`: expected recurring charge template and cadence metadata.
- `Category` / `Subcategory`: classification hierarchy.
- `TransactionClassificationOutcome`: final decision + confidence + assignment provenance for classification.
- `ClassificationStageOutput` and `ClassificationInsight`: per-stage trace and explainability records.
- `ReimbursementProposal`: reviewable reimbursement linkage workflow.
- `RawTransactionIngestionRecord`: immutable source payload lineage and processing cursor.
- `AgentRun` / `AgentRunStage` / `AgentSignal` / `AgentDecisionAudit` / `IdempotencyKey`: runtime conversational and workflow provenance.

## Constraints
- No double-entry debit/credit model is introduced.
- `UserNote` and `AgentNote` remain separate persisted fields.
- Amortization and projection computations do not mutate raw transaction truth.
- Ambiguous or high-impact classification remains fail-closed via `ReviewStatus = NeedsReview` and explicit audit records.

```mermaid
erDiagram
    CATEGORY ||--o{ SUBCATEGORY : contains
    SUBCATEGORY ||--o{ ENRICHED_TRANSACTION : assigned_to
    ENRICHED_TRANSACTION ||--o{ TRANSACTION_CLASSIFICATION_OUTCOME : evaluated_by
    TRANSACTION_CLASSIFICATION_OUTCOME ||--o{ CLASSIFICATION_STAGE_OUTPUT : emits
    TRANSACTION_CLASSIFICATION_OUTCOME ||--o{ CLASSIFICATION_INSIGHT : explains
    ENRICHED_TRANSACTION ||--o{ TRANSACTION_SPLIT : has
    RECURRING_ITEM ||--o{ ENRICHED_TRANSACTION : links
    ENRICHED_TRANSACTION ||--o{ REIMBURSEMENT_PROPOSAL : proposes
    ENRICHED_TRANSACTION ||--o{ RAW_TRANSACTION_INGESTION_RECORD : sourced_from
    AGENT_RUN ||--o{ AGENT_RUN_STAGE : contains
    AGENT_RUN ||--o{ AGENT_SIGNAL : raises
    AGENT_RUN ||--o{ AGENT_DECISION_AUDIT : records
    AGENT_RUN ||--o{ IDEMPOTENCY_KEY : finalizes

    ENRICHED_TRANSACTION {
        uuid Id PK
        uuid AccountId FK
        decimal Amount
        date TransactionDate
        uuid SubcategoryId FK
        int ReviewStatus
        string UserNote
        string AgentNote
        bool ExcludeFromBudget
        bool IsExtraPrincipal
        vector DescriptionEmbedding
    }

    TRANSACTION_SPLIT {
        uuid Id PK
        uuid ParentTransactionId FK
        decimal Amount
        uuid SubcategoryId FK
        int AmortizationMonths
        string UserNote
    }

    TRANSACTION_CLASSIFICATION_OUTCOME {
        uuid Id PK
        uuid TransactionId FK
        uuid ProposedSubcategoryId FK
        decimal FinalConfidence
        int Decision
        int ReviewStatus
        bool IsAiAssigned
        string AssignmentSource
        string AssignedByAgent
    }

    AGENT_RUN {
        uuid Id PK
        uuid HouseholdId FK
        string CorrelationId
        string WorkflowName
        string TriggerSource
        int Status
    }
```
