import {
  TransactionDto,
  CreateTransactionRequest,
  RecurringItemDto,
  CreateRecurringItemRequest,
  UpdateRecurringItemRequest,
  ReviewActionRequest,
  ReimbursementProposalDto,
  CreateReimbursementProposalRequest,
  ReimbursementDecisionRequest,
  ApiErrorResponse
} from './contracts';

export class ApiError extends Error {
  public readonly response: ApiErrorResponse;
  public readonly status: number;

  constructor(status: number, response: ApiErrorResponse) {
    super(response.error.message || 'API Error');
    this.name = 'ApiError';
    this.status = status;
    this.response = response;
  }
}

export interface ApiClientOptions {
  baseUrl: string;
  getToken?: () => Promise<string | null>;
}

export class MosaicMoneyApiClient {
  private readonly baseUrl: string;
  private readonly getToken?: () => Promise<string | null>;

  constructor(options: ApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, '');
    this.getToken = options.getToken;
  }

  private async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const url = `${this.baseUrl}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;
    const headers = new Headers(options.headers);
    
    headers.set('Content-Type', 'application/json');
    headers.set('Accept', 'application/json');

    if (this.getToken) {
      const token = await this.getToken();
      if (token) {
        headers.set('Authorization', `Bearer ${token}`);
      }
    }

    const response = await fetch(url, {
      ...options,
      headers,
    });

    if (!response.ok) {
      let errorData: ApiErrorResponse;
      try {
        errorData = await response.json();
      } catch {
        errorData = {
          error: {
            code: 'unknown_error',
            message: response.statusText,
            traceId: ''
          }
        };
      }
      throw new ApiError(response.status, errorData);
    }

    // Handle 204 No Content
    if (response.status === 204) {
      return undefined as any;
    }

    return response.json();
  }

  // Transactions
  async getTransactions(): Promise<TransactionDto[]> {
    return this.request<TransactionDto[]>('/api/v1/transactions');
  }

  async getTransaction(id: string): Promise<TransactionDto> {
    return this.request<TransactionDto>(`/api/v1/transactions/${id}`);
  }

  async createTransaction(request: CreateTransactionRequest): Promise<TransactionDto> {
    return this.request<TransactionDto>('/api/v1/transactions', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  // Recurring Items
  async getRecurringItems(): Promise<RecurringItemDto[]> {
    return this.request<RecurringItemDto[]>('/api/v1/recurring');
  }

  async createRecurringItem(request: CreateRecurringItemRequest): Promise<RecurringItemDto> {
    return this.request<RecurringItemDto>('/api/v1/recurring', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async updateRecurringItem(id: string, request: UpdateRecurringItemRequest): Promise<RecurringItemDto> {
    return this.request<RecurringItemDto>(`/api/v1/recurring/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(request),
    });
  }

  // Review Actions
  async submitReviewAction(request: ReviewActionRequest): Promise<void> {
    return this.request<void>('/api/v1/review-actions', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  // Reimbursements
  async getReimbursementProposals(): Promise<ReimbursementProposalDto[]> {
    return this.request<ReimbursementProposalDto[]>('/api/v1/reimbursements');
  }

  async createReimbursementProposal(request: CreateReimbursementProposalRequest): Promise<ReimbursementProposalDto> {
    return this.request<ReimbursementProposalDto>('/api/v1/reimbursements', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async submitReimbursementDecision(id: string, request: ReimbursementDecisionRequest): Promise<void> {
    return this.request<void>(`/api/v1/reimbursements/${id}/decision`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }
}
