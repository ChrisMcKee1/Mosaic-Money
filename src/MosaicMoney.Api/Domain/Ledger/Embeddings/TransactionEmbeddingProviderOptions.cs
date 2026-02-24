namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public sealed class TransactionEmbeddingProviderOptions
{
    public const string SectionName = "AiWorkflow:Embeddings";

    public string Provider { get; init; } = "deterministic";

    public AzureOpenAiEmbeddingProviderOptions AzureOpenAI { get; init; } = new();

    public bool ShouldUseAzureOpenAi()
    {
        return string.Equals(Provider, "azure-openai", StringComparison.OrdinalIgnoreCase)
            && AzureOpenAI.IsConfigured();
    }
}

public sealed class AzureOpenAiEmbeddingProviderOptions
{
    public string Endpoint { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string Deployment { get; init; } = "text-embedding-3-small";

    public string ApiVersion { get; init; } = "2024-10-21";

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(Endpoint)
            && !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(Deployment);
    }
}