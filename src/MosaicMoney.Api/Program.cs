using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Ingestion;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddAiWorkflowIntegrationChecks();
builder.AddNpgsqlDbContext<MosaicMoneyDbContext>(
	connectionName: "mosaicmoneydb",
	configureDbContextOptions: options => options.UseNpgsql(o => o.UseVector()));
builder.Services.AddScoped<PlaidDeltaIngestionService>();

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapGet("/", () => "Mosaic Money API");

app.MapGet("/api/health", () => new { Status = "ok", Timestamp = DateTime.UtcNow });

var v1 = app.MapGroup("/api/v1");

v1.MapGet("/transactions", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	Guid? accountId,
	DateOnly? fromDate,
	DateOnly? toDate,
	string? reviewStatus,
	bool? needsReviewOnly,
	int page = 1,
	int pageSize = 50) =>
{
	var errors = new List<ApiValidationError>();
	if (page < 1)
	{
		errors.Add(new ApiValidationError(nameof(page), "Page must be greater than or equal to 1."));
	}

	if (pageSize is < 1 or > 200)
	{
		errors.Add(new ApiValidationError(nameof(pageSize), "PageSize must be between 1 and 200."));
	}

	if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
	{
		errors.Add(new ApiValidationError(nameof(fromDate), "fromDate must be less than or equal to toDate."));
	}

	TransactionReviewStatus? reviewStatusFilter = null;
	if (!string.IsNullOrWhiteSpace(reviewStatus))
	{
		if (!TryParseEnum<TransactionReviewStatus>(reviewStatus, out var parsedReviewStatus))
		{
			errors.Add(new ApiValidationError(nameof(reviewStatus), "reviewStatus must be one of: None, NeedsReview, Reviewed."));
		}
		else
		{
			reviewStatusFilter = parsedReviewStatus;
		}
	}

	if (errors.Count > 0)
	{
		return ApiValidation.ToValidationResult(httpContext, errors);
	}

	var query = dbContext.EnrichedTransactions
		.AsNoTracking()
		.Include(x => x.Splits)
		.AsQueryable();

	if (accountId.HasValue)
	{
		query = query.Where(x => x.AccountId == accountId.Value);
	}

	if (fromDate.HasValue)
	{
		query = query.Where(x => x.TransactionDate >= fromDate.Value);
	}

	if (toDate.HasValue)
	{
		query = query.Where(x => x.TransactionDate <= toDate.Value);
	}

	if (reviewStatusFilter.HasValue)
	{
		query = query.Where(x => x.ReviewStatus == reviewStatusFilter.Value);
	}

	if (needsReviewOnly == true)
	{
		query = query.Where(x => x.ReviewStatus == TransactionReviewStatus.NeedsReview);
	}

	var transactions = await query
		.OrderByDescending(x => x.TransactionDate)
		.ThenByDescending(x => x.CreatedAtUtc)
		.Skip((page - 1) * pageSize)
		.Take(pageSize)
		.ToListAsync();

	return Results.Ok(transactions.Select(MapTransaction).ToList());
});

v1.MapGet("/transactions/{id:guid}", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	Guid id) =>
{
	var transaction = await dbContext.EnrichedTransactions
		.AsNoTracking()
		.Include(x => x.Splits)
		.FirstOrDefaultAsync(x => x.Id == id);

	if (transaction is null)
	{
		return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
	}

	return Results.Ok(MapTransaction(transaction));
});

