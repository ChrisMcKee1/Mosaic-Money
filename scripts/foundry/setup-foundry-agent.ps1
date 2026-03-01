param(
    [string]$ProjectEndpoint,
    [string]$ProjectApiKey,
    [string]$ModelDeploymentName,
    [string]$AgentName = "Mosaic",
    [string]$AgentInstructionsPath,
    [string]$AgentInstructionsText,
    [string]$AgentApiVersion = "v1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RequiredInput {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$CurrentValue,
        [string[]]$EnvironmentNames
    )

    if (-not [string]::IsNullOrWhiteSpace($CurrentValue)) {
        return $CurrentValue.Trim()
    }

    foreach ($envName in $EnvironmentNames) {
        if ([string]::IsNullOrWhiteSpace($envName)) {
            continue
        }

        $candidate = [Environment]::GetEnvironmentVariable($envName)
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return $candidate.Trim()
        }
    }

    throw "$Name is required. Provide -$Name or set one of these environment variables: $($EnvironmentNames -join ', ')."
}

function Resolve-OptionalInput {
    param(
        [string]$CurrentValue,
        [string[]]$EnvironmentNames
    )

    if (-not [string]::IsNullOrWhiteSpace($CurrentValue)) {
        return $CurrentValue.Trim()
    }

    foreach ($envName in $EnvironmentNames) {
        if ([string]::IsNullOrWhiteSpace($envName)) {
            continue
        }

        $candidate = [Environment]::GetEnvironmentVariable($envName)
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return $candidate.Trim()
        }
    }

    return $null
}

function Assert-HttpsUri {
    param(
        [AllowEmptyString()][string]$Value,
        [Parameter(Mandatory = $true)][string]$FieldName
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return
    }

    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri)) {
        throw "$FieldName must be a valid absolute URI. Value: '$Value'"
    }

    if ($uri.Scheme -ne 'https') {
        throw "$FieldName must use https. Value: '$Value'"
    }
}

function Get-MosaicDefaultSystemPrompt {
    return @'
You are Mosaic, the principal personal-finance CPA-style agent for Mosaic Money.

Mission and identity:
1. You exist to help the user understand, stabilize, and improve their financial life.
2. You treat finance as a mosaic: each transaction, account, obligation, and goal is one tile.
3. Your job is to combine tiles into a clear and actionable picture without distorting ledger truth.
4. You are calm, practical, and evidence-driven.
5. You are never condescending, alarmist, or vague.

Operating principles:
6. Preserve single-entry ledger semantics at all times.
7. Never rewrite history to force a narrative.
8. Keep UserNote and AgentNote as separate tracks.
9. Use deterministic data and in-database retrieval before model speculation.
10. Use the smallest necessary set of tools for each task.
11. Explicitly report uncertainty and what data would resolve it.
12. Prefer financially conservative recommendations when confidence is low.

Human-in-the-loop policy:
13. Route to NeedsReview when confidence is low on financial classification.
14. Route to NeedsReview for high-impact financial actions.
15. Never perform external messaging actions autonomously.

Communication style:
16. Lead with the answer, then supporting evidence.
17. Use concise sections and practical language.
18. Separate facts, assumptions, and recommendations.
19. Avoid over-claiming certainty.
20. End with one concrete next step.
'@
}

function Resolve-AgentInstructions {
    param(
        [string]$InstructionsPath,
        [string]$InstructionsText
    )

    if (-not [string]::IsNullOrWhiteSpace($InstructionsPath) -and -not [string]::IsNullOrWhiteSpace($InstructionsText)) {
        throw "Provide either -AgentInstructionsPath or -AgentInstructionsText, not both."
    }

    if (-not [string]::IsNullOrWhiteSpace($InstructionsText)) {
        return $InstructionsText.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($InstructionsPath)) {
        if (-not (Test-Path -LiteralPath $InstructionsPath)) {
            throw "Agent instructions file was not found: $InstructionsPath"
        }

        $content = Get-Content -LiteralPath $InstructionsPath -Raw
        if ([string]::IsNullOrWhiteSpace($content)) {
            throw "Agent instructions file is empty: $InstructionsPath"
        }

        return $content.Trim()
    }

    return (Get-MosaicDefaultSystemPrompt).Trim()
}

function Get-AccessToken {
    param([Parameter(Mandatory = $true)][string]$Resource)

    $token = az account get-access-token --resource $Resource --query accessToken -o tsv
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Failed to acquire Azure access token for resource '$Resource'."
    }

    return $token.Trim()
}

function Get-ErrorText {
    param([Parameter(Mandatory = $true)]$ErrorRecord)

    if ($ErrorRecord.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($ErrorRecord.ErrorDetails.Message)) {
        return $ErrorRecord.ErrorDetails.Message
    }

    if ($ErrorRecord.Exception -and -not [string]::IsNullOrWhiteSpace($ErrorRecord.Exception.Message)) {
        return $ErrorRecord.Exception.Message
    }

    return [string]$ErrorRecord
}

