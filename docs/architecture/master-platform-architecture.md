# Master Platform Architecture

Last updated: 2026-02-27

## Purpose
This document provides the platform-level architecture diagrams that span every major Mosaic Money surface and runtime boundary:

- Web and mobile frontends
- API and worker services
- Multi-agent runtime
- Eventing services
- PostgreSQL persistence
- External integrations and identity
- Observability and operations boundaries

## Diagram 1: End-to-End Platform Topology
```mermaid
flowchart LR
    subgraph Surfaces[User Surfaces]
        Web[MosaicMoney.Web\nNext.js 16 + React 19]
        Mobile[MosaicMoney.Mobile\nExpo SDK 55]
    end

    subgraph Identity[Identity]
        Clerk[Clerk\nAuthN + Session]
    end

    subgraph Edge[Application Edge]
        Api[MosaicMoney.Api\nMinimal APIs + AuthZ + Contracts]
    end

    subgraph Runtime[Async and Agent Runtime]
        Worker[MosaicMoney.Worker\nCommand Handlers + Scheduling]

        subgraph Agents[Multi-Agent Domain]
            Orch[ConversationOrchestratorAgent]
            Cat[CategorizationSpecialistAgent]
            Transfer[TransferDetectionSpecialistAgent]
            Income[IncomeNormalizationSpecialistAgent]
            Debt[DebtQualitySpecialistAgent]
            Invest[InvestmentClassificationSpecialistAgent]
            Anomaly[AnomalyDetectionSpecialistAgent]
            Guard[PolicyAndExplainabilityGuard]
        end
    end

    subgraph Eventing[Eventing Backbone]
        SB[Azure Service Bus\nDurable Command Lanes]
        EG[Azure Event Grid\nFan-out Domain Events]
        EH[Azure Event Hubs\nTelemetry + Replay]
    end

    subgraph Data[Data Plane]
        Pg[(PostgreSQL\nLedger + App + Agent Data)]
        Vec[(pgvector + azure_ai\nSemantic Retrieval)]
    end

    subgraph External[External Providers]
        Plaid[Plaid Sandbox/Prod]
    end

    Web --> Clerk
    Mobile --> Clerk
    Web --> Api
    Mobile --> Api
    Clerk --> Api

    Api --> SB
    SB --> Worker
    Worker --> Orch

    Orch --> Cat
    Orch --> Transfer
    Orch --> Income
    Orch --> Debt
    Orch --> Invest
    Orch --> Anomaly

    Cat --> Guard
    Transfer --> Guard
    Income --> Guard
    Debt --> Guard
    Invest --> Guard
    Anomaly --> Guard

    Api --> Pg
    Worker --> Pg
    Guard --> Pg
    Pg <--> Vec

    Worker --> EG
    Worker --> EH
    Api --> EH

    Worker --> Plaid
```

## Diagram 2: Service and Resource Wiring (Aspire-Centric)
```mermaid
flowchart TB
    subgraph AppHost[Aspire AppHost]
        APIR[api resource]
        WRKR[worker resource]
        WEBR[web resource]

        DBR[mosaicmoneydb resource]
        SBR[runtime-messaging\nAddAzureServiceBus]
        EHR[runtime-telemetry\nAddAzureEventHubs]

        Q1[runtime-ingestion-completed queue]
        Q2[runtime-assistant-message-posted queue]
        Q3[runtime-nightly-anomaly-sweep queue]

        HUB[runtime-telemetry-stream hub]
        CG[mosaic-money-runtime consumer group]

        APIR -->|WithReference| DBR
        WRKR -->|WithReference| DBR
        WEBR -->|Service discovery| APIR

        APIR -->|WithReference| Q2
        APIR -->|WithReference| HUB

        WRKR -->|WithReference| Q1
        WRKR -->|WithReference| Q2
        WRKR -->|WithReference| Q3
        WRKR -->|WithReference| HUB

        SBR --> Q1
        SBR --> Q2
        SBR --> Q3
        EHR --> HUB
        HUB --> CG
    end

    subgraph ExplicitConfig[Explicit Secret Configuration]
        EGCFG[RuntimeMessaging:EventGrid\nPublishEndpoint/PublishAccessKey/TopicName]
    end

    APIR -.env injection.-> EGCFG
    WRKR -.env injection.-> EGCFG
```

## Diagram 3: Control, Data, and Governance Boundaries
```mermaid
flowchart LR
    subgraph ControlPlane[Control Plane]
        AppHostCtrl[Aspire composition\nresource graph + references]
        Policies[Runtime policy checks\nfail-closed startup validation]
    end

    subgraph ExecutionPlane[Execution Plane]
        ApiExec[API request handling]
        WorkerExec[Worker command execution]
        AgentExec[Agent orchestration and specialist execution]
    end

    subgraph DataPlane[Data Plane]
        Ledger[(Ledger truth\nsingle-entry)]
        AgentAudit[(Agent runs/stages/audit)]
        Telemetry[(Event Hubs telemetry stream)]
    end

    subgraph Oversight[Human Oversight]
        Review[NeedsReview queue]
        Approval[Human approval actions]
    end

    AppHostCtrl --> ApiExec
    AppHostCtrl --> WorkerExec
    Policies --> ApiExec
    Policies --> WorkerExec

    ApiExec --> Ledger
    WorkerExec --> Ledger
    AgentExec --> AgentAudit
    WorkerExec --> Telemetry

    AgentExec --> Review
    Review --> Approval
    Approval --> WorkerExec
```

## Diagram 4: Unified Minimal API and MCP Entrypoints
```mermaid
flowchart LR
    subgraph Clients[Clients]
        RestClient[Web/Mobile/Partner HTTP Clients]
        McpClient[MCP Clients and Agents]
    end

    subgraph ApiHost[MosaicMoney.Api ASP.NET Core Host]
        RestLayer[Minimal API Endpoints]
        McpLayer[MCP Tool Endpoints\nJSON-RPC 2.0]
    end

    subgraph SharedCore[Shared Core Services]
        UseCase[Use Case Services]
        Policy[Policy and Validation]
    end

    subgraph DataAndEvents[Data and Event Integrations]
        Db[(PostgreSQL)]
        Msg[Service Bus / Event Grid / Event Hubs]
    end

    RestClient --> RestLayer
    McpClient --> McpLayer

    RestLayer --> UseCase
    McpLayer --> UseCase
    UseCase --> Policy

    UseCase --> Db
    UseCase --> Msg
```

See `docs/architecture/unified-api-mcp-entrypoints.md` for implementation constraints and composition rules.

## Coverage Notes
- This file is the master topology view and intentionally overlaps with deeper implementation docs.
- Use `multi-agent-system-topology.md` and `multi-agent-orchestration-sequences.md` for execution-level detail.
- Use `unified-api-mcp-entrypoints.md` for the REST+MCP shared-core architecture standard.
- Keep this document synchronized when adding or removing a service, queue, stream, agent, or major integration boundary.
