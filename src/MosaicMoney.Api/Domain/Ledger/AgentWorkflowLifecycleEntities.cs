using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MosaicMoney.Api.Domain.Ledger;

public enum AgentRunStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    NeedsReview = 5,
    Cancelled = 6,
}

public enum AgentRunStageStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    NeedsReview = 5,
    Skipped = 6,
}

public enum AgentSignalSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
}

public enum AgentDecisionOutcome
{
    Applied = 1,
    NeedsReview = 2,
    Rejected = 3,
    Deferred = 4,
}

public enum IdempotencyKeyStatus
{
    Reserved = 1,
    Completed = 2,
    Rejected = 3,
    Expired = 4,
}

public sealed class AgentRun
{
    public Guid Id { get; set; }

    public Guid? HouseholdId { get; set; }

    [MaxLength(120)]
    public string CorrelationId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string WorkflowName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string TriggerSource { get; set; } = string.Empty;

    [MaxLength(80)]
    public string PolicyVersion { get; set; } = string.Empty;

    public AgentRunStatus Status { get; set; } = AgentRunStatus.Pending;

    [MaxLength(120)]
    public string? FailureCode { get; set; }

    [MaxLength(500)]
    public string? FailureRationale { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public Household? Household { get; set; }

    public ICollection<AgentRunStage> Stages { get; set; } = new List<AgentRunStage>();

    public ICollection<AgentSignal> Signals { get; set; } = new List<AgentSignal>();

    public ICollection<AgentDecisionAudit> DecisionAudits { get; set; } = new List<AgentDecisionAudit>();

    public ICollection<IdempotencyKey> IdempotencyKeys { get; set; } = new List<IdempotencyKey>();
}

public sealed class AgentRunStage
{
    public Guid Id { get; set; }

    public Guid AgentRunId { get; set; }

    [MaxLength(120)]
    public string StageName { get; set; } = string.Empty;

    [Range(1, 64)]
    public int StageOrder { get; set; } = 1;

    [MaxLength(120)]
    public string Executor { get; set; } = string.Empty;

    public AgentRunStageStatus Status { get; set; } = AgentRunStageStatus.Pending;

    [Precision(5, 4)]
    public decimal? Confidence { get; set; }

    [MaxLength(120)]
    public string? OutcomeCode { get; set; }

    [MaxLength(500)]
    public string? OutcomeRationale { get; set; }

    [MaxLength(600)]
    public string? AgentNoteSummary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public AgentRun AgentRun { get; set; } = null!;

    public ICollection<AgentSignal> Signals { get; set; } = new List<AgentSignal>();

    public ICollection<AgentDecisionAudit> DecisionAudits { get; set; } = new List<AgentDecisionAudit>();
}

public sealed class AgentSignal
{
    public Guid Id { get; set; }

    public Guid AgentRunId { get; set; }

    public Guid? AgentRunStageId { get; set; }

    [MaxLength(120)]
    public string SignalCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Summary { get; set; } = string.Empty;

    public AgentSignalSeverity Severity { get; set; } = AgentSignalSeverity.Warning;

    public bool RequiresHumanReview { get; set; } = true;

    public bool IsResolved { get; set; }

    public DateTime RaisedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }

    public string? PayloadJson { get; set; }

    public AgentRun AgentRun { get; set; } = null!;

    public AgentRunStage? AgentRunStage { get; set; }
}

public sealed class AgentDecisionAudit
{
    public Guid Id { get; set; }

    public Guid AgentRunId { get; set; }

    public Guid? AgentRunStageId { get; set; }

    public AgentDecisionOutcome Outcome { get; set; } = AgentDecisionOutcome.NeedsReview;

    public TransactionReviewStatus ReviewStatus { get; set; } = TransactionReviewStatus.NeedsReview;

    [MaxLength(120)]
    public string DecisionType { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ReasonCode { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Rationale { get; set; } = string.Empty;

    [MaxLength(80)]
    public string PolicyVersion { get; set; } = string.Empty;

    [Precision(5, 4)]
    public decimal? Confidence { get; set; }

    [MaxLength(600)]
    public string? AgentNoteSummary { get; set; }

    public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public AgentRun AgentRun { get; set; } = null!;

    public AgentRunStage? AgentRunStage { get; set; }

    public HouseholdUser? ReviewedByUser { get; set; }
}

public sealed class IdempotencyKey
{
    public Guid Id { get; set; }

    [MaxLength(120)]
    public string Scope { get; set; } = string.Empty;

    [MaxLength(200)]
    public string KeyValue { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RequestHash { get; set; } = string.Empty;

    public IdempotencyKeyStatus Status { get; set; } = IdempotencyKeyStatus.Reserved;

    public Guid? AgentRunId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(1);

    public DateTime? FinalizedAtUtc { get; set; }

    [MaxLength(120)]
    public string? ResolutionCode { get; set; }

    [MaxLength(500)]
    public string? ResolutionRationale { get; set; }

    public AgentRun? AgentRun { get; set; }
}
