using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Transactions;

namespace MosaicMoney.Api.Apis;

public static class ReimbursementEndpoints
{
    public static RouteGroupBuilder MapReimbursementEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/reimbursements", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            string? status,
            int take = 100,
            CancellationToken cancellationToken = default) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot access reimbursements.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var errors = new List<ApiValidationError>();
            if (take is < 1 or > 500)
            {
                errors.Add(new ApiValidationError(nameof(take), "take must be between 1 and 500."));
            }

            ReimbursementProposalStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!ApiEndpointHelpers.TryParseEnum<ReimbursementProposalStatus>(status, out var parsedStatus))
                {
                    errors.Add(new ApiValidationError(nameof(status), "status must be one of: PendingApproval, Approved, Rejected, NeedsReview, Superseded, Cancelled."));
                }
                else
                {
                    statusFilter = parsedStatus;
                }
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var readableAccountIds = BuildReadableAccountIdsQuery(dbContext, accessScope.HouseholdUserId);
            var readableTransactionIds = dbContext.EnrichedTransactions
                .AsNoTracking()
                .Where(x => readableAccountIds.Contains(x.AccountId))
                .Select(x => x.Id);

            var query = dbContext.ReimbursementProposals
                .AsNoTracking()
                .Where(x => readableTransactionIds.Contains(x.IncomingTransactionId))
                .AsQueryable();
            if (statusFilter.HasValue)
            {
                query = query.Where(x => x.Status == statusFilter.Value);
            }

            var proposals = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(take)
                .ToListAsync(cancellationToken);

            return Results.Ok(proposals.Select(ApiEndpointHelpers.MapReimbursement).ToList());
        });

        group.MapPost("/reimbursements", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            [FromServices] ITransactionAccessQueryService transactionAccessQueryService,
            CreateReimbursementProposalRequest request,
            CancellationToken cancellationToken = default) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot create reimbursement proposals.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

            if (request.ProposedAmount <= 0)
            {
                errors.Add(new ApiValidationError(nameof(request.ProposedAmount), "ProposedAmount must be greater than zero."));
            }

            if (!ApiEndpointHelpers.TryParseEnum<ReimbursementProposalSource>(request.ProposalSource, out var parsedProposalSource))
            {
                errors.Add(new ApiValidationError(nameof(request.ProposalSource), "ProposalSource must be one of: Deterministic, Manual."));
            }

            if (string.IsNullOrWhiteSpace(request.StatusReasonCode))
            {
                errors.Add(new ApiValidationError(nameof(request.StatusReasonCode), "StatusReasonCode is required."));
            }

            if (string.IsNullOrWhiteSpace(request.StatusRationale))
            {
                errors.Add(new ApiValidationError(nameof(request.StatusRationale), "StatusRationale is required."));
            }

            if (string.IsNullOrWhiteSpace(request.ProvenanceSource))
            {
                errors.Add(new ApiValidationError(nameof(request.ProvenanceSource), "ProvenanceSource is required."));
            }

            var hasRelatedTransaction = request.RelatedTransactionId.HasValue;
            var hasRelatedSplit = request.RelatedTransactionSplitId.HasValue;
            if (hasRelatedTransaction == hasRelatedSplit)
            {
                errors.Add(new ApiValidationError(nameof(request.RelatedTransactionId), "Exactly one of RelatedTransactionId or RelatedTransactionSplitId is required."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var incomingTransactionAmount = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Where(x => x.Id == request.IncomingTransactionId)
                .Select(x => new
                {
                    x.Amount,
                    x.AccountId,
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (incomingTransactionAmount is null)
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.IncomingTransactionId), "IncomingTransactionId does not exist.")]);
            }

            if (!await transactionAccessQueryService.CanReadAccountAsync(
                    accessScope.HouseholdUserId,
                    incomingTransactionAmount.AccountId,
                    cancellationToken))
            {
                return ApiValidation.ToForbiddenResult(
                    httpContext,
                    "transaction_access_denied",
                    "The authenticated household member does not have access to the incoming transaction.");
            }

            if (request.RelatedTransactionId.HasValue)
            {
                var relatedTransaction = await dbContext.EnrichedTransactions
                    .AsNoTracking()
                    .Where(x => x.Id == request.RelatedTransactionId.Value)
                    .Select(x => new
                    {
                        x.Id,
                        x.AccountId,
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (relatedTransaction is null)
                {
                    return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.RelatedTransactionId), "RelatedTransactionId does not exist.")]);
                }

                if (!await transactionAccessQueryService.CanReadAccountAsync(
                        accessScope.HouseholdUserId,
                        relatedTransaction.AccountId,
                        cancellationToken))
                {
                    return ApiValidation.ToForbiddenResult(
                        httpContext,
                        "transaction_access_denied",
                        "The authenticated household member does not have access to the related transaction.");
                }
            }

            if (request.RelatedTransactionSplitId.HasValue)
            {
                var relatedSplit = await dbContext.TransactionSplits
                    .AsNoTracking()
                    .Where(x => x.Id == request.RelatedTransactionSplitId.Value)
                    .Select(x => new
                    {
                        x.Id,
                        x.ParentTransaction.AccountId,
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (relatedSplit is null)
                {
                    return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.RelatedTransactionSplitId), "RelatedTransactionSplitId does not exist.")]);
                }

                if (!await transactionAccessQueryService.CanReadAccountAsync(
                        accessScope.HouseholdUserId,
                        relatedSplit.AccountId,
                        cancellationToken))
                {
                    return ApiValidation.ToForbiddenResult(
                        httpContext,
                        "transaction_access_denied",
                        "The authenticated household member does not have access to the related transaction split.");
                }
            }

            ReimbursementProposal? supersededProposal = null;
            if (request.SupersedesProposalId.HasValue)
            {
                supersededProposal = await dbContext.ReimbursementProposals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.SupersedesProposalId.Value);

                if (supersededProposal is null)
                {
                    return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.SupersedesProposalId), "SupersedesProposalId does not exist.")]);
                }

                if (supersededProposal.IncomingTransactionId != request.IncomingTransactionId)
                {
                    return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.SupersedesProposalId), "Superseded proposal must reference the same IncomingTransactionId.")]);
                }

                if (request.LifecycleGroupId.HasValue && request.LifecycleGroupId.Value != supersededProposal.LifecycleGroupId)
                {
                    return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.LifecycleGroupId), "LifecycleGroupId must match the superseded proposal lifecycle group when SupersedesProposalId is provided.")]);
                }
            }

            var lifecycleGroupId = request.LifecycleGroupId
                ?? supersededProposal?.LifecycleGroupId
                ?? Guid.NewGuid();
            var lifecycleOrdinal = request.LifecycleOrdinal ?? 1;

            if (!request.LifecycleOrdinal.HasValue)
            {
                var maxOrdinalInGroup = await dbContext.ReimbursementProposals
                    .AsNoTracking()
                    .Where(x => x.IncomingTransactionId == request.IncomingTransactionId && x.LifecycleGroupId == lifecycleGroupId)
                    .Select(x => (int?)x.LifecycleOrdinal)
                    .MaxAsync(cancellationToken);

                lifecycleOrdinal = (maxOrdinalInGroup ?? 0) + 1;
            }

            var lifecycleOrdinalExists = await dbContext.ReimbursementProposals
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IncomingTransactionId == request.IncomingTransactionId &&
                    x.LifecycleGroupId == lifecycleGroupId &&
                    x.LifecycleOrdinal == lifecycleOrdinal,
                    cancellationToken);
            if (lifecycleOrdinalExists)
            {
                return ApiValidation.ToConflictResult(
                    httpContext,
                    "reimbursement_lifecycle_conflict",
                    "A reimbursement proposal already exists for this lifecycle group and ordinal.");
            }

            var existingProposals = await dbContext.ReimbursementProposals
                .AsNoTracking()
                .Where(x => x.IncomingTransactionId == request.IncomingTransactionId)
                .ToListAsync(cancellationToken);

            var conflictRouting = ReimbursementConflictRoutingPolicy.Evaluate(new ReimbursementConflictRoutingInput(
                request.IncomingTransactionId,
                incomingTransactionAmount.Amount,
                request.RelatedTransactionId,
                request.RelatedTransactionSplitId,
                request.ProposedAmount,
                lifecycleGroupId,
                lifecycleOrdinal,
                request.SupersedesProposalId,
                supersededProposal,
                existingProposals));

            var initialStatus = conflictRouting.RouteToNeedsReview
                ? ReimbursementProposalStatus.NeedsReview
                : ReimbursementProposalStatus.PendingApproval;

            var initialStatusReasonCode = conflictRouting.RouteToNeedsReview
                ? conflictRouting.ReasonCode!
                : request.StatusReasonCode.Trim();

            var initialStatusRationale = conflictRouting.RouteToNeedsReview
                ? conflictRouting.Rationale!
                : request.StatusRationale.Trim();

            var proposal = new ReimbursementProposal
            {
                Id = Guid.NewGuid(),
                IncomingTransactionId = request.IncomingTransactionId,
                RelatedTransactionId = request.RelatedTransactionId,
                RelatedTransactionSplitId = request.RelatedTransactionSplitId,
                ProposedAmount = decimal.Round(request.ProposedAmount, 2),
                LifecycleGroupId = lifecycleGroupId,
                LifecycleOrdinal = lifecycleOrdinal,
                Status = initialStatus,
                StatusReasonCode = initialStatusReasonCode,
                StatusRationale = initialStatusRationale,
                ProposalSource = parsedProposalSource,
                ProvenanceSource = request.ProvenanceSource.Trim(),
                ProvenanceReference = request.ProvenanceReference,
                ProvenancePayloadJson = request.ProvenancePayloadJson,
                SupersedesProposalId = request.SupersedesProposalId,
                UserNote = request.UserNote,
                AgentNote = request.AgentNote,
                CreatedAtUtc = DateTime.UtcNow,
            };

            dbContext.ReimbursementProposals.Add(proposal);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/reimbursements/{proposal.Id}", ApiEndpointHelpers.MapReimbursement(proposal));
        });

        group.MapPost("/reimbursements/{id:guid}/decision", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            ReimbursementDecisionRequest request,
            CancellationToken cancellationToken = default) =>
        {
            var accessScope = await AuthenticatedHouseholdScopeResolver.ResolveAsync(
                httpContext,
                dbContext,
                householdId: null,
                "The authenticated household member is not active and cannot decision reimbursements.",
                cancellationToken);
            if (accessScope.ErrorResult is not null)
            {
                return accessScope.ErrorResult;
            }

            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (request.DecisionedByUserId == Guid.Empty)
            {
                errors.Add(new ApiValidationError(nameof(request.DecisionedByUserId), "DecisionedByUserId is required."));
            }
            else if (request.DecisionedByUserId != accessScope.HouseholdUserId)
            {
                errors.Add(new ApiValidationError(nameof(request.DecisionedByUserId), "DecisionedByUserId must match the authenticated household member."));
            }

            if (!ReimbursementDecisionPolicy.TryParseAction(request.Action, out var action))
            {
                errors.Add(new ApiValidationError(nameof(request.Action), "Action must be one of: approve, reject."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            var readableAccountIds = BuildReadableAccountIdsQuery(dbContext, accessScope.HouseholdUserId);
            var proposal = await dbContext.ReimbursementProposals.FirstOrDefaultAsync(x => x.Id == id);
            if (proposal is null)
            {
                return ApiValidation.ToNotFoundResult(httpContext, "reimbursement_not_found", "The reimbursement proposal was not found.");
            }

            var incomingAccountId = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Where(x => x.Id == proposal.IncomingTransactionId)
                .Select(x => (Guid?)x.AccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!incomingAccountId.HasValue || !await readableAccountIds.AnyAsync(x => x == incomingAccountId.Value, cancellationToken))
            {
                return ApiValidation.ToNotFoundResult(httpContext, "reimbursement_not_found", "The reimbursement proposal was not found.");
            }

            if (proposal.Status != ReimbursementProposalStatus.PendingApproval)
            {
                return ApiValidation.ToConflictResult(httpContext, "reimbursement_not_pending", "Only pending reimbursement proposals can be decisioned.");
            }

            ReimbursementDecisionPolicy.ApplyDecision(
                proposal,
                action,
                accessScope.HouseholdUserId,
                DateTime.UtcNow,
                request.UserNote,
                request.AgentNote);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(ApiEndpointHelpers.MapReimbursement(proposal));
        });

        return group;
    }

    private static IQueryable<Guid> BuildReadableAccountIdsQuery(MosaicMoneyDbContext dbContext, Guid householdUserId)
    {
        return dbContext.AccountMemberAccessEntries
            .AsNoTracking()
            .Where(x =>
                x.HouseholdUserId == householdUserId
                && x.Visibility == AccountAccessVisibility.Visible
                && x.AccessRole != AccountAccessRole.None
                && x.HouseholdUser.MembershipStatus == HouseholdMembershipStatus.Active)
            .Select(x => x.AccountId);
    }
}
