$ErrorActionPreference = 'Stop'

$resourcesOutput = aspire resources --project src/apphost.cs | Out-String
$apiBaseUrl = ([regex]::Match($resourcesOutput, 'https://localhost:\d+')).Value
if ([string]::IsNullOrWhiteSpace($apiBaseUrl)) {
    throw 'Unable to determine API HTTPS endpoint from Aspire resources output.'
}
Invoke-RestMethod -Method Get -Uri "$apiBaseUrl/health" | Out-Null

$secretLines = dotnet user-secrets list --file src/apphost.cs
$plaidClientId = ($secretLines | Where-Object { $_ -like 'Parameters:plaid-client-id = *' } | ForEach-Object { $_.Split(' = ', 2)[1] } | Select-Object -First 1)
$plaidSecret = ($secretLines | Where-Object { $_ -like 'Parameters:plaid-secret = *' } | ForEach-Object { $_.Split(' = ', 2)[1] } | Select-Object -First 1)
$connString = ($secretLines | Where-Object { $_ -like 'ConnectionStrings:mosaicmoneydb = *' } | ForEach-Object { $_.Split(' = ', 2)[1] } | Select-Object -First 1)

if ([string]::IsNullOrWhiteSpace($plaidClientId) -or [string]::IsNullOrWhiteSpace($plaidSecret)) {
    throw 'Plaid secrets are missing from AppHost user-secrets.'
}

if ([string]::IsNullOrWhiteSpace($connString)) {
    throw 'ConnectionStrings:mosaicmoneydb is missing from AppHost user-secrets.'
}

$npgsqlPath = Resolve-Path 'src/MosaicMoney.Api/bin/Debug/net10.0/Npgsql.dll'
Add-Type -Path $npgsqlPath

function Invoke-ScalarSql {
    param(
        [string]$ConnectionString,
        [string]$Sql
    )

    $connection = [Npgsql.NpgsqlConnection]::new($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Sql
        return $command.ExecuteScalar()
    }
    finally {
        $connection.Dispose()
    }
}

function Get-Counts {
    param([string]$ConnectionString)

    return [pscustomobject]@{
        PlaidItemCredentials = [int](Invoke-ScalarSql -ConnectionString $ConnectionString -Sql 'SELECT COUNT(*) FROM "PlaidItemCredentials";')
        PlaidItemSyncStates = [int](Invoke-ScalarSql -ConnectionString $ConnectionString -Sql 'SELECT COUNT(*) FROM "PlaidItemSyncStates";')
        RawTransactionIngestionRecords = [int](Invoke-ScalarSql -ConnectionString $ConnectionString -Sql 'SELECT COUNT(*) FROM "RawTransactionIngestionRecords";')
        EnrichedTransactions = [int](Invoke-ScalarSql -ConnectionString $ConnectionString -Sql 'SELECT COUNT(*) FROM "EnrichedTransactions";')
    }
}

$before = Get-Counts -ConnectionString $connString

$householdId = [string](Invoke-ScalarSql -ConnectionString $connString -Sql 'SELECT "Id"::text FROM "Households" ORDER BY "CreatedAtUtc" LIMIT 1;')
if ([string]::IsNullOrWhiteSpace($householdId)) {
    $householdId = [Guid]::NewGuid().ToString()
    Invoke-ScalarSql -ConnectionString $connString -Sql "INSERT INTO \"Households\" (\"Id\",\"Name\",\"CreatedAtUtc\") VALUES ('$householdId', 'Plaid Sandbox Household', now() at time zone 'utc'); SELECT 1;" | Out-Null
}

$publicTokenRequest = @{
    client_id = $plaidClientId
    secret = $plaidSecret
    institution_id = 'ins_109508'
    initial_products = @('transactions')
    options = @{
        override_username = 'user_good'
        override_password = 'pass_good'
    }
} | ConvertTo-Json -Depth 6

$publicTokenResponse = Invoke-RestMethod -Method Post -Uri 'https://sandbox.plaid.com/sandbox/public_token/create' -Body $publicTokenRequest -ContentType 'application/json'
$publicToken = $publicTokenResponse.public_token
if ([string]::IsNullOrWhiteSpace($publicToken)) {
    throw 'Plaid sandbox did not return a public_token.'
}

$exchangeRequest = @{
    householdId = $householdId
    publicToken = $publicToken
    institutionId = 'ins_109508'
    clientMetadataJson = '{"source":"aspire-cli-validation"}'
} | ConvertTo-Json -Depth 5

$exchangeResponse = Invoke-RestMethod -Method Post -Uri "$apiBaseUrl/api/v1/plaid/public-token-exchange" -Body $exchangeRequest -ContentType 'application/json'

Start-Sleep -Seconds 12

$after = Get-Counts -ConnectionString $connString

$transactions = Invoke-RestMethod -Method Get -Uri "$apiBaseUrl/api/v1/transactions?page=1&pageSize=5"
$transactionCount = @($transactions).Count

[pscustomobject]@{
    ApiBaseUrl = $apiBaseUrl
    HouseholdIdUsed = $householdId
    ExchangeItemId = $exchangeResponse.itemId
    Before = $before
    After = $after
    Delta = [pscustomobject]@{
        PlaidItemCredentials = ($after.PlaidItemCredentials - $before.PlaidItemCredentials)
        PlaidItemSyncStates = ($after.PlaidItemSyncStates - $before.PlaidItemSyncStates)
        RawTransactionIngestionRecords = ($after.RawTransactionIngestionRecords - $before.RawTransactionIngestionRecords)
        EnrichedTransactions = ($after.EnrichedTransactions - $before.EnrichedTransactions)
    }
    TransactionsEndpointTop5Count = $transactionCount
} | ConvertTo-Json -Depth 6
