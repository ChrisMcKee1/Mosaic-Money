#pragma warning disable OPENAI001
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Options;
using MosaicMoney.Api.Contracts.V1;
using OpenAI;
using OpenAI.Responses;

namespace MosaicMoney.Api.Domain.Ledger.AgentPrompts;

public sealed class AgentPromptGenerationOptions
{
    public const string SectionName = "AiWorkflow:PromptGeneration:ModelRouter";

    public bool Enabled { get; init; } = true;

    public string Endpoint { get; init; } = string.Empty;

    public string Deployment { get; init; } = "model-router";

    public bool UseDefaultAzureCredential { get; init; } = true;

    public string AzureAiScope { get; init; } = "https://cognitiveservices.azure.com/.default";

    public string ApiKey { get; init; } = string.Empty;

    public float Temperature { get; init; } = 0.35f;

    public bool IsConfigured()
    {
        var hasAuth = UseDefaultAzureCredential || !string.IsNullOrWhiteSpace(ApiKey);
        return Enabled
            && !string.IsNullOrWhiteSpace(Endpoint)
            && !string.IsNullOrWhiteSpace(Deployment)
            && hasAuth;
    }

    public string GetResponsesEndpoint()
    {
        var trimmed = Endpoint.Trim().TrimEnd('/');

        if (trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"{trimmed}/openai/v1";
    }
}

public sealed record AgentPromptGenerationMessage(string Role, string Text);

public sealed record AgentPromptGenerationInput(
    AgentPromptGenerationMode Mode,
    string? InitialPrompt,
    bool IncludePromptText,
    IReadOnlyList<AgentPromptGenerationMessage> ConversationMessages);

public sealed record AgentPromptGenerationResult(
    string Title,
    string? PromptText,
    string Model,
    string? SourceSummary);

public interface IAgentPromptGenerationService
{
    bool IsConfigured { get; }

