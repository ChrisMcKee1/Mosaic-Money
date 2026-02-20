---
name: Mosaic Money Security
description: Security-first guidance aligned to OWASP-style risks for backend, frontend, and integration code.
applyTo: '**/*.{cs,ts,tsx,js,jsx,json,yml,yaml}'
---

# Mosaic Money Security Rules

## Core rules
- Default to least privilege and deny-by-default authorization decisions.
- Validate and sanitize all external input.
- Use parameterized queries and safe serialization practices.
- Do not hardcode secrets, tokens, or credentials.

## Web and API safety
- Enforce authentication and authorization for sensitive actions.
- Use secure session and token handling.
- Prevent SSRF and path traversal where user-controlled paths or URLs are involved.
- Return safe error payloads without leaking internals.

## Dependency and config hygiene
- Prefer stable, maintained dependencies.
- Keep secure defaults for production configuration.
- Review and update vulnerable packages during dependency changes.

## AI workflow safety
- For agentic flows, enforce explicit human review for high-impact operations.
- Do not allow autonomous external messaging execution.
