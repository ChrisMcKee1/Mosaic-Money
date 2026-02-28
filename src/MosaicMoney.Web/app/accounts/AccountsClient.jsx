"use client";

import { useState } from "react";
import Link from "next/link";
import { PageLayout } from "../../components/layout/PageLayout";
import { AccountsList } from "../../components/accounts/AccountsList";
import { PieChart, Shield, Users, User, Eye, Settings, Plus } from "lucide-react";
import { CurrencyDisplay } from "../../components/ui/CurrencyDisplay";

export function AccountsClient({ initialAccounts }) {
  const [accounts, setAccounts] = useState(initialAccounts);
  const [selectedAccount, setSelectedAccount] = useState(null);

  const totalAssets = accounts.filter(a => a.balance > 0).reduce((sum, a) => sum + a.balance, 0);
  const totalDebts = accounts.filter(a => a.balance < 0).reduce((sum, a) => sum + Math.abs(a.balance), 0);
  const netWorth = totalAssets - totalDebts;

  const handleVisibilityChange = (accountId, newVisibility) => {
    const currentAccount = accounts.find((acc) => acc.id === accountId);
    if (!currentAccount || currentAccount.visibility === newVisibility) {
      return;
    }

    const visibilityLabels = {
      Mine: "Mine Only",
      Joint: "Joint",
      Shared: "Shared (Read-only)",
    };

    const confirmed = window.confirm(
      `Change account visibility from ${visibilityLabels[currentAccount.visibility] ?? currentAccount.visibility} to ${visibilityLabels[newVisibility] ?? newVisibility}?`,
    );

    if (!confirmed) {
      return;
    }

    setAccounts(accounts.map(acc => 
      acc.id === accountId ? { ...acc, visibility: newVisibility } : acc
    ));
    if (selectedAccount?.id === accountId) {
      setSelectedAccount({ ...selectedAccount, visibility: newVisibility });
    }
  };

  const rightPanel = selectedAccount ? (
    <div className="space-y-6">
      <div>
        <div className="w-12 h-12 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center text-white font-bold text-xl mb-4 border border-[var(--color-border)]">
          {selectedAccount.institution.charAt(0)}
        </div>
        <h2 className="text-xl font-display font-bold text-white">{selectedAccount.name}</h2>
        <p className="text-sm text-[var(--color-text-muted)] mt-1">{selectedAccount.institution} {selectedAccount.mask ? `•••• ${selectedAccount.mask}` : ''}</p>
        <CurrencyDisplay 
          amount={selectedAccount.balance} 
          type="balance"
          className="text-3xl mt-4 block font-mono" 
        />
      </div>

      <div className="space-y-6 pt-6 border-t border-[var(--color-border)]">
        <div>
          <h3 className="text-sm font-medium text-white flex items-center gap-2 mb-3">
            <Shield className="w-4 h-4 text-[var(--color-primary)]" />
            Access & Visibility
          </h3>
          <p className="text-xs text-[var(--color-text-muted)] mb-4">
            Control who in your household can see and manage this account.
          </p>
          
          <div className="space-y-2">
            <button 
              onClick={() => handleVisibilityChange(selectedAccount.id, "Mine")}
              className={`w-full flex items-center justify-between p-3 rounded-lg border transition-colors ${
                selectedAccount.visibility === "Mine" 
                  ? "bg-emerald-500/10 border-emerald-500/30" 
                  : "bg-[var(--color-surface)] border-[var(--color-border)] hover:bg-[var(--color-surface-hover)]"
              }`}
            >
              <div className="flex items-center gap-3">
                <div className={`p-2 rounded-md ${selectedAccount.visibility === "Mine" ? "bg-emerald-500/20 text-emerald-400" : "bg-[var(--color-surface-hover)] text-[var(--color-text-muted)]"}`}>
                  <User className="w-4 h-4" />
                </div>
                <div className="text-left">
                  <p className={`text-sm font-medium ${selectedAccount.visibility === "Mine" ? "text-emerald-400" : "text-white"}`}>Mine Only</p>
                  <p className="text-xs text-[var(--color-text-muted)]">Only you can see this account</p>
                </div>
              </div>
              {selectedAccount.visibility === "Mine" && (
                <div className="w-2 h-2 rounded-full bg-emerald-400" />
              )}
            </button>

            <button 
              onClick={() => handleVisibilityChange(selectedAccount.id, "Joint")}
              className={`w-full flex items-center justify-between p-3 rounded-lg border transition-colors ${
                selectedAccount.visibility === "Joint" 
                  ? "bg-blue-500/10 border-blue-500/30" 
                  : "bg-[var(--color-surface)] border-[var(--color-border)] hover:bg-[var(--color-surface-hover)]"
              }`}
            >
              <div className="flex items-center gap-3">
                <div className={`p-2 rounded-md ${selectedAccount.visibility === "Joint" ? "bg-blue-500/20 text-blue-400" : "bg-[var(--color-surface-hover)] text-[var(--color-text-muted)]"}`}>
                  <Users className="w-4 h-4" />
                </div>
                <div className="text-left">
                  <p className={`text-sm font-medium ${selectedAccount.visibility === "Joint" ? "text-blue-400" : "text-white"}`}>Joint Account</p>
                  <p className="text-xs text-[var(--color-text-muted)]">Full access for household members</p>
                </div>
              </div>
              {selectedAccount.visibility === "Joint" && (
                <div className="w-2 h-2 rounded-full bg-blue-400" />
              )}
            </button>

            <button 
              onClick={() => handleVisibilityChange(selectedAccount.id, "Shared")}
              className={`w-full flex items-center justify-between p-3 rounded-lg border transition-colors ${
                selectedAccount.visibility === "Shared" 
                  ? "bg-purple-500/10 border-purple-500/30" 
                  : "bg-[var(--color-surface)] border-[var(--color-border)] hover:bg-[var(--color-surface-hover)]"
              }`}
            >
              <div className="flex items-center gap-3">
                <div className={`p-2 rounded-md ${selectedAccount.visibility === "Shared" ? "bg-purple-500/20 text-purple-400" : "bg-[var(--color-surface-hover)] text-[var(--color-text-muted)]"}`}>
                  <Eye className="w-4 h-4" />
                </div>
                <div className="text-left">
                  <p className={`text-sm font-medium ${selectedAccount.visibility === "Shared" ? "text-purple-400" : "text-white"}`}>Shared (Read-only)</p>
                  <p className="text-xs text-[var(--color-text-muted)]">Members can view but not edit</p>
                </div>
              </div>
              {selectedAccount.visibility === "Shared" && (
                <div className="w-2 h-2 rounded-full bg-purple-400" />
              )}
            </button>
          </div>
        </div>

        <div className="pt-6 border-t border-[var(--color-border)]">
          <button className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-[var(--color-surface)] border border-[var(--color-border)] rounded-lg text-sm font-medium text-white hover:bg-[var(--color-surface-hover)] transition-colors">
            <Settings className="w-4 h-4" />
            Advanced Settings
          </button>
        </div>
      </div>
    </div>
  ) : (
    <div className="space-y-8">
      {/* Net Worth Summary */}
      <div className="bg-[var(--color-surface-hover)] rounded-xl p-5 border border-[var(--color-border)]">
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] mb-1">Net Worth</h3>
        <CurrencyDisplay amount={netWorth} type="balance" className="text-3xl font-display font-bold" />

        <div className="mt-6 space-y-4">
          <div>
            <div className="flex justify-between text-sm mb-1">
              <span className="text-[var(--color-text-muted)]">Assets</span>
              <CurrencyDisplay amount={totalAssets} type="balance" className="font-mono" />
            </div>
            <div className="h-1.5 w-full bg-[var(--color-surface)] rounded-full overflow-hidden">
              <div className="h-full bg-[var(--color-positive)] rounded-full" style={{ width: '100%' }} />
            </div>
          </div>

          <div>
            <div className="flex justify-between text-sm mb-1">
              <span className="text-[var(--color-text-muted)]">Debts</span>
              <CurrencyDisplay amount={-totalDebts} type="balance" className="font-mono" />
            </div>
            <div className="h-1.5 w-full bg-[var(--color-surface)] rounded-full overflow-hidden">
              <div className="h-full bg-[var(--color-negative)] rounded-full" style={{ width: `${(totalDebts / totalAssets) * 100}%` }} />
            </div>
          </div>
        </div>
      </div>

      {/* Account Details Placeholder */}
      <div className="bg-[var(--color-surface-hover)] rounded-xl p-5 border border-[var(--color-border)] text-center py-12">
        <div className="w-12 h-12 rounded-full bg-[var(--color-surface)] flex items-center justify-center mx-auto mb-3">
          <PieChart className="w-6 h-6 text-[var(--color-text-muted)]" />
        </div>
        <h3 className="text-sm font-medium text-white">Select an account</h3>
        <p className="text-xs text-[var(--color-text-muted)] mt-1">Click on any account to view its details, history, and settings.</p>
      </div>
    </div>
  );

  return (
    <PageLayout 
      title="Accounts" 
      subtitle="Manage your connected institutions and manual accounts."
      rightPanel={rightPanel}
      actions={
        <Link 
          href="/onboarding/plaid"
          className="flex items-center gap-2 px-4 py-2 bg-[var(--color-primary)] text-[var(--color-primary-text)] text-sm font-medium rounded-lg hover:bg-[var(--color-primary-hover)] transition-colors shadow-sm"
        >
          <Plus className="w-4 h-4" />
          Add Account
        </Link>
      }
    >
      <AccountsList accounts={accounts} onSelectAccount={setSelectedAccount} selectedAccountId={selectedAccount?.id} />
    </PageLayout>
  );
}