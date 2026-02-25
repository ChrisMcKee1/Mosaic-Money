"use client";

import { useActionState } from "react";
import { inviteMember } from "./actions";
import { Loader2, Send } from "lucide-react";

const initialState = {
  error: null,
  success: false,
  message: null,
};

export function InviteForm({ householdId }) {
  const [state, formAction, isPending] = useActionState(inviteMember, initialState);
  const isDisabled = isPending || !householdId;

  return (
    <form action={formAction} className="space-y-4">
      <input type="hidden" name="householdId" value={householdId ?? ""} />

      {!householdId && (
        <div className="rounded-md bg-[var(--color-warning-bg)] p-3 text-sm text-[var(--color-warning)] border border-[var(--color-warning)]/20">
          Create a household first to invite members.
        </div>
      )}

      <div>
        <label htmlFor="email" className="block text-sm font-medium text-[var(--color-text-main)] mb-1">
          Email Address
        </label>
        <input
          type="email"
          id="email"
          name="email"
          required
          placeholder="member@example.com"
          className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-text-main)] placeholder:text-[var(--color-text-subtle)] focus:border-[var(--color-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-primary)] transition-colors"
          disabled={isDisabled}
        />
      </div>

      <div>
        <label htmlFor="role" className="block text-sm font-medium text-[var(--color-text-main)] mb-1">
          Role
        </label>
        <select
          id="role"
          name="role"
          className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-text-main)] focus:border-[var(--color-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-primary)] transition-colors"
          disabled={isDisabled}
        >
          <option value="Member">Member</option>
          <option value="Admin">Admin</option>
        </select>
      </div>

      {state?.error && (
        <div className="rounded-md bg-[var(--color-negative-bg)] p-3 text-sm text-[var(--color-negative)] border border-[var(--color-negative)]/20">
          {state.error}
        </div>
      )}

      {state?.success && (
        <div className="rounded-md bg-[var(--color-positive-bg)] p-3 text-sm text-[var(--color-positive)] border border-[var(--color-positive)]/20">
          {state.message}
        </div>
      )}

      <button
        type="submit"
        disabled={isDisabled}
        className="w-full flex items-center justify-center gap-2 rounded-lg bg-[var(--color-primary)] px-4 py-2 text-sm font-medium text-[var(--color-background)] hover:bg-[var(--color-primary-hover)] transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isPending ? (
          <Loader2 className="h-4 w-4 animate-spin" />
        ) : (
          <Send className="h-4 w-4" />
        )}
        {isPending ? "Sending..." : "Send Invite"}
      </button>
    </form>
  );
}