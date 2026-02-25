param(
    [string]$OutputPath = "artifacts/release-gates/mm-ai-11/latest.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $resolvedOutputPath = $OutputPath
}
else {
    $resolvedOutputPath = Join-Path $repoRoot $OutputPath
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$env:MM_AI_11_EVIDENCE_PATH = $resolvedOutputPath

try {
    dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj `
        --filter "FullyQualifiedName~AgenticEvalReleaseGateTests.EvaluateAsync_AllCriteriaMeetReleaseBlockingThresholds" `
        --logger "console;verbosity=normal"

    if ($LASTEXITCODE -ne 0) {
        throw "MM-AI-11 release gate failed. See test output for criterion details."
    }

    Write-Host "MM-AI-11 release gate passed. Evidence artifact: $resolvedOutputPath"
}
finally {
    Remove-Item Env:MM_AI_11_EVIDENCE_PATH -ErrorAction SilentlyContinue
}
