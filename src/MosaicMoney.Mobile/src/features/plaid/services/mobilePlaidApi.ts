import {
  parsePlaidLinkSessionEventLogged,
  parsePlaidLinkTokenIssued,
  parsePlaidPublicTokenExchangeResult,
} from "../../../../../../packages/shared/src/validation";
import { requestJson } from "../../../shared/services/mobileApiClient";
import type {
  CreatePlaidLinkTokenInput,
  ExchangePlaidPublicTokenInput,
  LogPlaidLinkSessionEventInput,
  PlaidLinkSessionEventLoggedDto,
  PlaidLinkTokenIssuedDto,
  PlaidPublicTokenExchangeResultDto,
} from "../contracts";

function toMetadataJson(metadata?: Record<string, unknown>): string | undefined {
  if (!metadata) {
    return undefined;
  }

  return JSON.stringify(metadata);
}

export async function createPlaidLinkToken(
  input: CreatePlaidLinkTokenInput,
  signal?: AbortSignal,
): Promise<PlaidLinkTokenIssuedDto> {
  return requestJson<PlaidLinkTokenIssuedDto>("/api/v1/plaid/link-tokens", {
    method: "POST",
    signal,
    body: {
      householdId: input.householdId,
      clientUserId: input.clientUserId,
      redirectUri: input.redirectUri,
      products: input.products,
      clientMetadataJson: toMetadataJson(input.metadata),
    },
    parse: parsePlaidLinkTokenIssued,
  });
}

export async function logPlaidLinkSessionEvent(
  input: LogPlaidLinkSessionEventInput,
): Promise<PlaidLinkSessionEventLoggedDto> {
  const encodedSessionId = encodeURIComponent(input.linkSessionId);
  return requestJson<PlaidLinkSessionEventLoggedDto>(`/api/v1/plaid/link-sessions/${encodedSessionId}/events`, {
    method: "POST",
    body: {
      eventType: input.eventType,
      source: input.source ?? "mobile",
      clientMetadataJson: toMetadataJson(input.metadata),
    },
    parse: parsePlaidLinkSessionEventLogged,
  });
}

export async function exchangePlaidPublicToken(
  input: ExchangePlaidPublicTokenInput,
): Promise<PlaidPublicTokenExchangeResultDto> {
  return requestJson<PlaidPublicTokenExchangeResultDto>("/api/v1/plaid/public-token-exchange", {
    method: "POST",
    body: {
      householdId: input.householdId,
      linkSessionId: input.linkSessionId,
      publicToken: input.publicToken,
      institutionId: input.institutionId,
      clientMetadataJson: toMetadataJson(input.metadata),
    },
    parse: parsePlaidPublicTokenExchangeResult,
  });
}