    Task<AgentPromptGenerationResult?> GenerateAsync(
        AgentPromptGenerationInput input,
        CancellationToken cancellationToken = default);
}

public sealed class AgentPromptGenerationService(
    IOptions<AgentPromptGenerationOptions> options,
    ILogger<AgentPromptGenerationService> logger) : IAgentPromptGenerationService
{
    private readonly DefaultAzureCredential tokenCredential = new();

    public bool IsConfigured => options.Value.IsConfigured();

    public async Task<AgentPromptGenerationResult?> GenerateAsync(
        AgentPromptGenerationInput input,
        CancellationToken cancellationToken = default)
    {
        var generationOptions = options.Value;
        if (!generationOptions.IsConfigured())
        {
            return null;
        }

        var userInstruction = BuildUserInstruction(input);
        var outputText = await GenerateWithResponsesAsync(generationOptions, userInstruction, cancellationToken);

        if (!string.IsNullOrWhiteSpace(outputText)
            && TryParseOutput(outputText, input.IncludePromptText, generationOptions.Deployment, out var parsed))
        {
            return parsed;
        }

        logger.LogWarning(
            "Model-router prompt generation fell back to deterministic output. Mode={Mode}.",
            input.Mode);

        return BuildFallback(input, generationOptions.Deployment);
    }

    private async Task<string?> GenerateWithResponsesAsync(
        AgentPromptGenerationOptions generationOptions,
        string instruction,
        CancellationToken cancellationToken)
    {
        try
        {
            if (generationOptions.UseDefaultAzureCredential)
            {
                var tokenPolicy = new BearerTokenPolicy(
                    tokenCredential,
                    generationOptions.AzureAiScope.Trim());

                var client = new ResponsesClient(
                    authenticationPolicy: tokenPolicy,
                    options: new OpenAIClientOptions
                    {
                        Endpoint = new Uri(generationOptions.GetResponsesEndpoint()),
                    });

                var responseResult = await client.CreateResponseAsync(
                    model: generationOptions.Deployment,
                    userInputText: instruction,
                    previousResponseId: null,
                    cancellationToken: cancellationToken);
                return responseResult.Value.GetOutputText();
            }

            var apiKeyClient = new ResponsesClient(
                credential: new ApiKeyCredential(generationOptions.ApiKey),
                options: new OpenAIClientOptions
                {
                    Endpoint = new Uri(generationOptions.GetResponsesEndpoint()),
                });

            var response = await apiKeyClient.CreateResponseAsync(
                model: generationOptions.Deployment,
                userInputText: instruction,
                previousResponseId: null,
                cancellationToken: cancellationToken);

            return response.Value.GetOutputText();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Model-router prompt generation request failed.");
            return null;
        }
    }

    private static bool TryParseOutput(
        string outputText,
        bool includePromptText,
        string deployment,
        out AgentPromptGenerationResult? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(outputText))
        {
            return false;
        }

        var normalized = ExtractJsonObject(outputText.Trim());

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(normalized);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            var title = root.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString()?.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            var promptText = root.TryGetProperty("promptText", out var promptElement)
                ? promptElement.GetString()?.Trim()
                : null;

            if (includePromptText && string.IsNullOrWhiteSpace(promptText))
            {
                return false;
            }

            result = new AgentPromptGenerationResult(
                Title: Truncate(title, 120),
                PromptText: includePromptText ? Truncate(promptText!, 1000) : null,
                Model: deployment,
                SourceSummary: root.TryGetProperty("sourceSummary", out var summaryElement)
                    ? Truncate(summaryElement.GetString(), 220)
                    : null);

            return true;
        }
    }

    private static AgentPromptGenerationResult BuildFallback(AgentPromptGenerationInput input, string deployment)
    {
        var basePrompt = ResolveBasePrompt(input);
        var fallbackTitle = BuildFallbackTitle(basePrompt);

        return new AgentPromptGenerationResult(
            Title: fallbackTitle,
            PromptText: input.IncludePromptText ? Truncate(basePrompt, 1000) : null,
            Model: deployment,
            SourceSummary: "Generated from available prompt context.");
    }

    private static string ResolveBasePrompt(AgentPromptGenerationInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.InitialPrompt))
        {
            return input.InitialPrompt.Trim();
        }

        var latestUserMessage = input.ConversationMessages
            .LastOrDefault(x => string.Equals(x.Role, "user", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(latestUserMessage?.Text))
        {
            return latestUserMessage.Text.Trim();
        }

        return "Summarize my current financial conversation and provide practical next actions.";
    }

    private static string BuildFallbackTitle(string prompt)
    {
        var words = prompt
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(6)
            .ToArray();

        if (words.Length == 0)
        {
            return "Reusable Prompt";
        }

        var candidate = string.Join(" ", words);
        return Truncate(candidate, 120);
    }

    private static string BuildUserInstruction(AgentPromptGenerationInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You produce reusable financial assistant prompts for Mosaic Money.");
        builder.AppendLine("Return STRICT JSON only with this schema:");
        builder.AppendLine("{\"title\":\"string\",\"promptText\":\"string or null\",\"sourceSummary\":\"string\"}");
        builder.AppendLine("Rules:");
        builder.AppendLine("1) Title must be specific, action-oriented, and <= 120 characters.");
        builder.AppendLine("2) Never include markdown, code fences, or extra keys.");
        builder.AppendLine("3) Preserve user intent and constraints.");

        if (input.Mode == AgentPromptGenerationMode.InitialPrompt)
        {
            builder.AppendLine("4) Mode=InitialPrompt: generate a title from the initial prompt.");
            if (input.IncludePromptText)
            {
                builder.AppendLine("5) Polish the initial prompt for spelling, grammar, and organization while preserving all functional intent.");
            }
            else
            {
                builder.AppendLine("5) Set promptText to null.");
            }
        }
        else
        {
            builder.AppendLine("4) Mode=ConversationReusable: synthesize a future reusable prompt from conversation context + initial prompt.");
            if (input.IncludePromptText)
            {
                builder.AppendLine("5) promptText should be concise, actionable, and <= 1000 characters.");
            }
            else
            {
                builder.AppendLine("5) Set promptText to null.");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"Mode: {input.Mode}");
        builder.AppendLine($"IncludePromptText: {input.IncludePromptText}");
        builder.AppendLine("InitialPrompt:");
        builder.AppendLine(string.IsNullOrWhiteSpace(input.InitialPrompt) ? "(none)" : input.InitialPrompt.Trim());

        builder.AppendLine();
        builder.AppendLine("ConversationMessages (latest first relevance, oldest to newest order shown):");

        if (input.ConversationMessages.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var message in input.ConversationMessages.TakeLast(14))
            {
                var role = NormalizeRole(message.Role);
                var text = Truncate(message.Text, 320);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                builder.AppendLine($"- {role}: {text}");
            }
        }

        return builder.ToString();
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "user";
        }

        return role.Trim().ToLowerInvariant() switch
        {
            "assistant" => "assistant",
            "system" => "system",
            _ => "user",
        };
    }

    private static string ExtractJsonObject(string value)
    {
        var start = value.IndexOf('{');
        var end = value.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return value[start..(end + 1)];
        }

        return value;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
