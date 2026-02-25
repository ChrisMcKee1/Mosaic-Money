// Shared domain contracts aligned with backend payloads

export interface ApiErrorResponse {
  error: ApiErrorEnvelope;
}

export interface ApiErrorEnvelope {
  code: string;
  message: string;
  traceId: string;
  details?: ApiValidationError[];
}

export interface ApiValidationError {
  field: string;
  message: string;
}

export interface TransactionSplitDto {
  id: string;
  subcategoryId?: string;
  amount: number;
  amortizationMonths: number;
  userNote?: string;
  agentNote?: string;
}

export interface TransactionDto {
  id: string;
  accountId: string;
  recurringItemId?: string;
  subcategoryId?: string;
  needsReviewByUserId?: string;
  plaidTransactionId?: string;
  description: string;
  amount: number;
  transactionDate: string; // DateOnly mapped to string (YYYY-MM-DD)
  reviewStatus: string;
  reviewReason?: string;
  excludeFromBudget: boolean;
  isExtraPrincipal: boolean;
  userNote?: string;
  agentNote?: string;
  splits: TransactionSplitDto[];
  createdAtUtc: string; // DateTime mapped to ISO string
  lastModifiedAtUtc: string;
}

export interface TransactionSplitProjectionMetadataDto {
  id: string;
  subcategoryId?: string;
  rawAmount: number;
  amortizationMonths: number;
}

export interface RecurringProjectionMetadataDto {
  isLinked: boolean;
  recurringItemId?: string;
  isActive?: boolean;
  frequency?: string;
  nextDueDate?: string;
}

export interface ReimbursementProjectionMetadataDto {
  hasProposals: boolean;
  proposalCount: number;
  hasPendingHumanReview: boolean;
  latestStatus?: string;
  latestStatusReasonCode?: string;
  pendingOrNeedsReviewAmount: number;
  approvedAmount: number;
}

export interface TransactionProjectionMetadataDto {
  id: string;
  accountId: string;
  description: string;
  rawAmount: number;
  rawTransactionDate: string;
  reviewStatus: string;
  reviewReason?: string;
  excludeFromBudget: boolean;
  isExtraPrincipal: boolean;
  recurring: RecurringProjectionMetadataDto;
  reimbursement: ReimbursementProjectionMetadataDto;
  splits: TransactionSplitProjectionMetadataDto[];
  createdAtUtc: string;
  lastModifiedAtUtc: string;
}

export interface PlaidLinkTokenIssuedDto {
  linkSessionId: string;
  linkToken: string;
  expiresAtUtc: string;
  environment: string;
  products: string[];
  // System.Text.Json camel-cases OAuthEnabled as oAuthEnabled.
  oAuthEnabled: boolean;
  redirectUri?: string;
}

export interface PlaidLinkSessionEventLoggedDto {
  linkSessionId: string;
  eventType: string;
  loggedAtUtc: string;
}

export interface PlaidPublicTokenExchangeResultDto {
  credentialId: string;
  linkSessionId?: string;
  itemId: string;
  environment: string;
  status: string;
  institutionId?: string;
  storedAtUtc: string;
}

export interface PlaidPublicTokenExchangeRequest {
  publicToken: string;
  linkSessionId: string;
  institutionId?: string;
  institutionName?: string;
}

export interface RecurringItemDto {
  id: string;
  householdId: string;
  merchantName: string;
  expectedAmount: number;
  isVariable: boolean;
  frequency: string;
  nextDueDate: string;
  isActive: boolean;
  userNote?: string;
  agentNote?: string;
}

export interface ReimbursementProposalDto {
  id: string;
  incomingTransactionId: string;
  relatedTransactionId?: string;
  relatedTransactionSplitId?: string;
  proposedAmount: number;
  status: string;
  decisionedByUserId?: string;
  decisionedAtUtc?: string;
  userNote?: string;
  agentNote?: string;
  createdAtUtc: string;
}

export interface CreateTransactionSplitRequest {
  subcategoryId?: string;
  amount: number;
  amortizationMonths?: number; // Default 1
  userNote?: string;
  agentNote?: string;
}

export interface CreateTransactionRequest {
  accountId: string;
  recurringItemId?: string;
  subcategoryId?: string;
  needsReviewByUserId?: string;
  plaidTransactionId?: string;
  description: string;
  amount: number;
  transactionDate: string;
  reviewStatus?: string; // Default "None"
  reviewReason?: string;
  excludeFromBudget?: boolean;
  isExtraPrincipal?: boolean;
  userNote?: string;
  agentNote?: string;
  splits?: CreateTransactionSplitRequest[];
}

export interface CreateRecurringItemRequest {
  householdId: string;
  merchantName: string;
  expectedAmount: number;
  isVariable: boolean;
  frequency?: string; // Default "Monthly"
  nextDueDate: string;
  isActive?: boolean; // Default true
  userNote?: string;
  agentNote?: string;
}

export interface UpdateRecurringItemRequest {
  merchantName?: string;
  expectedAmount?: number;
  isVariable?: boolean;
  frequency?: string;
  nextDueDate?: string;
  isActive?: boolean;
  userNote?: string;
  agentNote?: string;
}

export interface ReviewActionRequest {
  transactionId: string;
  action: string;
  subcategoryId?: string;
  reviewReason?: string;
  needsReviewByUserId?: string;
  userNote?: string;
  agentNote?: string;
  excludeFromBudget?: boolean;
  isExtraPrincipal?: boolean;
}

export interface CreateReimbursementProposalRequest {
  incomingTransactionId: string;
  relatedTransactionId?: string;
  relatedTransactionSplitId?: string;
  proposedAmount: number;
  userNote?: string;
  agentNote?: string;
}

export interface ReimbursementDecisionRequest {
  action: string;
  decisionedByUserId: string;
  userNote?: string;
  agentNote?: string;
}
