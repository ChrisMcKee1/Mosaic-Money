import { TransactionDto, CreateTransactionRequest, RecurringItemDto, CreateRecurringItemRequest, UpdateRecurringItemRequest, ReviewActionRequest, ReimbursementProposalDto, CreateReimbursementProposalRequest, ReimbursementDecisionRequest, ApiErrorResponse } from './contracts';
export declare class ApiError extends Error {
    readonly response: ApiErrorResponse;
    readonly status: number;
    constructor(status: number, response: ApiErrorResponse);
}
export interface ApiClientOptions {
    baseUrl: string;
    getToken?: () => Promise<string | null>;
}
export declare class MosaicMoneyApiClient {
    private readonly baseUrl;
    private readonly getToken?;
    constructor(options: ApiClientOptions);
    private request;
    getTransactions(): Promise<TransactionDto[]>;
    getTransaction(id: string): Promise<TransactionDto>;
    createTransaction(request: CreateTransactionRequest): Promise<TransactionDto>;
    getRecurringItems(): Promise<RecurringItemDto[]>;
    createRecurringItem(request: CreateRecurringItemRequest): Promise<RecurringItemDto>;
    updateRecurringItem(id: string, request: UpdateRecurringItemRequest): Promise<RecurringItemDto>;
    submitReviewAction(request: ReviewActionRequest): Promise<void>;
    getReimbursementProposals(): Promise<ReimbursementProposalDto[]>;
    createReimbursementProposal(request: CreateReimbursementProposalRequest): Promise<ReimbursementProposalDto>;
    submitReimbursementDecision(id: string, request: ReimbursementDecisionRequest): Promise<void>;
}
