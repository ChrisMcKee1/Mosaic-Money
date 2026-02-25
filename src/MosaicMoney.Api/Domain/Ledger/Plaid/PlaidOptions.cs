namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed class PlaidOptions
{
    public const string SectionName = "Plaid";
    public const int TransactionsHistoryDaysRequestedMinimum = 30;
    public const int TransactionsHistoryDaysRequestedMaximum = 730;
    public const int TransactionsHistoryDaysRequestedDefault = 730;

    public string Environment { get; init; } = "sandbox";

    public string ClientId { get; init; } = string.Empty;

    public string Secret { get; init; } = string.Empty;

    public string ClientName { get; init; } = "Mosaic Money";

    public string Language { get; init; } = "en";

    public IReadOnlyList<string> Products { get; init; } = ["transactions"];

    public string? RedirectUri { get; init; }

    public string? WebhookUrl { get; init; }

    public IReadOnlyList<string> CountryCodes { get; init; } = ["US"];

    public string TransactionsSyncBootstrapCursor { get; init; } = "now";

    public int TransactionsSyncBootstrapCount { get; init; } = 1;

    public int TransactionsHistoryDaysRequested { get; init; } = TransactionsHistoryDaysRequestedDefault;

    public bool UseDeterministicProvider { get; init; }
}
