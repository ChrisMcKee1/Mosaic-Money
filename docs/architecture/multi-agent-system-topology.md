# Multi-Agent System Topology

## Purpose
Define the runtime component topology for Mosaic Money multi-agent orchestration, including worker ownership, eventing boundaries, and conversational assistant integration.

## Scope
- Product runtime architecture only.
- No coding-agent mode configuration.

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
        Orchestrator[ConversationOrchestratorAgent]
        Cat[CategorizationSpecialistAgent]
        Transfer[TransferDetectionSpecialistAgent]
        Income[IncomeNormalizationSpecialistAgent]
        Debt[DebtQualitySpecialistAgent]
        Invest[InvestmentClassificationSpecialistAgent]
        Anomaly[AnomalyDetectionSpecialistAgent]
        Guard[PolicyAndExplainabilityGuard]
    end

    subgraph Messaging[Event and Command Fabric]
        SB[Azure Service Bus\nDurable Command Lanes]
        EG[Azure Event Grid\nFan-out Notifications]
        EH[Azure Event Hubs\nTelemetry and Replay Streams]
    end

    subgraph Data[Persistence]
        Pg[(PostgreSQL\nLedger + Agent Runs)]
    end

    Web --> Api
    Mobile --> Api
    Api --> SB
    SB --> Worker
    Worker --> Orchestrator
    Orchestrator --> Cat
    Orchestrator --> Transfer
    Orchestrator --> Income
    Orchestrator --> Debt
    Orchestrator --> Invest
    Orchestrator --> Anomaly
    Cat --> Guard
    Transfer --> Guard
    Income --> Guard
    Debt --> Guard
    Invest --> Guard
    Anomaly --> Guard
    Guard --> Pg
    Worker --> Pg
    Api --> Pg
    Worker --> EG
    Worker --> EH
    Api --> EH
```

## Runtime Ownership Rules
- API owns request validation, auth, and explicit review-action contracts.
- Worker owns asynchronous orchestration and command retries.
- Agent decisions must persist run/stage provenance before user-visible completion.
- Guardrail checks execute before any state transition away from `NeedsReview`.

## Required Persistence for This Topology
- `AgentRuns`
- `AgentRunStages`
- `AgentSignals`
- `AgentDecisionAudit`
- `IdempotencyKeys`
