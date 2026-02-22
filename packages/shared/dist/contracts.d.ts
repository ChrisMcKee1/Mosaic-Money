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
    transactionDate: string;
    reviewStatus: string;
    reviewReason?: string;
    excludeFromBudget: boolean;
    isExtraPrincipal: boolean;
    userNote?: string;
    agentNote?: string;
    splits: TransactionSplitDto[];
    createdAtUtc: string;
    lastModifiedAtUtc: string;
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
    amortizationMonths?: number;
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
    reviewStatus?: string;
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
    frequency?: string;
    nextDueDate: string;
    isActive?: boolean;
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
