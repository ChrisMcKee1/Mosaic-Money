import { useCallback, useRef, useState } from "react";
import { Platform } from "react-native";
import type { LinkExit, LinkSuccess } from "react-native-plaid-link-sdk";
import { toReadableError } from "../../../shared/services/mobileApiClient";
import type { PlaidPublicTokenExchangeResultDto } from "../contracts";
import {
  createPlaidLinkToken,
  exchangePlaidPublicToken,
  logPlaidLinkSessionEvent,
} from "../services/mobilePlaidApi";

export type PlaidOnboardingStatus =
  | "idle"
  | "issuingToken"
  | "ready"
  | "openingLink"
  | "exchangingToken"
  | "success"
  | "error";

export interface PlaidLinkOnboardingState {
  status: PlaidOnboardingStatus;
  error: string | null;
  linkSessionId: string | null;
  exchangeResult: PlaidPublicTokenExchangeResultDto | null;
  isBusy: boolean;
  canStart: boolean;
  canOpen: boolean;
  start: () => Promise<void>;
  openLink: () => Promise<void>;
  reset: () => void;
}

type PlaidLinkSdk = typeof import("react-native-plaid-link-sdk");

function getPlaidSdk(): PlaidLinkSdk | null {
  if (Platform.OS === "web") {
    return null;
  }

  try {
    return require("react-native-plaid-link-sdk") as PlaidLinkSdk;
  } catch {
    return null;
  }
}

function createClientUserId(): string {
  return `mobile-user-${Date.now()}`;
}

function buildExitMetadata(exit: LinkExit): Record<string, unknown> {
  return {
    status: exit.metadata?.status ?? null,
    requestId: exit.metadata?.requestId ?? null,
    linkSessionId: exit.metadata?.linkSessionId ?? null,
    institutionId: exit.metadata?.institution?.id ?? null,
    institutionName: exit.metadata?.institution?.name ?? null,
    errorCode: exit.error?.errorCode ?? null,
    errorType: exit.error?.errorType ?? null,
  };
}

function buildSuccessMetadata(success: LinkSuccess): Record<string, unknown> {
  return {
    accountCount: success.metadata?.accounts?.length ?? 0,
    linkSessionId: success.metadata?.linkSessionId ?? null,
    institutionId: success.metadata?.institution?.id ?? null,
    institutionName: success.metadata?.institution?.name ?? null,
  };
}

