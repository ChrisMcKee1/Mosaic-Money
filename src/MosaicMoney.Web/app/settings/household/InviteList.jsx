"use client";

import { useState } from "react";
import { Check, Loader2, Mail, X } from "lucide-react";
import { acceptInvite, cancelInvite } from "./actions";

export function InviteList({ householdId, invites }) {
  const [cancelingId, setCancelingId] = useState(null);
  const [acceptingId, setAcceptingId] = useState(null);

  const handleCancel = async (id) => {
    if (!householdId) {
      return;
    }

    setCancelingId(id);
    try {
      await cancelInvite(householdId, id);
    } finally {
      setCancelingId(null);
    }
  };

  const handleAccept = async (invite) => {
    if (!householdId) {
      return;
    }

    setAcceptingId(invite.id);
    try {
      await acceptInvite(householdId, invite.id);
    } finally {
      setAcceptingId(null);
    }
  };

  if (!invites || invites.length === 0) {
    return null;
  }

  return (
    <div className="space-y-3">
      {invites.map((invite) => (
        <div 
          key={invite.id} 
          className="flex items-center justify-between p-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] opacity-80 hover:opacity-100 transition-opacity"
        >
          <div className="flex items-center gap-4">
            <div className="h-10 w-10 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center border border-[var(--color-border)]">
              <Mail className="h-5 w-5 text-[var(--color-text-subtle)]" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <p className="text-sm font-medium text-[var(--color-text-main)]">
                  {invite.email}
                </p>
                <span className="inline-flex items-center rounded-full bg-[var(--color-warning-bg)] px-2 py-0.5 text-[10px] font-medium text-[var(--color-warning)] ring-1 ring-inset ring-[var(--color-warning)]/20">
                  Pending
                </span>
              </div>
              <p className="text-xs text-[var(--color-text-muted)] mt-0.5">
                Invited as {invite.role ?? "Member"}
                {invite.invitedAtUtc ? ` • Sent ${new Date(invite.invitedAtUtc).toLocaleDateString()}` : ""}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-1">
            <button
              onClick={() => handleAccept(invite)}
              disabled={acceptingId === invite.id || cancelingId === invite.id}
              className="p-2 text-[var(--color-text-subtle)] hover:text-[var(--color-positive)] hover:bg-[var(--color-positive-bg)] rounded-lg transition-colors disabled:opacity-50"
              aria-label={`Accept invite for ${invite.email}`}
              title="Accept invite"
            >
              {acceptingId === invite.id ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Check className="h-4 w-4" />
              )}
            </button>

            <button
              onClick={() => handleCancel(invite.id)}
              disabled={cancelingId === invite.id || acceptingId === invite.id}
              className="p-2 text-[var(--color-text-subtle)] hover:text-[var(--color-negative)] hover:bg-[var(--color-negative-bg)] rounded-lg transition-colors disabled:opacity-50"
              aria-label={`Cancel invite for ${invite.email}`}
              title="Cancel invite"
            >
              {cancelingId === invite.id ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <X className="h-4 w-4" />
              )}
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}