v1.MapPost("/transactions", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	CreateTransactionRequest request) =>
{
	var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

	if (request.Amount == 0)
	{
		errors.Add(new ApiValidationError(nameof(request.Amount), "Amount must be a non-zero signed value."));
	}

	if (request.TransactionDate == default)
	{
		errors.Add(new ApiValidationError(nameof(request.TransactionDate), "TransactionDate is required."));
	}

	if (string.IsNullOrWhiteSpace(request.Description))
	{
		errors.Add(new ApiValidationError(nameof(request.Description), "Description is required."));
	}

	if (!TryParseEnum<TransactionReviewStatus>(request.ReviewStatus, out var parsedReviewStatus))
	{
		errors.Add(new ApiValidationError(nameof(request.ReviewStatus), "ReviewStatus must be one of: None, NeedsReview, Reviewed."));
	}

	for (var index = 0; index < request.Splits.Count; index++)
	{
		var split = request.Splits[index];
		errors.AddRange(ApiValidation.ValidateDataAnnotations(split)
			.Select(x => x with { Field = $"Splits[{index}].{x.Field}" }));

		if (split.Amount == 0)
		{
			errors.Add(new ApiValidationError($"Splits[{index}].Amount", "Split amount must be non-zero."));
		}
	}

	if (request.Splits.Count > 0)
	{
		var splitTotal = request.Splits.Sum(x => x.Amount);
		if (decimal.Round(splitTotal, 2) != decimal.Round(request.Amount, 2))
		{
			errors.Add(new ApiValidationError(nameof(request.Splits), "Split amounts must sum to the transaction amount to preserve single-entry truth."));
		}
	}

	if (parsedReviewStatus == TransactionReviewStatus.NeedsReview)
	{
		if (request.NeedsReviewByUserId is null)
		{
			errors.Add(new ApiValidationError(nameof(request.NeedsReviewByUserId), "NeedsReviewByUserId is required when ReviewStatus is NeedsReview."));
		}

		if (string.IsNullOrWhiteSpace(request.ReviewReason))
		{
			errors.Add(new ApiValidationError(nameof(request.ReviewReason), "ReviewReason is required when ReviewStatus is NeedsReview."));
		}
	}

	if (errors.Count > 0)
	{
		return ApiValidation.ToValidationResult(httpContext, errors);
	}

	var accountExists = await dbContext.Accounts.AnyAsync(x => x.Id == request.AccountId);
	if (!accountExists)
	{
		return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.AccountId), "AccountId does not exist.")]);
	}

	if (request.RecurringItemId.HasValue && !await dbContext.RecurringItems.AnyAsync(x => x.Id == request.RecurringItemId.Value))
	{
		return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.RecurringItemId), "RecurringItemId does not exist.")]);
	}

	if (request.SubcategoryId.HasValue && !await dbContext.Subcategories.AnyAsync(x => x.Id == request.SubcategoryId.Value))
	{
		return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.SubcategoryId), "SubcategoryId does not exist.")]);
	}

	if (request.NeedsReviewByUserId.HasValue && !await dbContext.HouseholdUsers.AnyAsync(x => x.Id == request.NeedsReviewByUserId.Value))
	{
		return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.NeedsReviewByUserId), "NeedsReviewByUserId does not exist.")]);
	}

	var transaction = new EnrichedTransaction
	{
		Id = Guid.NewGuid(),
		AccountId = request.AccountId,
		RecurringItemId = request.RecurringItemId,
		SubcategoryId = request.SubcategoryId,
		NeedsReviewByUserId = request.NeedsReviewByUserId,
		PlaidTransactionId = request.PlaidTransactionId,
		Description = request.Description.Trim(),
		Amount = decimal.Round(request.Amount, 2),
		TransactionDate = request.TransactionDate,
		ReviewStatus = parsedReviewStatus,
		ReviewReason = request.ReviewReason,
		ExcludeFromBudget = request.ExcludeFromBudget,
		IsExtraPrincipal = request.IsExtraPrincipal,
		UserNote = request.UserNote,
		AgentNote = request.AgentNote,
		CreatedAtUtc = DateTime.UtcNow,
		LastModifiedAtUtc = DateTime.UtcNow,
	};

	foreach (var split in request.Splits)
	{
		transaction.Splits.Add(new TransactionSplit
		{
			Id = Guid.NewGuid(),
			Amount = decimal.Round(split.Amount, 2),
			SubcategoryId = split.SubcategoryId,
			AmortizationMonths = split.AmortizationMonths,
			UserNote = split.UserNote,
			AgentNote = split.AgentNote,
		});
	}

	dbContext.EnrichedTransactions.Add(transaction);

	try
	{
		await dbContext.SaveChangesAsync();
	}
	catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(request.PlaidTransactionId))
	{
		return ApiValidation.ToConflictResult(httpContext, "idempotency_conflict", "A transaction with the same PlaidTransactionId already exists.");
	}

	var response = await dbContext.EnrichedTransactions
		.AsNoTracking()
		.Include(x => x.Splits)
		.FirstAsync(x => x.Id == transaction.Id);

	return Results.Created($"/api/v1/transactions/{transaction.Id}", MapTransaction(response));
});

v1.MapPost("/ingestion/plaid-delta", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	PlaidDeltaIngestionService ingestionService,
	IngestPlaidDeltaRequest request,
	CancellationToken cancellationToken) =>
{
	var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

	if (request.Transactions.Count == 0)
	{
		errors.Add(new ApiValidationError(nameof(request.Transactions), "At least one transaction payload is required."));
	}

	for (var index = 0; index < request.Transactions.Count; index++)
	{
		var item = request.Transactions[index];
		errors.AddRange(ApiValidation.ValidateDataAnnotations(item)
			.Select(x => x with { Field = $"Transactions[{index}].{x.Field}" }));

		if (item.Amount == 0)
		{
			errors.Add(new ApiValidationError($"Transactions[{index}].Amount", "Amount must be a non-zero signed value to preserve single-entry semantics."));
		}

		if (item.TransactionDate == default)
		{
			errors.Add(new ApiValidationError($"Transactions[{index}].TransactionDate", "TransactionDate is required."));
		}

		if (string.IsNullOrWhiteSpace(item.Description))
		{
			errors.Add(new ApiValidationError($"Transactions[{index}].Description", "Description is required."));
		}

		if (string.IsNullOrWhiteSpace(item.RawPayloadJson))
		{
			errors.Add(new ApiValidationError($"Transactions[{index}].RawPayloadJson", "RawPayloadJson is required."));
		}
	}

	if (errors.Count > 0)
	{
		return ApiValidation.ToValidationResult(httpContext, errors);
	}

	var accountExists = await dbContext.Accounts
		.AsNoTracking()
		.AnyAsync(x => x.Id == request.AccountId, cancellationToken);
	if (!accountExists)
	{
		return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.AccountId), "AccountId does not exist.")]);
	}

	var ingestionRequest = new PlaidDeltaIngestionRequest(
		request.AccountId,
		request.DeltaCursor,
		request.Transactions
			.Select(x => new PlaidDeltaIngestionItemInput(
				x.PlaidTransactionId,
				x.Description,
				x.Amount,
				x.TransactionDate,
				x.RawPayloadJson,
				x.IsAmbiguous,
				x.ReviewReason))
			.ToList());

	var result = await ingestionService.IngestAsync(ingestionRequest, cancellationToken);
	return Results.Ok(MapPlaidDeltaIngestionResult(result));
});

