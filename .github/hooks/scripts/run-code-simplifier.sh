#!/usr/bin/env bash
# run-code-simplifier.sh
# Stop hook: blocks the agent from stopping when edits are detected,
# returning a reason that instructs it to review the git delta via the code-simplifier agent.
set -euo pipefail

input=$(cat)
[[ -z "$input" ]] && exit 0

# Prevent infinite loops: if the hook already blocked once, let the agent stop.
stop_active=$(printf '%s' "$input" | jq -r '.stop_hook_active // false')
[[ "$stop_active" == "true" ]] && exit 0

# Check transcript for edit tool usage.
transcript_path=$(printf '%s' "$input" | jq -r '.transcript_path // .transcriptPath // empty')
has_edits=false

if [[ -n "${transcript_path:-}" && -f "$transcript_path" ]]; then
    if grep -qE '"(toolName|tool_name|tool|name)"\s*:\s*"(edit|write|create|apply_patch|edit_file|write_file|create_file|insert|str_replace|delete_file|replace_string_in_file|multi_replace_string_in_file)"' "$transcript_path"; then
        has_edits=true
    fi
fi

[[ "$has_edits" == "false" ]] && exit 0

reason="Before finishing, delegate to the code-simplifier agent to review and simplify all files that have changed since the last commit (uncommitted working-directory changes). Use 'git diff HEAD --name-only --diff-filter=ACMR' to identify changed files. Do not modify any files under .github/hooks/. Do not commit code."

jq -nc --arg reason "$reason" '{hookSpecificOutput: {hookEventName: "Stop", decision: "block", reason: $reason}}'
exit 0
