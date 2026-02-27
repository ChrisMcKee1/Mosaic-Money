param(
    [string]$OutputPath = "artifacts/release-gates/mm-ai-11/latest.json",
    [string]$OfficialEvaluatorOutputPath = "artifacts/release-gates/mm-ai-12/latest.json",
    [string]$SpecialistEvaluatorOutputPath = "artifacts/release-gates/mm-ai-15/latest.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

function Resolve-ArtifactPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Ensure-ArtifactDirectory {
    param([string]$ResolvedPath)

    $directory = Split-Path -Parent $ResolvedPath
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
}

$resolvedOutputPath = Resolve-ArtifactPath $OutputPath
$resolvedOfficialEvaluatorOutputPath = Resolve-ArtifactPath $OfficialEvaluatorOutputPath
$resolvedSpecialistEvaluatorOutputPath = Resolve-ArtifactPath $SpecialistEvaluatorOutputPath

Ensure-ArtifactDirectory $resolvedOutputPath
Ensure-ArtifactDirectory $resolvedOfficialEvaluatorOutputPath
Ensure-ArtifactDirectory $resolvedSpecialistEvaluatorOutputPath

$env:MM_AI_11_EVIDENCE_PATH = $resolvedOutputPath
$env:MM_AI_12_EVIDENCE_PATH = $resolvedOfficialEvaluatorOutputPath
$env:MM_AI_15_EVIDENCE_PATH = $resolvedSpecialistEvaluatorOutputPath

try {
    dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj `
        --filter "FullyQualifiedName~AgenticEvalReleaseGateTests.EvaluateAsync_AllCriteriaMeetReleaseBlockingThresholds" `
        --logger "console;verbosity=normal"

    if ($LASTEXITCODE -ne 0) {
        throw "MM-AI-11 release gate failed. See test output for criterion details."
    }

    dotnet test src/MosaicMoney.Api.Tests/MosaicMoney.Api.Tests.csproj `
        --filter "FullyQualifiedName~AgenticEvalOfficialEvaluatorArtifactsTests|FullyQualifiedName~AgenticEvalSpecialistEvaluatorArtifactsTests" `
        --logger "console;verbosity=normal"

    if ($LASTEXITCODE -ne 0) {
        throw "MM-AI-12/MM-AI-15 artifact validation failed. See test output for details."
    }

    Write-Host "MM-AI-11 release gate passed. Evidence artifact: $resolvedOutputPath"
    Write-Host "MM-AI-12 official evaluator replay artifact: $resolvedOfficialEvaluatorOutputPath"
    Write-Host "MM-AI-15 specialist evaluator replay artifact: $resolvedSpecialistEvaluatorOutputPath"
}
finally {
    Remove-Item Env:MM_AI_11_EVIDENCE_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:MM_AI_12_EVIDENCE_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:MM_AI_15_EVIDENCE_PATH -ErrorAction SilentlyContinue
}
