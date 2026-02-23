import type {
  PlaidLinkSessionEventLoggedDto,
  PlaidLinkTokenIssuedDto,
  PlaidPublicTokenExchangeResultDto,
  ReviewActionRequest,
  RecurringProjectionMetadataDto,
  ReimbursementProjectionMetadataDto,
  TransactionProjectionMetadataDto,
  TransactionSplitProjectionMetadataDto,
} from "./contracts";

export class SchemaValidationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "SchemaValidationError";
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function readString(value: unknown, path: string): string {
  if (typeof value !== "string") {
    throw new SchemaValidationError(`${path} must be a string.`);
  }

  return value;
}

function readOptionalString(value: unknown, path: string): string | undefined {
  if (value === undefined || value === null) {
    return undefined;
  }

  return readString(value, path);
}

function readNumber(value: unknown, path: string): number {
  if (typeof value !== "number" || Number.isNaN(value)) {
    throw new SchemaValidationError(`${path} must be a finite number.`);
  }

  return value;
}

function readBoolean(value: unknown, path: string): boolean {
  if (typeof value !== "boolean") {
    throw new SchemaValidationError(`${path} must be a boolean.`);
  }

  return value;
}

function readStringArray(value: unknown, path: string): string[] {
  if (!Array.isArray(value)) {
    throw new SchemaValidationError(`${path} must be an array.`);
  }

  return value.map((entry, index) => readString(entry, `${path}[${index}]`));
}

function parseRecurringProjectionMetadata(
  value: unknown,
  path: string,
): RecurringProjectionMetadataDto {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    isLinked: readBoolean(value.isLinked, `${path}.isLinked`),
    recurringItemId: readOptionalString(value.recurringItemId, `${path}.recurringItemId`),
    isActive: value.isActive === undefined || value.isActive === null
      ? undefined
      : readBoolean(value.isActive, `${path}.isActive`),
    frequency: readOptionalString(value.frequency, `${path}.frequency`),
    nextDueDate: readOptionalString(value.nextDueDate, `${path}.nextDueDate`),
  };
}

function parseReimbursementProjectionMetadata(
  value: unknown,
  path: string,
): ReimbursementProjectionMetadataDto {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    hasProposals: readBoolean(value.hasProposals, `${path}.hasProposals`),
    proposalCount: readNumber(value.proposalCount, `${path}.proposalCount`),
    hasPendingHumanReview: readBoolean(value.hasPendingHumanReview, `${path}.hasPendingHumanReview`),
    latestStatus: readOptionalString(value.latestStatus, `${path}.latestStatus`),
    latestStatusReasonCode: readOptionalString(value.latestStatusReasonCode, `${path}.latestStatusReasonCode`),
    pendingOrNeedsReviewAmount: readNumber(
      value.pendingOrNeedsReviewAmount,
      `${path}.pendingOrNeedsReviewAmount`,
    ),
    approvedAmount: readNumber(value.approvedAmount, `${path}.approvedAmount`),
  };
}

function parseTransactionSplitProjectionMetadata(
  value: unknown,
  path: string,
): TransactionSplitProjectionMetadataDto {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    id: readString(value.id, `${path}.id`),
    subcategoryId: readOptionalString(value.subcategoryId, `${path}.subcategoryId`),
    rawAmount: readNumber(value.rawAmount, `${path}.rawAmount`),
    amortizationMonths: readNumber(value.amortizationMonths, `${path}.amortizationMonths`),
  };
}

export function parseTransactionProjectionMetadata(
  value: unknown,
  path = "transactionProjection",
): TransactionProjectionMetadataDto {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  const splitsRaw = value.splits;
  if (!Array.isArray(splitsRaw)) {
    throw new SchemaValidationError(`${path}.splits must be an array.`);
  }

  return {
    id: readString(value.id, `${path}.id`),
    accountId: readString(value.accountId, `${path}.accountId`),
    description: readString(value.description, `${path}.description`),
    rawAmount: readNumber(value.rawAmount, `${path}.rawAmount`),
    rawTransactionDate: readString(value.rawTransactionDate, `${path}.rawTransactionDate`),
    reviewStatus: readString(value.reviewStatus, `${path}.reviewStatus`),
    reviewReason: readOptionalString(value.reviewReason, `${path}.reviewReason`),
    excludeFromBudget: readBoolean(value.excludeFromBudget, `${path}.excludeFromBudget`),
    isExtraPrincipal: readBoolean(value.isExtraPrincipal, `${path}.isExtraPrincipal`),
    recurring: parseRecurringProjectionMetadata(value.recurring, `${path}.recurring`),
    reimbursement: parseReimbursementProjectionMetadata(value.reimbursement, `${path}.reimbursement`),
    splits: splitsRaw.map((split, index) =>
      parseTransactionSplitProjectionMetadata(split, `${path}.splits[${index}]`),
    ),
    createdAtUtc: readString(value.createdAtUtc, `${path}.createdAtUtc`),
    lastModifiedAtUtc: readString(value.lastModifiedAtUtc, `${path}.lastModifiedAtUtc`),
  };
}

