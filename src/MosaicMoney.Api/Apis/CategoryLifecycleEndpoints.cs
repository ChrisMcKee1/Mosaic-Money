using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Taxonomy;

namespace MosaicMoney.Api.Apis;

public static class CategoryLifecycleEndpoints
{
    public static RouteGroupBuilder MapCategoryLifecycleEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            string scope,
            Guid? householdId,
            Guid? ownerUserId,
            bool includeArchived,
            CancellationToken cancellationToken) =>
        {
            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var resolvedScopeResult = ResolveScope(
                httpContext,
                scope,
                householdId,
                ownerUserId,
                actorContext,
                allowPlatformMutation: true);
            if (resolvedScopeResult.ErrorResult is not null)
            {
                return resolvedScopeResult.ErrorResult;
            }

            var resolvedScope = resolvedScopeResult.Value!;

            var categories = await ApplyScope(dbContext.Categories.AsNoTracking(), resolvedScope)
                .Include(x => x.Subcategories)
                .Where(x => includeArchived || !x.IsArchived)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var response = categories
                .Select(x => MapCategory(x, includeArchivedSubcategories: includeArchived))
                .ToList();

            return Results.Ok(response);
        });

        group.MapPost("/categories", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ICategoryLifecycleAuditTrail auditTrail,
            CreateCategoryRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors.Add(new ApiValidationError(nameof(request.Name), "Name is required."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var resolvedScopeResult = ResolveScope(
                httpContext,
                request.Scope,
                request.HouseholdId,
                request.OwnerUserId,
                actorContext,
                allowPlatformMutation: false);
            if (resolvedScopeResult.ErrorResult is not null)
            {
                return resolvedScopeResult.ErrorResult;
            }

            var resolvedScope = resolvedScopeResult.Value!;
            var trimmedName = request.Name.Trim();

            var duplicateExists = await ApplyScope(dbContext.Categories.AsNoTracking(), resolvedScope)
                .AnyAsync(x => !x.IsArchived && x.Name == trimmedName, cancellationToken);
            if (duplicateExists)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "category_name_conflict",
                    "An active category with the same name already exists in the selected scope.");
            }

            var now = DateTime.UtcNow;
            var nextDisplayOrder = request.DisplayOrder
                ?? await ResolveNextCategoryDisplayOrderAsync(dbContext, resolvedScope, cancellationToken);

            var category = new Category
            {
                Id = Guid.CreateVersion7(),
                Name = trimmedName,
                DisplayOrder = nextDisplayOrder,
                IsSystem = resolvedScope.OwnerType == CategoryOwnerType.Platform,
                OwnerType = resolvedScope.OwnerType,
                HouseholdId = resolvedScope.HouseholdId,
                OwnerUserId = resolvedScope.OwnerUserId,
                IsArchived = false,
                ArchivedAtUtc = null,
                CreatedAtUtc = now,
                LastModifiedAtUtc = now,
            };

            dbContext.Categories.Add(category);

            auditTrail.Record(
                dbContext,
                entityType: "Category",
                entityId: category.Id,
                operation: "Created",
                scopeOwnerType: category.OwnerType,
                householdId: category.HouseholdId,
                ownerUserId: category.OwnerUserId,
                performedByHouseholdUserId: actorContext.HouseholdUserId,
                metadata: new { category.Name, category.DisplayOrder });

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/categories/{category.Id}", MapCategory(category));
        });

        group.MapPatch("/categories/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ICategoryLifecycleAuditTrail auditTrail,
            Guid id,
            UpdateCategoryRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
            {
                errors.Add(new ApiValidationError(nameof(request.Name), "Name cannot be empty when provided."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var category = await dbContext.Categories
                .Include(x => x.Subcategories)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (category is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "category_not_found", "The requested category was not found.");
            }

            var authorizationResult = AuthorizeCategoryMutation(httpContext, category, actorContext);
            if (authorizationResult is not null)
            {
                return authorizationResult;
            }

            if (category.IsArchived)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "category_archived",
                    "Archived categories cannot be modified.");
            }

            var changed = false;
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                var trimmedName = request.Name.Trim();
                if (!string.Equals(category.Name, trimmedName, StringComparison.Ordinal))
                {
                    var duplicateExists = await ApplyScope(dbContext.Categories.AsNoTracking(), ResolveScopeForCategory(category))
                        .AnyAsync(
                            x =>
                                x.Id != category.Id
                                && !x.IsArchived
                                && x.Name == trimmedName,
                            cancellationToken);
                    if (duplicateExists)
                    {
                        return ApiValidation.ToConflictResult(
                            httpContext,
                            "category_name_conflict",
                            "An active category with the same name already exists in the selected scope.");
                    }

                    category.Name = trimmedName;
                    changed = true;
                }
            }

            if (!changed)
            {
                return Results.Ok(MapCategory(category));
            }

            category.LastModifiedAtUtc = DateTime.UtcNow;

            auditTrail.Record(
                dbContext,
                entityType: "Category",
                entityId: category.Id,
                operation: "Updated",
                scopeOwnerType: category.OwnerType,
                householdId: category.HouseholdId,
                ownerUserId: category.OwnerUserId,
                performedByHouseholdUserId: actorContext.HouseholdUserId,
                metadata: new { category.Name });

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(MapCategory(category));
        });

        group.MapDelete("/categories/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ICategoryLifecycleAuditTrail auditTrail,
            Guid id,
            bool? allowLinkedTransactions,
            CancellationToken cancellationToken) =>
        {
            var allowLinkedTransactionsValue = allowLinkedTransactions == true;

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var category = await dbContext.Categories
                .Include(x => x.Subcategories)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (category is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "category_not_found", "The requested category was not found.");
            }

            var authorizationResult = AuthorizeCategoryMutation(httpContext, category, actorContext);
            if (authorizationResult is not null)
            {
                return authorizationResult;
            }

            var subcategoryIds = category.Subcategories.Select(x => x.Id).ToList();
            var hasLinkedTransactions = subcategoryIds.Count > 0 && await dbContext.EnrichedTransactions
                .AnyAsync(
                    x => x.SubcategoryId.HasValue && subcategoryIds.Contains(x.SubcategoryId.Value),
                    cancellationToken);
            var hasLinkedSplits = subcategoryIds.Count > 0 && await dbContext.TransactionSplits
                .AnyAsync(
                    x => x.SubcategoryId.HasValue && subcategoryIds.Contains(x.SubcategoryId.Value),
                    cancellationToken);

            if ((hasLinkedTransactions || hasLinkedSplits) && !allowLinkedTransactionsValue)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "category_linked_transactions_exist",
                    "Linked transaction assignments exist. Retry with allowLinkedTransactions=true to archive while preserving links.");
            }

            if (!category.IsArchived)
            {
                var now = DateTime.UtcNow;
                category.IsArchived = true;
                category.ArchivedAtUtc = now;
                category.LastModifiedAtUtc = now;

                foreach (var subcategory in category.Subcategories)
                {
                    if (subcategory.IsArchived)
                    {
                        continue;
                    }

                    subcategory.IsArchived = true;
                    subcategory.ArchivedAtUtc = now;
                    subcategory.LastModifiedAtUtc = now;

                    auditTrail.Record(
                        dbContext,
                        entityType: "Subcategory",
                        entityId: subcategory.Id,
                        operation: "Archived",
                        scopeOwnerType: category.OwnerType,
                        householdId: category.HouseholdId,
                        ownerUserId: category.OwnerUserId,
                        performedByHouseholdUserId: actorContext.HouseholdUserId,
                        metadata: new { ParentCategoryId = category.Id, allowLinkedTransactions = allowLinkedTransactionsValue });
                }

                auditTrail.Record(
                    dbContext,
                    entityType: "Category",
                    entityId: category.Id,
                    operation: "Archived",
                    scopeOwnerType: category.OwnerType,
                    householdId: category.HouseholdId,
                    ownerUserId: category.OwnerUserId,
                    performedByHouseholdUserId: actorContext.HouseholdUserId,
                    metadata: new { allowLinkedTransactions = allowLinkedTransactionsValue, hasLinkedTransactions, hasLinkedSplits });

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(MapCategory(category, includeArchivedSubcategories: true));
        });

        group.MapPost("/categories/reorder", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ICategoryLifecycleAuditTrail auditTrail,
            ReorderCategoriesRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            var hasDuplicateIds = request.CategoryIds
                .GroupBy(x => x)
                .Any(x => x.Count() > 1);
            if (hasDuplicateIds)
            {
                errors.Add(new ApiValidationError(nameof(request.CategoryIds), "CategoryIds must not contain duplicates."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var resolvedScopeResult = ResolveScope(
                httpContext,
                request.Scope,
                request.HouseholdId,
                request.OwnerUserId,
                actorContext,
                allowPlatformMutation: false);
            if (resolvedScopeResult.ErrorResult is not null)
            {
                return resolvedScopeResult.ErrorResult;
            }

            var resolvedScope = resolvedScopeResult.Value!;
            var scopedCategories = await ApplyScope(dbContext.Categories, resolvedScope)
                .Include(x => x.Subcategories)
                .Where(x => !x.IsArchived)
                .ToListAsync(cancellationToken);

            var currentIds = scopedCategories.Select(x => x.Id).OrderBy(x => x).ToList();
            var requestedIds = request.CategoryIds.OrderBy(x => x).ToList();
            var exactSetMatch = currentIds.Count == requestedIds.Count && currentIds.SequenceEqual(requestedIds);
            if (!exactSetMatch)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "category_reorder_set_mismatch",
                    "Reorder requests must provide every active category ID in the selected scope exactly once.");
            }

            if (request.ExpectedLastModifiedAtUtc.HasValue)
            {
                var latestModifiedAtUtc = scopedCategories.Count == 0
                    ? DateTime.MinValue
                    : scopedCategories.Max(x => x.LastModifiedAtUtc);

                if (latestModifiedAtUtc > request.ExpectedLastModifiedAtUtc.Value)
                {
                    return ApiValidation.ToConflictResult(
                        httpContext,
                        "category_reorder_conflict",
                        "The category ordering changed since the expected revision timestamp.");
                }
            }

            var now = DateTime.UtcNow;
            var changedCategories = new List<Category>();
            for (var index = 0; index < request.CategoryIds.Count; index++)
            {
                var categoryId = request.CategoryIds[index];
                var category = scopedCategories.First(x => x.Id == categoryId);

                if (category.DisplayOrder == index)
                {
                    continue;
                }

                category.DisplayOrder = index;
                category.LastModifiedAtUtc = now;
                changedCategories.Add(category);
            }

            if (changedCategories.Count > 0)
            {
                foreach (var category in changedCategories)
                {
                    auditTrail.Record(
                        dbContext,
                        entityType: "Category",
                        entityId: category.Id,
                        operation: "Reordered",
                        scopeOwnerType: category.OwnerType,
                        householdId: category.HouseholdId,
                        ownerUserId: category.OwnerUserId,
                        performedByHouseholdUserId: actorContext.HouseholdUserId,
                        metadata: new { category.DisplayOrder });
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var response = scopedCategories
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(x => MapCategory(x))
                .ToList();

            return Results.Ok(response);
        });

        group.MapPost("/subcategories", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ICategoryLifecycleAuditTrail auditTrail,
            CreateSubcategoryRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors.Add(new ApiValidationError(nameof(request.Name), "Name is required."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var parentCategory = await dbContext.Categories
                .Include(x => x.Subcategories)
                .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken);
            if (parentCategory is null)
            {
                return ApiValidation.ToValidationResult(
                    httpContext,
                    [new ApiValidationError(nameof(request.CategoryId), "CategoryId does not exist.")]);
            }

            var authorizationResult = AuthorizeCategoryMutation(httpContext, parentCategory, actorContext);
            if (authorizationResult is not null)
            {
                return authorizationResult;
            }

            if (parentCategory.IsArchived)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "category_archived",
                    "Subcategories cannot be created under an archived category.");
            }

            var trimmedName = request.Name.Trim();
            var duplicateExists = await dbContext.Subcategories
                .AsNoTracking()
                .AnyAsync(
                    x => x.CategoryId == request.CategoryId && !x.IsArchived && x.Name == trimmedName,
                    cancellationToken);
            if (duplicateExists)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "subcategory_name_conflict",
                    "An active subcategory with the same name already exists in this category.");
            }

            var now = DateTime.UtcNow;
            var nextDisplayOrder = request.DisplayOrder
                ?? await ResolveNextSubcategoryDisplayOrderAsync(dbContext, request.CategoryId, cancellationToken);

            var subcategory = new Subcategory
            {
                Id = Guid.CreateVersion7(),
                CategoryId = request.CategoryId,
                Name = trimmedName,
                DisplayOrder = nextDisplayOrder,
                IsBusinessExpense = request.IsBusinessExpense,
                IsArchived = false,
                ArchivedAtUtc = null,
                CreatedAtUtc = now,
                LastModifiedAtUtc = now,
            };

            dbContext.Subcategories.Add(subcategory);

            auditTrail.Record(
                dbContext,
                entityType: "Subcategory",
                entityId: subcategory.Id,
                operation: "Created",
                scopeOwnerType: parentCategory.OwnerType,
                householdId: parentCategory.HouseholdId,
                ownerUserId: parentCategory.OwnerUserId,
                performedByHouseholdUserId: actorContext.HouseholdUserId,
                metadata: new { subcategory.Name, subcategory.DisplayOrder, subcategory.CategoryId });

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/subcategories/{subcategory.Id}", MapSubcategory(subcategory));
        });

        group.MapPatch("/subcategories/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ICategoryLifecycleAuditTrail auditTrail,
            Guid id,
            UpdateSubcategoryRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
            {
                errors.Add(new ApiValidationError(nameof(request.Name), "Name cannot be empty when provided."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var subcategory = await dbContext.Subcategories
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (subcategory is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "subcategory_not_found", "The requested subcategory was not found.");
            }

            var authorizationResult = AuthorizeCategoryMutation(httpContext, subcategory.Category, actorContext);
            if (authorizationResult is not null)
            {
                return authorizationResult;
            }

            if (subcategory.IsArchived)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "subcategory_archived",
                    "Archived subcategories cannot be modified.");
            }

            var changed = false;
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                var trimmedName = request.Name.Trim();
                if (!string.Equals(subcategory.Name, trimmedName, StringComparison.Ordinal))
                {
                    var duplicateExists = await dbContext.Subcategories
                        .AsNoTracking()
                        .AnyAsync(
                            x =>
                                x.Id != subcategory.Id
                                && x.CategoryId == subcategory.CategoryId
                                && !x.IsArchived
                                && x.Name == trimmedName,
                            cancellationToken);
                    if (duplicateExists)
                    {
                        return ApiValidation.ToConflictResult(
                            httpContext,
                            "subcategory_name_conflict",
                            "An active subcategory with the same name already exists in this category.");
                    }

                    subcategory.Name = trimmedName;
                    changed = true;
                }
            }

            if (request.IsBusinessExpense.HasValue && subcategory.IsBusinessExpense != request.IsBusinessExpense.Value)
            {
                subcategory.IsBusinessExpense = request.IsBusinessExpense.Value;
                changed = true;
            }

            if (request.DisplayOrder.HasValue && subcategory.DisplayOrder != request.DisplayOrder.Value)
            {
                subcategory.DisplayOrder = request.DisplayOrder.Value;
                changed = true;
            }

            if (!changed)
            {
                return Results.Ok(MapSubcategory(subcategory));
            }

            subcategory.LastModifiedAtUtc = DateTime.UtcNow;

            auditTrail.Record(
                dbContext,
                entityType: "Subcategory",
                entityId: subcategory.Id,
                operation: "Updated",
                scopeOwnerType: subcategory.Category.OwnerType,
                householdId: subcategory.Category.HouseholdId,
                ownerUserId: subcategory.Category.OwnerUserId,
                performedByHouseholdUserId: actorContext.HouseholdUserId,
                metadata: new { subcategory.Name, subcategory.DisplayOrder, subcategory.IsBusinessExpense });

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(MapSubcategory(subcategory));
        });

        group.MapPost("/subcategories/{id:guid}/reparent", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ICategoryLifecycleAuditTrail auditTrail,
            Guid id,
            ReparentSubcategoryRequest request,
            CancellationToken cancellationToken) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (request.TargetCategoryId == id)
            {
                errors.Add(new ApiValidationError(nameof(request.TargetCategoryId), "TargetCategoryId must reference a category, not the subcategory ID."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var subcategory = await dbContext.Subcategories
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (subcategory is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "subcategory_not_found", "The requested subcategory was not found.");
            }

            var sourceAuthorizationResult = AuthorizeCategoryMutation(httpContext, subcategory.Category, actorContext);
            if (sourceAuthorizationResult is not null)
            {
                return sourceAuthorizationResult;
            }

            var targetCategory = await dbContext.Categories
                .Include(x => x.Subcategories)
                .FirstOrDefaultAsync(x => x.Id == request.TargetCategoryId, cancellationToken);
            if (targetCategory is null)
            {
                return ApiValidation.ToValidationResult(
                    httpContext,
                    [new ApiValidationError(nameof(request.TargetCategoryId), "TargetCategoryId does not exist.")]);
            }

            var targetAuthorizationResult = AuthorizeCategoryMutation(httpContext, targetCategory, actorContext);
            if (targetAuthorizationResult is not null)
            {
                return targetAuthorizationResult;
            }

            if (subcategory.IsArchived)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "subcategory_archived",
                    "Archived subcategories cannot be reparented.");
            }

            if (targetCategory.IsArchived)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "target_category_archived",
                    "Subcategories cannot be moved into an archived category.");
            }

            var scopeMatches =
                subcategory.Category.OwnerType == targetCategory.OwnerType
                && subcategory.Category.HouseholdId == targetCategory.HouseholdId
                && subcategory.Category.OwnerUserId == targetCategory.OwnerUserId;
            if (!scopeMatches)
            {
                return ApiValidation.ToForbiddenResult(
                    httpContext,
                    "subcategory_scope_boundary_violation",
                    "Subcategories cannot be reparented across ownership scope boundaries.");
            }

            var duplicateExists = await dbContext.Subcategories
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id != subcategory.Id
                        && x.CategoryId == targetCategory.Id
                        && !x.IsArchived
                        && x.Name == subcategory.Name,
                    cancellationToken);
            if (duplicateExists)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "subcategory_name_conflict",
                    "An active subcategory with the same name already exists in the target category.");
            }

            var previousCategoryId = subcategory.CategoryId;
            subcategory.CategoryId = targetCategory.Id;
            subcategory.DisplayOrder = request.DisplayOrder
                ?? await ResolveNextSubcategoryDisplayOrderAsync(dbContext, targetCategory.Id, cancellationToken);
            subcategory.LastModifiedAtUtc = DateTime.UtcNow;

            auditTrail.Record(
                dbContext,
                entityType: "Subcategory",
                entityId: subcategory.Id,
                operation: "Reparented",
                scopeOwnerType: targetCategory.OwnerType,
                householdId: targetCategory.HouseholdId,
                ownerUserId: targetCategory.OwnerUserId,
                performedByHouseholdUserId: actorContext.HouseholdUserId,
                metadata: new { previousCategoryId, targetCategoryId = targetCategory.Id, subcategory.DisplayOrder });

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(MapSubcategory(subcategory));
        });

        group.MapDelete("/subcategories/{id:guid}", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            ICategoryLifecycleAuditTrail auditTrail,
            Guid id,
            bool? allowLinkedTransactions,
            CancellationToken cancellationToken) =>
        {
            var allowLinkedTransactionsValue = allowLinkedTransactions == true;

            var actor = await ResolveActiveMemberAsync(httpContext, dbContext, cancellationToken);
            if (actor.ErrorResult is not null)
            {
                return actor.ErrorResult;
            }

            var actorContext = actor.Value!;

            var subcategory = await dbContext.Subcategories
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (subcategory is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "subcategory_not_found", "The requested subcategory was not found.");
            }

            var authorizationResult = AuthorizeCategoryMutation(httpContext, subcategory.Category, actorContext);
            if (authorizationResult is not null)
            {
                return authorizationResult;
            }

            var hasLinkedTransactions = await dbContext.EnrichedTransactions
                .AnyAsync(x => x.SubcategoryId == subcategory.Id, cancellationToken);
            var hasLinkedSplits = await dbContext.TransactionSplits
                .AnyAsync(x => x.SubcategoryId == subcategory.Id, cancellationToken);

            if ((hasLinkedTransactions || hasLinkedSplits) && !allowLinkedTransactionsValue)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "subcategory_linked_transactions_exist",
                    "Linked transaction assignments exist. Retry with allowLinkedTransactions=true to archive while preserving links.");
            }

            if (!subcategory.IsArchived)
            {
                var now = DateTime.UtcNow;
                subcategory.IsArchived = true;
                subcategory.ArchivedAtUtc = now;
                subcategory.LastModifiedAtUtc = now;

                auditTrail.Record(
                    dbContext,
                    entityType: "Subcategory",
                    entityId: subcategory.Id,
                    operation: "Archived",
                    scopeOwnerType: subcategory.Category.OwnerType,
                    householdId: subcategory.Category.HouseholdId,
                    ownerUserId: subcategory.Category.OwnerUserId,
                    performedByHouseholdUserId: actorContext.HouseholdUserId,
                    metadata: new { allowLinkedTransactions = allowLinkedTransactionsValue, hasLinkedTransactions, hasLinkedSplits });

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(MapSubcategory(subcategory));
        });

        return group;
    }

    private static IQueryable<Category> ApplyScope(IQueryable<Category> query, ResolvedCategoryScope scope)
    {
        return scope.OwnerType switch
        {
            CategoryOwnerType.Platform => query.Where(x =>
                x.OwnerType == CategoryOwnerType.Platform
                && x.HouseholdId == null
                && x.OwnerUserId == null),
            CategoryOwnerType.HouseholdShared => query.Where(x =>
                x.OwnerType == CategoryOwnerType.HouseholdShared
                && x.HouseholdId == scope.HouseholdId
                && x.OwnerUserId == null),
            CategoryOwnerType.User => query.Where(x =>
                x.OwnerType == CategoryOwnerType.User
                && x.HouseholdId == scope.HouseholdId
                && x.OwnerUserId == scope.OwnerUserId),
            _ => query.Where(_ => false),
        };
    }

    private static CategoryLifecycleDto MapCategory(Category category, bool includeArchivedSubcategories = false)
    {
        var subcategories = category.Subcategories
            .Where(x => includeArchivedSubcategories || !x.IsArchived)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(MapSubcategory)
            .ToList();

        return new CategoryLifecycleDto(
            category.Id,
            category.Name,
            category.DisplayOrder,
            category.IsSystem,
            category.OwnerType.ToString(),
            category.HouseholdId,
            category.OwnerUserId,
            category.IsArchived,
            category.CreatedAtUtc,
            category.LastModifiedAtUtc,
            category.ArchivedAtUtc,
            subcategories);
    }

    private static CategorySubcategoryDto MapSubcategory(Subcategory subcategory)
    {
        return new CategorySubcategoryDto(
            subcategory.Id,
            subcategory.CategoryId,
            subcategory.Name,
            subcategory.IsBusinessExpense,
            subcategory.DisplayOrder,
            subcategory.IsArchived,
            subcategory.CreatedAtUtc,
            subcategory.LastModifiedAtUtc,
            subcategory.ArchivedAtUtc);
    }

    private static async Task<int> ResolveNextCategoryDisplayOrderAsync(
        MosaicMoneyDbContext dbContext,
        ResolvedCategoryScope scope,
        CancellationToken cancellationToken)
    {
        var maxDisplayOrder = await ApplyScope(dbContext.Categories.AsNoTracking(), scope)
            .Where(x => !x.IsArchived)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync(cancellationToken);

        return (maxDisplayOrder ?? -1) + 1;
    }

    private static async Task<int> ResolveNextSubcategoryDisplayOrderAsync(
        MosaicMoneyDbContext dbContext,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var maxDisplayOrder = await dbContext.Subcategories
            .AsNoTracking()
            .Where(x => x.CategoryId == categoryId && !x.IsArchived)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync(cancellationToken);

        return (maxDisplayOrder ?? -1) + 1;
    }

    private static IResult? AuthorizeCategoryMutation(HttpContext httpContext, Category category, ActiveMemberContext actor)
    {
        if (category.OwnerType == CategoryOwnerType.Platform)
        {
            return ApiValidation.ToForbiddenResult(
                httpContext,
                "platform_scope_mutation_denied",
                "Platform taxonomy mutations are blocked in this API lane and require an operator workflow.");
        }

        var isAllowed = category.OwnerType switch
        {
            CategoryOwnerType.HouseholdShared => category.HouseholdId == actor.HouseholdId,
            CategoryOwnerType.User => category.HouseholdId == actor.HouseholdId && category.OwnerUserId == actor.HouseholdUserId,
            _ => false,
        };

        return isAllowed
            ? null
            : ApiValidation.ToForbiddenResult(
                httpContext,
                "category_scope_access_denied",
                "The authenticated household member does not have access to mutate this category scope.");
    }

    private static ScopeResolutionResult ResolveScope(
        HttpContext httpContext,
        string scope,
        Guid? householdId,
        Guid? ownerUserId,
        ActiveMemberContext actor,
        bool allowPlatformMutation)
    {
        if (!ApiEndpointHelpers.TryParseEnum<CategoryOwnerType>(scope, out var parsedScope))
        {
            return new ScopeResolutionResult(
                null,
                ApiValidation.ToValidationResult(
                    httpContext,
                    [new ApiValidationError(nameof(scope), "Scope must be one of: Platform, HouseholdShared, User.")]));
        }

        if (parsedScope == CategoryOwnerType.Platform)
        {
            if (householdId.HasValue || ownerUserId.HasValue)
            {
                return new ScopeResolutionResult(
                    null,
                    ApiValidation.ToValidationResult(
                        httpContext,
                        [new ApiValidationError(nameof(householdId), "HouseholdId and OwnerUserId must be empty for Platform scope.")]));
            }

            if (!allowPlatformMutation)
            {
                return new ScopeResolutionResult(
                    null,
                    ApiValidation.ToForbiddenResult(
                        httpContext,
                        "platform_scope_mutation_denied",
                        "Platform taxonomy mutations are blocked in this API lane and require an operator workflow."));
            }

            return new ScopeResolutionResult(new ResolvedCategoryScope(CategoryOwnerType.Platform, null, null), null);
        }

        var resolvedHouseholdId = householdId ?? actor.HouseholdId;
        if (resolvedHouseholdId != actor.HouseholdId)
        {
            return new ScopeResolutionResult(
                null,
                ApiValidation.ToForbiddenResult(
                    httpContext,
                    "category_scope_access_denied",
                    "The authenticated household member does not have access to the requested household scope."));
        }

        if (parsedScope == CategoryOwnerType.HouseholdShared)
        {
            if (ownerUserId.HasValue)
            {
                return new ScopeResolutionResult(
                    null,
                    ApiValidation.ToValidationResult(
                        httpContext,
                        [new ApiValidationError(nameof(ownerUserId), "OwnerUserId must be empty for HouseholdShared scope.")]));
            }

            return new ScopeResolutionResult(
                new ResolvedCategoryScope(CategoryOwnerType.HouseholdShared, resolvedHouseholdId, null),
                null);
        }

        if (parsedScope == CategoryOwnerType.User)
        {
            var resolvedOwnerUserId = ownerUserId ?? actor.HouseholdUserId;
            if (resolvedOwnerUserId != actor.HouseholdUserId)
            {
                return new ScopeResolutionResult(
                    null,
                    ApiValidation.ToForbiddenResult(
                        httpContext,
                        "category_scope_access_denied",
                        "The authenticated household member can only operate on their own user scope."));
            }

            return new ScopeResolutionResult(
                new ResolvedCategoryScope(CategoryOwnerType.User, resolvedHouseholdId, resolvedOwnerUserId),
                null);
        }

        return new ScopeResolutionResult(
            null,
            ApiValidation.ToValidationResult(
                httpContext,
                [new ApiValidationError(nameof(scope), "Scope must be one of: Platform, HouseholdShared, User.")]));
    }

    private static ResolvedCategoryScope ResolveScopeForCategory(Category category)
    {
        return new ResolvedCategoryScope(category.OwnerType, category.HouseholdId, category.OwnerUserId);
    }

    private static async Task<MemberResolutionResult> ResolveActiveMemberAsync(
        HttpContext httpContext,
        MosaicMoneyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var scope = await HouseholdMemberContextResolver.ResolveAsync(
            httpContext,
            dbContext,
            householdId: null,
            "The household member is not active and cannot access category lifecycle operations.",
            cancellationToken);

        if (scope.ErrorResult is not null)
        {
            return new MemberResolutionResult(null, scope.ErrorResult);
        }

        var actor = await dbContext.HouseholdUsers
            .AsNoTracking()
            .Where(x =>
                x.Id == scope.HouseholdUserId
                && x.MembershipStatus == HouseholdMembershipStatus.Active)
            .Select(x => new ActiveMemberContext(x.Id, x.HouseholdId))
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null)
        {
            return new MemberResolutionResult(
                null,
                ApiValidation.ToForbiddenResult(
                    httpContext,
                    "membership_access_denied",
                    "The household member is not active and cannot access category lifecycle operations."));
        }

        return new MemberResolutionResult(actor, null);
    }

    private sealed record ActiveMemberContext(Guid HouseholdUserId, Guid HouseholdId);

    private sealed record MemberResolutionResult(ActiveMemberContext? Value, IResult? ErrorResult);

    private sealed record ResolvedCategoryScope(
        CategoryOwnerType OwnerType,
        Guid? HouseholdId,
        Guid? OwnerUserId);

    private sealed record ScopeResolutionResult(ResolvedCategoryScope? Value, IResult? ErrorResult);
}
