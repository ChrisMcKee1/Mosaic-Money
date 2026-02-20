# run-code-simplifier.ps1
# Stop hook: launches the code-simplifier agent when file edits were detected.

# Guard: copilot CLI must be available.
if (-not (Get-Command copilot -ErrorAction SilentlyContinue)) {
    exit 0
}

$event = ($input | Out-String).Trim()
if (-not $event) { exit 0 }

$data = $event | ConvertFrom-Json -ErrorAction SilentlyContinue
if (-not $data) { exit 0 }

$editTools = @('edit', 'write', 'create', 'apply_patch')
$hasEdits = $data.tool_calls | Where-Object { $_.tool -in $editTools } | Select-Object -First 1
if (-not $hasEdits) { exit 0 }

copilot `
  --agent code-simplifier `
  --prompt "Review and simplify the code delta from the most recent changes." `
  --allow-all-tools `
  --silent
