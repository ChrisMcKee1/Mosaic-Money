---
name: agentic-eval
description: Evaluation loop for AI and feature quality improvements. Use when adding AI behavior, changing ranking/classification logic, or shipping non-trivial feature slices that require quality validation.
---

# Agentic Eval

Use this skill to prevent single-pass changes for quality-critical work.

## Evaluation loop
1. Define measurable success criteria before coding.
2. Implement change with a minimal, testable slice.
3. Evaluate against explicit checks.
4. Refine until criteria pass or escalate with known gaps.

## Minimum checks
- Functional correctness against requested behavior.
- Regression checks for existing flows.
- Edge-case handling for date, money, and categorization ambiguity.
- Explainability of AI decisions in concise notes.

## Delivery requirement
Ship changes with a short evaluation summary:
- `Criteria:` what was measured
- `Result:` pass/fail per criterion
- `Follow-up:` remaining risks or next validation step