export function usePlaidLinkOnboarding(): PlaidLinkOnboardingState {
  const [status, setStatus] = useState<PlaidOnboardingStatus>("idle");
  const [error, setError] = useState<string | null>(null);
  const [linkSessionId, setLinkSessionId] = useState<string | null>(null);
  const [exchangeResult, setExchangeResult] = useState<PlaidPublicTokenExchangeResultDto | null>(null);

  const linkTokenRef = useRef<string | null>(null);
  const linkSessionIdRef = useRef<string | null>(null);

  const safeLogEvent = useCallback(
    async (eventType: "OPEN" | "EXIT" | "SUCCESS" | "ERROR", metadata?: Record<string, unknown>) => {
      const currentSession = linkSessionIdRef.current;
      if (!currentSession) {
        return;
      }

      try {
        await logPlaidLinkSessionEvent({
          linkSessionId: currentSession,
          eventType,
          source: "mobile",
          metadata,
        });
      } catch {
        // Logging must never block onboarding progression.
      }
    },
    [],
  );

  const start = useCallback(async () => {
    const plaidSdk = getPlaidSdk();
    if (!plaidSdk) {
      setError("Plaid Link is unavailable in this runtime. Use a native iOS or Android target.");
      setStatus("error");
      return;
    }

    setStatus("issuingToken");
    setError(null);
    setExchangeResult(null);

    try {
      const issued = await createPlaidLinkToken({
        clientUserId: createClientUserId(),
        products: ["transactions"],
        metadata: {
          source: "mobile",
          platform: "ios",
        },
      });

      if (plaidSdk.destroy) {
        await plaidSdk.destroy();
      }

      await Promise.resolve(
        plaidSdk.create({
          token: issued.linkToken,
          noLoadingState: false,
          logLevel: plaidSdk.LinkLogLevel.INFO,
        }),
      );

      linkTokenRef.current = issued.linkToken;
      linkSessionIdRef.current = issued.linkSessionId;
      setLinkSessionId(issued.linkSessionId);
      setStatus("ready");
    } catch (requestError) {
      setError(toReadableError(requestError, "Unable to prepare a secure Plaid Link session."));
      setStatus("error");
    }
  }, []);

  const openLink = useCallback(async () => {
    const plaidSdk = getPlaidSdk();
    if (!plaidSdk) {
      setError("Plaid Link is unavailable in this runtime. Use a native iOS or Android target.");
      setStatus("error");
      return;
    }

    if (!linkTokenRef.current || !linkSessionIdRef.current) {
      setError("A Link session is not ready. Start a new secure session first.");
      setStatus("error");
      return;
    }

    setStatus("openingLink");
    setError(null);
    await safeLogEvent("OPEN", { source: "mobile" });

    const handleSuccess = async (success: LinkSuccess) => {
      setStatus("exchangingToken");
      await safeLogEvent("SUCCESS", buildSuccessMetadata(success));

      try {
        const result = await exchangePlaidPublicToken({
          publicToken: success.publicToken,
          linkSessionId: linkSessionIdRef.current ?? undefined,
          institutionId: success.metadata?.institution?.id ?? undefined,
          metadata: {
            source: "mobile",
            linkSessionId: success.metadata?.linkSessionId ?? null,
            accountCount: success.metadata?.accounts?.length ?? 0,
          },
        });

        setExchangeResult(result);
        setStatus("success");
      } catch (requestError) {
        await safeLogEvent("ERROR", {
          source: "mobile",
          stage: "exchange",
          message: toReadableError(requestError, "Public token exchange failed."),
        });
        setError(toReadableError(requestError, "Unable to complete secure token exchange."));
        setStatus("error");
      }
    };

    const handleExit = async (exit: LinkExit) => {
      const metadata = buildExitMetadata(exit);
      await safeLogEvent("EXIT", metadata);

      if (exit.error?.errorCode === "INVALID_LINK_TOKEN") {
        linkTokenRef.current = null;
        setError("Your secure Link session expired. Start again to continue.");
        setStatus("error");
        return;
      }

      if (exit.error) {
        setError(exit.error.displayMessage ?? exit.error.errorMessage ?? "Plaid Link exited with an error.");
        setStatus("error");
        return;
      }

      setStatus("ready");
    };

    try {
      plaidSdk.open({
        onSuccess: (success) => {
          void handleSuccess(success);
        },
        onExit: (exit) => {
          void handleExit(exit);
        },
        iOSPresentationStyle: plaidSdk.LinkIOSPresentationStyle.FULL_SCREEN,
        logLevel: plaidSdk.LinkLogLevel.INFO,
      });
    } catch (openError) {
      setError(toReadableError(openError, "Unable to launch Plaid Link in this runtime."));
      setStatus("error");
      void safeLogEvent("ERROR", {
        source: "mobile",
        stage: "open",
      });
    }
  }, [safeLogEvent]);

  const reset = useCallback(() => {
    linkTokenRef.current = null;
    linkSessionIdRef.current = null;
    setLinkSessionId(null);
    setExchangeResult(null);
    setError(null);
    setStatus("idle");
  }, []);

  const isBusy =
    status === "issuingToken" ||
    status === "openingLink" ||
    status === "exchangingToken";

  return {
    status,
    error,
    linkSessionId,
    exchangeResult,
    isBusy,
    canStart: status === "idle" || status === "error" || status === "success",
    canOpen: status === "ready",
    start,
    openLink,
    reset,
  };
}
