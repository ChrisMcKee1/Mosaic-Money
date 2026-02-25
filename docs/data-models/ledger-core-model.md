# Ledger Core Model

## Core Entities
- `EnrichedTransaction`: canonical single-entry transaction row.
- `TransactionSplit`: optional child allocations for a parent transaction.
- `RecurringItem`: expected recurring charge template and cadence metadata.
- `SinkingFund`: planned reserve tracking (projection/plan layer).
- `Category` / `Subcategory`: classification hierarchy.

## Constraints
- No double-entry debit/credit model is introduced.
- `UserNote` and `AgentNote` remain separate persisted fields.
- Amortization and projection computations do not mutate raw transaction truth.

```mermaid
erDiagram
    CATEGORY ||--o{ SUBCATEGORY : contains
    SUBCATEGORY ||--o{ ENRICHED_TRANSACTION : classifies
    ENRICHED_TRANSACTION ||--o{ TRANSACTION_SPLIT : has
    RECURRING_ITEM ||--o{ ENRICHED_TRANSACTION : links

    ENRICHED_TRANSACTION {
        uuid Id PK
        uuid AccountId FK
        decimal Amount
        date TransactionDate
        uuid SubcategoryId FK
        string UserNote
        string AgentNote
        bool ExcludeFromBudget
        bool IsExtraPrincipal
        vector SemanticEmbedding
    }

    TRANSACTION_SPLIT {
        uuid Id PK
        uuid ParentTransactionId FK
        decimal Amount
        uuid SubcategoryId FK
        int AmortizationMonths
        string UserNote
    }
```
