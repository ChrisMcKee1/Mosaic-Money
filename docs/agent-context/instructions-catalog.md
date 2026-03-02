# Mosaic Money Custom Instructions Catalog


## Agent Loading
- Load when: adding, removing, or restructuring custom instruction files and policy layering.
- Apply with workspace policy: [.github/copilot-instructions.md](../../.github/copilot-instructions.md)

This catalog records instruction-file decisions after reviewing VS Code customization docs and the Awesome Copilot instructions repository.

## File types reviewed
- Always-on instructions: `.github/copilot-instructions.md`, `AGENTS.md`, `CLAUDE.md`.
- File-scoped instructions: `.github/instructions/*.instructions.md` using `applyTo` patterns.

## Local strategy selected
- Use `.github/copilot-instructions.md` for repository-wide architecture and product guardrails.
- Use focused `.github/instructions/*.instructions.md` for language and framework behavior.
- Do not add nested `AGENTS.md` or `CLAUDE.md` right now to avoid overlapping always-on policies.

## Source review performed
- VS Code agent skills: `https://code.visualstudio.com/docs/copilot/customization/agent-skills`
- VS Code custom instructions: `https://code.visualstudio.com/docs/copilot/customization/custom-instructions`
- Awesome Copilot instructions index: `https://github.com/github/awesome-copilot/tree/main/instructions`

## Relevant concepts adopted and adapted
- From `csharp.instructions.md`: modern C# and .NET quality conventions.
- From `aspnet-rest-apis.instructions.md`: API design/testing/documentation principles, adapted to Minimal API-first.
- From `nextjs.instructions.md` and `nextjs-tailwind.instructions.md`: App Router, server/client boundaries, and UI standards.
- From `typescript-5-es2022.instructions.md`: strong typing and maintainability rules.
- From `security-and-owasp.instructions.md`: secure-by-default coding patterns.
- From `agent-safety.instructions.md`: fail-closed and human-review patterns for agentic workflows.
- From `update-docs-on-code-change.instructions.md`: docs synchronization intent, simplified for Mosaic Money.

## Rejected or not directly imported
- Broad framework instructions not in the Mosaic Money stack (Angular, Java/Spring, Rails, Terraform-only, Power Platform, etc.).
- Instructions with heavy generic process/config blocks that would add noise or conflict with Mosaic Money guardrails.
- Guidance that encourages architecture drift (for example, controller-first defaults when this project is Minimal API-first).

## Installed instruction files
- `.github/copilot-instructions.md`
- `.github/instructions/mosaic-money-dotnet-api.instructions.md`
- `.github/instructions/mosaic-money-nextjs-frontend.instructions.md`
- `.github/instructions/mosaic-money-typescript.instructions.md`
- `.github/instructions/mosaic-money-security.instructions.md`
- `.github/instructions/mosaic-money-doc-sync.instructions.md`

