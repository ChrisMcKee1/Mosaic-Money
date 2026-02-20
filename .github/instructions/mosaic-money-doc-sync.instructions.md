---
name: Mosaic Money Documentation Sync
description: Keep architecture, PRD context, and policy docs synchronized when code or governance behavior changes.
applyTo: '**/*.{md,cs,ts,tsx,js,jsx}'
---

# Mosaic Money Documentation Sync Rules

- Update relevant docs when behavior, APIs, or architecture decisions change.
- When changing orchestration/package policies, update matching files in `docs/agent-context/`.
- When changing feature behavior that affects scope or acceptance criteria, update PRD context docs.
- Keep examples and command snippets aligned with current tooling versions.
- Avoid speculative documentation for features that are not implemented.
