#!/usr/bin/env pwsh
# Sync all Mosaic-Money issues to the GitHub Projects board with correct statuses
# Usage: pwsh .github/scripts/sync-project-board.ps1

$ErrorActionPreference = "Continue"
$PROJECT_ID = "PVT_kwHOAYj6Kc4BP962"
$FIELD_ID = "PVTSSF_lAHOAYj6Kc4BP962zg-OQcQ"

# Status option IDs (from the Status field we just configured)
$OPT = @{
    "Not Started" = "3ec0d41f"
    "In Progress" = "3e144626"
    "Blocked"     = "05d5ea50"
    "Parked"      = "0754d578"
    "In Review"   = "98133e2d"
    "Done"        = "0584d743"
    "Cut"         = "1774580b"
}

# Complete issue map: issue number -> (node_id, desired_status)
# Derived from specs 001-006 and the issue listing
$issues = @(
    # --- M1: Platform & Contract Foundation (all Done) ---
    @{ num=1;  nid="I_kwDORVI-G87tOhl7"; status="Done" }        # MM-ASP-01
    @{ num=2;  nid="I_kwDORVI-G87tOhps"; status="Done" }        # MM-ASP-02
    @{ num=3;  nid="I_kwDORVI-G87tOhr2"; status="Done" }        # MM-ASP-03
    @{ num=4;  nid="I_kwDORVI-G87tOhxQ"; status="Done" }        # MM-ASP-04
    @{ num=5;  nid="I_kwDORVI-G87tOh1i"; status="Done" }        # MM-BE-01
    @{ num=6;  nid="I_kwDORVI-G87tOh3v"; status="Done" }        # MM-BE-02
    @{ num=7;  nid="I_kwDORVI-G87tOh6c"; status="Done" }        # MM-BE-03
    @{ num=8;  nid="I_kwDORVI-G87tOh_I"; status="Done" }        # MM-BE-04
    @{ num=9;  nid="I_kwDORVI-G87tOiBx"; status="Done" }        # MM-AI-01
    @{ num=10; nid="I_kwDORVI-G87tOiFn"; status="Done" }        # MM-AI-02
    @{ num=11; nid="I_kwDORVI-G87tOiIN"; status="Done" }        # MM-FE-01
    @{ num=12; nid="I_kwDORVI-G87tOiNX"; status="Done" }        # MM-FE-02
    @{ num=13; nid="I_kwDORVI-G87tOiQo"; status="Done" }        # MM-FE-03
    @{ num=14; nid="I_kwDORVI-G87tOiST"; status="Done" }        # MM-MOB-01

    # --- M2: Ledger Truth & Review Core ---
    @{ num=15; nid="I_kwDORVI-G87tOi3C"; status="Done" }        # MM-BE-05
    @{ num=16; nid="I_kwDORVI-G87tOi5M"; status="Done" }        # MM-BE-06
    @{ num=17; nid="I_kwDORVI-G87tOi8h"; status="Done" }        # MM-BE-12
    @{ num=18; nid="I_kwDORVI-G87tOi-v"; status="Done" }        # MM-BE-13
    @{ num=19; nid="I_kwDORVI-G87tOjBj"; status="Done" }        # MM-BE-14
    @{ num=20; nid="I_kwDORVI-G87tOjF8"; status="Done" }        # MM-FE-04
    @{ num=21; nid="I_kwDORVI-G87tOjHr"; status="Done" }        # MM-FE-05
    @{ num=22; nid="I_kwDORVI-G87tOjK3"; status="Not Started" } # MM-FE-09
    @{ num=23; nid="I_kwDORVI-G87tOjN9"; status="Done" }        # MM-MOB-02
    @{ num=24; nid="I_kwDORVI-G87tOjRE"; status="Done" }        # MM-MOB-03
    @{ num=25; nid="I_kwDORVI-G87tOjTk"; status="Done" }        # MM-MOB-04
    @{ num=26; nid="I_kwDORVI-G87tOjWi"; status="Done" }        # MM-MOB-05
    @{ num=27; nid="I_kwDORVI-G87tOjcR"; status="Not Started" } # MM-MOB-08
    @{ num=64; nid="I_kwDORVI-G87tRGKo"; status="Done" }        # MM-BE-15

    # --- M3: Ingestion, Recurring, Reimbursements & Projections (all Done) ---
    @{ num=28; nid="I_kwDORVI-G87tOkds"; status="Done" }        # MM-BE-07A
    @{ num=29; nid="I_kwDORVI-G87tOkhq"; status="Done" }        # MM-BE-07B
    @{ num=30; nid="I_kwDORVI-G87tOkk3"; status="Done" }        # MM-BE-07C
    @{ num=31; nid="I_kwDORVI-G87tOknV"; status="Done" }        # MM-BE-08A
    @{ num=32; nid="I_kwDORVI-G87tOks8"; status="Done" }        # MM-BE-08B
    @{ num=33; nid="I_kwDORVI-G87tOkwd"; status="Done" }        # MM-BE-08C
    @{ num=34; nid="I_kwDORVI-G87tOkz8"; status="Done" }        # MM-BE-09A
    @{ num=35; nid="I_kwDORVI-G87tOk3B"; status="Done" }        # MM-BE-09B
    @{ num=36; nid="I_kwDORVI-G87tOk5Y"; status="Done" }        # MM-FE-06
    @{ num=37; nid="I_kwDORVI-G87tOk8o"; status="Done" }        # MM-FE-07
    @{ num=38; nid="I_kwDORVI-G87tOlBb"; status="Done" }        # MM-MOB-06.1
    @{ num=39; nid="I_kwDORVI-G87tOlDv"; status="Done" }        # MM-MOB-06.2
    @{ num=40; nid="I_kwDORVI-G87tOlKo"; status="Done" }        # MM-MOB-06.3
    @{ num=41; nid="I_kwDORVI-G87tOlNJ"; status="Done" }        # MM-MOB-06.4
    @{ num=42; nid="I_kwDORVI-G87tOlOY"; status="Done" }        # MM-M3-GOV-01

    # --- M4: AI Escalation Pipeline ---
    @{ num=43; nid="I_kwDORVI-G87tOlWz"; status="Done" }        # MM-AI-03
    @{ num=44; nid="I_kwDORVI-G87tOlZd"; status="Done" }        # MM-AI-04
    @{ num=45; nid="I_kwDORVI-G87tOlcC"; status="Done" }        # MM-BE-10
    @{ num=46; nid="I_kwDORVI-G87tOleY"; status="Done" }        # MM-AI-05
    @{ num=47; nid="I_kwDORVI-G87tOljK"; status="Done" }        # MM-AI-06
    @{ num=48; nid="I_kwDORVI-G87tOloV"; status="Done" }        # MM-AI-07
    @{ num=49; nid="I_kwDORVI-G87tOlqE"; status="Done" }      # MM-AI-08
    @{ num=50; nid="I_kwDORVI-G87tOlr1"; status="Done" }      # MM-AI-09
    @{ num=51; nid="I_kwDORVI-G87tOltb"; status="Done" }      # MM-AI-10

    # --- M5: UX Completion & Release Gates ---
    @{ num=52; nid="I_kwDORVI-G87tOmSr"; status="Done" }        # MM-ASP-05
    @{ num=53; nid="I_kwDORVI-G87tOmXP"; status="Done" }        # MM-ASP-06
    @{ num=54; nid="I_kwDORVI-G87tOmY-"; status="Done" } # MM-ASP-07
    @{ num=55; nid="I_kwDORVI-G87tOmat"; status="Done" }        # MM-BE-11
    @{ num=56; nid="I_kwDORVI-G87tOmey"; status="Done" }        # MM-AI-11
    @{ num=57; nid="I_kwDORVI-G87tOmgs"; status="Done" }        # MM-FE-08
    @{ num=58; nid="I_kwDORVI-G87tOmjY"; status="Done" }        # MM-MOB-07.1
    @{ num=59; nid="I_kwDORVI-G87tOmlT"; status="Done" }        # MM-MOB-07.2
    @{ num=60; nid="I_kwDORVI-G87tOmnF"; status="Done" }        # MM-MOB-07.3
    @{ num=61; nid="I_kwDORVI-G87tOmq3"; status="Not Started" } # MM-QA-01
    @{ num=62; nid="I_kwDORVI-G87tOmsd"; status="Not Started" } # MM-QA-02
    @{ num=63; nid="I_kwDORVI-G87tOmtk"; status="Not Started" } # MM-QA-03
    @{ num=72; nid="I_kwDORVI-G87tkz1h"; status="Done" } # MM-BE-16
    @{ num=73; nid="I_kwDORVI-G87tkz5d"; status="Done" } # MM-BE-17
    @{ num=74; nid="I_kwDORVI-G87tk0AH"; status="Done" } # MM-BE-18
    @{ num=75; nid="I_kwDORVI-G87tk0EI"; status="Done" } # MM-FE-17

    # --- M6: UI Redesign & Theming ---
    @{ num=65; nid="I_kwDORVI-G87tUSTb"; status="In Review" } # MM-FE-10
    @{ num=66; nid="I_kwDORVI-G87tUSUp"; status="In Review" } # MM-FE-11
    @{ num=67; nid="I_kwDORVI-G87tUSWo"; status="In Review" } # MM-FE-12
    @{ num=68; nid="I_kwDORVI-G87tUSYH"; status="In Review" } # MM-FE-13
    @{ num=69; nid="I_kwDORVI-G87tUSZk"; status="In Review" } # MM-FE-14
    @{ num=70; nid="I_kwDORVI-G87tUSbK"; status="In Review" } # MM-FE-15
    @{ num=71; nid="I_kwDORVI-G87tUScw"; status="In Review" } # MM-FE-16
    @{ num=76; nid="I_kwDORVI-G87tmWNi"; status="Not Started" } # MM-FE-18
    @{ num=77; nid="I_kwDORVI-G87tmWOJ"; status="Not Started" } # MM-MOB-09
    @{ num=78; nid="I_kwDORVI-G87tnvvu"; status="Not Started" } # MM-AI-12
)

