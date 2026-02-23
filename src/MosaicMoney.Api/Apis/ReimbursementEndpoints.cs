using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;

namespace MosaicMoney.Api.Apis;

public static class ReimbursementEndpoints
{
    public static RouteGroupBuilder MapReimbursementEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/reimbursements", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            string? status,
            int take = 100) =>
        {
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

            var query = dbContext.ReimbursementProposals.AsNoTracking().AsQueryable();
            if (statusFilter.HasValue)
            {
                query = query.Where(x => x.Status == statusFilter.Value);
            }

            var proposals = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(take)
                .ToListAsync();

            return Results.Ok(proposals.Select(ApiEndpointHelpers.MapReimbursement).ToList());
        });

        group.MapPost("/reimbursements", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            CreateReimbursementProposalRequest request) =>
        {
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
                .Select(x => (decimal?)x.Amount)
                .FirstOrDefaultAsync();
            if (!incomingTransactionAmount.HasValue)
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.IncomingTransactionId), "IncomingTransactionId does not exist.")]);
            }

            if (request.RelatedTransactionId.HasValue && !await dbContext.EnrichedTransactions.AnyAsync(x => x.Id == request.RelatedTransactionId.Value))
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.RelatedTransactionId), "RelatedTransactionId does not exist.")]);
            }

            if (request.RelatedTransactionSplitId.HasValue && !await dbContext.TransactionSplits.AnyAsync(x => x.Id == request.RelatedTransactionSplitId.Value))
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.RelatedTransactionSplitId), "RelatedTransactionSplitId does not exist.")]);
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
                    .MaxAsync();

                lifecycleOrdinal = (maxOrdinalInGroup ?? 0) + 1;
            }

            var lifecycleOrdinalExists = await dbContext.ReimbursementProposals
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IncomingTransactionId == request.IncomingTransactionId &&
                    x.LifecycleGroupId == lifecycleGroupId &&
                    x.LifecycleOrdinal == lifecycleOrdinal);
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
                .ToListAsync();

            var conflictRouting = ReimbursementConflictRoutingPolicy.Evaluate(new ReimbursementConflictRoutingInput(
                request.IncomingTransactionId,
                incomingTransactionAmount.Value,
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
            await dbContext.SaveChangesAsync();

            return Results.Created($"/api/v1/reimbursements/{proposal.Id}", ApiEndpointHelpers.MapReimbursement(proposal));
        });

        group.MapPost("/reimbursements/{id:guid}/decision", async (
            HttpContext httpContext,
            MosaicMoneyDbContext dbContext,
            Guid id,
            ReimbursementDecisionRequest request) =>
        {
            var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
            if (request.DecisionedByUserId == Guid.Empty)
            {
                errors.Add(new ApiValidationError(nameof(request.DecisionedByUserId), "DecisionedByUserId is required."));
            }

            if (!ReimbursementDecisionPolicy.TryParseAction(request.Action, out var action))
            {
                errors.Add(new ApiValidationError(nameof(request.Action), "Action must be one of: approve, reject."));
            }

            if (errors.Count > 0)
            {
                return ApiValidation.ToValidationResult(httpContext, errors);
            }

            if (!await dbContext.HouseholdUsers.AnyAsync(x => x.Id == request.DecisionedByUserId))
            {
                return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.DecisionedByUserId), "DecisionedByUserId does not exist.")]);
            }

            var proposal = await dbContext.ReimbursementProposals.FirstOrDefaultAsync(x => x.Id == id);
            if (proposal is null)
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
                request.DecisionedByUserId,
                DateTime.UtcNow,
                request.UserNote,
                request.AgentNote);

            await dbContext.SaveChangesAsync();
            return Results.Ok(ApiEndpointHelpers.MapReimbursement(proposal));
        });

        return group;
    }
}