v1.MapGet("/transactions/{transactionId:guid}/classification-outcomes", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	Guid transactionId) =>
{
	var transactionExists = await dbContext.EnrichedTransactions
		.AsNoTracking()
		.AnyAsync(x => x.Id == transactionId);

	if (!transactionExists)
	{
		return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
	}

	var outcomes = await dbContext.TransactionClassificationOutcomes
		.AsNoTracking()
		.Where(x => x.TransactionId == transactionId)
		.Include(x => x.StageOutputs)
		.OrderByDescending(x => x.CreatedAtUtc)
		.ToListAsync();

	return Results.Ok(outcomes.Select(MapClassificationOutcome).ToList());
});

v1.MapPost("/transactions/{transactionId:guid}/classification-outcomes", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	Guid transactionId,
	CreateClassificationOutcomeRequest request) =>
{
	var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

	if (request.StageOutputs.Count == 0)
	{
		errors.Add(new ApiValidationError(nameof(request.StageOutputs), "At least one stage output is required."));
	}

	if (!TryParseEnum<ClassificationDecision>(request.Decision, out var parsedDecision))
	{
		errors.Add(new ApiValidationError(nameof(request.Decision), "Decision must be one of: Categorized, NeedsReview."));
	}

	if (!TryParseEnum<TransactionReviewStatus>(request.ReviewStatus, out var parsedReviewStatus))
	{
		errors.Add(new ApiValidationError(nameof(request.ReviewStatus), "ReviewStatus must be one of: None, NeedsReview, Reviewed."));
	}

	if (parsedDecision == ClassificationDecision.NeedsReview && parsedReviewStatus != TransactionReviewStatus.NeedsReview)
	{
		errors.Add(new ApiValidationError(nameof(request.ReviewStatus), "ReviewStatus must be NeedsReview when Decision is NeedsReview."));
	}

	if (parsedDecision == ClassificationDecision.Categorized && parsedReviewStatus == TransactionReviewStatus.NeedsReview)
	{
		errors.Add(new ApiValidationError(nameof(request.ReviewStatus), "ReviewStatus cannot be NeedsReview when Decision is Categorized."));
	}

	var parsedStageOutputs = new List<(CreateClassificationStageOutputRequest Request, ClassificationStage Stage)>();
	for (var index = 0; index < request.StageOutputs.Count; index++)
	{
		var stageOutput = request.StageOutputs[index];
		errors.AddRange(ApiValidation.ValidateDataAnnotations(stageOutput)
			.Select(x => x with { Field = $"StageOutputs[{index}].{x.Field}" }));

		if (!TryParseEnum<ClassificationStage>(stageOutput.Stage, out var parsedStage))
		{
			errors.Add(new ApiValidationError($"StageOutputs[{index}].Stage", "Stage must be one of: Deterministic, Semantic, MafFallback."));
			continue;
		}

		var expectedStageOrder = GetExpectedStageOrder(parsedStage);
		if (stageOutput.StageOrder != expectedStageOrder)
		{
			errors.Add(new ApiValidationError($"StageOutputs[{index}].StageOrder", $"StageOrder must be {expectedStageOrder} for stage {parsedStage}."));
		}

		parsedStageOutputs.Add((stageOutput, parsedStage));
	}

	var distinctStageCount = parsedStageOutputs.Select(x => x.Stage).Distinct().Count();
	if (distinctStageCount != parsedStageOutputs.Count)
	{
		errors.Add(new ApiValidationError(nameof(request.StageOutputs), "Each stage can appear at most once per classification outcome."));
	}

	var distinctStageOrderCount = parsedStageOutputs.Select(x => x.Request.StageOrder).Distinct().Count();
	if (distinctStageOrderCount != parsedStageOutputs.Count)
	{
		errors.Add(new ApiValidationError(nameof(request.StageOutputs), "StageOrder values must be unique per classification outcome."));
	}

	if (parsedStageOutputs.Count > 0)
	{
		var maxStageOrder = parsedStageOutputs.Max(x => x.Request.StageOrder);
		if (parsedStageOutputs.Any(x => x.Request.StageOrder < maxStageOrder && !x.Request.EscalatedToNextStage))
		{
			errors.Add(new ApiValidationError(nameof(request.StageOutputs), "Intermediate stages must set EscalatedToNextStage to true when later stages are present."));
		}

		if (parsedStageOutputs.Any(x => x.Request.StageOrder == maxStageOrder && x.Request.EscalatedToNextStage))
		{
			errors.Add(new ApiValidationError(nameof(request.StageOutputs), "Final stage output cannot escalate to a next stage."));
		}
	}

	var transactionExists = await dbContext.EnrichedTransactions
		.AsNoTracking()
		.AnyAsync(x => x.Id == transactionId);
	if (!transactionExists)
	{
		return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The requested transaction was not found.");
	}

	var proposedSubcategoryIds = request.StageOutputs
		.Where(x => x.ProposedSubcategoryId.HasValue)
		.Select(x => x.ProposedSubcategoryId!.Value)
		.ToHashSet();

	if (request.ProposedSubcategoryId.HasValue)
	{
		proposedSubcategoryIds.Add(request.ProposedSubcategoryId.Value);
	}

	if (proposedSubcategoryIds.Count > 0)
	{
		var existingSubcategoryIds = await dbContext.Subcategories
			.AsNoTracking()
			.Where(x => proposedSubcategoryIds.Contains(x.Id))
			.Select(x => x.Id)
			.ToListAsync();

		var missingIds = proposedSubcategoryIds.Except(existingSubcategoryIds).ToList();
		if (missingIds.Count > 0)
		{
			errors.Add(new ApiValidationError(nameof(request.ProposedSubcategoryId), "One or more proposed subcategory references do not exist."));
		}
	}

	if (errors.Count > 0)
	{
		return ApiValidation.ToValidationResult(httpContext, errors);
	}

	var now = DateTime.UtcNow;
	var outcome = new TransactionClassificationOutcome
	{
		Id = Guid.NewGuid(),
		TransactionId = transactionId,
		ProposedSubcategoryId = request.ProposedSubcategoryId,
		FinalConfidence = decimal.Round(request.FinalConfidence, 4),
		Decision = parsedDecision,
		ReviewStatus = parsedReviewStatus,
		DecisionReasonCode = request.DecisionReasonCode.Trim(),
		DecisionRationale = request.DecisionRationale.Trim(),
		AgentNoteSummary = string.IsNullOrWhiteSpace(request.AgentNoteSummary)
			? null
			: request.AgentNoteSummary.Trim(),
		CreatedAtUtc = now,
	};

	foreach (var parsedStageOutput in parsedStageOutputs.OrderBy(x => x.Request.StageOrder))
	{
		outcome.StageOutputs.Add(new ClassificationStageOutput
		{
			Id = Guid.NewGuid(),
			Stage = parsedStageOutput.Stage,
			StageOrder = parsedStageOutput.Request.StageOrder,
			ProposedSubcategoryId = parsedStageOutput.Request.ProposedSubcategoryId,
			Confidence = decimal.Round(parsedStageOutput.Request.Confidence, 4),
			RationaleCode = parsedStageOutput.Request.RationaleCode.Trim(),
			Rationale = parsedStageOutput.Request.Rationale.Trim(),
			EscalatedToNextStage = parsedStageOutput.Request.EscalatedToNextStage,
			ProducedAtUtc = now,
		});
	}

	dbContext.TransactionClassificationOutcomes.Add(outcome);
	await dbContext.SaveChangesAsync();

	var persistedOutcome = await dbContext.TransactionClassificationOutcomes
		.AsNoTracking()
		.Include(x => x.StageOutputs)
		.FirstAsync(x => x.Id == outcome.Id);

	return Results.Created(
		$"/api/v1/transactions/{transactionId}/classification-outcomes/{outcome.Id}",
		MapClassificationOutcome(persistedOutcome));
});

