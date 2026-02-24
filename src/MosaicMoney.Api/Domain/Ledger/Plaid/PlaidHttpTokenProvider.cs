using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed class PlaidHttpTokenProvider(
    HttpClient httpClient,
    IOptions<PlaidOptions> plaidOptions,
    ILogger<PlaidHttpTokenProvider> logger) : IPlaidTokenProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<PlaidLinkTokenCreateResult> CreateLinkTokenAsync(
        PlaidLinkTokenCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = plaidOptions.Value;
        EnsureConfiguration(options);

        var payload = new
        {
            client_name = ResolveClientName(options),
            language = ResolveLanguage(options),
            country_codes = request.CountryCodes,
            products = request.Products,
            redirect_uri = request.RedirectUri,
            webhook = ResolveOptional(options.WebhookUrl),
            user = new
            {
                client_user_id = request.ClientUserId,
            },
        };

        using var response = await SendRequestAsync(
            path: "/link/token/create",
            environment: request.Environment,
            payload,
            cancellationToken);

        var responseRoot = await ReadResponseJsonAsync(response, "/link/token/create", cancellationToken);
        var linkToken = GetRequiredString(responseRoot, "link_token");
        var expirationText = GetRequiredString(responseRoot, "expiration");
        var requestId = GetRequiredString(responseRoot, "request_id");

        if (!DateTime.TryParse(
                expirationText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var expiresAtUtc))
        {
            throw new InvalidOperationException("Plaid /link/token/create response did not include a valid expiration value.");
        }

        return new PlaidLinkTokenCreateResult(
            linkToken,
            expiresAtUtc,
            request.Environment,
            request.Products,
            request.OAuthEnabled,
            request.RedirectUri,
            requestId);
    }

    public async Task<PlaidPublicTokenExchangeResult> ExchangePublicTokenAsync(
        PlaidPublicTokenExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = plaidOptions.Value;
        EnsureConfiguration(options);

        var payload = new
        {
            public_token = request.PublicToken,
        };

        using var response = await SendRequestAsync(
            path: "/item/public_token/exchange",
            environment: request.Environment,
            payload,
            cancellationToken);

        var responseRoot = await ReadResponseJsonAsync(response, "/item/public_token/exchange", cancellationToken);

        return new PlaidPublicTokenExchangeResult(
            ItemId: GetRequiredString(responseRoot, "item_id"),
            AccessToken: GetRequiredString(responseRoot, "access_token"),
            Environment: request.Environment,
            InstitutionId: request.InstitutionId,
            RequestId: GetRequiredString(responseRoot, "request_id"));
    }

    public async Task<PlaidTransactionsSyncBootstrapResult> BootstrapTransactionsSyncAsync(
        PlaidTransactionsSyncBootstrapRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = plaidOptions.Value;
        EnsureConfiguration(options);

        var normalizedCursor = string.IsNullOrWhiteSpace(request.Cursor)
            ? ""
            : request.Cursor.Trim();
        var normalizedCount = Math.Clamp(request.Count, 1, 500);

        var payload = new
        {
            access_token = request.AccessToken,
            cursor = normalizedCursor,
            count = normalizedCount,
        };

        using var response = await SendRequestAsync(
            path: "/transactions/sync",
            environment: request.Environment,
            payload,
            cancellationToken);

        var responseRoot = await ReadResponseJsonAsync(response, "/transactions/sync", cancellationToken);
        var nextCursor = GetOptionalString(responseRoot, "next_cursor") ?? normalizedCursor;
        var hasMore = GetOptionalBool(responseRoot, "has_more");

        return new PlaidTransactionsSyncBootstrapResult(
            nextCursor,
            hasMore,
            GetRequiredString(responseRoot, "request_id"));
    }

    public async Task<PlaidTransactionsSyncPullResult> PullTransactionsSyncAsync(
        PlaidTransactionsSyncPullRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = plaidOptions.Value;
        EnsureConfiguration(options);

        var normalizedCursor = string.IsNullOrWhiteSpace(request.Cursor)
            ? string.Empty
            : request.Cursor.Trim();
        var normalizedCount = Math.Clamp(request.Count, 1, 500);

        var payload = new
        {
            access_token = request.AccessToken,
            cursor = normalizedCursor,
            count = normalizedCount,
        };

        using var response = await SendRequestAsync(
            path: "/transactions/sync",
            environment: request.Environment,
            payload,
            cancellationToken);

        var responseRoot = await ReadResponseJsonAsync(response, "/transactions/sync", cancellationToken);
        var nextCursor = GetOptionalString(responseRoot, "next_cursor") ?? normalizedCursor;
        var hasMore = GetOptionalBool(responseRoot, "has_more");

        return new PlaidTransactionsSyncPullResult(
            nextCursor,
            hasMore,
            GetRequiredString(responseRoot, "request_id"),
            ParseAccounts(responseRoot),
            ParseDeltaTransactions(responseRoot, "added"),
            ParseDeltaTransactions(responseRoot, "modified"),
            ParseRemovedTransactionIds(responseRoot));
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        string path,
        string environment,
        object payload,
        CancellationToken cancellationToken)
    {
        var options = plaidOptions.Value;
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, BuildEndpointUri(environment, path));

        requestMessage.Headers.TryAddWithoutValidation("PLAID-CLIENT-ID", options.ClientId.Trim());
        requestMessage.Headers.TryAddWithoutValidation("PLAID-SECRET", options.Secret.Trim());

        var body = JsonSerializer.Serialize(payload, SerializerOptions);
        requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return await httpClient.SendAsync(requestMessage, cancellationToken);
    }

    private async Task<JsonElement> ReadResponseJsonAsync(
        HttpResponseMessage response,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreatePlaidException(endpoint, response.StatusCode, body);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Plaid {endpoint} response was not valid JSON.", ex);
        }
    }

    private PlaidApiException CreatePlaidException(string endpoint, HttpStatusCode statusCode, string body)
    {
        string? requestId = null;
        string? errorCode = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                requestId = GetOptionalString(document.RootElement, "request_id");
                errorCode = GetOptionalString(document.RootElement, "error_code");
            }
            catch (JsonException)
            {
                // Ignore parse failures and fall back to status-only error context.
            }
        }

        logger.LogWarning(
            "Plaid endpoint {Endpoint} failed with HTTP {StatusCode}, request id {RequestId}, error code {ErrorCode}.",
            endpoint,
            (int)statusCode,
            requestId ?? "n/a",
            errorCode ?? "n/a");

        return new PlaidApiException(
            endpoint,
            statusCode,
            requestId,
            errorCode,
            $"Plaid request failed for {endpoint} with HTTP {(int)statusCode}. RequestId={requestId ?? "n/a"}, ErrorCode={errorCode ?? "n/a"}.");
    }

    private static IReadOnlyList<PlaidTransactionsSyncAccount> ParseAccounts(JsonElement root)
    {
        if (!root.TryGetProperty("accounts", out var accountsElement) || accountsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var accounts = new List<PlaidTransactionsSyncAccount>();

        foreach (var accountElement in accountsElement.EnumerateArray())
        {
            var accountId = GetOptionalString(accountElement, "account_id");
            if (string.IsNullOrWhiteSpace(accountId))
            {
                continue;
            }

            accounts.Add(new PlaidTransactionsSyncAccount(
                accountId.Trim(),
                GetOptionalString(accountElement, "name")?.Trim() ?? string.Empty,
                ResolveOptional(GetOptionalString(accountElement, "official_name")),
                ResolveOptional(GetOptionalString(accountElement, "mask")),
                ResolveOptional(GetOptionalString(accountElement, "type")),
                ResolveOptional(GetOptionalString(accountElement, "subtype"))));
        }

        return accounts;
    }

    private static IReadOnlyList<PlaidTransactionsSyncDeltaTransaction> ParseDeltaTransactions(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var transactionsElement)
            || transactionsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var transactions = new List<PlaidTransactionsSyncDeltaTransaction>();

        foreach (var transactionElement in transactionsElement.EnumerateArray())
        {
            var transactionId = GetOptionalString(transactionElement, "transaction_id");
            var accountId = GetOptionalString(transactionElement, "account_id");
            if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(accountId))
            {
                continue;
            }

            var description = GetOptionalString(transactionElement, "name")?.Trim() ?? string.Empty;
            var merchantName = ResolveOptional(GetOptionalString(transactionElement, "merchant_name"));
            var amount = GetOptionalDecimal(transactionElement, "amount");
            var transactionDate = GetOptionalDateOnly(transactionElement, "date");
            var pending = GetOptionalBool(transactionElement, "pending");

            transactions.Add(new PlaidTransactionsSyncDeltaTransaction(
                transactionId.Trim(),
                accountId.Trim(),
                description,
                merchantName,
                amount,
                transactionDate,
                transactionElement.GetRawText(),
                pending));
        }

        return transactions;
    }

    private static IReadOnlyList<string> ParseRemovedTransactionIds(JsonElement root)
    {
        if (!root.TryGetProperty("removed", out var removedElement) || removedElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var removed = new List<string>();
        foreach (var removedTransaction in removedElement.EnumerateArray())
        {
            var transactionId = GetOptionalString(removedTransaction, "transaction_id");
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                removed.Add(transactionId.Trim());
            }
        }

        return removed;
    }

    private static string BuildEndpointUri(string environment, string path)
    {
        var normalizedEnvironment = string.IsNullOrWhiteSpace(environment)
            ? "sandbox"
            : environment.Trim().ToLowerInvariant();

        var baseUri = normalizedEnvironment switch
        {
            "sandbox" => "https://sandbox.plaid.com",
            "development" => "https://development.plaid.com",
            "production" => "https://production.plaid.com",
            _ => throw new InvalidOperationException($"Unsupported Plaid environment '{environment}'."),
        };

        return string.Concat(baseUri, path);
    }

    private static void EnsureConfiguration(PlaidOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new InvalidOperationException(
                "Plaid ClientId/Secret are not configured. Provide values through AppHost parameters and user-secrets.");
        }
    }

    private static string ResolveClientName(PlaidOptions options)
    {
        var value = string.IsNullOrWhiteSpace(options.ClientName)
            ? "Mosaic Money"
            : options.ClientName.Trim();

        return value.Length > 30 ? value[..30] : value;
    }

    private static string ResolveLanguage(PlaidOptions options)
    {
        return string.IsNullOrWhiteSpace(options.Language)
            ? "en"
            : options.Language.Trim().ToLowerInvariant();
    }

    private static string? ResolveOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        var value = GetOptionalString(root, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Plaid response missing required '{propertyName}' field.");
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool GetOptionalBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        return property.GetBoolean();
    }

    private static decimal GetOptionalDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return 0m;
        }

        return property.GetDecimal();
    }

    private static DateOnly? GetOptionalDateOnly(JsonElement root, string propertyName)
    {
        var value = GetOptionalString(root, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
