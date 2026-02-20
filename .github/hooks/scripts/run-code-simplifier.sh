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
  --prompt "Review and simplify the code delta from the most recent changes." \
  --allow-all-tools \
  --silent
