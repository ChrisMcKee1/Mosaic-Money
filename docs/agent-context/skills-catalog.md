# Mosaic Money Skill Catalog

This catalog tracks all currently available skills across repository, user, and extension locations, and maps them to Mosaic Money agents for just-in-time loading.

## Skill directories audited
- Repo-local skills: `.github/skills/`
- User-level Copilot skills: `C:\Users\chrismckee\.copilot\skills\`
- User-level agent skill pack: `C:\Users\chrismckee\.agents\skills\`
- Extension-provided skills:
  - `C:\Users\chrismckee\.vscode-insiders\extensions\github.copilot-chat-*/assets/prompts/skills/`
  - `C:\Users\chrismckee\.vscode-insiders\extensions\ms-windows-ai-studio.*/resources/skills/`
  - `C:\Users\chrismckee\.vscode-insiders\extensions\ms-python.vscode-python-envs-*/.github/skills/`

## Installed project-local skills
- `.github/skills/agent-governance/SKILL.md`: risk classification, safety checks, and escalation workflow.
- `.github/skills/agentic-eval/SKILL.md`: evaluation loop for quality-critical feature changes.
- `.github/skills/aspire/SKILL.md`: Aspire CLI, MCP, orchestration and diagnostics baseline.
- `.github/skills/aspire-mosaic-money/SKILL.md`: Mosaic Money-specific Aspire package/wiring policy.
- `.github/skills/frontend-design/SKILL.md`: distinctive production UI implementation guidance.
- `.github/skills/gh-cli/SKILL.md`: GitHub CLI operations for repo/issue/PR/release/project flows.
- `.github/skills/git-commit/SKILL.md`: conventional commit flow and safe staging.
- `.github/skills/github-projects/SKILL.md`: Projects V2 IDs, status options, and GraphQL sync mutations.
- `.github/skills/nuget-manager/SKILL.md`: safe NuGet add/update/remove workflow.
- `.github/skills/playwright-cli/SKILL.md`: browser automation and interactive UI validation.
- `.github/skills/prd/SKILL.md`: decomposition into scope, acceptance criteria, and delivery slices.
- `.github/skills/webapp-testing/SKILL.md`: Playwright UX/integration testing workflow.

## User-level shared skills

### `.copilot/skills`
- `aspire`: general Aspire app creation/run/debug/deploy workflow.
- `azure-role-selector`: least-privilege role guidance for requested permissions.
- `git`: Git flow and branch/merge/rebase/PR lifecycle operations.
- `mcp-app` (`Create MCP App`): scaffold and wire interactive MCP app UIs.
- `microsoft-code-reference`: official API signatures and sample verification.
- `microsoft-docs`: official docs retrieval and synthesis across Microsoft stacks.

### `.agents/skills`
- `appinsights-instrumentation`: instrumentation guidance for Application Insights SDK usage.
- `azure-ai`: Azure AI Search/Speech/OpenAI/Doc Intelligence patterns.
- `azure-aigateway`: APIM as AI Gateway for model governance and control.
- `azure-compliance`: compliance/security posture audit and azqr-style reviews.
- `azure-cost-optimization`: identify spend reduction opportunities and cost waste.
- `azure-deploy`: execution phase for deploying prepared Azure workloads.
- `azure-diagnostics`: production troubleshooting for Azure workloads/logs.
- `azure-hosted-copilot-sdk`: build and deploy Copilot SDK apps on Azure.
- `azure-kusto`: KQL workflows for Azure Data Explorer and telemetry analysis.
- `azure-messaging`: Event Hubs and Service Bus SDK troubleshooting patterns.
- `azure-observability`: Azure Monitor/App Insights/alerts/workbooks guidance.
- `azure-postgres`: Azure Database for PostgreSQL and Entra ID auth setup.
- `azure-prepare`: default app preparation and infra scaffolding path for Azure.
- `azure-rbac`: role selection and assignment with least privilege.
- `azure-resource-lookup`: list/find/show Azure resources across scopes.
- `azure-resource-visualizer`: generate Mermaid architecture diagrams from Azure resources.
- `azure-storage`: blob/file/queue/table/data lake workflows.
- `azure-validate`: pre-deployment readiness and configuration checks.
- `building-native-ui`: Expo Router-native UI architecture and composition.
- `entra-app-registration`: Entra app registration and OAuth/MSAL setup.
- `expo-api-routes`: Expo Router API route guidance.
- `expo-cicd-workflows`: EAS workflow YAML authoring and CI/CD patterns.
- `expo-deployment`: mobile/web store and host deployment workflows.
- `expo-dev-client`: local/TestFlight dev client build and distribution.
- `expo-tailwind-setup`: Tailwind v4 and NativeWind setup for Expo.
- `expo-ui-swift-ui` (`Expo UI SwiftUI`): SwiftUI views/modifiers via Expo UI.
- `frontend-design`: user-level variant of high-quality frontend design guidance.
- `microsoft-foundry`: Foundry agents/models, quota, RBAC, deployment operations.
- `microsoft-foundry/models/deploy-model`: model deployment workflow entry.
- `microsoft-foundry/models/deploy-model/capacity`: capacity-focused model deployment guidance.
- `microsoft-foundry/models/deploy-model/customize`: custom model deployment guidance.
- `microsoft-foundry/models/deploy-model/preset`: preset-based model deployment guidance.
- `native-data-fetching`: robust network/data fetching patterns for native apps.
- `upgrading-expo`: Expo SDK upgrade and dependency compatibility workflow.
- `use-dom`: Expo DOM bridge patterns for web-to-native migration.

### Extension-provided skills in current environment
- `agent-customization`: create/fix/maintain agent customization files and skill wiring.
- `get-search-view-results`: read VS Code search panel results for task context.
- `install-vscode-extension`: extension installation workflow.
- `project-setup-info-context7`: project setup bootstrap info using Context7.
- `project-setup-info-local`: project setup bootstrap info from local workspace context.
- `agent-workflow-builder_ai_toolkit`: Microsoft Agent Framework workflow generation/development/deployment.
- Python extension skills available in environment: `cross-platform-paths`, `debug-failing-test`, `generate-snapshot`, `python-manager-discovery`, `run-e2e-tests`, `run-integration-tests`, `run-pre-commit-checks`, `run-smoke-tests`, `settings-precedence`.

## JIT skill loading policy
- Load skills only when the task intent matches the skill's explicit use-for domain.
- Prefer repo-local skills first for Mosaic Money policy enforcement.
- Add user/extension skills when the task scope exceeds local skill coverage.
- For Azure requests: use `azure-prepare` for build/update work, `azure-validate` before deployment, and `azure-deploy` for execution.
- For PR/board operations: load `gh-cli`, `github-projects`, and `git-commit` together.

## API research source policy
- For Plaid API endpoint, webhook, and Link lifecycle research, use Context7 MCP tools first: resolve library ID with `mcp_io_github_ups_resolve-library-id` (`plaid` -> `/websites/plaid`), then fetch docs with `mcp_io_github_ups_get-library-docs`.
- After Context7 lookup, verify critical request/response and webhook semantics against official Plaid docs before implementation.
- Include researched source URLs in implementation summaries for Plaid-related tasks.

## Agent-to-skill mapping
- Planner:
  - Baseline: `prd`, `agent-governance`, `agentic-eval`, `aspire-mosaic-money`, `github-projects`, `gh-cli`, `git-commit`, `microsoft-docs`.
  - On-demand: `git`, `microsoft-code-reference`, `azure-prepare`, `azure-validate`, `azure-deploy`, `azure-compliance`, `azure-cost-optimization`, `azure-resource-lookup`, `agent-customization`.
- Backend:
  - Baseline: `aspire`, `aspire-mosaic-money`, `nuget-manager`, `agent-governance`, `agentic-eval`, `git-commit`, `microsoft-code-reference`, `microsoft-docs`.
  - On-demand: `azure-postgres`, `azure-rbac`, `azure-observability`, `azure-diagnostics`, `azure-prepare`, `azure-validate`, `azure-deploy`.
- Frontend:
  - Baseline: `aspire`, `aspire-mosaic-money`, `webapp-testing`, `playwright-cli`, `agent-governance`, `agentic-eval`, `frontend-design`, `git-commit`.
  - On-demand: `mcp-app`, `azure-hosted-copilot-sdk`, `azure-observability`, `azure-prepare`, `azure-validate`, `azure-deploy`.
- Mobile:
  - Baseline: `agent-governance`, `agentic-eval`, `prd`, `frontend-design`, `git-commit`.
  - On-demand: `building-native-ui`, `native-data-fetching`, `expo-api-routes`, `expo-cicd-workflows`, `expo-deployment`, `expo-dev-client`, `expo-tailwind-setup`, `upgrading-expo`, `use-dom`.
- AI:
  - Baseline: `agent-governance`, `agentic-eval`, `aspire`, `aspire-mosaic-money`, `nuget-manager`, `git-commit`, `microsoft-docs`, `microsoft-code-reference`.
  - On-demand: `microsoft-foundry`, `agent-workflow-builder_ai_toolkit`, `azure-ai`, `azure-aigateway`, `azure-observability`, `azure-compliance`.
- DevOps:
  - Baseline: `aspire`, `aspire-mosaic-money`, `agent-governance`, `nuget-manager`, `gh-cli`, `github-projects`, `git-commit`, `microsoft-docs`.
  - On-demand: `azure-prepare`, `azure-validate`, `azure-deploy`, `azure-resource-lookup`, `azure-resource-visualizer`, `azure-compliance`, `azure-cost-optimization`, `azure-diagnostics`, `azure-observability`, `azure-rbac`.
- Microsoft Agent Framework .NET:
  - Baseline: `agent-governance`, `agentic-eval`, `nuget-manager`, `git-commit`, `microsoft-code-reference`, `microsoft-docs`.
  - On-demand: `microsoft-foundry`, `agent-workflow-builder_ai_toolkit`, `azure-hosted-copilot-sdk`.
- Code Simplifier:
  - Baseline: `git-commit`.
  - On-demand: `git`.

## Installation correctness checklist
- Repo-local skill directories are under `.github/skills/<skill-name>/SKILL.md`.
- Agent files explicitly instruct when to load skills before implementation.
- Skill docs separate baseline vs on-demand loading so context is pulled only when needed.