export function parseTransactionProjectionMetadataList(value: unknown): TransactionProjectionMetadataDto[] {
  if (!Array.isArray(value)) {
    throw new SchemaValidationError("transactionProjectionList must be an array.");
  }

  return value.map((entry, index) => parseTransactionProjectionMetadata(entry, `transactionProjectionList[${index}]`));
}

export function parsePlaidLinkTokenIssued(value: unknown): PlaidLinkTokenIssuedDto {
  const path = "plaidLinkTokenIssued";
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    linkSessionId: readString(value.linkSessionId, `${path}.linkSessionId`),
    linkToken: readString(value.linkToken, `${path}.linkToken`),
    expiresAtUtc: readString(value.expiresAtUtc, `${path}.expiresAtUtc`),
    environment: readString(value.environment, `${path}.environment`),
    products: readStringArray(value.products, `${path}.products`),
    oAuthEnabled: readBoolean(value.oAuthEnabled, `${path}.oAuthEnabled`),
    redirectUri: readOptionalString(value.redirectUri, `${path}.redirectUri`),
  };
}

export function parsePlaidLinkSessionEventLogged(value: unknown): PlaidLinkSessionEventLoggedDto {
  const path = "plaidLinkSessionEventLogged";
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    linkSessionId: readString(value.linkSessionId, `${path}.linkSessionId`),
    eventType: readString(value.eventType, `${path}.eventType`),
    loggedAtUtc: readString(value.loggedAtUtc, `${path}.loggedAtUtc`),
  };
}

export function parsePlaidPublicTokenExchangeResult(
  value: unknown,
): PlaidPublicTokenExchangeResultDto {
  const path = "plaidPublicTokenExchangeResult";
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    credentialId: readString(value.credentialId, `${path}.credentialId`),
    linkSessionId: readOptionalString(value.linkSessionId, `${path}.linkSessionId`),
    itemId: readString(value.itemId, `${path}.itemId`),
    environment: readString(value.environment, `${path}.environment`),
    status: readString(value.status, `${path}.status`),
    institutionId: readOptionalString(value.institutionId, `${path}.institutionId`),
    storedAtUtc: readString(value.storedAtUtc, `${path}.storedAtUtc`),
  };
}

export function parseReviewActionRequest(
  value: unknown,
  path = "reviewActionRequest",
): ReviewActionRequest {
  if (!isRecord(value)) {
    throw new SchemaValidationError(`${path} must be an object.`);
  }

  return {
    transactionId: readString(value.transactionId, `${path}.transactionId`),
    action: readString(value.action, `${path}.action`),
    subcategoryId: readOptionalString(value.subcategoryId, `${path}.subcategoryId`),
    reviewReason: readOptionalString(value.reviewReason, `${path}.reviewReason`),
    needsReviewByUserId: readOptionalString(value.needsReviewByUserId, `${path}.needsReviewByUserId`),
    userNote: readOptionalString(value.userNote, `${path}.userNote`),
    agentNote: readOptionalString(value.agentNote, `${path}.agentNote`),
    excludeFromBudget:
      value.excludeFromBudget === undefined || value.excludeFromBudget === null
        ? undefined
        : readBoolean(value.excludeFromBudget, `${path}.excludeFromBudget`),
    isExtraPrincipal:
      value.isExtraPrincipal === undefined || value.isExtraPrincipal === null
        ? undefined
        : readBoolean(value.isExtraPrincipal, `${path}.isExtraPrincipal`),
  };
}