v1.MapGet("/recurring", async (
	MosaicMoneyDbContext dbContext,
	Guid? householdId,
	bool? isActive) =>
{
	var query = dbContext.RecurringItems.AsNoTracking().AsQueryable();

	if (householdId.HasValue)
	{
		query = query.Where(x => x.HouseholdId == householdId.Value);
	}

	if (isActive.HasValue)
	{
		query = query.Where(x => x.IsActive == isActive.Value);
	}

	var items = await query
		.OrderBy(x => x.NextDueDate)
		.ThenBy(x => x.MerchantName)
		.ToListAsync();

	return Results.Ok(items.Select(MapRecurringItem).ToList());
});

v1.MapPost("/recurring", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	CreateRecurringItemRequest request) =>
{
	var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

	if (!TryParseEnum<RecurringFrequency>(request.Frequency, out var frequency))
	{
		errors.Add(new ApiValidationError(nameof(request.Frequency), "Frequency must be one of: Weekly, BiWeekly, Monthly, Quarterly, Annually."));
	}

	if (request.ExpectedAmount <= 0)
	{
		errors.Add(new ApiValidationError(nameof(request.ExpectedAmount), "ExpectedAmount must be greater than zero."));
	}

	if (request.NextDueDate == default)
	{
		errors.Add(new ApiValidationError(nameof(request.NextDueDate), "NextDueDate is required."));
	}

	if (!AreScoreWeightsValid(request.DueDateScoreWeight, request.AmountScoreWeight, request.RecencyScoreWeight))
	{
		errors.Add(new ApiValidationError(nameof(request.DueDateScoreWeight), "DueDateScoreWeight, AmountScoreWeight, and RecencyScoreWeight must sum to 1.0000."));
	}

	if (string.IsNullOrWhiteSpace(request.DeterministicScoreVersion))
	{
		errors.Add(new ApiValidationError(nameof(request.DeterministicScoreVersion), "DeterministicScoreVersion is required."));
	}

	if (string.IsNullOrWhiteSpace(request.TieBreakPolicy))
	{
		errors.Add(new ApiValidationError(nameof(request.TieBreakPolicy), "TieBreakPolicy is required."));
	}

	if (errors.Count > 0)
	{
		return ApiValidation.ToValidationResult(httpContext, errors);
	}

	var householdExists = await dbContext.Households.AnyAsync(x => x.Id == request.HouseholdId);
	if (!householdExists)
	{
		return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.HouseholdId), "HouseholdId does not exist.")]);
	}

	var recurringItem = new RecurringItem
	{
		Id = Guid.NewGuid(),
		HouseholdId = request.HouseholdId,
		MerchantName = request.MerchantName.Trim(),
		ExpectedAmount = decimal.Round(request.ExpectedAmount, 2),
		IsVariable = request.IsVariable,
		Frequency = frequency,
		NextDueDate = request.NextDueDate,
		DueWindowDaysBefore = request.DueWindowDaysBefore,
		DueWindowDaysAfter = request.DueWindowDaysAfter,
		AmountVariancePercent = decimal.Round(request.AmountVariancePercent, 2),
		AmountVarianceAbsolute = decimal.Round(request.AmountVarianceAbsolute, 2),
		DeterministicMatchThreshold = decimal.Round(request.DeterministicMatchThreshold, 4),
		DueDateScoreWeight = decimal.Round(request.DueDateScoreWeight, 4),
		AmountScoreWeight = decimal.Round(request.AmountScoreWeight, 4),
		RecencyScoreWeight = decimal.Round(request.RecencyScoreWeight, 4),
		DeterministicScoreVersion = request.DeterministicScoreVersion.Trim(),
		TieBreakPolicy = request.TieBreakPolicy.Trim(),
		IsActive = request.IsActive,
		UserNote = request.UserNote,
		AgentNote = request.AgentNote,
	};

	dbContext.RecurringItems.Add(recurringItem);
	await dbContext.SaveChangesAsync();

	return Results.Created($"/api/v1/recurring/{recurringItem.Id}", MapRecurringItem(recurringItem));
});

