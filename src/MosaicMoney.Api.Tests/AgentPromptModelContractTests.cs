using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgentPromptModelContractTests
{
    [Fact]
    public void AgentReusablePrompt_DefaultsToUserScope()
    {
        var prompt = new AgentReusablePrompt();

        Assert.Equal(AgentPromptScope.User, prompt.Scope);
        Assert.False(prompt.IsFavorite);
        Assert.False(prompt.IsArchived);
    }

    [Fact]
    public void DbModel_ConfiguresScopeSentinelToUserDefault()
    {
        using var dbContext = CreateDbContext();

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var promptEntity = model.FindEntityType(typeof(AgentReusablePrompt));

        Assert.NotNull(promptEntity);

        var scopeProperty = promptEntity.FindProperty(nameof(AgentReusablePrompt.Scope));

        Assert.NotNull(scopeProperty);
        Assert.Equal(AgentPromptScope.User, (AgentPromptScope)scopeProperty.Sentinel!);
    }

    private static MosaicMoneyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MosaicMoneyDbContext>()
            .UseInMemoryDatabase($"mosaicmoney-agent-prompt-model-tests-{Guid.NewGuid()}")
            .Options;

        return new MosaicMoneyDbContext(options);
    }
}
