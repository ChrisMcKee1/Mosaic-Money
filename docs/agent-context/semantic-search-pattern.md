# Semantic Search Pattern (Mosaic Money)


## Agent Loading
- Load when: implementing transaction search, embedding lifecycle, or hybrid semantic retrieval behavior.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

This document defines the canonical semantic-search implementation pattern for Mosaic Money transaction search APIs.

Use this pattern for any search or typeahead surface in web/mobile that queries backend transaction data.

## Goals
- Keep semantic behavior dynamic and data-driven.
- Avoid hardcoded merchant/brand synonym lists in application code.
- Ensure searchable meaning is persisted as embeddings in PostgreSQL (`pgvector`).
- Support both natural-language intent and direct amount lookup.

## Data Model (Current)
Primary entity: `EnrichedTransaction`.

Relevant fields:
- `Description` (merchant-like source text)
- `Amount` (single-entry signed money value)
- `UserNote`
- `AgentNote`
- `DescriptionEmbedding` (`vector(1536)`)
- `DescriptionEmbeddingHash` (hash of the semantic search document)

Current EF and index configuration:
- `DescriptionEmbedding` mapped as `vector(1536)` for Npgsql provider.
- HNSW index with `vector_cosine_ops` and `DescriptionEmbedding IS NOT NULL` filter.

## Semantic Document Construction
The embedding input must be a composed search document, not a single field.

Current composed document (see `TransactionSemanticSearchDocument`):
- `Description: <description>`
- `Amount: <signed amount with 2 decimals>`
- `AmountAbsolute: <absolute amount with 2 decimals>`
- `UserNote: <user note>` (when present)
- `AgentNote: <agent note>` (when present)

Why:
- Preserves natural language context from notes.
- Allows amount-oriented search intent to participate in semantic ranking.
- Keeps behavior dynamic without maintaining code-time merchant synonym dictionaries.

## Embedding Lifecycle
### Enqueue points
Transactions are queued for embedding refresh when data is created or changed in key paths:
- Plaid ingestion endpoints
- Plaid sync processor
- Transaction create endpoint (`POST /transactions`)
- Review action endpoint (`POST /review-actions`)

### Queue behavior
- Queue item hash is derived from the composed semantic document.
- Processor recomputes current hash and skips stale queue payloads safely.
- Embedding write is idempotent when stored hash already matches.
- Failures retry with bounded attempts and dead-letter behavior.

## Query Pattern (Hybrid)
Endpoint: `GET /api/v1/search/transactions`

1. Normalize incoming query.
2. Generate query embedding.
3. Retrieve semantic candidates with cosine-distance ordering (`DescriptionEmbedding`).
4. Retrieve lexical candidates from text fields (`Description`, `UserNote`, `AgentNote`).
5. Parse money tokens from query (for example `17.85`, `$17.85`) and include amount matches.
6. Merge and rank candidates with deterministic hybrid scoring.

### Ranking
- Semantic score contribution: 85%
- Lexical score contribution: 15%
- Deterministic tiebreakers: semantic rank, lexical rank, transaction date, transaction id.

## Amount Search Semantics
Supported amount query tokens:
- `17.85`
- `$17.85`
- `-17.85`

Behavior:
- Amount tokens are parsed into decimal values (rounded to 2 decimals).
- Positive amount token also matches signed negative counterpart to support expense-style input.
- Amount matching participates in lexical candidate retrieval.

## Explicit Anti-Pattern
Do not implement hardcoded merchant synonym expansion in API code.

Examples of disallowed patterns:
- Merchant-specific keyword maps in endpoint source files.
- Static expansion dictionaries requiring redeploys for vocabulary updates.

If domain synonyms are needed later, they must be data-driven (persisted and managed via data/config), not code literals.

## Aspire and Integration Notes
- Keep PostgreSQL wiring reference-driven through AppHost `WithReference(...)`.
- Service registration remains `AddNpgsqlDbContext(...)` via Aspire integration package conventions.
- Do not hardcode connection strings for semantic-search components.

## Implementation Checklist for New Search Endpoints
1. Define a composed semantic document from meaningful persisted fields.
2. Persist vector embedding + hash in database.
3. Add/verify vector index with correct operator class.
4. Add queue or equivalent refresh path for every mutation that changes composed fields.
5. Implement hybrid retrieval (semantic + lexical + numeric filters where relevant).
6. Keep scoring deterministic and testable.
7. Add focused tests for:
   - lexical fallback
   - amount query behavior
   - stale queue safety
   - no hardcoded synonym dependencies

## Verification Commands
- API search tests: `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~SearchEndpointsTests"`
- Embedding queue tests: `dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj --filter "FullyQualifiedName~TransactionEmbeddingQueuePipelineTests"`

## External References
- Microsoft Learn: Azure Database for PostgreSQL `pgvector`
  - https://learn.microsoft.com/azure/postgresql/extensions/how-to-use-pgvector
- Microsoft Learn: semantic search tutorial with PostgreSQL + Azure OpenAI
  - https://learn.microsoft.com/azure/postgresql/azure-ai/generative-ai-semantic-search
- Microsoft Learn: pgvector performance and index tradeoffs
  - https://learn.microsoft.com/azure/postgresql/extensions/how-to-optimize-performance-pgvector
- Aspire docs: PostgreSQL EF Core integrations
  - https://aspire.dev/integrations/databases/efcore/postgres/postgresql-get-started/