v1.MapPatch("/recurring/{id:guid}", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	Guid id,
	UpdateRecurringItemRequest request) =>
{
	var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

	RecurringFrequency? frequency = null;
	if (!string.IsNullOrWhiteSpace(request.Frequency))
	{
		if (!TryParseEnum<RecurringFrequency>(request.Frequency, out var parsedFrequency))
		{
			errors.Add(new ApiValidationError(nameof(request.Frequency), "Frequency must be one of: Weekly, BiWeekly, Monthly, Quarterly, Annually."));
		}
		else
		{
			frequency = parsedFrequency;
		}
	}

	if (request.ExpectedAmount.HasValue && request.ExpectedAmount <= 0)
	{
		errors.Add(new ApiValidationError(nameof(request.ExpectedAmount), "ExpectedAmount must be greater than zero when provided."));
	}

	var dueDateWeight = request.DueDateScoreWeight;
	var amountWeight = request.AmountScoreWeight;
	var recencyWeight = request.RecencyScoreWeight;
	if (dueDateWeight.HasValue || amountWeight.HasValue || recencyWeight.HasValue)
	{
		var resolvedDueDateWeight = dueDateWeight ?? 0m;
		var resolvedAmountWeight = amountWeight ?? 0m;
		var resolvedRecencyWeight = recencyWeight ?? 0m;

		if (!(dueDateWeight.HasValue && amountWeight.HasValue && recencyWeight.HasValue))
		{
			errors.Add(new ApiValidationError(nameof(request.DueDateScoreWeight), "DueDateScoreWeight, AmountScoreWeight, and RecencyScoreWeight must be patched together."));
		}
		else if (!AreScoreWeightsValid(resolvedDueDateWeight, resolvedAmountWeight, resolvedRecencyWeight))
		{
			errors.Add(new ApiValidationError(nameof(request.DueDateScoreWeight), "DueDateScoreWeight, AmountScoreWeight, and RecencyScoreWeight must sum to 1.0000."));
		}
	}

	if (request.DeterministicScoreVersion is not null && string.IsNullOrWhiteSpace(request.DeterministicScoreVersion))
	{
		errors.Add(new ApiValidationError(nameof(request.DeterministicScoreVersion), "DeterministicScoreVersion cannot be empty when provided."));
	}

	if (request.TieBreakPolicy is not null && string.IsNullOrWhiteSpace(request.TieBreakPolicy))
	{
		errors.Add(new ApiValidationError(nameof(request.TieBreakPolicy), "TieBreakPolicy cannot be empty when provided."));
	}

	if (errors.Count > 0)
	{
		return ApiValidation.ToValidationResult(httpContext, errors);
	}

	var recurringItem = await dbContext.RecurringItems.FirstOrDefaultAsync(x => x.Id == id);
	if (recurringItem is null)
	{
		return ApiValidation.ToNotFoundResult(httpContext, "recurring_not_found", "The requested recurring item was not found.");
	}

	if (!string.IsNullOrWhiteSpace(request.MerchantName))
	{
		recurringItem.MerchantName = request.MerchantName.Trim();
	}

	if (request.ExpectedAmount.HasValue)
	{
		recurringItem.ExpectedAmount = decimal.Round(request.ExpectedAmount.Value, 2);
	}

	if (request.IsVariable.HasValue)
	{
		recurringItem.IsVariable = request.IsVariable.Value;
	}

	if (frequency.HasValue)
	{
		recurringItem.Frequency = frequency.Value;
	}

	if (request.NextDueDate.HasValue)
	{
		recurringItem.NextDueDate = request.NextDueDate.Value;
	}

	if (request.DueWindowDaysBefore.HasValue)
	{
		recurringItem.DueWindowDaysBefore = request.DueWindowDaysBefore.Value;
	}

	if (request.DueWindowDaysAfter.HasValue)
	{
		recurringItem.DueWindowDaysAfter = request.DueWindowDaysAfter.Value;
	}

	if (request.AmountVariancePercent.HasValue)
	{
		recurringItem.AmountVariancePercent = decimal.Round(request.AmountVariancePercent.Value, 2);
	}

	if (request.AmountVarianceAbsolute.HasValue)
	{
		recurringItem.AmountVarianceAbsolute = decimal.Round(request.AmountVarianceAbsolute.Value, 2);
	}

	if (request.DeterministicMatchThreshold.HasValue)
	{
		recurringItem.DeterministicMatchThreshold = decimal.Round(request.DeterministicMatchThreshold.Value, 4);
	}

	if (request.DueDateScoreWeight.HasValue)
	{
		recurringItem.DueDateScoreWeight = decimal.Round(request.DueDateScoreWeight.Value, 4);
	}

	if (request.AmountScoreWeight.HasValue)
	{
		recurringItem.AmountScoreWeight = decimal.Round(request.AmountScoreWeight.Value, 4);
	}

	if (request.RecencyScoreWeight.HasValue)
	{
		recurringItem.RecencyScoreWeight = decimal.Round(request.RecencyScoreWeight.Value, 4);
	}

	if (!string.IsNullOrWhiteSpace(request.DeterministicScoreVersion))
	{
		recurringItem.DeterministicScoreVersion = request.DeterministicScoreVersion.Trim();
	}

	if (!string.IsNullOrWhiteSpace(request.TieBreakPolicy))
	{
		recurringItem.TieBreakPolicy = request.TieBreakPolicy.Trim();
	}

	if (request.IsActive.HasValue)
	{
		recurringItem.IsActive = request.IsActive.Value;
	}

	recurringItem.UserNote = request.UserNote ?? recurringItem.UserNote;
	recurringItem.AgentNote = request.AgentNote ?? recurringItem.AgentNote;

	await dbContext.SaveChangesAsync();

	return Results.Ok(MapRecurringItem(recurringItem));
});

