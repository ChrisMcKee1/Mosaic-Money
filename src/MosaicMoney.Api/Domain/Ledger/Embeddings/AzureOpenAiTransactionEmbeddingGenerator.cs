using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public sealed class AzureOpenAiTransactionEmbeddingGenerator(
    HttpClient httpClient,
    IOptions<TransactionEmbeddingProviderOptions> options) : ITransactionEmbeddingGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var normalizedText = EmbeddingTextHasher.Normalize(text);
        if (normalizedText.Length == 0)
        {
            return new float[DeterministicTransactionEmbeddingGenerator.EmbeddingDimensions];
        }

        var provider = options.Value.AzureOpenAI;
        if (!provider.IsConfigured())
        {
            throw new InvalidOperationException(
                "AiWorkflow:Embeddings:AzureOpenAI requires Endpoint, ApiKey, and Deployment when Provider is set to azure-openai.");
        }

        var endpoint = provider.Endpoint.TrimEnd('/');
        var deployment = Uri.EscapeDataString(provider.Deployment);
        var apiVersion = Uri.EscapeDataString(provider.ApiVersion);
        var requestUri = $"{endpoint}/openai/deployments/{deployment}/embeddings?api-version={apiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(new { input = normalizedText }, options: JsonOptions),
        };
        request.Headers.Add("api-key", provider.ApiKey);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Azure OpenAI embedding request failed with status {(int)response.StatusCode}: {Truncate(body, 512)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var dataElement)
            || dataElement.ValueKind != JsonValueKind.Array
            || dataElement.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Azure OpenAI embedding response did not include a data array.");
        }

        var first = dataElement[0];
        if (!first.TryGetProperty("embedding", out var embeddingElement)
            || embeddingElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Azure OpenAI embedding response did not include data[0].embedding.");
        }

        var dimensions = embeddingElement.GetArrayLength();
        if (dimensions != DeterministicTransactionEmbeddingGenerator.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding dimension mismatch. Expected {DeterministicTransactionEmbeddingGenerator.EmbeddingDimensions}, received {dimensions}. Configure a 1536-dimension model such as text-embedding-3-small.");
        }

        var values = new float[dimensions];
        for (var index = 0; index < dimensions; index++)
        {
            values[index] = embeddingElement[index].GetSingle();
        }

        return values;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}