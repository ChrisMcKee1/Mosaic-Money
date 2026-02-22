#!/usr/bin/env bash
# run-code-simplifier.sh
# Stop hook: launches the code-simplifier agent when file edits were detected.
set -euo pipefail

# Guard: copilot CLI must be available.
command -v copilot &>/dev/null || exit 0

event=$(cat)
[[ -z $event ]] && exit 0

if ! printf '%s' "$event" | grep -qE '"tool"[[:space:]]*:[[:space:]]*"(edit|write|create|apply_patch)"'; then
    exit 0
fi

copilot \
  --agent code-simplifier \
  --prompt "Review and simplify all files that have changed since the last commit (uncommitted working-directory changes). Use 'git diff HEAD --name-only --diff-filter=ACMR' to identify changed files, and 'git diff HEAD -- <file>' to see each diff. If no issues are found, respond with 'No changes needed.' Do not commit code." \
  --allow-all-tools \
  --silent
