# Auth Scope and Access Control Flow

Last updated: 2026-03-01

## Purpose
Capture how authenticated identity is translated into household-member scope and then enforced through account-level access queries for both REST and MCP surfaces.

## Key Runtime Components
- `HouseholdMemberContextResolver`
- `AuthenticatedHouseholdScopeResolver`
- `McpAuthenticatedContextAccessor`
- `ITransactionAccessQueryService`
- `AccountMemberAccessEntries` + active `HouseholdUsers` membership checks

## Diagram A: REST Endpoint Scope Resolution
```mermaid
sequenceDiagram
    participant Client as Web/Mobile Client
    participant API as Minimal API Endpoint
    participant Resolver as HouseholdMemberContextResolver
    participant Scoped as AuthenticatedHouseholdScopeResolver
    participant TxAccess as ITransactionAccessQueryService
    participant DB as PostgreSQL

    Client->>API: Authenticated request
    API->>Resolver: Resolve member context (claim/header/subject map)
    Resolver->>DB: Validate active HouseholdUser membership
    DB-->>Resolver: HouseholdUserId
    Resolver->>Scoped: Resolve authenticated household scope
    Scoped->>DB: Resolve HouseholdId + active membership
    DB-->>Scoped: HouseholdUserId + HouseholdId
    API->>TxAccess: CanReadAccount/GetReadableTransaction/ListReadableTransactions
    TxAccess->>DB: Evaluate AccountMemberAccessEntries visibility/role
    DB-->>TxAccess: Scoped account/transaction set
    TxAccess-->>API: Authorized data result
    API-->>Client: Scoped response
```

## Diagram B: MCP Tool Scope Resolution
```mermaid
sequenceDiagram
    participant Agent as MCP Client/Agent
    participant MCP as MCP Tool Endpoint (/api/mcp)
    participant McpScope as McpAuthenticatedContextAccessor
    participant Resolver as HouseholdMemberContextResolver
    participant TxAccess as ITransactionAccessQueryService
    participant DB as PostgreSQL

    Agent->>MCP: JSON-RPC tool call (bearer-authenticated)
    MCP->>McpScope: GetRequiredHouseholdUserIdAsync()
    McpScope->>Resolver: Resolve member context from HttpContext
    Resolver->>DB: Validate subject mapping + active membership
    DB-->>Resolver: HouseholdUserId
    Resolver-->>McpScope: Scoped member context
    MCP->>TxAccess: Query readable account/transaction scope
    TxAccess->>DB: Evaluate ACL and membership predicates
    DB-->>TxAccess: Allowed records
    TxAccess-->>MCP: Authorized result
    MCP-->>Agent: Scoped tool output
```

## Guardrails
- Caller-supplied identity IDs are not trusted for authorization decisions.
- Ambiguous membership mapping fails closed.
- Reads and mutations must pass household-member and account ACL checks before business logic execution.
