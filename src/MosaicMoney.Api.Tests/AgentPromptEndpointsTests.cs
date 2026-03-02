using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Authentication;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.AgentPrompts;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgentPromptEndpointsTests
{
    private const string TestIssuer = "https://issuer.tests.mosaic-money.local";
    private const string TestSigningKey = "mosaic-money-auth-tests-signing-key-2026";
    private const string TestAuthProvider = "clerk";
    private const string TestAuthSubject = "agent_prompt_tests_subject";

    [Fact]
    public async Task GetPromptLibrary_ReturnsFavoritesTopThreeAndBaselinePrompts()
    {
        Guid actorHouseholdUserId = Guid.Empty;
        Guid householdId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
            householdId = seeded.HouseholdId;

            var now = DateTime.UtcNow;
            dbContext.AgentReusablePrompts.AddRange(
                new AgentReusablePrompt
                {
                    Id = Guid.CreateVersion7(),
                    StableKey = "weekly-cash-flow-summary",
                    Title = "Weekly cash flow summary",
                    PromptText = "Summarize my inflows and outflows.",
                    Scope = AgentPromptScope.Platform,
                    DisplayOrder = 0,
                    IsFavorite = false,
                    UsageCount = 0,
                    IsArchived = false,
                    CreatedAtUtc = now,
                    LastModifiedAtUtc = now,
                },
                new AgentReusablePrompt
                {
                    Id = Guid.CreateVersion7(),
                    StableKey = "needs-review-transactions",
                    Title = "Transactions needing review",
                    PromptText = "Show likely review candidates.",
                    Scope = AgentPromptScope.Platform,
                    DisplayOrder = 1,
                    IsFavorite = false,
                    UsageCount = 0,
                    IsArchived = false,
                    CreatedAtUtc = now,
                    LastModifiedAtUtc = now,
                },
                new AgentReusablePrompt
                {
                    Id = Guid.CreateVersion7(),
                    Title = "Sunday budget check-in",
                    PromptText = "Give me my weekly budget check-in.",
                    Scope = AgentPromptScope.User,
                    HouseholdId = householdId,
                    HouseholdUserId = actorHouseholdUserId,
                    IsFavorite = true,
                    DisplayOrder = 0,
                    UsageCount = 2,
                    LastUsedAtUtc = now,
                    IsArchived = false,
                    CreatedAtUtc = now,
                    LastModifiedAtUtc = now,
                });
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);
        var response = await client.GetAsync("/api/v1/agent/prompts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AgentPromptLibraryDto>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Favorites);
        Assert.Equal(2, payload.BaselinePrompts.Count);
        Assert.Single(payload.UserPrompts);
    }

    [Fact]
    public async Task PromptCrud_UserOwnedPrompt_CreateUpdateUseDelete_Succeeds()
    {
        Guid actorHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var createResponse = await client.PostAsJsonAsync("/api/v1/agent/prompts", new CreateAgentPromptRequest
        {
            Title = "Morning review",
            PromptText = "Summarize my morning account changes.",
            IsFavorite = true,
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AgentReusablePromptDto>();
        Assert.NotNull(created);

        var patchResponse = await client.PatchAsJsonAsync($"/api/v1/agent/prompts/{created!.Id}", new UpdateAgentPromptRequest
        {
            Title = "Morning review v2",
            PromptText = "Summarize my account changes and outliers.",
            IsFavorite = false,
        });

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var useResponse = await client.PostAsJsonAsync($"/api/v1/agent/prompts/{created.Id}/use", new AgentPromptUseRequest
        {
            ConversationId = Guid.CreateVersion7().ToString("D"),
        });

        Assert.Equal(HttpStatusCode.OK, useResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/v1/agent/prompts/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();
        var prompt = await dbContext.AgentReusablePrompts
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.True(prompt.IsArchived);
        Assert.Equal(1, prompt.UsageCount);
    }

    [Fact]
    public async Task PatchPrompt_NotOwnedByActor_ReturnsNotFound()
    {
        Guid actorHouseholdUserId = Guid.Empty;
        Guid otherHouseholdUserId = Guid.Empty;
        Guid promptId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;

            otherHouseholdUserId = Guid.CreateVersion7();
            dbContext.HouseholdUsers.Add(new HouseholdUser
            {
                Id = otherHouseholdUserId,
                HouseholdId = seeded.HouseholdId,
                DisplayName = "Other member",
                MembershipStatus = HouseholdMembershipStatus.Active,
                ActivatedAtUtc = DateTime.UtcNow,
            });

            promptId = Guid.CreateVersion7();
            dbContext.AgentReusablePrompts.Add(new AgentReusablePrompt
            {
                Id = promptId,
                Title = "Private prompt",
                PromptText = "Only for other member.",
                Scope = AgentPromptScope.User,
                HouseholdId = seeded.HouseholdId,
                HouseholdUserId = otherHouseholdUserId,
                IsFavorite = false,
                DisplayOrder = 0,
                UsageCount = 0,
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var response = await client.PatchAsJsonAsync($"/api/v1/agent/prompts/{promptId}", new UpdateAgentPromptRequest
        {
            Title = "Attempted overwrite",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GeneratePromptSuggestion_InitialPromptMode_ReturnsTitleAndPrompt()
    {
        Guid actorHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var response = await client.PostAsJsonAsync("/api/v1/agent/prompts/generate", new GenerateAgentPromptSuggestionRequest
        {
            Mode = nameof(AgentPromptGenerationMode.InitialPrompt),
            InitialPrompt = "summarize my spending patterns and call out risks",
            IncludePromptText = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AgentPromptSuggestionDto>();
        Assert.NotNull(payload);
        Assert.Equal(nameof(AgentPromptGenerationMode.InitialPrompt), payload!.Mode);
        Assert.Equal("Generated Reusable Prompt", payload.Title);
        Assert.Contains("summarize my spending patterns", payload.PromptText!, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateAuthorizedClient(WebApplication app, Guid actorHouseholdUserId)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken());
        client.DefaultRequestHeaders.Add("X-Mosaic-Household-User-Id", actorHouseholdUserId.ToString());
        return client;
    }

    private static SeededActorMembership SeedActorMembership(MosaicMoneyDbContext dbContext)
    {
        var householdId = Guid.CreateVersion7();
        var mosaicUserId = Guid.CreateVersion7();
        var actorHouseholdUserId = Guid.CreateVersion7();

        dbContext.Households.Add(new Household
        {
            Id = householdId,
            Name = "Actor Household",
            CreatedAtUtc = DateTime.UtcNow,
        });

        dbContext.MosaicUsers.Add(new MosaicUser
        {
            Id = mosaicUserId,
            AuthProvider = TestAuthProvider,
            AuthSubject = TestAuthSubject,
            DisplayName = "Actor User",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });

        dbContext.HouseholdUsers.Add(new HouseholdUser
        {
            Id = actorHouseholdUserId,
            HouseholdId = householdId,
            MosaicUserId = mosaicUserId,
            DisplayName = "Actor Member",
            MembershipStatus = HouseholdMembershipStatus.Active,
            ActivatedAtUtc = DateTime.UtcNow,
        });

        return new SeededActorMembership(householdId, actorHouseholdUserId);
    }

    private static async Task<WebApplication> CreateApiAsync(Action<MosaicMoneyDbContext>? seed = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Authentication:Clerk:Issuer"] = TestIssuer,
            ["Authentication:Clerk:SecretKey"] = "test-secret-key",
        };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        var testDatabaseName = $"agent-prompt-api-tests-{Guid.NewGuid()}";

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configurationValues);

        builder.Services.AddDbContext<MosaicMoneyDbContext>(options =>
            options.UseInMemoryDatabase(testDatabaseName));

        builder.Services.AddClerkJwtAuthentication(builder.Configuration);
        builder.Services.AddSingleton<IAgentPromptGenerationService, StubAgentPromptGenerationService>();

        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.RequireHttpsMetadata = false;
            options.Configuration = new OpenIdConnectConfiguration
            {
                Issuer = TestIssuer,
            };
            options.Configuration.SigningKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)));
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = ClaimTypes.NameIdentifier,
                RoleClaimType = ClaimTypes.Role,
                ValidateIssuer = true,
                ValidIssuer = TestIssuer,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
            };
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        var v1 = app.MapGroup("/api/v1").RequireAuthorization();
        v1.MapAgentPromptEndpoints();

        await app.StartAsync();

        if (seed is not null)
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();
            seed(dbContext);
            await dbContext.SaveChangesAsync();
        }

        return app;
    }

    private static string CreateValidToken()
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            claims: [new Claim("sub", TestAuthSubject)],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StubAgentPromptGenerationService : IAgentPromptGenerationService
    {
        public bool IsConfigured => true;

        public Task<AgentPromptGenerationResult?> GenerateAsync(
            AgentPromptGenerationInput input,
            CancellationToken cancellationToken = default)
        {
            var promptText = input.IncludePromptText
                ? (input.InitialPrompt ?? "No prompt provided.")
                : null;

            return Task.FromResult<AgentPromptGenerationResult?>(new AgentPromptGenerationResult(
                Title: "Generated Reusable Prompt",
                PromptText: promptText,
                Model: "model-router",
                SourceSummary: "Generated by test stub."));
        }
    }

    private sealed record SeededActorMembership(Guid HouseholdId, Guid ActorHouseholdUserId);
}
