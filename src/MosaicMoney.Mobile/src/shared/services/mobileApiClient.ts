import { getApiBaseUrl } from "../../features/transactions/services/apiConfig";

interface ApiErrorEnvelope {
  code: string;
  message: string;
  traceId: string;
}

interface ApiErrorResponse {
  error: ApiErrorEnvelope;
}

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
    typeof candidate.error.message === "string" &&
    typeof candidate.error.traceId === "string"
  );
}

function hasRequestBody(method: string): boolean {
  return method !== "GET" && method !== "HEAD";
}

interface JsonRequestOptions<TBody, TParsed> {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: TBody;
  signal?: AbortSignal;
  parse?: (payload: unknown) => TParsed;
}

async function parseResponse<TParsed>(
  response: Response,
  parse?: (payload: unknown) => TParsed,
): Promise<TParsed> {
  if (response.status === 204) {
    return undefined as TParsed;
  }

  const rawPayload = (await response.json()) as unknown;
  return parse ? parse(rawPayload) : (rawPayload as TParsed);
}

export async function requestJson<TParsed, TBody = unknown>(
  path: string,
  options: JsonRequestOptions<TBody, TParsed> = {},
): Promise<TParsed> {
  const method = options.method ?? "GET";
  const baseUrl = getApiBaseUrl();
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: {
      Accept: "application/json",
      ...(hasRequestBody(method) ? { "Content-Type": "application/json" } : {}),
    },
    body: hasRequestBody(method) && options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options.signal,
  });

  if (response.ok) {
    return parseResponse(response, options.parse);
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

export function toReadableError(error: unknown, fallbackMessage: string): string {
  if (error instanceof MobileApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return fallbackMessage;
}
