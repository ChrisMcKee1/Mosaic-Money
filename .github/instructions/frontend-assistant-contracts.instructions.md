---
name: Frontend Assistant Contracts
description: Enforce Mosaic Money web/mobile assistant contract alignment for API routes, runtime provenance fields, and local standalone or E2E configuration behavior.
applyTo: 'src/MosaicMoney.{Web,Mobile}/**/*.{ts,tsx,js,jsx,mjs}'
---

# Frontend Assistant Contract Rules

## Canonical backend contract
- Treat `src/MosaicMoney.Api/Apis/AgentOrchestrationEndpoints.cs` as the source of truth for conversational endpoints.
- Use `/api/v1/agent/conversations/*` for message, approval, and stream calls.
- Do not introduce or reintroduce `/api/v1/assistant/conversations/*` in frontend code.

## Web route and stream behavior
- Keep server-side assistant routes (`src/MosaicMoney.Web/app/api/assistant/chat/route.js` and `src/MosaicMoney.Web/app/api/agent/chat/route.js`) aligned with the same backend contract and payload shape.
- Use `fetchApi(...)` from `src/MosaicMoney.Web/lib/api.js`; do not hardcode API host URLs in route handlers.
- Preserve run-provenance envelope fields used by assistant UX (`commandId`, `correlationId`, `conversationId`, `policyDisposition`, and run status payloads).

## Mobile assistant contract parity
- Keep `src/MosaicMoney.Mobile/src/features/agent/services/mobileAgentApi.ts` in parity with web API route conventions.
- Keep mobile contract types (`AgentCommandAcceptedDto`, `AgentConversationStreamDto`, and related request DTOs) synchronized with backend response shapes.

## Standalone and E2E configuration
- For standalone web runs outside Aspire service discovery, use `API_URL` fallback behavior defined in `src/MosaicMoney.Web/lib/api.js`.
- Keep E2E ports and mock API assumptions aligned with `src/MosaicMoney.Web/playwright.config.mjs`.
- Avoid adding ad hoc env variable names for assistant API base URL unless they are documented and wired through shared API client logic.

## Required references
- `docs/architecture/unified-api-mcp-entrypoints.md`
- `docs/agent-context/runtime-agentic-worker-runbook.md`
- `docs/agent-context/aspire-javascript-frontend-policy.md`

## Verification checklist
- Web: `npm --prefix src/MosaicMoney.Web run build`
- Mobile: `npm --prefix src/MosaicMoney.Mobile run typecheck`
- Route drift check: search for `/api/v1/assistant/conversations` and ensure zero frontend matches.