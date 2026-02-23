import type {
  PlaidLinkSessionEventLoggedDto as SharedPlaidLinkSessionEventLoggedDto,
  PlaidLinkTokenIssuedDto as SharedPlaidLinkTokenIssuedDto,
  PlaidPublicTokenExchangeResultDto as SharedPlaidPublicTokenExchangeResultDto,
} from "../../../../../packages/shared/src/contracts";

export type PlaidLinkTokenIssuedDto = SharedPlaidLinkTokenIssuedDto;
export type PlaidLinkSessionEventLoggedDto = SharedPlaidLinkSessionEventLoggedDto;
export type PlaidPublicTokenExchangeResultDto = SharedPlaidPublicTokenExchangeResultDto;

export type PlaidSessionEventType = "OPEN" | "EXIT" | "SUCCESS" | "HANDOFF" | "ERROR";

export interface CreatePlaidLinkTokenInput {
  clientUserId: string;
  householdId?: string;
  redirectUri?: string;
  products?: string[];
  metadata?: Record<string, unknown>;
}

export interface LogPlaidLinkSessionEventInput {
  linkSessionId: string;
  eventType: PlaidSessionEventType;
  source?: string;
  metadata?: Record<string, unknown>;
}

export interface ExchangePlaidPublicTokenInput {
  publicToken: string;
  linkSessionId?: string;
  householdId?: string;
  institutionId?: string;
  metadata?: Record<string, unknown>;
}
