using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Transactions;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class ApiAuthorizationTests
{
    private const string TestIssuer = "https://issuer.tests.mosaic-money.local";
    private const string TestSigningKey = "mosaic-money-auth-tests-signing-key-2026";
    private const string TestAuthProvider = "clerk";
    private const string TestAuthSubject = "user_test_01";

    [Fact]
    public async Task ProtectedRoute_MissingBearerToken_ReturnsUnauthorized()
    {
        await using var app = await CreateApiAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/v1/households");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_InvalidBearerToken_ReturnsUnauthorized()
    {
        await using var app = await CreateApiAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await client.GetAsync("/api/v1/households");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FallbackPolicy_UnannotatedRoute_RequiresAuthentication()
    {
        await using var app = await CreateApiAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/fallback-auth-probe");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TransactionsRoute_ValidTokenWithoutActiveMembership_ReturnsForbidden()
    {
        await using var app = await CreateApiAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken());
        client.DefaultRequestHeaders.Add("X-Mosaic-Household-User-Id", Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/v1/transactions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_ValidBearerToken_AllowsRequest()
    {
        await using var app = await CreateApiAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken());

        var response = await client.GetAsync("/api/v1/households");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TransactionsRoute_SubClaimMappedToSingleActiveMembership_AllowsRequestWithoutHeader()
    {
        await using var app = await CreateApiAsync(dbContext =>
        {
            var householdId = Guid.CreateVersion7();
            var mosaicUserId = Guid.CreateVersion7();

            dbContext.Households.Add(new Household
            {
                Id = householdId,
                Name = "Mapped household",
                CreatedAtUtc = DateTime.UtcNow,
            });

            dbContext.MosaicUsers.Add(new MosaicUser
            {
                Id = mosaicUserId,
                AuthProvider = TestAuthProvider,
                AuthSubject = TestAuthSubject,
                DisplayName = "Mapped User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            });

            dbContext.HouseholdUsers.Add(new HouseholdUser
            {
                Id = Guid.CreateVersion7(),
                HouseholdId = householdId,
                MosaicUserId = mosaicUserId,
                DisplayName = "Mapped Member",
                MembershipStatus = HouseholdMembershipStatus.Active,
                ActivatedAtUtc = DateTime.UtcNow,
            });
        });

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();
            var userIds = await dbContext.MosaicUsers
                .AsNoTracking()
                .Where(x => x.AuthSubject == TestAuthSubject)
                .Select(x => x.Id)
                .ToListAsync();

            var matchCount = await dbContext.HouseholdUsers
                .AsNoTracking()
                .CountAsync(x =>
                    x.MembershipStatus == HouseholdMembershipStatus.Active
                    && x.MosaicUserId.HasValue
                    && userIds.Contains(x.MosaicUserId.Value));

            Assert.Equal(1, matchCount);
        }

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken());

        var response = await client.GetAsync("/api/v1/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TransactionsRoute_SubClaimMappedToMultipleActiveMemberships_RequiresExplicitContext()
    {
        await using var app = await CreateApiAsync(dbContext =>
        {
            var mosaicUserId = Guid.CreateVersion7();

            dbContext.MosaicUsers.Add(new MosaicUser
            {
                Id = mosaicUserId,
                AuthProvider = TestAuthProvider,
                AuthSubject = TestAuthSubject,
                DisplayName = "Mapped User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            });

            for (var index = 0; index < 2; index++)
            {
                var householdId = Guid.CreateVersion7();
                dbContext.Households.Add(new Household
                {
                    Id = householdId,
                    Name = $"Mapped household {index + 1}",
                    CreatedAtUtc = DateTime.UtcNow,
                });

                dbContext.HouseholdUsers.Add(new HouseholdUser
                {
                    Id = Guid.CreateVersion7(),
                    HouseholdId = householdId,
                    MosaicUserId = mosaicUserId,
                    DisplayName = $"Mapped Member {index + 1}",
                    MembershipStatus = HouseholdMembershipStatus.Active,
                    ActivatedAtUtc = DateTime.UtcNow,
                });
            }
        });

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();
            var userIds = await dbContext.MosaicUsers
                .AsNoTracking()
                .Where(x => x.AuthSubject == TestAuthSubject)
                .Select(x => x.Id)
                .ToListAsync();

            var matchCount = await dbContext.HouseholdUsers
                .AsNoTracking()
                .CountAsync(x =>
                    x.MembershipStatus == HouseholdMembershipStatus.Active
                    && x.MosaicUserId.HasValue
                    && userIds.Contains(x.MosaicUserId.Value));

            Assert.Equal(2, matchCount);
        }

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken());

        var response = await client.GetAsync("/api/v1/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TransactionsRoute_SubClaimMappedToDifferentAuthProvider_ReturnsForbidden()
    {
        await using var app = await CreateApiAsync(dbContext =>
        {
            var householdId = Guid.CreateVersion7();
            var mosaicUserId = Guid.CreateVersion7();

            dbContext.Households.Add(new Household
            {
                Id = householdId,
                Name = "Different provider household",
                CreatedAtUtc = DateTime.UtcNow,
            });

            dbContext.MosaicUsers.Add(new MosaicUser
            {
                Id = mosaicUserId,
                AuthProvider = "entra-id",
                AuthSubject = TestAuthSubject,
                DisplayName = "Different Provider User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            });

            dbContext.HouseholdUsers.Add(new HouseholdUser
            {
                Id = Guid.CreateVersion7(),
                HouseholdId = householdId,
                MosaicUserId = mosaicUserId,
                DisplayName = "Different Provider Member",
                MembershipStatus = HouseholdMembershipStatus.Active,
                ActivatedAtUtc = DateTime.UtcNow,
            });
        });

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken());

        var response = await client.GetAsync("/api/v1/transactions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TransactionsRoute_ExplicitMemberContextMustMatchAuthenticatedSubject()
    {
        var householdUserId = Guid.CreateVersion7();

        await using var app = await CreateApiAsync(dbContext =>
        {
            var householdId = Guid.CreateVersion7();
            var mosaicUserId = Guid.CreateVersion7();

            dbContext.Households.Add(new Household
            {
                Id = householdId,
                Name = "Mismatch household",
                CreatedAtUtc = DateTime.UtcNow,
            });

            dbContext.MosaicUsers.Add(new MosaicUser
            {
                Id = mosaicUserId,
                AuthProvider = TestAuthProvider,
                AuthSubject = "different_subject",
                DisplayName = "Different User",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
            });

            dbContext.HouseholdUsers.Add(new HouseholdUser
            {
                Id = householdUserId,
                HouseholdId = householdId,
                MosaicUserId = mosaicUserId,
                DisplayName = "Mismatch Member",
                MembershipStatus = HouseholdMembershipStatus.Active,
                ActivatedAtUtc = DateTime.UtcNow,
            });
        });

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken());
        client.DefaultRequestHeaders.Add("X-Mosaic-Household-User-Id", householdUserId.ToString());

        var response = await client.GetAsync("/api/v1/transactions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

        var testDatabaseName = $"api-auth-tests-{Guid.NewGuid()}";

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configurationValues);

        builder.Services.AddDbContext<MosaicMoneyDbContext>(options =>
            options.UseInMemoryDatabase(testDatabaseName));
        builder.Services.AddScoped<TransactionProjectionMetadataQueryService>();
        builder.Services.AddScoped<ITransactionAccessQueryService, TransactionAccessQueryService>();

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

        // This endpoint intentionally omits RequireAuthorization() to verify fallback policy enforcement.
        app.MapGet("/api/fallback-auth-probe", () => Results.Ok(new { ok = true }));

        var v1 = app.MapGroup("/api/v1").RequireAuthorization();
        v1.MapHouseholdEndpoints();
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
}
