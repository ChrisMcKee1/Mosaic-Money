---
name: github-projects
description: GitHub Projects V2 board management for Mosaic Money. Use when syncing task statuses between spec files and the GitHub project board, adding or updating issues, querying board state, or modifying project field options via GraphQL.
---

# GitHub Projects

Use this skill to read and write the Mosaic Money GitHub Projects V2 board.

## Project identifiers

| Key | Value |
|---|---|
| Owner | `ChrisMcKee1` |
| Project number | `1` |
| Project name | `Mosaic-Money` |
| Project node ID | `PVT_kwHOAYj6Kc4BP962` |
| Repository node ID | `R_kgDORVI-Gw` |
| Status field ID | `PVTSSF_lAHOAYj6Kc4BP962zg-OQcQ` |
| Project URL | `https://github.com/users/ChrisMcKee1/projects/1` |

## Status field options

These map 1-to-1 with the status values defined in `project-plan/specs/001-mvp-foundation-task-breakdown.md`.

| Status | Option ID | Color |
|---|---|---|
| Not Started | `3ec0d41f` | GRAY |
| In Progress | `3e144626` | YELLOW |
| Blocked | `05d5ea50` | RED |
| Parked | `0754d578` | ORANGE |
| In Review | `98133e2d` | PURPLE |
| Done | `0584d743` | GREEN |
| Cut | `1774580b` | PINK |

## Authentication

The `gh` CLI must have the `project` scope. Verify with:

```bash
gh auth status
```

If the scope is missing, refresh:

```bash
gh auth refresh -s project --hostname github.com
```

## Common operations

### Query board items

```bash
gh project item-list 1 --owner ChrisMcKee1 --format json --limit 100
```

### Add an issue to the board

```graphql
mutation {
  addProjectV2ItemById(input: {
    projectId: "PVT_kwHOAYj6Kc4BP962"
    contentId: "<ISSUE_NODE_ID>"
  }) {
    item { id }
  }
}
```

Run via CLI:

```bash
gh api graphql -f query='mutation { addProjectV2ItemById(input: {projectId: "PVT_kwHOAYj6Kc4BP962", contentId: "<ISSUE_NODE_ID>"}) { item { id } } }'
```

### Set an item's status

```graphql
mutation {
  updateProjectV2ItemFieldValue(input: {
    projectId: "PVT_kwHOAYj6Kc4BP962"
    itemId: "<ITEM_ID>"
    fieldId: "PVTSSF_lAHOAYj6Kc4BP962zg-OQcQ"
    value: { singleSelectOptionId: "<OPTION_ID>" }
  }) {
    projectV2Item { id }
  }
}
```

### Get an issue's node ID

```bash
gh issue view <NUMBER> --repo ChrisMcKee1/Mosaic-Money --json id --jq .id
```

### Batch sync script

A pre-built script at `.github/scripts/sync-project-board.ps1` adds all tracked issues and sets their statuses. Update the `$issues` array in the script when tasks are added or statuses change, then run:

```powershell
pwsh .github/scripts/sync-project-board.ps1
```

## Dual-update lifecycle

Task status lives in **two places** and must stay synchronized:

1. **Spec files** — the source of truth for task definitions and done criteria.
   - Master breakdown: `project-plan/specs/001-mvp-foundation-task-breakdown.md`
   - Milestone specs: `project-plan/specs/002-006`
2. **GitHub Projects board** — the visual/queryable view for tracking.

### When changing a task status

1. Update the `Status` column in the relevant spec file(s) (both 001 and the milestone spec).
2. Set the matching status on the GitHub Projects board using the GraphQL mutation above.
3. If adding a new issue, also add it to the board and the sync script.

### Status transition rules

- Only the `mosaic-money-planner` may set a task to `Done` or `Cut`.
- Subagents may set `In Progress`, `Blocked`, `Parked`, or `In Review`.
- Every `Blocked` status must include a note explaining the blocker.
- Every `Cut` status must include a documented reason.

## Querying board state

### Group items by status

```powershell
$items = (gh project item-list 1 --owner ChrisMcKee1 --format json --limit 100 | ConvertFrom-Json).items
$items | Group-Object -Property status | Select-Object Name, Count
```

### List non-Done items

```powershell
$items | Where-Object { $_.status -ne "Done" } | ForEach-Object {
    "$($_.status.PadRight(12)) $($_.title)"
}
```

## Updating project field options

If the status set needs to change, use `updateProjectV2Field` with **all** options (existing and new). Each option requires `name`, `color`, and `description` (all required). The `id` field is **not accepted** on input — options are matched by name.

```graphql
mutation {
  updateProjectV2Field(input: {
    fieldId: "PVTSSF_lAHOAYj6Kc4BP962zg-OQcQ"
    name: "Status"
    singleSelectOptions: [
      { name: "Not Started", color: GRAY, description: "Work has not begun" }
      { name: "In Progress", color: YELLOW, description: "Actively being implemented" }
      { name: "Blocked", color: RED, description: "Cannot proceed (dependency/issue/external)" }
      { name: "Parked", color: ORANGE, description: "Deliberately deferred, not blocked" }
      { name: "In Review", color: PURPLE, description: "Implementation complete, awaiting planner verification" }
      { name: "Done", color: GREEN, description: "Planner verified and accepted" }
      { name: "Cut", color: PINK, description: "Removed from scope" }
    ]
  }) {
    projectV2Field { ... on ProjectV2SingleSelectField { id name options { id name } } }
  }
}
```

> **Warning:** Replacing options clears all existing item statuses. After updating field options, re-run the sync script to restore statuses.

## Troubleshooting

| Problem | Fix |
|---|---|
| `missing required scopes [read:project]` | Run `gh auth refresh -s project --hostname github.com` and complete the device flow. |
| Mutation returns `null` data | Verify the node ID exists and the authenticated user owns the project. |
| Status not updating | Confirm the option ID matches the current field configuration — option IDs change when field options are replaced. |
