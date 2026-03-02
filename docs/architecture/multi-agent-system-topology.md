# Runtime Agent Topology (As-Built)

## Purpose
Define the current runtime component topology for conversational orchestration, including worker ownership, eventing boundaries, and Foundry runtime integration.

## Scope
- Product runtime architecture only.
- No coding-agent mode configuration.
- Planned specialist multi-agent expansion is tracked as a future-state roadmap, not the current runtime.

## Topology Diagram
```mermaid
flowchart LR
    subgraph UserSurfaces[User Surfaces]
        Web[Next.js Web Assistant]
        Mobile[Expo Mobile Assistant]
    end

    subgraph Edge[Application Edge]
        Api[MosaicMoney.Api\nContracts + Auth + Review Endpoints]
    end

    subgraph Runtime[Agent Runtime]
        Worker[MosaicMoney.Worker\nOrchestration Command Handlers]
        FoundryRuntime[FoundryAgentRuntimeService\nBootstrap + Invoke + Fail-Closed Routing]
        FoundryAgent[Foundry Project Agent\nMosaic]
    end

    subgraph Messaging[Event and Command Fabric]
        SB[Azure Service Bus\nDurable Command Lanes]
        EG[Azure Event Grid\nFan-out Notifications (planned lane)]
        EH[Azure Event Hubs\nTelemetry and Replay Streams]
    end

    subgraph Data[Persistence]
        Pg[(PostgreSQL\nLedger + Agent Runs)]
    end

    Web --> Api
    Mobile --> Api
    Api --> SB
    SB --> Worker
    Worker --> FoundryRuntime
    FoundryRuntime --> FoundryAgent
    FoundryRuntime --> Pg
    Worker --> Pg
    Api --> Pg
    Worker -.planned publish path.-> EG
    Worker --> EH
    Api --> EH
```

## Runtime Ownership Rules
- API owns request validation, auth, and explicit review-action contracts.
- Worker owns asynchronous orchestration, queue retries, idempotency, and fail-closed terminal status routing.
- Foundry runtime calls are wrapped by policy disposition hints and fall back to `NeedsReview` when unavailable or invalid.
- Agent decisions must persist run/stage provenance before user-visible completion.

## Required Persistence for This Topology
- `AgentRuns`
- `AgentRunStages`
- `AgentSignals`
- `AgentDecisionAudit`
- `IdempotencyKeys`