v1.MapPost("/review-actions", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	ReviewActionRequest request) =>
{
	var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
	var actionValue = request.Action?.Trim() ?? string.Empty;
	var parsedAction = default(TransactionReviewAction);

	if (!TransactionReviewStateMachine.TryParseAction(actionValue, out parsedAction))
	{
		errors.Add(new ApiValidationError(nameof(request.Action), "Action must be one of: approve, reclassify, route_to_needs_review."));
	}

	if (parsedAction == TransactionReviewAction.Reclassify && request.SubcategoryId is null)
	{
		errors.Add(new ApiValidationError(nameof(request.SubcategoryId), "SubcategoryId is required for reclassify action."));
	}

	if (parsedAction == TransactionReviewAction.RouteToNeedsReview)
	{
		if (request.NeedsReviewByUserId is null)
		{
			errors.Add(new ApiValidationError(nameof(request.NeedsReviewByUserId), "NeedsReviewByUserId is required for route_to_needs_review action."));
		}

		if (string.IsNullOrWhiteSpace(request.ReviewReason))
		{
			errors.Add(new ApiValidationError(nameof(request.ReviewReason), "ReviewReason is required for route_to_needs_review action."));
		}
	}

	if (errors.Count > 0)
	{
		return ApiValidation.ToValidationResult(httpContext, errors);
	}

	var transaction = await dbContext.EnrichedTransactions
		.Include(x => x.Splits)
		.FirstOrDefaultAsync(x => x.Id == request.TransactionId);

	if (transaction is null)
	{
		return ApiValidation.ToNotFoundResult(httpContext, "transaction_not_found", "The transaction for the requested review action was not found.");
	}

	if (request.SubcategoryId.HasValue && !await dbContext.Subcategories.AnyAsync(x => x.Id == request.SubcategoryId.Value))
	{
		return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.SubcategoryId), "SubcategoryId does not exist.")]);
	}

	if (request.NeedsReviewByUserId.HasValue && !await dbContext.HouseholdUsers.AnyAsync(x => x.Id == request.NeedsReviewByUserId.Value))
	{
		return ApiValidation.ToValidationResult(httpContext, [new ApiValidationError(nameof(request.NeedsReviewByUserId), "NeedsReviewByUserId does not exist.")]);
	}

	if (!TransactionReviewStateMachine.TryTransition(transaction.ReviewStatus, parsedAction, out var targetStatus))
	{
		return ApiValidation.ToConflictResult(
			httpContext,
			"invalid_review_transition",
			$"Action '{actionValue}' is not allowed when ReviewStatus is '{transaction.ReviewStatus}'.");
	}

	switch (parsedAction)
	{
		case TransactionReviewAction.Approve:
			transaction.ReviewStatus = targetStatus;
			transaction.ReviewReason = null;
			transaction.NeedsReviewByUserId = null;
			break;
		case TransactionReviewAction.Reclassify:
			transaction.SubcategoryId = request.SubcategoryId;
			transaction.ReviewStatus = targetStatus;
			transaction.ReviewReason = request.ReviewReason;
			transaction.NeedsReviewByUserId = null;
			break;
		case TransactionReviewAction.RouteToNeedsReview:
			transaction.ReviewStatus = targetStatus;
			transaction.ReviewReason = request.ReviewReason;
			transaction.NeedsReviewByUserId = request.NeedsReviewByUserId;
			break;
	}

	if (request.ExcludeFromBudget.HasValue)
	{
		transaction.ExcludeFromBudget = request.ExcludeFromBudget.Value;
	}

	if (request.IsExtraPrincipal.HasValue)
	{
		transaction.IsExtraPrincipal = request.IsExtraPrincipal.Value;
	}

	transaction.UserNote = request.UserNote ?? transaction.UserNote;
	transaction.AgentNote = request.AgentNote ?? transaction.AgentNote;
	transaction.LastModifiedAtUtc = DateTime.UtcNow;

	await dbContext.SaveChangesAsync();

	return Results.Ok(MapTransaction(transaction));
});

