import type { ReviewActionRequest, TransactionDto } from "../contracts";
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
