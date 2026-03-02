using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

public sealed record AgentReusablePromptDto(
    Guid Id,
    string Title,
    string PromptText,
    string Scope,
    bool IsFavorite,
    bool IsEditable,
    int DisplayOrder,
    int UsageCount,
    DateTime? LastUsedAtUtc,
    DateTime CreatedAtUtc,
    DateTime LastModifiedAtUtc,
    string? StableKey = null);

public sealed record AgentPromptLibraryDto(
    IReadOnlyList<AgentReusablePromptDto> Favorites,
    IReadOnlyList<AgentReusablePromptDto> UserPrompts,
    IReadOnlyList<AgentReusablePromptDto> BaselinePrompts);

public sealed class CreateAgentPromptRequest
{
    [Required]
    [MaxLength(120)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string PromptText { get; init; } = string.Empty;

    public bool IsFavorite { get; init; }
}

public sealed class UpdateAgentPromptRequest
{
    [MaxLength(120)]
    public string? Title { get; init; }

    [MaxLength(1000)]
    public string? PromptText { get; init; }

    public bool? IsFavorite { get; init; }
}

public sealed class AgentPromptUseRequest
{
    [MaxLength(120)]
    public string? ConversationId { get; init; }
}

public enum AgentPromptGenerationMode
{
    InitialPrompt = 1,
    ConversationReusable = 2,
}

public sealed class AgentPromptGenerationMessageRequest
{
    [Required]
    [MaxLength(20)]
    public string Role { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Text { get; init; } = string.Empty;
}

public sealed class GenerateAgentPromptSuggestionRequest
{
    [Required]
    [MaxLength(40)]
    public string Mode { get; init; } = nameof(AgentPromptGenerationMode.InitialPrompt);

    [MaxLength(2000)]
    public string? InitialPrompt { get; init; }

    public bool IncludePromptText { get; init; } = true;

    public IReadOnlyList<AgentPromptGenerationMessageRequest>? ConversationMessages { get; init; }
}

public sealed record AgentPromptSuggestionDto(
    string Mode,
    string Title,
    string? PromptText,
    string Model,
    string? SourceSummary);
