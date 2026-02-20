# Architecture Agentic Context

This file is the planner-friendly architecture grounding summary.
Canonical source: [Full Architecture](../../project-plan/architecture.md)

## Core Stack
- Backend: C# 14 with .NET 10 Minimal APIs.
- Orchestration: Aspire 13.2 AppHost with API, worker, and frontend composition.
- Web: Next.js 16 with React 19 and Tailwind CSS.
- Mobile: React Native via Expo SDK 55 with shared logic.
- Data: PostgreSQL 18 with `azure_ai` extension and `pgvector`.
- AI: Microsoft Agent Framework 1.0 RC plus `Microsoft.Extensions.AI`.

## Architectural Rules
- Keep Copilot as UI and coding assistant, not runtime orchestration.
- Keep deterministic and in-database AI paths primary for cost and latency control.
- Route low-confidence cases into `NeedsReview` instead of forced auto-resolution.
- Preserve source-of-truth ledger integrity. Projection logic stays in presentation layers.

## Team Routing Map
- `mosaic-money-backend`: API, entities, migrations, ingestion.
- `mosaic-money-frontend`: web dashboards, projection UX, data visuals.
- `mosaic-money-mobile`: Expo flows and shared package integration.
- `mosaic-money-ai`: retrieval, classification confidence routing, HITL guardrails.
- `mosaic-money-devops`: Aspire AppHost, `AddJavaScriptApp`, containers, and MCP diagnostics.
