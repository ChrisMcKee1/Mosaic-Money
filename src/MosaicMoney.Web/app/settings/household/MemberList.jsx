"use client";

import { useState } from "react";
import { User, Trash2, Loader2 } from "lucide-react";
import { removeMember } from "./actions";

export function MemberList({ householdId, members }) {
  const [removingId, setRemovingId] = useState(null);

  const handleRemove = async (id) => {
    if (!householdId) {
      return;
    }

    if (!confirm("Are you sure you want to remove this member? They will lose access to all household accounts.")) {
      return;
    }
    
    setRemovingId(id);
    try {
      await removeMember(householdId, id);
    } finally {
      setRemovingId(null);
    }
  };

  if (!members || members.length === 0) {
    return (
      <div className="text-center py-8 rounded-xl border border-dashed border-[var(--color-border)] bg-[var(--color-surface-hover)]/50">
        <p className="text-sm text-[var(--color-text-muted)]">No active members found.</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {members.map((member) => {
        const memberName = member.displayName ?? member.name ?? "Unknown User";
        const memberEmail = member.externalUserKey ?? member.email ?? "No email";

        return (
          <div 
            key={member.id} 
            className="flex items-center justify-between p-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] hover:border-[var(--color-primary)]/30 transition-colors group"
          >
            <div className="flex items-center gap-4">
              <div className="h-10 w-10 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center border border-[var(--color-border)]">
                <User className="h-5 w-5 text-[var(--color-text-subtle)]" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-[var(--color-text-main)]">
                    {memberName}
                  </p>
                  {member.role === "Owner" && (
                    <span className="inline-flex items-center rounded-full bg-[var(--color-primary)]/10 px-2 py-0.5 text-[10px] font-medium text-[var(--color-primary)] ring-1 ring-inset ring-[var(--color-primary)]/20">
                      Owner
                    </span>
                  )}
                  {member.role === "Admin" && (
                    <span className="inline-flex items-center rounded-full bg-[var(--color-warning-bg)] px-2 py-0.5 text-[10px] font-medium text-[var(--color-warning)] ring-1 ring-inset ring-[var(--color-warning)]/20">
                      Admin
                    </span>
                  )}
                </div>
                <p className="text-xs text-[var(--color-text-muted)] mt-0.5">
                  {memberEmail}
                </p>
              </div>
            </div>

            {member.role !== "Owner" && (
              <button
                onClick={() => handleRemove(member.id)}
                disabled={removingId === member.id}
                className="p-2 text-[var(--color-text-subtle)] hover:text-[var(--color-negative)] hover:bg-[var(--color-negative-bg)] rounded-lg transition-colors disabled:opacity-50"
                aria-label={`Remove ${memberName}`}
                title="Remove member"
              >
                {removingId === member.id ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Trash2 className="h-4 w-4" />
                )}
              </button>
            )}
          </div>
        );
      })}
    </div>
  );
}