import { PageLayout } from "../../components/layout/PageLayout";
import { AccountsList } from "../../components/accounts/AccountsList";
import { PieChart, Wallet, CreditCard, Building, Landmark, TrendingUp } from "lucide-react";

export const metadata = {
  title: "Accounts | Mosaic Money",
};

export default function AccountsPage() {
  // Mock data for accounts
  const accounts = [
    { id: 1, name: "Chase Checking", type: "Depository", balance: 4500.00, mask: "1234", institution: "Chase" },
    { id: 2, name: "Ally Savings", type: "Depository", balance: 12500.00, mask: "5678", institution: "Ally Bank" },
    { id: 3, name: "Amex Sapphire", type: "Credit Card", balance: -1250.00, mask: "9012", institution: "American Express" },
    { id: 4, name: "Fidelity Brokerage", type: "Investment", balance: 45000.00, mask: "3456", institution: "Fidelity" },
    { id: 5, name: "Vanguard 401k", type: "Investment", balance: 120000.00, mask: "7890", institution: "Vanguard" },
    { id: 6, name: "Auto Loan", type: "Loan", balance: -15000.00, mask: "1122", institution: "Capital One" },
    { id: 7, name: "Primary Residence", type: "Real Estate", balance: 450000.00, mask: null, institution: "Zillow Estimate" },
    { id: 8, name: "Mortgage", type: "Loan", balance: -320000.00, mask: "3344", institution: "Rocket Mortgage" },
  ];

  const totalAssets = accounts.filter(a => a.balance > 0).reduce((sum, a) => sum + a.balance, 0);
  const totalDebts = accounts.filter(a => a.balance < 0).reduce((sum, a) => sum + Math.abs(a.balance), 0);
  const netWorth = totalAssets - totalDebts;

  const rightPanel = (
    <div className="space-y-8">
      {/* Net Worth Summary */}
      <div className="bg-[var(--color-surface-hover)] rounded-xl p-5 border border-[var(--color-border)]">
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] mb-1">Net Worth</h3>
        <p className="text-3xl font-display font-bold text-white">${netWorth.toLocaleString()}</p>
        
        <div className="mt-6 space-y-4">
          <div>
            <div className="flex justify-between text-sm mb-1">
              <span className="text-[var(--color-text-muted)]">Assets</span>
              <span className="text-[var(--color-positive)] font-mono">${totalAssets.toLocaleString()}</span>
            </div>
            <div className="h-1.5 w-full bg-[var(--color-surface)] rounded-full overflow-hidden">
              <div className="h-full bg-[var(--color-positive)] rounded-full" style={{ width: '100%' }} />
            </div>
          </div>
          
          <div>
            <div className="flex justify-between text-sm mb-1">
              <span className="text-[var(--color-text-muted)]">Debts</span>
              <span className="text-[var(--color-negative)] font-mono">${totalDebts.toLocaleString()}</span>
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
    >
      <AccountsList accounts={accounts} />
    </PageLayout>
  );
}
