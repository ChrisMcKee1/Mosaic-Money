import { useCallback, useEffect, useMemo, useState } from "react";
import { requestJson } from "../../../shared/services/mobileApiClient";
import type { HouseholdAccountAccessSummaryDto, HouseholdDto } from "../../../../../../packages/shared/src/contracts";

export type AccountVisibility = "Mine" | "Joint" | "Shared" | "Hidden";
export type AccountRole = "Owner" | "ReadOnly" | "None";

export interface AccountAccessPolicy {
  accountId: string;
  visibility: AccountVisibility;
  role: AccountRole;
}

const HIDDEN_POLICY: AccountAccessPolicy = {
  accountId: "",
  visibility: "Hidden",
  role: "None",
};

function mapSummaryToPolicy(summary: HouseholdAccountAccessSummaryDto): AccountAccessPolicy {
  const role = summary.currentMemberAccessRole === "ReadOnly"
    ? "ReadOnly"
    : summary.currentMemberAccessRole === "Owner"
      ? "Owner"
      : "None";

  if (role === "None" || summary.currentMemberVisibility !== "Visible") {
    return {
      accountId: summary.accountId,
      visibility: "Hidden",
      role: "None",
    };
  }

  const sharingPreset = summary.sharingPreset === "Mine" || summary.sharingPreset === "Joint" || summary.sharingPreset === "Shared"
    ? summary.sharingPreset
    : "Joint";

  return {
    accountId: summary.accountId,
    visibility: sharingPreset,
    role,
  };
}

export function useHouseholdAccountAccess() {
  const [policiesByAccountId, setPoliciesByAccountId] = useState<Record<string, AccountAccessPolicy>>({});
  const [isLoadingAccess, setIsLoadingAccess] = useState(true);
  const [accessError, setAccessError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setIsLoadingAccess(true);
    setAccessError(null);

    try {
      const households = await requestJson<HouseholdDto[]>("/api/v1/households");
      if (!Array.isArray(households) || households.length === 0) {
        setPoliciesByAccountId({});
        return;
      }

      const accountAccessSummaries = await requestJson<HouseholdAccountAccessSummaryDto[]>(
        `/api/v1/households/${households[0].id}/account-access`,
      );

      const mappedPolicies: Record<string, AccountAccessPolicy> = {};
      for (const summary of accountAccessSummaries) {
        mappedPolicies[summary.accountId] = mapSummaryToPolicy(summary);
      }

      setPoliciesByAccountId(mappedPolicies);
    } catch (error) {
      console.error("Failed to load account access policies:", error);
      setPoliciesByAccountId({});
      setAccessError("Unable to load account access settings right now.");
    } finally {
      setIsLoadingAccess(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const getAccountAccessPolicy = useCallback(
    (accountId?: string): AccountAccessPolicy => {
      if (!accountId) {
        return HIDDEN_POLICY;
      }

      return policiesByAccountId[accountId] ?? { ...HIDDEN_POLICY, accountId };
    },
    [policiesByAccountId],
  );

  return {
    getAccountAccessPolicy,
    isLoadingAccess,
    accessError,
    refresh,
  };
}

export function useAccountAccess(accountId?: string) {
  const { getAccountAccessPolicy, isLoadingAccess, accessError, refresh } = useHouseholdAccountAccess();
  const policy = useMemo(() => getAccountAccessPolicy(accountId), [accountId, getAccountAccessPolicy]);

  return {
    policy,
    isReadOnly: policy.role === "ReadOnly",
    isHidden: policy.visibility === "Hidden" || policy.role === "None",
    isLoadingAccess,
    accessError,
    refresh,
  };
}
