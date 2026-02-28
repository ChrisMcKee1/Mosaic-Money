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
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class AgentOrchestrationEndpointsTests
{
    private const string TestIssuer = "https://issuer.tests.mosaic-money.local";
    private const string TestSigningKey = "mosaic-money-assistant-stream-tests-signing-key-2026";
    private const string TestAuthProvider = "clerk";
    private const string TestAuthSubject = "assistant_stream_test_user";

    [Fact]
    public async Task GetAssistantConversationStream_MapsAdditiveTimelineFieldsFromLatestStage()
    {
        var conversationId = Guid.CreateVersion7();

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedAuthorizedLedgerContext(dbContext);
            var runId = Guid.CreateVersion7();
            var now = DateTime.UtcNow;

            dbContext.AgentRuns.Add(new AgentRun
            {
                Id = runId,
                HouseholdId = seeded.HouseholdId,
                CorrelationId = $"assistant:{seeded.HouseholdId:N}:{conversationId:N}:{Guid.CreateVersion7():N}",
                WorkflowName = "assistant_message_posted",
                TriggerSource = "runtime-assistant-message-posted",
                PolicyVersion = "m10-worker-orchestration-v1",
                Status = AgentRunStatus.Completed,
                CreatedAtUtc = now,
                LastModifiedAtUtc = now,
                CompletedAtUtc = now,
            });

            dbContext.AgentRunStages.Add(new AgentRunStage
            {
                Id = Guid.CreateVersion7(),
                AgentRunId = runId,
                StageName = "assistant_message_posted",
                StageOrder = 1,
                Executor = "foundry:Mosaic",
                Status = AgentRunStageStatus.Succeeded,
                Confidence = 1.0000m,
                OutcomeCode = "assistant_run_completed",
                OutcomeRationale = "assignment_hint=approval_required; Foundry agent invocation completed.",
                AgentNoteSummary = "Assistant response summary.",
                CreatedAtUtc = now,
                LastModifiedAtUtc = now,
                CompletedAtUtc = now,
            });
        });

        var client = CreateAuthorizedClient(app);
        var response = await client.GetAsync($"/api/v1/assistant/conversations/{conversationId}/stream");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stream = await response.Content.ReadFromJsonAsync<AssistantConversationStreamDto>();
        Assert.NotNull(stream);
        var run = Assert.Single(stream!.Runs);

        Assert.Equal("Mosaic", run.AgentName);
        Assert.Equal("foundry", run.AgentSource);
        Assert.Equal("Foundry agent invocation completed.", run.LatestStageOutcomeSummary);
        Assert.Equal("approval_required", run.AssignmentHint);
    }

    private static HttpClient CreateAuthorizedClient(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken());
        return client;
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

        var testDatabaseName = $"assistant-orchestration-endpoints-tests-{Guid.NewGuid()}";

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configurationValues);

        builder.Services.AddDbContext<MosaicMoneyDbContext>(options =>
            options.UseInMemoryDatabase(testDatabaseName));

        builder.Services.AddClerkJwtAuthentication(builder.Configuration);
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
        v1.MapAgentOrchestrationEndpoints();

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

    private static (Guid HouseholdId, Guid HouseholdUserId) SeedAuthorizedLedgerContext(MosaicMoneyDbContext dbContext)
    {
        var householdId = Guid.CreateVersion7();
        var mosaicUserId = Guid.CreateVersion7();
        var householdUserId = Guid.CreateVersion7();

        dbContext.Households.Add(new Household
        {
            Id = householdId,
            Name = "Test household",
            CreatedAtUtc = DateTime.UtcNow,
        });

        dbContext.MosaicUsers.Add(new MosaicUser
        {
            Id = mosaicUserId,
            AuthProvider = TestAuthProvider,
            AuthSubject = TestAuthSubject,
            DisplayName = "Test user",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
        });

        dbContext.HouseholdUsers.Add(new HouseholdUser
        {
            Id = householdUserId,
            HouseholdId = householdId,
            MosaicUserId = mosaicUserId,
            DisplayName = "Household member",
            MembershipStatus = HouseholdMembershipStatus.Active,
            ActivatedAtUtc = DateTime.UtcNow,
        });

        return (householdId, householdUserId);
    }

    private static string CreateValidToken()
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            claims:
            [
                new Claim("sub", TestAuthSubject),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