Write-Host "=== Phase 1: Ensure all $($issues.Count) issues are on the project board ==="
$itemMap = @{} # issue number -> project item ID

foreach ($issue in $issues) {
    $nid = $issue.nid
    $addQuery = @"
mutation {
  addProjectV2ItemById(input: {projectId: "$PROJECT_ID", contentId: "$nid"}) {
    item { id }
  }
}
"@
    $raw = gh api graphql -f query=$addQuery 2>&1
    try {
        $result = $raw | ConvertFrom-Json
        if ($result.data) {
            $itemId = $result.data.addProjectV2ItemById.item.id
            $itemMap[$issue.num] = $itemId
            Write-Host "  #$($issue.num): $itemId (added/confirmed)"
        } else {
            Write-Host "  #$($issue.num): ERROR - $raw" -ForegroundColor Red
        }
    } catch {
        Write-Host "  #$($issue.num): PARSE ERROR - $raw" -ForegroundColor Red
    }
}

Write-Host "`n=== Phase 2: Set correct statuses for all $($itemMap.Count) items ==="
$successCount = 0
$failCount = 0

foreach ($issue in $issues) {
    $itemId = $itemMap[$issue.num]
    if (-not $itemId) {
        Write-Host "  #$($issue.num): SKIP - no item ID" -ForegroundColor Yellow
        $failCount++
        continue
    }

    $optionId = $OPT[$issue.status]
    $statusQuery = @"
mutation {
  updateProjectV2ItemFieldValue(input: {
    projectId: "$PROJECT_ID"
    itemId: "$itemId"
    fieldId: "$FIELD_ID"
    value: { singleSelectOptionId: "$optionId" }
  }) {
    projectV2Item { id }
  }
}
"@
    $raw = gh api graphql -f query=$statusQuery 2>&1
    try {
        $result = $raw | ConvertFrom-Json
        if ($result.data) {
            Write-Host "  #$($issue.num) -> $($issue.status)" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "  #$($issue.num): STATUS ERROR - $raw" -ForegroundColor Red
            $failCount++
        }
    } catch {
        Write-Host "  #$($issue.num): PARSE ERROR - $raw" -ForegroundColor Red
        $failCount++
    }
}

Write-Host "`n=== Summary ==="
Write-Host "Total issues: $($issues.Count)"
Write-Host "Statuses set: $successCount"
Write-Host "Failures: $failCount"
