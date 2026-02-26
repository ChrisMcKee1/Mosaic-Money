import { useCallback, useEffect, useState } from "react";
import { requestJson } from "../../../shared/services/mobileApiClient";
import type {
  HouseholdDto,
  HouseholdMemberDto,
  HouseholdInviteDto,
  HouseholdAccountAccessSummaryDto,
  AccountSharingPreset,
  UpdateAccountSharingPresetRequest,
} from "../../../../../../packages/shared/src/contracts";

export function useHouseholdSettings() {
  const [household, setHousehold] = useState<HouseholdDto | null>(null);
  const [members, setMembers] = useState<HouseholdMemberDto[]>([]);
  const [invites, setInvites] = useState<HouseholdInviteDto[]>([]);
  const [accountAccess, setAccountAccess] = useState<HouseholdAccountAccessSummaryDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const households = await requestJson<HouseholdDto[]>("/api/v1/households");
      
      if (!Array.isArray(households) || households.length === 0) {
        setHousehold(null);
        setMembers([]);
        setInvites([]);
        setAccountAccess([]);
        return;
      }

      const primaryHousehold = households[0];
      setHousehold(primaryHousehold);

      const [membersData, invitesData] = await Promise.all([
        requestJson<HouseholdMemberDto[]>(`/api/v1/households/${primaryHousehold.id}/members`),
        requestJson<HouseholdInviteDto[]>(`/api/v1/households/${primaryHousehold.id}/invites`),
      ]);

      let accountAccessData: HouseholdAccountAccessSummaryDto[] = [];
      try {
        accountAccessData = await requestJson<HouseholdAccountAccessSummaryDto[]>(
          `/api/v1/households/${primaryHousehold.id}/account-access`,
        );
      } catch (accountAccessError) {
        // Account-sharing controls are access-scoped; continue loading membership settings fail-closed.
        console.warn("Unable to load account access controls:", accountAccessError);
      }

      setMembers(Array.isArray(membersData) ? membersData : []);
      setInvites(Array.isArray(invitesData) ? invitesData : []);
      setAccountAccess(Array.isArray(accountAccessData) ? accountAccessData : []);
    } catch (err) {
      console.error("Failed to load household settings:", err);
      setError("Unable to load household data right now.");
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const inviteMember = async (email: string, role: string) => {
    if (!household) return { error: "No household context." };
    
    try {
      await requestJson(`/api/v1/households/${household.id}/invites`, {
        method: "POST",
        body: { email, role },
      });
      await loadData();
      return { success: true };
    } catch (err) {
      console.error("Failed to invite member:", err);
      return { error: "Failed to send invitation." };
    }
  };

  const acceptInvite = async (inviteId: string, displayName?: string) => {
    if (!household) return { error: "No household context." };
    
    try {
      await requestJson(`/api/v1/households/${household.id}/invites/${inviteId}/accept`, {
        method: "POST",
        body: { displayName: displayName ?? null },
      });
      await loadData();
      return { success: true };
    } catch (err) {
      console.error("Failed to accept invite:", err);
      return { error: "Failed to accept invitation." };
    }
  };

  const removeMember = async (memberId: string) => {
    if (!household) return { error: "No household context." };
    
    try {
      await requestJson(`/api/v1/households/${household.id}/members/${memberId}`, {
        method: "DELETE",
      });
      await loadData();
      return { success: true };
    } catch (err) {
      console.error("Failed to remove member:", err);
      return { error: "Failed to remove member." };
    }
  };

  const cancelInvite = async (inviteId: string) => {
    if (!household) return { error: "No household context." };
    
    try {
      await requestJson(`/api/v1/households/${household.id}/invites/${inviteId}`, {
        method: "DELETE",
      });
      await loadData();
      return { success: true };
    } catch (err) {
      console.error("Failed to cancel invite:", err);
      return { error: "Failed to cancel invitation." };
    }
  };

  const updateAccountSharingPreset = async (accountId: string, preset: AccountSharingPreset) => {
    if (!household) return { error: "No household context." };

    try {
      const updatedSummary = await requestJson<HouseholdAccountAccessSummaryDto, UpdateAccountSharingPresetRequest>(
        `/api/v1/households/${household.id}/accounts/${accountId}/sharing-preset`,
        {
          method: "PUT",
          body: { preset },
        },
      );

      setAccountAccess((current) => {
        const remaining = current.filter((entry) => entry.accountId !== updatedSummary.accountId);
        return [...remaining, updatedSummary].sort((a, b) => a.accountName.localeCompare(b.accountName));
      });

      return { success: true };
    } catch (err) {
      console.error("Failed to update account sharing preset:", err);
      return { error: "Failed to update account sharing settings." };
    }
  };

  return {
    household,
    members,
    invites,
    accountAccess,
    isLoading,
    error,
    refresh: loadData,
    inviteMember,
    acceptInvite,
    removeMember,
    cancelInvite,
    updateAccountSharingPreset,
  };
}
