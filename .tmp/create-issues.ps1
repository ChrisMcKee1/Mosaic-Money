$issues = @(
    @{ title = "MM-FE-10: Global Layout & Theming"; body = "Implement Dark/Light mode toggle, CSS variable color system, distinctive typography, and the main application shell (Left Sidebar, Main Content, Right Context Panel)." },
    @{ title = "MM-FE-11: Dashboard Overview Screen"; body = "Implement Monthly spending line chart, Net worth line chart, Transactions to review widget, Top categories summary, and Next two weeks recurring widget." },
    @{ title = "MM-FE-12: Accounts Screen"; body = "Implement Assets/Debts summary chart, and grouped lists for Credit cards, Depository, Investment, Loan, and Real estate with sparklines. Right panel for specific account details." },
    @{ title = "MM-FE-13: Transactions Screen"; body = "Implement grouped transaction list (Today, Yesterday, etc.) with category tags and amounts. Right panel for transaction details, categorization, and history." },
    @{ title = "MM-FE-14: Categories & Budgeting Screen"; body = "Implement total spent vs budget donut chart, detailed progress bars for regular categories. Right panel for category breakdown and historical bar chart." },
    @{ title = "MM-FE-15: Investments Screen"; body = "Implement live balance estimate chart, top movers widget, and account list with 1W balance change. Right panel for specific asset details (e.g., Crypto chart and positions)." },
    @{ title = "MM-FE-16: Recurrings Screen"; body = "Implement left to pay vs paid so far donut chart, list of recurring transactions with status (paid, overdue, upcoming). Right panel for recurring rule details and history." }
)

$projectId = "PVT_kwHOAYj6Kc4BP962"
$statusFieldId = "PVTSSF_lAHOAYj6Kc4BP962zg-OQcQ"
$inProgressOptionId = "3e144626"

foreach ($issue in $issues) {
    Write-Host "Creating issue: $($issue.title)"
    $issueUrl = gh issue create --repo ChrisMcKee1/Mosaic-Money --title $issue.title --body $issue.body
    $issueNumber = ($issueUrl -split "/")[-1]
    
    Write-Host "Getting node ID for issue #$issueNumber"
    $nodeId = gh issue view $issueNumber --repo ChrisMcKee1/Mosaic-Money --json id --jq .id
    
    Write-Host "Adding issue to project board"
    $addResult = gh api graphql -f query="mutation { addProjectV2ItemById(input: {projectId: `"$projectId`", contentId: `"$nodeId`"}) { item { id } } }" | ConvertFrom-Json
    $itemId = $addResult.data.addProjectV2ItemById.item.id
    
    Write-Host "Setting status to In Progress"
    gh api graphql -f query="mutation { updateProjectV2ItemFieldValue(input: {projectId: `"$projectId`", itemId: `"$itemId`", fieldId: `"$statusFieldId`", value: { singleSelectOptionId: `"$inProgressOptionId`" }}) { projectV2Item { id } } }" | Out-Null
    
    Write-Host "Done with $($issue.title)`n"
}