function Get-FoundryBearerHeaders {
    $token = Get-AccessToken -Resource "https://ai.azure.com"
    return @{
        Authorization = "Bearer $token"
        "Content-Type" = "application/json"
    }
}

$ProjectEndpoint = Resolve-RequiredInput -Name "ProjectEndpoint" -CurrentValue $ProjectEndpoint -EnvironmentNames @("FOUNDRY_PROJECT_ENDPOINT", "MOSAIC_FOUNDRY_PROJECT_ENDPOINT")
$ProjectApiKey = Resolve-OptionalInput -CurrentValue $ProjectApiKey -EnvironmentNames @("FOUNDRY_PROJECT_API_KEY", "MOSAIC_FOUNDRY_PROJECT_API_KEY")
$ModelDeploymentName = Resolve-RequiredInput -Name "ModelDeploymentName" -CurrentValue $ModelDeploymentName -EnvironmentNames @("FOUNDRY_MODEL_DEPLOYMENT", "MOSAIC_FOUNDRY_MODEL_DEPLOYMENT", "AZURE_OPENAI_CHAT_DEPLOYMENT")

Assert-HttpsUri -Value $ProjectEndpoint -FieldName "ProjectEndpoint"

$agentInstructions = Resolve-AgentInstructions -InstructionsPath $AgentInstructionsPath -InstructionsText $AgentInstructionsText

$agentDefinition = [ordered]@{
    kind = "prompt"
    model = $ModelDeploymentName
    instructions = $agentInstructions
}

$agentPayload = [ordered]@{
    name = $AgentName
    definition = $agentDefinition
}

$agentBody = $agentPayload | ConvertTo-Json -Depth 12
$projectBaseUri = $ProjectEndpoint.TrimEnd("/")
$candidateApiVersions = @($AgentApiVersion, "v1", "2025-11-01-preview", "2025-05-01") |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Unique

Write-Host "Creating or updating Foundry agent '$AgentName' (minimal mode: model + instructions only)..."

$agentResponse = $null
$finalApiVersion = $null
$finalAuthMode = $null
$lastFailure = $null

$authAttempts = [System.Collections.Generic.List[object]]::new()
$authAttempts.Add([ordered]@{
    Name = "entra-token"
    Headers = (Get-FoundryBearerHeaders)
})

if (-not [string]::IsNullOrWhiteSpace($ProjectApiKey)) {
    $authAttempts.Add([ordered]@{
        Name = "api-key"
        Headers = @{
            "Content-Type" = "application/json"
            "api-key" = $ProjectApiKey
        }
    })
}

foreach ($apiVersion in $candidateApiVersions) {
    $agentUri = "{0}/agents?api-version={1}" -f $projectBaseUri, $apiVersion

    foreach ($authAttempt in $authAttempts) {
        try {
            $agentResponse = Invoke-RestMethod -Method Post -Uri $agentUri -Headers $authAttempt.Headers -Body $agentBody
            $finalApiVersion = $apiVersion
            $finalAuthMode = $authAttempt.Name
            break
        }
        catch {
            $failureText = Get-ErrorText -ErrorRecord $_

            if ($failureText -match "API version not supported") {
                $lastFailure = $_
                break
            }

            if ($failureText -match "already exists" -or $failureText -match "conflict") {
                $updateUri = "{0}/agents/{1}?api-version={2}" -f $projectBaseUri, $AgentName, $apiVersion
                try {
                    $agentResponse = Invoke-RestMethod -Method Post -Uri $updateUri -Headers $authAttempt.Headers -Body $agentBody
                    $finalApiVersion = $apiVersion
                    $finalAuthMode = $authAttempt.Name
                    break
                }
                catch {
                    $lastFailure = $_
                    continue
                }
            }

            if ($failureText -match "Key-based authentication is not supported for this route" -or $failureText -match "401" -or $failureText -match "Unauthorized") {
                $lastFailure = $_
                continue
            }

            $lastFailure = $_
            throw
        }
    }

    if ($null -ne $agentResponse) {
        break
    }
}

if ($null -eq $agentResponse) {
    if ($null -ne $lastFailure) {
        throw $lastFailure
    }

    throw "Foundry agent bootstrap failed before receiving a response."
}

$createdAgentId = $agentResponse.id
if ([string]::IsNullOrWhiteSpace($createdAgentId)) {
    $createdAgentId = $agentResponse.agent_id
}

Write-Host "Agent bootstrap complete."
Write-Host "Agent name: $AgentName"
if (-not [string]::IsNullOrWhiteSpace($createdAgentId)) {
    Write-Host "Agent id: $createdAgentId"
}
Write-Host "Configured tools: 0 (minimal mode)"
Write-Host "Prompt length (characters): $($agentInstructions.Length)"
if (-not [string]::IsNullOrWhiteSpace($finalApiVersion)) {
    Write-Host "Agent API version used: $finalApiVersion"
}
if (-not [string]::IsNullOrWhiteSpace($finalAuthMode)) {
    Write-Host "Foundry auth mode used: $finalAuthMode"
}
