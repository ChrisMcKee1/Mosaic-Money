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
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class SearchEndpointsTests
{
    private const string TestIssuer = "https://issuer.tests.mosaic-money.local";
    private const string TestSigningKey = "mosaic-money-search-tests-signing-key-2026";
    private const string TestAuthSubject = "search_endpoint_test_user";

    [Fact]
    public async Task SearchTransactions_UsesLexicalFallback_WhenEmbeddingsAreSparse()
    {
        await using var app = await CreateApiAsync(dbContext =>
        {
            var accountId = Guid.CreateVersion7();
            dbContext.Accounts.Add(new Account
            {
                Id = accountId,
                HouseholdId = Guid.CreateVersion7(),
                Name = "Primary Checking",
                IsActive = true,
            });

            dbContext.EnrichedTransactions.AddRange(
                new EnrichedTransaction
                {
                    Id = Guid.CreateVersion7(),
                    AccountId = accountId,
                    Description = "Debit card purchase",
                    UserNote = "grocery run",
                    Amount = -42.10m,
                    TransactionDate = new DateOnly(2026, 2, 21),
                    ReviewStatus = TransactionReviewStatus.None,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                },
                new EnrichedTransaction
                {
                    Id = Guid.CreateVersion7(),
                    AccountId = accountId,
                    Description = "Household bill",
                    AgentNote = "grocery catch-up",
                    Amount = -58.44m,
                    TransactionDate = new DateOnly(2026, 2, 19),
                    ReviewStatus = TransactionReviewStatus.None,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                });
        });

        var client = CreateAuthorizedClient(app);

        var response = await client.GetAsync("/api/v1/search/transactions?query=grocery&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions!.Count);
        Assert.Contains(transactions, x => string.Equals(x.UserNote, "grocery run", StringComparison.Ordinal));
        Assert.Contains(transactions, x => string.Equals(x.AgentNote, "grocery catch-up", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("fried chicken")]
    [InlineData("fried-chicken combo")]
    public async Task SearchTransactions_UsesDynamicLexicalTokenization_ForMultiWordQueries(string query)
    {
        await using var app = await CreateApiAsync(dbContext =>
        {
            var accountId = Guid.CreateVersion7();
            dbContext.Accounts.Add(new Account
            {
                Id = accountId,
                HouseholdId = Guid.CreateVersion7(),
                Name = "Primary Checking",
                IsActive = true,
            });

            dbContext.EnrichedTransactions.AddRange(
                new EnrichedTransaction
                {
                    Id = Guid.CreateVersion7(),
                    AccountId = accountId,
                    Description = "Fried chicken dinner",
                    Amount = -17.85m,
                    TransactionDate = new DateOnly(2026, 2, 22),
                    ReviewStatus = TransactionReviewStatus.None,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                },
                new EnrichedTransaction
                {
                    Id = Guid.CreateVersion7(),
                    AccountId = accountId,
                    Description = "Gym membership",
                    Amount = -49.00m,
                    TransactionDate = new DateOnly(2026, 2, 20),
                    ReviewStatus = TransactionReviewStatus.None,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                });
        });

        var client = CreateAuthorizedClient(app);

        var encodedQuery = Uri.EscapeDataString(query);
        var response = await client.GetAsync($"/api/v1/search/transactions?query={encodedQuery}&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        Assert.NotNull(transactions);

        var match = Assert.Single(transactions!);
        Assert.Equal("Fried chicken dinner", match.Description);
    }

    [Theory]
    [InlineData("17.85")]
    [InlineData("$17.85")]
    public async Task SearchTransactions_MatchesTransactionsByAmountTokens(string query)
    {
        await using var app = await CreateApiAsync(dbContext =>
        {
            var accountId = Guid.CreateVersion7();
            dbContext.Accounts.Add(new Account
            {
                Id = accountId,
                HouseholdId = Guid.CreateVersion7(),
                Name = "Primary Checking",
                IsActive = true,
            });

            dbContext.EnrichedTransactions.AddRange(
                new EnrichedTransaction
                {
                    Id = Guid.CreateVersion7(),
                    AccountId = accountId,
                    Description = "Cafe purchase",
                    Amount = -17.85m,
                    TransactionDate = new DateOnly(2026, 2, 24),
                    ReviewStatus = TransactionReviewStatus.None,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                },
                new EnrichedTransaction
                {
                    Id = Guid.CreateVersion7(),
                    AccountId = accountId,
                    Description = "Streaming subscription",
                    Amount = -12.99m,
                    TransactionDate = new DateOnly(2026, 2, 20),
                    ReviewStatus = TransactionReviewStatus.None,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                });
        });

        var client = CreateAuthorizedClient(app);

        var encodedQuery = Uri.EscapeDataString(query);
        var response = await client.GetAsync($"/api/v1/search/transactions?query={encodedQuery}&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        Assert.NotNull(transactions);

        var match = Assert.Single(transactions!);
        Assert.Equal(-17.85m, match.Amount);
    }

    [Fact]
    public void RankHybridTransactionCandidates_KeepsSemanticRankStable_WhenLexicalCandidatesOverlap()
    {
        var semanticTop = new EnrichedTransaction
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AccountId = Guid.CreateVersion7(),
            Description = "semantic-top",
            Amount = -1m,
            TransactionDate = new DateOnly(2026, 2, 27),
            ReviewStatus = TransactionReviewStatus.None,
        };

        var semanticSecond = new EnrichedTransaction
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AccountId = Guid.CreateVersion7(),
            Description = "semantic-second",
            Amount = -1m,
            TransactionDate = new DateOnly(2026, 2, 27),
            ReviewStatus = TransactionReviewStatus.None,
        };

        var ranked = SearchEndpoints.RankHybridTransactionCandidates(
            semanticCandidates: [semanticTop, semanticSecond],
            lexicalCandidates: [semanticSecond],
            limit: 10);

        Assert.Equal(2, ranked.Count);
        Assert.Equal(semanticTop.Id, ranked[0].Id);
        Assert.Equal(semanticSecond.Id, ranked[1].Id);
    }

    [Fact]
    public void ExpandQueryTerms_NormalizesAndTokenizesQueryWithoutHardcodedExpansions()
    {
        var terms = SearchEndpoints.ExpandQueryTerms("  Fried-Chicken combo!  ");

        Assert.Contains(terms, x => string.Equals(x, "Fried-Chicken combo!", StringComparison.Ordinal));
        Assert.Contains(terms, x => string.Equals(x, "fried", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(terms, x => string.Equals(x, "chicken", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(terms, x => string.Equals(x, "combo", StringComparison.OrdinalIgnoreCase));

        var hardcodedCheckTerms = SearchEndpoints.ExpandQueryTerms("kfc");
        Assert.Single(hardcodedCheckTerms);
        Assert.Equal("kfc", hardcodedCheckTerms[0], ignoreCase: true);
    }

    [Fact]
    public void ExtractAmountTerms_ParsesCurrencyAndAddsSignedMatches()
    {
        var terms = SearchEndpoints.ExtractAmountTerms("show me $42.50 and 17");

        Assert.Contains(42.50m, terms);
        Assert.Contains(-42.50m, terms);
        Assert.Contains(17m, terms);
        Assert.Contains(-17m, terms);
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

        var testDatabaseName = $"search-endpoints-tests-{Guid.NewGuid()}";

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configurationValues);

        builder.Services.AddDbContext<MosaicMoneyDbContext>(options =>
            options.UseInMemoryDatabase(testDatabaseName));
        builder.Services.AddSingleton<ITransactionEmbeddingGenerator, StableEmbeddingGenerator>();

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
        v1.MapSearchEndpoints();

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
            claims: new[]
            {
                new Claim("sub", TestAuthSubject),
            },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StableEmbeddingGenerator : ITransactionEmbeddingGenerator
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new[] { 0.11f, 0.22f, 0.33f });
        }
    }
}
