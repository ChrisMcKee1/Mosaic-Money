# run-code-simplifier.ps1
# Stop hook: blocks the agent from stopping when edits are detected,
# returning a reason that instructs it to review the git delta via the code-simplifier agent.

$ErrorActionPreference = "Stop"

try {
    $rawInput = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($rawInput)) { exit 0 }

    $payload = $rawInput | ConvertFrom-Json -ErrorAction Stop

    # Prevent infinite loops: if the hook already blocked once, let the agent stop.
    if ($payload.PSObject.Properties.Name -contains 'stop_hook_active' -and $payload.stop_hook_active -eq $true) {
        exit 0
    }

    # Check transcript for edit tool usage.
    $transcriptPath = $null
    foreach ($key in @('transcript_path', 'transcriptPath')) {
        if ($payload.PSObject.Properties.Name -contains $key) {
            $value = [string]$payload.$key
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                $transcriptPath = $value
                break
            }
        }
    }

    $hasEdits = $false
    if ($transcriptPath -and (Test-Path -LiteralPath $transcriptPath -PathType Leaf)) {
        $text = Get-Content -LiteralPath $transcriptPath -Raw -ErrorAction SilentlyContinue
        $editPattern = '"(?:toolName|tool_name|tool|name)"\s*:\s*"(?:edit|write|create|apply_patch|edit_file|write_file|create_file|insert|str_replace|delete_file|replace_string_in_file|multi_replace_string_in_file)"'
        if ($text -and [regex]::IsMatch($text, $editPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
            $hasEdits = $true
        }
    }

    if (-not $hasEdits) { exit 0 }

    $reason = "Before finishing, delegate to the code-simplifier agent to review and simplify all files that have changed since the last commit (uncommitted working-directory changes). Use 'git diff HEAD --name-only --diff-filter=ACMR' to identify changed files. Do not modify any files under .github/hooks/. Do not commit code."

    $output = @{
        hookSpecificOutput = @{
            hookEventName = "Stop"
            decision      = "block"
            reason        = $reason
        }
    }

    $output | ConvertTo-Json -Compress -Depth 3
    exit 0
} catch {
    # Non-blocking: exit cleanly on any error.
    exit 0
}