v1.MapGet("/reimbursements", async (
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
		if (!TryParseEnum<ReimbursementProposalStatus>(status, out var parsedStatus))
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

	return Results.Ok(proposals.Select(MapReimbursement).ToList());
});

v1.MapPost("/reimbursements", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	CreateReimbursementProposalRequest request) =>
{
	var errors = ApiValidation.ValidateDataAnnotations(request).ToList();

	if (request.ProposedAmount <= 0)
	{
		errors.Add(new ApiValidationError(nameof(request.ProposedAmount), "ProposedAmount must be greater than zero."));
	}

	if (!TryParseEnum<ReimbursementProposalSource>(request.ProposalSource, out var parsedProposalSource))
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

	if (!await dbContext.EnrichedTransactions.AnyAsync(x => x.Id == request.IncomingTransactionId))
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

	var proposal = new ReimbursementProposal
	{
		Id = Guid.NewGuid(),
		IncomingTransactionId = request.IncomingTransactionId,
		RelatedTransactionId = request.RelatedTransactionId,
		RelatedTransactionSplitId = request.RelatedTransactionSplitId,
		ProposedAmount = decimal.Round(request.ProposedAmount, 2),
		LifecycleGroupId = lifecycleGroupId,
		LifecycleOrdinal = lifecycleOrdinal,
		Status = ReimbursementProposalStatus.PendingApproval,
		StatusReasonCode = request.StatusReasonCode.Trim(),
		StatusRationale = request.StatusRationale.Trim(),
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

	return Results.Created($"/api/v1/reimbursements/{proposal.Id}", MapReimbursement(proposal));
});

v1.MapPost("/reimbursements/{id:guid}/decision", async (
	HttpContext httpContext,
	MosaicMoneyDbContext dbContext,
	Guid id,
	ReimbursementDecisionRequest request) =>
{
	var errors = ApiValidation.ValidateDataAnnotations(request).ToList();
	var action = request.Action?.Trim().ToLowerInvariant() ?? string.Empty;

	if (action is not ("approve" or "reject"))
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

	proposal.Status = action == "approve"
		? ReimbursementProposalStatus.Approved
		: ReimbursementProposalStatus.Rejected;
	proposal.StatusReasonCode = action == "approve"
		? "approved_by_human"
		: "rejected_by_human";
	proposal.StatusRationale = action == "approve"
		? "Proposal approved by human reviewer."
		: "Proposal rejected by human reviewer.";
	proposal.DecisionedByUserId = request.DecisionedByUserId;
	proposal.DecisionedAtUtc = DateTime.UtcNow;
	proposal.UserNote = request.UserNote ?? proposal.UserNote;
	proposal.AgentNote = request.AgentNote ?? proposal.AgentNote;

	await dbContext.SaveChangesAsync();
	return Results.Ok(MapReimbursement(proposal));
});

app.Run();

