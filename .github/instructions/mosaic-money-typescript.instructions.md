---
name: Mosaic Money TypeScript
description: TypeScript coding guidance for shared web/mobile logic and frontend modules in Mosaic Money.
applyTo: '**/*.{ts,tsx}'
---

# Mosaic Money TypeScript Rules

## Type safety
- Prefer explicit types and discriminated unions for workflow and state transitions.
- Avoid `any`; use `unknown` with safe narrowing when needed.
- Keep shared contracts centralized to prevent drift across modules.

## Code quality
- Favor readable and deterministic logic over clever abstractions.
- Keep functions focused and separate domain rules from UI concerns.
- Reuse existing shared modules before creating new utility layers.

## Async and reliability
- Use `async/await` with explicit error handling.
- Handle cancellation, retry, or backoff deliberately for network operations.
- Avoid hidden side effects in shared logic used by both web and mobile.

## Security basics
- Validate untrusted input at boundaries.
- Avoid unsafe rendering and dynamic code execution patterns.
- Never hardcode secrets or credentials.
