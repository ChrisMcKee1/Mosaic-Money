import type { ApiErrorResponse, TransactionDto } from "../contracts";
import { getApiBaseUrl } from "./apiConfig";

export class MobileApiError extends Error {
  public readonly status: number;
  public readonly code?: string;
  public readonly traceId?: string;

  constructor(status: number, message: string, code?: string, traceId?: string) {
    super(message);
    this.name = "MobileApiError";
    this.status = status;
    this.code = code;
    this.traceId = traceId;
  }
}

function isApiErrorResponse(payload: unknown): payload is ApiErrorResponse {
  if (!payload || typeof payload !== "object") {
    return false;
  }

  const candidate = payload as Partial<ApiErrorResponse>;
  return (
    !!candidate.error &&
    typeof candidate.error === "object" &&
    typeof candidate.error.code === "string" &&
    typeof candidate.error.message === "string"
  );
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function requestJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const baseUrl = getApiBaseUrl();
  const response = await fetch(`${baseUrl}${path}`, {
    method: "GET",
    headers: {
      Accept: "application/json",
    },
    signal,
  });

  if (response.ok) {
    return parseResponse<T>(response);
  }

  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    throw new MobileApiError(response.status, `Request failed: ${response.status} ${response.statusText}`);
  }

  if (isApiErrorResponse(payload)) {
    throw new MobileApiError(
      response.status,
      payload.error.message,
      payload.error.code,
      payload.error.traceId,
    );
  }

  throw new MobileApiError(response.status, `Request failed: ${response.status} ${response.statusText}`);
}

export function toReadableError(error: unknown): string {
  if (error instanceof MobileApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Unexpected error while loading transactions.";
}

export async function fetchNeedsReviewTransactions(signal?: AbortSignal): Promise<TransactionDto[]> {
  return requestJson<TransactionDto[]>("/api/v1/transactions?needsReviewOnly=true&pageSize=100", signal);
}

export async function fetchTransactionDetail(transactionId: string, signal?: AbortSignal): Promise<TransactionDto> {
  return requestJson<TransactionDto>(`/api/v1/transactions/${encodeURIComponent(transactionId)}`, signal);
}
