import { Suspense } from "react";
import Link from "next/link";
import { ArrowLeft, Users, Mail, Shield, History } from "lucide-react";
import { InviteForm } from "./InviteForm";
import { MemberList } from "./MemberList";
import { InviteList } from "./InviteList";
import { fetchApi } from "../../../lib/api";

export const metadata = {
  title: "Household Settings | Mosaic Money",
};

async function getHouseholdData() {
  try {
    const households = await fetchApi("/api/v1/households");

    if (!Array.isArray(households) || households.length === 0) {
      return {
        householdId: null,
        householdName: null,
        members: [],
        invites: [],
        loadError: null,
      };
    }

    const primaryHousehold = households[0];
    const [members, invites] = await Promise.all([
      fetchApi(`/api/v1/households/${primaryHousehold.id}/members`),
      fetchApi(`/api/v1/households/${primaryHousehold.id}/invites`),
    ]);

    return {
      householdId: primaryHousehold.id,
      householdName: primaryHousehold.name ?? "Household",
      members: Array.isArray(members) ? members : [],
      invites: Array.isArray(invites) ? invites : [],
      loadError: null,
    };
  } catch (error) {
    console.error("Failed to load household settings data:", error);
    return {
      householdId: null,
      householdName: null,
      members: [],
      invites: [],
      loadError: "Unable to load household data right now.",
    };
  }
}

export default async function HouseholdSettingsPage() {
  const data = await getHouseholdData();

  return (
    <div className="p-6 md:p-10 max-w-4xl w-full overflow-y-auto">
      <div className="mb-6">
        <Link 
          href="/settings" 
          className="inline-flex items-center text-sm text-[var(--color-text-muted)] hover:text-[var(--color-text-main)] transition-colors"
        >
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back to Settings
        </Link>
      </div>

      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-6 md:p-8">
        <div className="flex items-center gap-3 mb-2">
          <div className="p-2 rounded-lg bg-[var(--color-primary-hover)]/10 text-[var(--color-primary)]">
            <Users className="h-6 w-6" />
          </div>
          <h1 className="text-2xl md:text-3xl font-display font-bold text-[var(--color-text-main)]">
            Household Members
          </h1>
        </div>
        <p className="text-sm text-[var(--color-text-muted)] mb-8">
          {data.householdName
            ? `Manage members and permissions for ${data.householdName}.`
            : "Manage who has access to your household accounts and their permissions."}
        </p>

        {data.loadError && (
          <div className="mb-6 rounded-md bg-[var(--color-warning-bg)] p-3 text-sm text-[var(--color-warning)] border border-[var(--color-warning)]/20">
            {data.loadError}
          </div>
        )}

        {!data.householdId && !data.loadError && (
          <div className="mb-6 rounded-md bg-[var(--color-surface-hover)] p-3 text-sm text-[var(--color-text-muted)] border border-[var(--color-border)]">
            No household found yet. Create one through the API first, then return to manage members and invites.
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          <div className="lg:col-span-2 space-y-8">
            <section>
              <h2 className="text-lg font-semibold text-[var(--color-text-main)] mb-4 flex items-center gap-2">
                <Shield className="h-5 w-5 text-[var(--color-text-subtle)]" />
                Active Members
              </h2>
              <Suspense fallback={<div className="animate-pulse h-32 bg-[var(--color-surface-hover)] rounded-xl"></div>}>
                <MemberList householdId={data.householdId} members={data.members} />
              </Suspense>
            </section>

            {data.invites.length > 0 && (
              <section>
                <h2 className="text-lg font-semibold text-[var(--color-text-main)] mb-4 flex items-center gap-2">
                  <History className="h-5 w-5 text-[var(--color-text-subtle)]" />
                  Pending Invites
                </h2>
                <Suspense fallback={<div className="animate-pulse h-24 bg-[var(--color-surface-hover)] rounded-xl"></div>}>
                  <InviteList householdId={data.householdId} invites={data.invites} />
                </Suspense>
              </section>
            )}
          </div>

          <div className="lg:col-span-1">
            <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-hover)] p-5 sticky top-6">
              <h2 className="text-base font-semibold text-[var(--color-text-main)] mb-1 flex items-center gap-2">
                <Mail className="h-4 w-4 text-[var(--color-primary)]" />
                Invite Member
              </h2>
              <p className="text-xs text-[var(--color-text-muted)] mb-4">
                Send an email invitation to join your household.
              </p>
              <InviteForm householdId={data.householdId} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}