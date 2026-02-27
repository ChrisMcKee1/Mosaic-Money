using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgentWorkflowLifecycleModelContractTests
{
    [Fact]
    public void LifecycleDefaults_AreFailClosedAndReplaySafe()
    {
        var run = new AgentRun();
        var stage = new AgentRunStage();
        var signal = new AgentSignal();
        var audit = new AgentDecisionAudit();
        var idempotency = new IdempotencyKey();

        Assert.Equal(AgentRunStatus.Pending, run.Status);
        Assert.Equal(AgentRunStageStatus.Pending, stage.Status);
        Assert.True(signal.RequiresHumanReview);
        Assert.Equal(AgentDecisionOutcome.NeedsReview, audit.Outcome);
        Assert.Equal(TransactionReviewStatus.NeedsReview, audit.ReviewStatus);
        Assert.Equal(IdempotencyKeyStatus.Reserved, idempotency.Status);
    }

    [Fact]
    public void DbModel_DeclaresAgentRunConstraintsAndIndexes()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var runEntity = model.FindEntityType(typeof(AgentRun));

        Assert.NotNull(runEntity);

        var constraintNames = runEntity!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_AgentRun_CorrelationIdRequired", constraintNames);
        Assert.Contains("CK_AgentRun_WorkflowNameRequired", constraintNames);
        Assert.Contains("CK_AgentRun_TriggerSourceRequired", constraintNames);
        Assert.Contains("CK_AgentRun_PolicyVersionRequired", constraintNames);
        Assert.Contains("CK_AgentRun_StatusRange", constraintNames);
        Assert.Contains("CK_AgentRun_TerminalCompletionAudit", constraintNames);
        Assert.Contains("CK_AgentRun_FailureAuditForEscalatedStates", constraintNames);

        var workflowIndex = runEntity
            .GetIndexes()
            .SingleOrDefault(x =>
                x.Properties.Count == 3
                && x.Properties[0].Name == nameof(AgentRun.WorkflowName)
                && x.Properties[1].Name == nameof(AgentRun.TriggerSource)
                && x.Properties[2].Name == nameof(AgentRun.CreatedAtUtc));

        Assert.NotNull(workflowIndex);
    }

    [Fact]
    public void DbModel_DeclaresStageSignalAuditAndIdempotencyContracts()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var stageEntity = model.FindEntityType(typeof(AgentRunStage));
        var signalEntity = model.FindEntityType(typeof(AgentSignal));
        var auditEntity = model.FindEntityType(typeof(AgentDecisionAudit));
        var idempotencyEntity = model.FindEntityType(typeof(IdempotencyKey));

        Assert.NotNull(stageEntity);
        Assert.NotNull(signalEntity);
        Assert.NotNull(auditEntity);
        Assert.NotNull(idempotencyEntity);

        var stageOrderIndex = stageEntity!
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 2
                && x.Properties[0].Name == nameof(AgentRunStage.AgentRunId)
                && x.Properties[1].Name == nameof(AgentRunStage.StageOrder));

        Assert.NotNull(stageOrderIndex);

        var signalConstraintNames = signalEntity!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_AgentSignal_HumanReviewRequiredForHighSeverity", signalConstraintNames);

        var signalRunForeignKey = signalEntity
            .GetForeignKeys()
            .SingleOrDefault(x => x.Properties.Any(p => p.Name == nameof(AgentSignal.AgentRunId)));

        Assert.NotNull(signalRunForeignKey);
        Assert.Equal(typeof(AgentRun), signalRunForeignKey!.PrincipalEntityType.ClrType);

        var auditConstraintNames = auditEntity!
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_AgentDecisionAudit_FailClosedNeedsReview", auditConstraintNames);

        var idempotencyUniqueIndex = idempotencyEntity!
            .GetIndexes()
            .SingleOrDefault(x =>
                x.IsUnique
                && x.Properties.Count == 2
                && x.Properties[0].Name == nameof(IdempotencyKey.Scope)
                && x.Properties[1].Name == nameof(IdempotencyKey.KeyValue));

        Assert.NotNull(idempotencyUniqueIndex);

        var idempotencyConstraintNames = idempotencyEntity
            .GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CK_IdempotencyKey_FinalizationAudit", idempotencyConstraintNames);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-agent-workflow-lifecycle-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
