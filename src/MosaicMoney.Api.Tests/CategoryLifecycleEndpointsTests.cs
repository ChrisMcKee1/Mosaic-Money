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
using MosaicMoney.Api.Domain.Ledger.Taxonomy;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class CategoryLifecycleEndpointsTests
{
    private const string TestIssuer = "https://issuer.tests.mosaic-money.local";
    private const string TestSigningKey = "mosaic-money-auth-tests-signing-key-2026";
    private const string TestAuthProvider = "clerk";
    private const string TestAuthSubject = "category_lifecycle_test_user";
    private const string TestOperatorApiKey = "taxonomy-operator-test-key";

    [Fact]
    public async Task PostCategory_UserScopeOwnerMismatch_ReturnsForbidden()
    {
        Guid actorHouseholdUserId = Guid.Empty;
        Guid householdId = Guid.Empty;
        Guid otherHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
            householdId = seeded.HouseholdId;

            otherHouseholdUserId = Guid.CreateVersion7();
            dbContext.HouseholdUsers.Add(new HouseholdUser
            {
                Id = otherHouseholdUserId,
                HouseholdId = householdId,
                DisplayName = "Other member",
                MembershipStatus = HouseholdMembershipStatus.Active,
                ActivatedAtUtc = DateTime.UtcNow,
            });
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var response = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Private category",
            Scope = nameof(CategoryOwnerType.User),
            HouseholdId = householdId,
            OwnerUserId = otherHouseholdUserId,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostCategory_HouseholdSharedAcrossHouseholdBoundary_ReturnsForbidden()
    {
        Guid actorHouseholdUserId = Guid.Empty;
        Guid outsideHouseholdId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;

            outsideHouseholdId = Guid.CreateVersion7();
            dbContext.Households.Add(new Household
            {
                Id = outsideHouseholdId,
                Name = "Outside Household",
                CreatedAtUtc = DateTime.UtcNow,
            });
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var response = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Shared mismatch",
            Scope = nameof(CategoryOwnerType.HouseholdShared),
            HouseholdId = outsideHouseholdId,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostCategory_PlatformScopeMutation_ReturnsForbidden()
    {
        Guid actorHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var response = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Platform attempt",
            Scope = nameof(CategoryOwnerType.Platform),
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformScopeCategoryCrud_WithOperatorAccess_Succeeds_AndWritesAuditEntries()
    {
        Guid actorHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId, includeOperatorKey: true);

        var createResponse = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Platform Ops",
            Scope = nameof(CategoryOwnerType.Platform),
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdCategory = await createResponse.Content.ReadFromJsonAsync<CategoryLifecycleDto>();
        Assert.NotNull(createdCategory);

        var renameResponse = await client.PatchAsJsonAsync($"/api/v1/categories/{createdCategory!.Id}", new UpdateCategoryRequest
        {
            Name = "Platform Ops Renamed",
        });

        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);

        var archiveResponse = await client.DeleteAsync($"/api/v1/categories/{createdCategory.Id}?allowLinkedTransactions=true");
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();

        var category = await dbContext.Categories
            .AsNoTracking()
            .FirstAsync(x => x.Id == createdCategory.Id);
        Assert.True(category.IsArchived);
        Assert.Equal(CategoryOwnerType.Platform, category.OwnerType);

        var operations = await dbContext.TaxonomyLifecycleAuditEntries
            .AsNoTracking()
            .Where(x => x.EntityType == "Category" && x.EntityId == createdCategory.Id)
            .Select(x => x.Operation)
            .ToListAsync();

        Assert.Contains("Created", operations);
        Assert.Contains("Updated", operations);
        Assert.Contains("Archived", operations);
    }

    [Fact]
    public async Task PostCategory_PlatformScopeMutation_WithWrongOperatorKey_ReturnsForbidden()
    {
        Guid actorHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
        });

        var client = CreateAuthorizedClient(
            app,
            actorHouseholdUserId,
            includeOperatorKey: true,
            operatorKeyOverride: "taxonomy-operator-incorrect-key");

        var response = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Platform wrong-key attempt",
            Scope = nameof(CategoryOwnerType.Platform),
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostCategory_PlatformScopeMutation_WithMultipleOperatorKeyHeaders_ReturnsForbidden()
    {
        Guid actorHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId, includeOperatorKey: true);
        client.DefaultRequestHeaders.Remove(TaxonomyOperatorOptions.OperatorApiKeyHeaderName);
        client.DefaultRequestHeaders.Add(
            TaxonomyOperatorOptions.OperatorApiKeyHeaderName,
            [TestOperatorApiKey, TestOperatorApiKey]);

        var response = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Platform duplicate-header attempt",
            Scope = nameof(CategoryOwnerType.Platform),
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostCategory_PlatformScopeMutation_WithUnauthorizedSubject_ReturnsForbidden()
    {
        Guid actorHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(
            dbContext =>
            {
                var seeded = SeedActorMembership(dbContext);
                actorHouseholdUserId = seeded.ActorHouseholdUserId;
            },
            allowedAuthSubjectsCsv: "some-other-operator-subject");

        var client = CreateAuthorizedClient(app, actorHouseholdUserId, includeOperatorKey: true);

        var response = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Platform unauthorized subject attempt",
            Scope = nameof(CategoryOwnerType.Platform),
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformScopeReorderAndSubcategoryLifecycle_WithOperatorAccess_Succeeds_AndWritesAuditEntries()
    {
        Guid actorHouseholdUserId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId, includeOperatorKey: true);

        var createPrimaryCategoryResponse = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Platform Primary",
            Scope = nameof(CategoryOwnerType.Platform),
        });
        Assert.Equal(HttpStatusCode.Created, createPrimaryCategoryResponse.StatusCode);

        var createSecondaryCategoryResponse = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Platform Secondary",
            Scope = nameof(CategoryOwnerType.Platform),
        });
        Assert.Equal(HttpStatusCode.Created, createSecondaryCategoryResponse.StatusCode);

        var primaryCategory = await createPrimaryCategoryResponse.Content.ReadFromJsonAsync<CategoryLifecycleDto>();
        var secondaryCategory = await createSecondaryCategoryResponse.Content.ReadFromJsonAsync<CategoryLifecycleDto>();
        Assert.NotNull(primaryCategory);
        Assert.NotNull(secondaryCategory);

        var reorderResponse = await client.PostAsJsonAsync("/api/v1/categories/reorder", new ReorderCategoriesRequest
        {
            Scope = nameof(CategoryOwnerType.Platform),
            CategoryIds = [secondaryCategory!.Id, primaryCategory!.Id],
        });
        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

        var createSubcategoryResponse = await client.PostAsJsonAsync("/api/v1/subcategories", new CreateSubcategoryRequest
        {
            CategoryId = primaryCategory.Id,
            Name = "Platform Subcategory",
        });
        Assert.Equal(HttpStatusCode.Created, createSubcategoryResponse.StatusCode);

        var createdSubcategory = await createSubcategoryResponse.Content.ReadFromJsonAsync<CategorySubcategoryDto>();
        Assert.NotNull(createdSubcategory);

        var renameSubcategoryResponse = await client.PatchAsJsonAsync($"/api/v1/subcategories/{createdSubcategory!.Id}", new UpdateSubcategoryRequest
        {
            Name = "Platform Subcategory Renamed",
        });
        Assert.Equal(HttpStatusCode.OK, renameSubcategoryResponse.StatusCode);

        var reparentResponse = await client.PostAsJsonAsync($"/api/v1/subcategories/{createdSubcategory.Id}/reparent", new ReparentSubcategoryRequest
        {
            TargetCategoryId = secondaryCategory.Id,
        });
        Assert.Equal(HttpStatusCode.OK, reparentResponse.StatusCode);

        var archiveSubcategoryResponse = await client.DeleteAsync($"/api/v1/subcategories/{createdSubcategory.Id}?allowLinkedTransactions=true");
        Assert.Equal(HttpStatusCode.OK, archiveSubcategoryResponse.StatusCode);

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();

        var reorderedCategories = await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.Id == primaryCategory.Id || x.Id == secondaryCategory.Id)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        Assert.Equal(secondaryCategory.Id, reorderedCategories[0].Id);
        Assert.Equal(primaryCategory.Id, reorderedCategories[1].Id);

        var storedSubcategory = await dbContext.Subcategories
            .AsNoTracking()
            .FirstAsync(x => x.Id == createdSubcategory.Id);

        Assert.Equal(secondaryCategory.Id, storedSubcategory.CategoryId);
        Assert.True(storedSubcategory.IsArchived);

        var reorderedOperations = await dbContext.TaxonomyLifecycleAuditEntries
            .AsNoTracking()
            .Where(x => x.EntityType == "Category" && x.Operation == "Reordered")
            .CountAsync();
        Assert.True(reorderedOperations >= 1);

        var subcategoryOperations = await dbContext.TaxonomyLifecycleAuditEntries
            .AsNoTracking()
            .Where(x => x.EntityType == "Subcategory" && x.EntityId == createdSubcategory.Id)
            .Select(x => x.Operation)
            .ToListAsync();

        Assert.Contains("Created", subcategoryOperations);
        Assert.Contains("Updated", subcategoryOperations);
        Assert.Contains("Reparented", subcategoryOperations);
        Assert.Contains("Archived", subcategoryOperations);
    }

    [Fact]
    public async Task CategoryNameDuplicate_IsScopedPerOwnershipLane()
    {
        Guid actorHouseholdUserId = Guid.Empty;
        Guid householdId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
            householdId = seeded.HouseholdId;

            dbContext.Categories.Add(new Category
            {
                Id = Guid.CreateVersion7(),
                Name = "Utilities",
                DisplayOrder = 0,
                OwnerType = CategoryOwnerType.HouseholdShared,
                HouseholdId = householdId,
                OwnerUserId = null,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var duplicateShared = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Utilities",
            Scope = nameof(CategoryOwnerType.HouseholdShared),
            HouseholdId = householdId,
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicateShared.StatusCode);

        var userScopeCreate = await client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest
        {
            Name = "Utilities",
            Scope = nameof(CategoryOwnerType.User),
            HouseholdId = householdId,
            OwnerUserId = actorHouseholdUserId,
        });

        Assert.Equal(HttpStatusCode.Created, userScopeCreate.StatusCode);
    }

    [Fact]
    public async Task ReorderCategories_IsIdempotent_AndRejectsStaleRevision()
    {
        Guid actorHouseholdUserId = Guid.Empty;
        Guid householdId = Guid.Empty;
        Guid firstCategoryId = Guid.Empty;
        Guid secondCategoryId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
            householdId = seeded.HouseholdId;

            firstCategoryId = Guid.CreateVersion7();
            secondCategoryId = Guid.CreateVersion7();

            dbContext.Categories.AddRange(
                new Category
                {
                    Id = firstCategoryId,
                    Name = "First",
                    DisplayOrder = 0,
                    OwnerType = CategoryOwnerType.User,
                    HouseholdId = householdId,
                    OwnerUserId = actorHouseholdUserId,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                },
                new Category
                {
                    Id = secondCategoryId,
                    Name = "Second",
                    DisplayOrder = 1,
                    OwnerType = CategoryOwnerType.User,
                    HouseholdId = householdId,
                    OwnerUserId = actorHouseholdUserId,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                });
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var reorderRequest = new ReorderCategoriesRequest
        {
            Scope = nameof(CategoryOwnerType.User),
            HouseholdId = householdId,
            OwnerUserId = actorHouseholdUserId,
            CategoryIds = [secondCategoryId, firstCategoryId],
        };

        var firstReorder = await client.PostAsJsonAsync("/api/v1/categories/reorder", reorderRequest);
        Assert.Equal(HttpStatusCode.OK, firstReorder.StatusCode);

        var secondReorder = await client.PostAsJsonAsync("/api/v1/categories/reorder", reorderRequest);
        Assert.Equal(HttpStatusCode.OK, secondReorder.StatusCode);

        var staleRevisionResponse = await client.PostAsJsonAsync("/api/v1/categories/reorder", new ReorderCategoriesRequest
        {
            Scope = nameof(CategoryOwnerType.User),
            HouseholdId = householdId,
            OwnerUserId = actorHouseholdUserId,
            CategoryIds = [firstCategoryId, secondCategoryId],
            ExpectedLastModifiedAtUtc = DateTime.UtcNow.AddDays(-2),
        });

        Assert.Equal(HttpStatusCode.Conflict, staleRevisionResponse.StatusCode);
    }

    [Fact]
    public async Task ReparentSubcategory_AcrossOwnershipScopes_ReturnsForbidden()
    {
        Guid actorHouseholdUserId = Guid.Empty;
        Guid householdId = Guid.Empty;
        Guid sourceSubcategoryId = Guid.Empty;
        Guid targetCategoryId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;
            householdId = seeded.HouseholdId;

            var userCategoryId = Guid.CreateVersion7();
            targetCategoryId = Guid.CreateVersion7();
            sourceSubcategoryId = Guid.CreateVersion7();

            dbContext.Categories.AddRange(
                new Category
                {
                    Id = userCategoryId,
                    Name = "User Category",
                    DisplayOrder = 0,
                    OwnerType = CategoryOwnerType.User,
                    HouseholdId = householdId,
                    OwnerUserId = actorHouseholdUserId,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                },
                new Category
                {
                    Id = targetCategoryId,
                    Name = "Shared Category",
                    DisplayOrder = 1,
                    OwnerType = CategoryOwnerType.HouseholdShared,
                    HouseholdId = householdId,
                    OwnerUserId = null,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastModifiedAtUtc = DateTime.UtcNow,
                });

            dbContext.Subcategories.Add(new Subcategory
            {
                Id = sourceSubcategoryId,
                CategoryId = userCategoryId,
                Name = "Source Subcategory",
                DisplayOrder = 0,
                IsBusinessExpense = false,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var response = await client.PostAsJsonAsync($"/api/v1/subcategories/{sourceSubcategoryId}/reparent", new ReparentSubcategoryRequest
        {
            TargetCategoryId = targetCategoryId,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithLinkedTransactions_RequiresExplicitAllowFlag_AndArchivesWithoutBreakingLinks()
    {
        Guid actorHouseholdUserId = Guid.Empty;
        Guid categoryId = Guid.Empty;
        Guid subcategoryId = Guid.Empty;
        Guid transactionId = Guid.Empty;

        await using var app = await CreateApiAsync(dbContext =>
        {
            var seeded = SeedActorMembership(dbContext);
            actorHouseholdUserId = seeded.ActorHouseholdUserId;

            var accountId = Guid.CreateVersion7();
            categoryId = Guid.CreateVersion7();
            subcategoryId = Guid.CreateVersion7();
            transactionId = Guid.CreateVersion7();

            dbContext.Accounts.Add(new Account
            {
                Id = accountId,
                HouseholdId = seeded.HouseholdId,
                Name = "Checking",
                IsActive = true,
            });

            dbContext.Categories.Add(new Category
            {
                Id = categoryId,
                Name = "User Category",
                DisplayOrder = 0,
                OwnerType = CategoryOwnerType.User,
                HouseholdId = seeded.HouseholdId,
                OwnerUserId = seeded.ActorHouseholdUserId,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });

            dbContext.Subcategories.Add(new Subcategory
            {
                Id = subcategoryId,
                CategoryId = categoryId,
                Name = "Linked Subcategory",
                DisplayOrder = 0,
                IsBusinessExpense = false,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });

            dbContext.EnrichedTransactions.Add(new EnrichedTransaction
            {
                Id = transactionId,
                AccountId = accountId,
                SubcategoryId = subcategoryId,
                Description = "Linked transaction",
                Amount = -45.20m,
                TransactionDate = new DateOnly(2026, 2, 27),
                ReviewStatus = TransactionReviewStatus.None,
                CreatedAtUtc = DateTime.UtcNow,
                LastModifiedAtUtc = DateTime.UtcNow,
            });
        });

        var client = CreateAuthorizedClient(app, actorHouseholdUserId);

        var noFlagResponse = await client.DeleteAsync($"/api/v1/categories/{categoryId}");
        Assert.Equal(HttpStatusCode.Conflict, noFlagResponse.StatusCode);

        var withFlagResponse = await client.DeleteAsync($"/api/v1/categories/{categoryId}?allowLinkedTransactions=true");
        Assert.Equal(HttpStatusCode.OK, withFlagResponse.StatusCode);

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MosaicMoneyDbContext>();

        var category = await dbContext.Categories.AsNoTracking().FirstAsync(x => x.Id == categoryId);
        var subcategory = await dbContext.Subcategories.AsNoTracking().FirstAsync(x => x.Id == subcategoryId);
        var transaction = await dbContext.EnrichedTransactions.AsNoTracking().FirstAsync(x => x.Id == transactionId);

        Assert.True(category.IsArchived);
        Assert.True(subcategory.IsArchived);
        Assert.Equal(subcategoryId, transaction.SubcategoryId);
    }

    private static HttpClient CreateAuthorizedClient(
        WebApplication app,
        Guid actorHouseholdUserId,
        bool includeOperatorKey = false,
        string? operatorKeyOverride = null,
        string? authSubjectOverride = null)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateValidToken(authSubjectOverride));
        client.DefaultRequestHeaders.Add("X-Mosaic-Household-User-Id", actorHouseholdUserId.ToString());
        if (includeOperatorKey)
        {
            client.DefaultRequestHeaders.Add(TaxonomyOperatorOptions.OperatorApiKeyHeaderName, operatorKeyOverride ?? TestOperatorApiKey);
        }

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

    private static async Task<WebApplication> CreateApiAsync(
        Action<MosaicMoneyDbContext>? seed = null,
        string? allowedAuthSubjectsCsv = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Authentication:Clerk:Issuer"] = TestIssuer,
            ["Authentication:Clerk:SecretKey"] = "test-secret-key",
            ["TaxonomyOperator:ApiKey"] = TestOperatorApiKey,
            ["TaxonomyOperator:AllowedAuthSubjectsCsv"] = allowedAuthSubjectsCsv ?? TestAuthSubject,
        };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        var testDatabaseName = $"category-lifecycle-api-tests-{Guid.NewGuid()}";

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configurationValues);

        builder.Services.AddDbContext<MosaicMoneyDbContext>(options =>
            options.UseInMemoryDatabase(testDatabaseName));
        builder.Services.AddScoped<ICategoryLifecycleAuditTrail, CategoryLifecycleAuditTrail>();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddClerkJwtAuthentication(builder.Configuration);
        builder.Services.Configure<TaxonomyOperatorOptions>(builder.Configuration.GetSection(TaxonomyOperatorOptions.SectionName));

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
        v1.MapCategoryLifecycleEndpoints();

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

    private static string CreateValidToken(string? authSubjectOverride = null)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            claims: new[]
            {
                new Claim("sub", authSubjectOverride ?? TestAuthSubject),
            },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record SeededActorMembership(Guid HouseholdId, Guid ActorHouseholdUserId);
}