static bool TryParseEnum<TEnum>(string value, out TEnum parsed) where TEnum : struct, Enum
{
	return Enum.TryParse<TEnum>(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
}

static TransactionDto MapTransaction(EnrichedTransaction transaction)
{
	return new TransactionDto(
		transaction.Id,
		transaction.AccountId,
		transaction.RecurringItemId,
		transaction.SubcategoryId,
		transaction.NeedsReviewByUserId,
		transaction.PlaidTransactionId,
		transaction.Description,
		transaction.Amount,
		transaction.TransactionDate,
		transaction.ReviewStatus.ToString(),
		transaction.ReviewReason,
		transaction.ExcludeFromBudget,
		transaction.IsExtraPrincipal,
		transaction.UserNote,
		transaction.AgentNote,
		transaction.Splits
			.OrderBy(x => x.Id)
			.Select(x => new TransactionSplitDto(
				x.Id,
				x.SubcategoryId,
				x.Amount,
				x.AmortizationMonths,
				x.UserNote,
				x.AgentNote))
			.ToList(),
		transaction.CreatedAtUtc,
		transaction.LastModifiedAtUtc);
}

static RecurringItemDto MapRecurringItem(RecurringItem recurringItem)
{
	return new RecurringItemDto(
		recurringItem.Id,
		recurringItem.HouseholdId,
		recurringItem.MerchantName,
		recurringItem.ExpectedAmount,
		recurringItem.IsVariable,
		recurringItem.Frequency.ToString(),
		recurringItem.NextDueDate,
		recurringItem.DueWindowDaysBefore,
		recurringItem.DueWindowDaysAfter,
		recurringItem.AmountVariancePercent,
		recurringItem.AmountVarianceAbsolute,
		recurringItem.DeterministicMatchThreshold,
		recurringItem.DueDateScoreWeight,
		recurringItem.AmountScoreWeight,
		recurringItem.RecencyScoreWeight,
		recurringItem.DeterministicScoreVersion,
		recurringItem.TieBreakPolicy,
		recurringItem.IsActive,
		recurringItem.UserNote,
		recurringItem.AgentNote);
}

static ReimbursementProposalDto MapReimbursement(ReimbursementProposal proposal)
{
	return new ReimbursementProposalDto(
		proposal.Id,
		proposal.IncomingTransactionId,
		proposal.RelatedTransactionId,
		proposal.RelatedTransactionSplitId,
		proposal.ProposedAmount,
		proposal.LifecycleGroupId,
		proposal.LifecycleOrdinal,
		proposal.Status.ToString(),
		proposal.StatusReasonCode,
		proposal.StatusRationale,
		proposal.ProposalSource.ToString(),
		proposal.ProvenanceSource,
		proposal.ProvenanceReference,
		proposal.ProvenancePayloadJson,
		proposal.SupersedesProposalId,
		proposal.DecisionedByUserId,
		proposal.DecisionedAtUtc,
		proposal.UserNote,
		proposal.AgentNote,
		proposal.CreatedAtUtc);
}

static bool AreScoreWeightsValid(decimal dueDateWeight, decimal amountWeight, decimal recencyWeight)
{
	return decimal.Round(dueDateWeight + amountWeight + recencyWeight, 4) == 1.0000m;
}

static ClassificationOutcomeDto MapClassificationOutcome(TransactionClassificationOutcome outcome)
{
	return new ClassificationOutcomeDto(
		outcome.Id,
		outcome.TransactionId,
		outcome.ProposedSubcategoryId,
		outcome.FinalConfidence,
		outcome.Decision.ToString(),
		outcome.ReviewStatus.ToString(),
		outcome.DecisionReasonCode,
		outcome.DecisionRationale,
		outcome.AgentNoteSummary,
		outcome.CreatedAtUtc,
		outcome.StageOutputs
			.OrderBy(x => x.StageOrder)
			.Select(x => new ClassificationStageOutputDto(
				x.Id,
				x.Stage.ToString(),
				x.StageOrder,
				x.ProposedSubcategoryId,
				x.Confidence,
				x.RationaleCode,
				x.Rationale,
				x.EscalatedToNextStage,
				x.ProducedAtUtc))
			.ToList());
}

static PlaidDeltaIngestionResultDto MapPlaidDeltaIngestionResult(PlaidDeltaIngestionResult result)
{
	return new PlaidDeltaIngestionResultDto(
		result.RawStoredCount,
		result.RawDuplicateCount,
		result.InsertedCount,
		result.UpdatedCount,
		result.UnchangedCount,
		result.Items.Select(x => new PlaidDeltaIngestionItemResultDto(
			x.PlaidTransactionId,
			x.EnrichedTransactionId,
			x.RawDuplicate,
			x.Disposition.ToString(),
			x.ReviewStatus.ToString(),
			x.ReviewReason)).ToList());
}

static int GetExpectedStageOrder(ClassificationStage stage)
{
	return stage switch
	{
		ClassificationStage.Deterministic => 1,
		ClassificationStage.Semantic => 2,
		ClassificationStage.MafFallback => 3,
		_ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown classification stage."),
	};
}
