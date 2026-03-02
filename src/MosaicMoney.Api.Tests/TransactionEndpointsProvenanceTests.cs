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
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using MosaicMoney.Api.Domain.Ledger.Transactions;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class TransactionEndpointsProvenanceTests
{
    private const string TestIssuer = "https://issuer.tests.mosaic-money.local";
    private const string TestSigningKey = "mosaic-money-transaction-provenance-tests-signing-key-2026";
    private const string TestAuthProvider = "clerk";
    private const string TestAuthSubject = "transaction_provenance_test_user";

    [Fact]
    public async Task GetTransactions_UsesLatestClassificationOutcomeForProvenanceFields()
    {
        var transactionId = Guid.CreateVersion7();

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedAuthorizedLedgerContext(dbContext);

            dbContext.EnrichedTransactions.Add(new EnrichedTransaction
            {
                Id = transactionId,
                AccountId = seeded.AccountId,
                Description = "Neighborhood market",
                Amount = -53.27m,
                TransactionDate = new DateOnly(2026, 2, 20),
                ReviewStatus = TransactionReviewStatus.Reviewed,
                CreatedAtUtc = new DateTime(2026, 2, 20, 8, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2026, 2, 20, 8, 0, 0, DateTimeKind.Utc),
            });

            dbContext.TransactionClassificationOutcomes.AddRange(
                new TransactionClassificationOutcome
                {
                    Id = Guid.CreateVersion7(),
                    TransactionId = transactionId,
                    FinalConfidence = 0.4100m,
                    Decision = ClassificationDecision.NeedsReview,
                    ReviewStatus = TransactionReviewStatus.NeedsReview,
                    DecisionReasonCode = "older_reason_code",
                    DecisionRationale = "Older rationale that should not be selected.",
                    IsAiAssigned = true,
                    AssignmentSource = "foundry_responses_api",
                    AssignedByAgent = "mosaic-money-classifier-old",
                    CreatedAtUtc = new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc),
                },
                new TransactionClassificationOutcome
                {
                    Id = Guid.CreateVersion7(),
                    TransactionId = transactionId,
                    FinalConfidence = 0.9200m,
                    Decision = ClassificationDecision.Categorized,
                    ReviewStatus = TransactionReviewStatus.Reviewed,
                    DecisionReasonCode = "latest_reason_code",
                    DecisionRationale = "Latest rationale selected for API provenance.",
                    IsAiAssigned = true,
                    AssignmentSource = "foundry_responses_api",
                    AssignedByAgent = "mosaic-money-classifier",
                    CreatedAtUtc = new DateTime(2026, 2, 20, 11, 0, 0, DateTimeKind.Utc),
                });
        });

        var client = CreateAuthorizedClient(app);
        var response = await client.GetAsync("/api/v1/transactions?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        var transaction = Assert.Single(transactions!);

        Assert.Equal(transactionId, transaction.Id);
        Assert.Equal(true, transaction.IsAiAssigned);
        Assert.Equal("foundry_responses_api", transaction.AssignmentSource);
        Assert.Equal("mosaic-money-classifier", transaction.AssignedByAgent);
        Assert.Equal("latest_reason_code", transaction.LatestClassificationReasonCode);
        Assert.Equal("Latest rationale selected for API provenance.", transaction.LatestClassificationRationale);
        Assert.Equal(0.9200m, transaction.LatestClassificationConfidence);
    }

    [Fact]
    public async Task GetTransactions_LeavesProvenanceFieldsNullWhenNoClassificationOutcomeExists()
    {
        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedAuthorizedLedgerContext(dbContext);

            dbContext.EnrichedTransactions.Add(new EnrichedTransaction
            {
                Id = Guid.CreateVersion7(),
                AccountId = seeded.AccountId,
                Description = "Transit fare",
                Amount = -9.75m,
                TransactionDate = new DateOnly(2026, 2, 21),
                ReviewStatus = TransactionReviewStatus.None,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });
        });

        var client = CreateAuthorizedClient(app);
        var response = await client.GetAsync("/api/v1/transactions?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        var transaction = Assert.Single(transactions!);

        Assert.Null(transaction.IsAiAssigned);
        Assert.Null(transaction.AssignmentSource);
        Assert.Null(transaction.AssignedByAgent);
        Assert.Null(transaction.LatestClassificationReasonCode);
        Assert.Null(transaction.LatestClassificationRationale);
        Assert.Null(transaction.LatestClassificationConfidence);
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

        var testDatabaseName = $"transaction-provenance-tests-{Guid.NewGuid()}";

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configurationValues);

        builder.Services.AddDbContext<MosaicMoneyDbContext>(options =>
            options.UseInMemoryDatabase(testDatabaseName));
        builder.Services.AddScoped<TransactionProjectionMetadataQueryService>();
        builder.Services.AddScoped<ITransactionAccessQueryService, TransactionAccessQueryService>();
        builder.Services.AddSingleton<ITransactionEmbeddingQueueService, NoOpTransactionEmbeddingQueueService>();

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
        v1.MapTransactionEndpoints();

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

    private static (Guid HouseholdUserId, Guid AccountId) SeedAuthorizedLedgerContext(MosaicMoneyDbContext dbContext)
    {
        var householdId = Guid.CreateVersion7();
        var mosaicUserId = Guid.CreateVersion7();
        var householdUserId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();

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

        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            HouseholdId = householdId,
            Name = "Primary checking",
            IsActive = true,
        });

        dbContext.AccountMemberAccessEntries.Add(new AccountMemberAccess
        {
            AccountId = accountId,
            HouseholdUserId = householdUserId,
            AccessRole = AccountAccessRole.Owner,
            Visibility = AccountAccessVisibility.Visible,
            GrantedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow,
        });

        return (householdUserId, accountId);
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

    private sealed class NoOpTransactionEmbeddingQueueService : ITransactionEmbeddingQueueService
    {
        public Task<int> EnqueueTransactionsAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(transactionIds.Count);
        }
    }
}
