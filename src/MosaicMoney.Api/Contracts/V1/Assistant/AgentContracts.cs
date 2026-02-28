using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

public enum AssistantApprovalDecision
{
    Approve = 1,
    Reject = 2,
}

public sealed class AssistantConversationMessageRequest
{
    [Required]
    [MaxLength(2000)]
    public string Message { get; init; } = string.Empty;

    [MaxLength(120)]
    public string? ClientMessageId { get; init; }

    [MaxLength(600)]
    public string? UserNote { get; init; }
}

public sealed class AssistantApprovalRequest
{
    [Required]
    public string Decision { get; init; } = nameof(AssistantApprovalDecision.Approve);

    [MaxLength(120)]
    public string? ClientApprovalId { get; init; }

    [MaxLength(500)]
    public string? Rationale { get; init; }
}

public sealed record AssistantCommandAcceptedDto(
    Guid CommandId,
    string CorrelationId,
    Guid ConversationId,
    string CommandType,
    string Queue,
    string PolicyDisposition,
    DateTime QueuedAtUtc,
    string Status);

public sealed record AssistantConversationRunStatusDto(
    Guid RunId,
    string CorrelationId,
    string Status,
    string TriggerSource,
    string? FailureCode,
    string? FailureRationale,
    DateTime CreatedAtUtc,
    DateTime LastModifiedAtUtc,
    DateTime? CompletedAtUtc,
    string? AgentName = null,
    string? AgentSource = null,
    string? LatestStageOutcomeSummary = null,
    string? AssignmentHint = null);

public sealed record AssistantConversationStreamDto(
    Guid ConversationId,
    IReadOnlyList<AssistantConversationRunStatusDto> Runs);