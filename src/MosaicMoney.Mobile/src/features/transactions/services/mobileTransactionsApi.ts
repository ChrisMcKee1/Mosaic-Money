import type { ReviewActionRequest, TransactionDto, CategorySearchResultDto } from "../contracts";
import { requestJson, toReadableError as toReadableErrorBase } from "../../../shared/services/mobileApiClient";

export function toReadableError(error: unknown): string {
  return toReadableErrorBase(error, "Unexpected error while loading transactions.");
}

export async function fetchNeedsReviewTransactions(signal?: AbortSignal): Promise<TransactionDto[]> {
  return requestJson<TransactionDto[]>("/api/v1/transactions?needsReviewOnly=true&pageSize=100", { signal });
}

export async function fetchTransactionDetail(transactionId: string, signal?: AbortSignal): Promise<TransactionDto> {
  return requestJson<TransactionDto>(`/api/v1/transactions/${encodeURIComponent(transactionId)}`, { signal });
}

export async function searchTransactions(query: string, limit: number = 20, signal?: AbortSignal): Promise<TransactionDto[]> {
  if (!query.trim()) return [];
  return requestJson<TransactionDto[]>(`/api/v1/search/transactions?query=${encodeURIComponent(query.trim())}&limit=${limit}`, { signal });
}

export async function searchCategories(query: string, limit: number = 10, signal?: AbortSignal): Promise<CategorySearchResultDto[]> {
  if (!query.trim()) return [];
  return requestJson<CategorySearchResultDto[]>(`/api/v1/search/categories?query=${encodeURIComponent(query.trim())}&limit=${limit}`, { signal });
}

export async function submitReviewAction(
  request: ReviewActionRequest,
  signal?: AbortSignal,
): Promise<TransactionDto> {
  return requestJson<TransactionDto, ReviewActionRequest>("/api/v1/review-actions", {
    method: "POST",
    body: request,
    signal,
  });
}
