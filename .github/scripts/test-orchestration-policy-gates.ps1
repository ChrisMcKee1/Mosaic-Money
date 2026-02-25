#!/usr/bin/env pwsh
<#!
.SYNOPSIS
Deterministic orchestration policy gate checks for MM-ASP-07.

.DESCRIPTION
Fails with non-zero exit code when disallowed Aspire/orchestration patterns are detected.
Designed for both local use and CI execution.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDirectory "..\..")).Path

$violations = [System.Collections.Generic.List[object]]::new()

function Add-Violation {
    param(
        [Parameter(Mandatory = $true)][string]$Check,
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Line,
        [Parameter(Mandatory = $true)][string]$Message
    )

    $relativePath = [System.IO.Path]::GetRelativePath($repoRoot, $Path).Replace('\', '/')

    $violations.Add([PSCustomObject]@{
            Check   = $Check
            Path    = $relativePath
            Line    = $Line
            Message = $Message
        })
}

function Find-Matches {
    param(
        [Parameter(Mandatory = $true)][string[]]$Paths,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [switch]$SimpleMatch
    )

    if (-not $Paths -or $Paths.Count -eq 0) {
        return @()
    }

    foreach ($path in $Paths) {
        if (-not (Test-Path $path)) {
            continue
        }

        if ($SimpleMatch) {
            Select-String -Path $path -Pattern $Pattern -SimpleMatch -AllMatches
        }
        else {
            Select-String -Path $path -Pattern $Pattern -AllMatches
        }
    }
}

function Get-AppCodeFiles {
    param(
        [Parameter(Mandatory = $true)][string]$Root
    )

    if (-not (Test-Path $Root)) {
        return @()
    }

    $allowedExtensions = @('.cs', '.ts', '.tsx', '.js', '.jsx', '.mjs', '.cjs')

    Get-ChildItem -Path $Root -Recurse -File | Where-Object {
        $extension = $_.Extension.ToLowerInvariant()
        if ($allowedExtensions -notcontains $extension) {
            return $false
        }

        $full = $_.FullName

        if ($full -match '[\\/](bin|obj|node_modules|\.next|\.playwright-cli|coverage|dist|build)[\\/]') {
            return $false
        }

        if ($full -match '[\\/]tests?[\\/]' -or $full -match '[\\/]__tests__[\\/]') {
            return $false
        }

        if ($_.Name -match '\.(spec|test)\.(c|m)?(t|j)sx?$') {
            return $false
        }

        if ($_.Name -match '^playwright\.config\.') {
            return $false
        }

        return $true
    }
}

Write-Host "Running orchestration policy gate checks (MM-ASP-07)..."

# Check 1: Reject deprecated AddNpmApp in AppHost code.
$appHostFiles = Get-ChildItem -Path (Join-Path $repoRoot "src") -Recurse -File -Filter "apphost.cs" |
    Select-Object -ExpandProperty FullName

$addNpmMatches = Find-Matches -Paths $appHostFiles -Pattern '\bAddNpmApp\s*\('
foreach ($match in $addNpmMatches) {
    Add-Violation -Check 'NO_ADD_NPM_APP' -Path $match.Path -Line $match.LineNumber -Message 'Deprecated AddNpmApp usage detected. Use AddJavaScriptApp, AddViteApp, or AddNodeApp.'
}

# Check 2: Reject hardcoded localhost service URLs in web/api/worker app code.
$scanRoots = @(
    (Join-Path $repoRoot "src\MosaicMoney.Web"),
    (Join-Path $repoRoot "src\MosaicMoney.Api"),
    (Join-Path $repoRoot "src\MosaicMoney.Worker")
)

$appCodeFiles = foreach ($scanRoot in $scanRoots) { Get-AppCodeFiles -Root $scanRoot }
$localhostUrlPattern = '(?i)https?://(?:localhost|127(?:\.\d{1,3}){3}|\[::1\])(?::\d+)?(?:/|\b)'

$localhostMatches = Find-Matches -Paths $appCodeFiles.FullName -Pattern $localhostUrlPattern
foreach ($match in $localhostMatches) {
    Add-Violation -Check 'NO_HARDCODED_LOCALHOST_URLS' -Path $match.Path -Line $match.LineNumber -Message 'Hardcoded localhost service URL detected in app code. Use service discovery and AppHost references/injected environment values.'
}

# Check 3: Verify .NET service entrypoints include AddServiceDefaults().
$serviceEntrypoints = @(
    (Join-Path $repoRoot "src\MosaicMoney.Api\Program.cs"),
    (Join-Path $repoRoot "src\MosaicMoney.Worker\Program.cs")
)

foreach ($entrypoint in $serviceEntrypoints) {
    if (-not (Test-Path $entrypoint)) {
        Add-Violation -Check 'SERVICE_DEFAULTS_PRESENT' -Path $entrypoint -Message 'Entrypoint file is missing; cannot verify AddServiceDefaults().' 
        continue
    }

    $entryContent = Get-Content -Path $entrypoint -Raw
    if ($entryContent -notmatch '\bAddServiceDefaults\s*\(') {
        Add-Violation -Check 'SERVICE_DEFAULTS_PRESENT' -Path $entrypoint -Message 'Missing AddServiceDefaults() in service entrypoint.'
    }
}

# Check 4: Verify AppHost wiring uses WithReference(...) where expected.
$appHostPath = Join-Path $repoRoot "src\apphost.cs"
if (-not (Test-Path $appHostPath)) {
    Add-Violation -Check 'APPHOST_REFERENCE_WIRING' -Path $appHostPath -Message 'Main AppHost file not found; cannot verify reference wiring expectations.'
}
else {
    $appHostContent = Get-Content -Path $appHostPath -Raw

    if ($appHostContent -notmatch '(?s)AddProject<Projects\.MosaicMoney_Api>\("api"\).*?\.WithReference\(ledgerDb\)') {
        Add-Violation -Check 'APPHOST_REFERENCE_WIRING' -Path $appHostPath -Message 'API resource is expected to include WithReference(ledgerDb).' 
    }

    if ($appHostContent -notmatch '(?s)var\s+worker\s*=\s*builder\s*\.AddProject<Projects\.MosaicMoney_Worker>\("worker"\).*?\.WithReference\(ledgerDb\)') {
        Add-Violation -Check 'APPHOST_REFERENCE_WIRING' -Path $appHostPath -Message 'Worker resource is expected to include WithReference(ledgerDb).' 
    }

    if ($appHostContent -notmatch '(?s)var\s+worker\s*=\s*builder\s*\.AddProject<Projects\.MosaicMoney_Worker>\("worker"\).*?\.WithReference\(api\)') {
        Add-Violation -Check 'APPHOST_REFERENCE_WIRING' -Path $appHostPath -Message 'Worker resource is expected to include WithReference(api).' 
    }

    if ($appHostContent -notmatch '(?s)AddJavaScriptApp\("web"\s*,\s*"\./MosaicMoney\.Web"\).*?\.WithReference\(api\)') {
        Add-Violation -Check 'APPHOST_REFERENCE_WIRING' -Path $appHostPath -Message 'Web resource is expected to include WithReference(api).' 
    }
}

if ($violations.Count -gt 0) {
    Write-Host ""
    Write-Host "[FAIL] Orchestration policy gate failed with $($violations.Count) violation(s):" -ForegroundColor Red

    foreach ($violation in $violations) {
        $location = if ([string]::IsNullOrWhiteSpace($violation.Line)) {
            $violation.Path
        }
        else {
            "$($violation.Path):$($violation.Line)"
        }

        Write-Host " - [$($violation.Check)] $location :: $($violation.Message)" -ForegroundColor Red
    }

    exit 1
}

Write-Host ""
Write-Host "[PASS] Orchestration policy gate checks passed." -ForegroundColor Green
exit 0
