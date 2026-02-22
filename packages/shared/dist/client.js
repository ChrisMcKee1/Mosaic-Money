"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.MosaicMoneyApiClient = exports.ApiError = void 0;
class ApiError extends Error {
    response;
    status;
    constructor(status, response) {
        super(response.error.message || 'API Error');
        this.name = 'ApiError';
        this.status = status;
        this.response = response;
    }
}
exports.ApiError = ApiError;
class MosaicMoneyApiClient {
    baseUrl;
    getToken;
    constructor(options) {
        this.baseUrl = options.baseUrl.replace(/\/$/, '');
        this.getToken = options.getToken;
    }
    async request(endpoint, options = {}) {
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
            let errorData;
            try {
                errorData = await response.json();
            }
            catch {
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
            return undefined;
        }
        return response.json();
    }
    // Transactions
    async getTransactions() {
        return this.request('/api/v1/transactions');
    }
    async getTransaction(id) {
        return this.request(`/api/v1/transactions/${id}`);
    }
    async createTransaction(request) {
        return this.request('/api/v1/transactions', {
            method: 'POST',
            body: JSON.stringify(request),
        });
    }
    // Recurring Items
    async getRecurringItems() {
        return this.request('/api/v1/recurring');
    }
    async createRecurringItem(request) {
        return this.request('/api/v1/recurring', {
            method: 'POST',
            body: JSON.stringify(request),
        });
    }
    async updateRecurringItem(id, request) {
        return this.request(`/api/v1/recurring/${id}`, {
            method: 'PATCH',
            body: JSON.stringify(request),
        });
    }
    // Review Actions
    async submitReviewAction(request) {
        return this.request('/api/v1/review-actions', {
            method: 'POST',
            body: JSON.stringify(request),
        });
    }
    // Reimbursements
    async getReimbursementProposals() {
        return this.request('/api/v1/reimbursements');
    }
    async createReimbursementProposal(request) {
        return this.request('/api/v1/reimbursements', {
            method: 'POST',
            body: JSON.stringify(request),
        });
    }
    async submitReimbursementDecision(id, request) {
        return this.request(`/api/v1/reimbursements/${id}/decision`, {
            method: 'POST',
            body: JSON.stringify(request),
        });
    }
}
exports.MosaicMoneyApiClient = MosaicMoneyApiClient;
