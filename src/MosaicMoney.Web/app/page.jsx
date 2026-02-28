import { fetchApi } from "../lib/api";
import { cn } from "../lib/utils";
import { PageLayout } from "../components/layout/PageLayout";
import { DashboardCharts } from "../components/dashboard/DashboardCharts";
import { CurrencyDisplay } from "../components/ui/CurrencyDisplay";
import { AlertCircle, ArrowRight, Calendar, TrendingUp } from "lucide-react";
import Link from "next/link";

export const dynamic = "force-dynamic";

export default async function HomePage() {
  let apiStatus = "Unknown";
  let transactions = [];
  let recentTransactions = [];
  let recurringItems = [];
  let reimbursements = [];
  let netWorthHistory = [];
  let investmentAccounts = [];
  let liabilityAccounts = [];
  let householdId = null;

  try {
    const health = await fetchApi("/api/health", { cache: "no-store" });
    apiStatus = health.status === "ok" ? "Connected" : "Error";

    // Fetch household first to get the ID for other endpoints
    const households = await fetchApi("/api/v1/households", { cache: "no-store" });
    if (households && households.length > 0) {
      householdId = households[0].id;
    }

    const [
      txData,
      recentTxData,
      recurringData,
      reimbursementsData,
      nwHistoryData,
      investmentsData,
      liabilitiesData
    ] = await Promise.all([
      fetchApi("/api/v1/transactions/projection-metadata?pageSize=200", { cache: "no-store" }),
      fetchApi("/api/v1/transactions?pageSize=5", { cache: "no-store" }),
      fetchApi(householdId ? `/api/v1/recurring?householdId=${householdId}` : "/api/v1/recurring", { cache: "no-store" }),
      fetchApi("/api/v1/reimbursements?status=PendingApproval", { cache: "no-store" }),
      householdId ? fetchApi(`/api/v1/net-worth/history?householdId=${householdId}&months=6`, { cache: "no-store" }) : Promise.resolve([]),
      householdId ? fetchApi(`/api/v1/investments/accounts?householdId=${householdId}`, { cache: "no-store" }) : Promise.resolve([]),
      householdId ? fetchApi(`/api/v1/liabilities/accounts?householdId=${householdId}`, { cache: "no-store" }) : Promise.resolve([])
    ]);

    transactions = txData || [];
    recentTransactions = recentTxData || [];
    recurringItems = recurringData || [];
    reimbursements = reimbursementsData || [];
    netWorthHistory = nwHistoryData || [];
    investmentAccounts = investmentsData || [];
    liabilityAccounts = liabilitiesData || [];
  } catch (error) {
    console.error("Failed to fetch dashboard data:", error);
    apiStatus = "Disconnected";
  }

  const totalLiquidity = transactions.reduce((sum, tx) => sum + tx.rawAmount, 0);
  const householdBurn = transactions
    .filter(tx => !tx.excludeFromBudget && tx.rawAmount < 0)
    .reduce((sum, tx) => sum + Math.abs(tx.rawAmount), 0);
  const businessExpenses = transactions
    .filter(tx => tx.excludeFromBudget && tx.rawAmount < 0)
    .reduce((sum, tx) => sum + Math.abs(tx.rawAmount), 0);
  const upcomingRecurring = recurringItems.reduce((sum, item) => sum + item.expectedAmount, 0);
  const pendingReimbursements = reimbursements.reduce((sum, item) => sum + item.proposedAmount, 0);
  const safeToSpend = totalLiquidity - upcomingRecurring + pendingReimbursements;

  const needsReviewCount = transactions.filter(tx => tx.needsReview).length || 3; // Mocking 3 for visual if none

  const rightPanel = (
    <div className="space-y-8">
      {/* Action Required Widget */}
      <div className="group relative overflow-hidden rounded-2xl p-[1px] transition-all hover:shadow-[0_0_2rem_-0.5rem_var(--color-warning)] border border-[var(--color-warning)]/30">
        <div className="absolute inset-0 bg-gradient-to-br from-[var(--color-warning-bg)] to-[var(--color-negative-bg)] opacity-100 transition-opacity" />
        <div className="relative bg-[var(--color-surface)]/60 backdrop-blur-sm rounded-[15px] p-5 h-full flex flex-col justify-between">
          <div className="flex items-start justify-between">
            <div>
              <h3 className="text-sm font-medium text-[var(--color-text-main)] flex items-center gap-2 uppercase tracking-wider">
                <AlertCircle className="w-4 h-4 text-[var(--color-warning)] animate-pulse" />
                Action Required
              </h3>
              <div className="mt-3 flex items-baseline gap-2">
                <p className="text-4xl font-display font-bold text-[var(--color-text-main)] tracking-tight">{needsReviewCount}</p>
                <p className="text-sm text-[var(--color-text-muted)] font-medium">transactions</p>
              </div>
              <p className="text-xs text-[var(--color-text-subtle)] mt-1">Need your review to categorize</p>
            </div>
            <Link href="/needs-review" className="p-2.5 bg-[var(--color-warning)]/10 hover:bg-[var(--color-warning)]/20 rounded-xl transition-all hover:scale-105 hover:-translate-y-0.5 border border-[var(--color-warning)]/20">
              <ArrowRight className="w-4 h-4 text-[var(--color-warning)]" />
            </Link>
          </div>
        </div>
      </div>

      {/* Upcoming Recurring Widget */}
      <div>
        <h3 className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest mb-4 flex items-center gap-2">
          <Calendar className="w-3.5 h-3.5" />
          Next 14 Days
        </h3>
        <div className="space-y-3">
          {recurringItems.slice(0, 4).map((item, i) => {
            const daysUntil = item.nextDueDate ? Math.ceil((new Date(item.nextDueDate) - new Date()) / (1000 * 60 * 60 * 24)) : i + 2;
            return (
              <div key={item.id || i} className="group flex items-center justify-between p-3.5 rounded-xl bg-[var(--color-surface)] border border-[var(--color-border)] hover:border-[var(--color-primary)]/30 hover:bg-[var(--color-surface-hover)] transition-all">
                <div className="flex items-center gap-3.5">
                  <div className="w-9 h-9 rounded-lg bg-[var(--color-surface-hover)] border border-[var(--color-border)] flex items-center justify-center text-xs font-bold text-[var(--color-primary)] group-hover:scale-105 transition-transform">
                    {item.merchantName?.charAt(0) || "R"}
                  </div>
                  <div>
                    <p className="text-sm font-medium text-white/90 group-hover:text-white transition-colors">{item.merchantName || "Unknown"}</p>
                    <div className="flex items-center gap-2 mt-0.5">
                      <p className="text-xs text-[var(--color-text-muted)] font-medium">
                        {daysUntil === 0 ? "Today" : daysUntil === 1 ? "Tomorrow" : `In ${daysUntil} days`}
                      </p>
                      {item.recurringSource === "plaid" && (
                        <span className="text-[9px] px-1.5 py-0.5 rounded bg-blue-500/10 text-blue-400 border border-blue-500/20 font-medium uppercase tracking-wider">
                          Plaid
                        </span>
                      )}
                    </div>
                  </div>
                </div>
                <span className="text-sm font-mono font-medium text-white tracking-tight">
                  ${item.expectedAmount?.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) || "0.00"}
                </span>
              </div>
            );
          })}
          {recurringItems.length === 0 && (
            <div className="text-sm text-[var(--color-text-muted)] p-6 text-center border border-dashed border-[var(--color-border)] rounded-xl bg-[var(--color-surface)]/50">
              No upcoming bills
            </div>
          )}
        </div>
      </div>

      {/* Top Categories Summary */}
      <div>
        <h3 className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest mb-4 flex items-center gap-2">
          <TrendingUp className="w-3.5 h-3.5" />
          Top Categories
        </h3>
        <div className="space-y-5 bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)]">
          {[
            { name: "Groceries", amount: 450.20, max: 600, color: "var(--color-primary)" },
            { name: "Dining Out", amount: 320.50, max: 400, color: "var(--color-warning)" },
            { name: "Transportation", amount: 150.00, max: 200, color: "var(--color-positive)" },
          ].map((cat) => (
            <div key={cat.name} className="group">
              <div className="flex justify-between text-sm mb-2">
                <span className="text-white/90 font-medium group-hover:text-white transition-colors">{cat.name}</span>
                <span className="font-mono text-[var(--color-text-muted)] tracking-tight">${cat.amount.toLocaleString(undefined, { maximumFractionDigits: 0 })}</span>
              </div>
              <div className="h-2 w-full bg-[var(--color-surface-hover)] rounded-full overflow-hidden border border-[var(--color-border)]/50">
                <div 
                  className="h-full rounded-full transition-all duration-1000 ease-out relative" 
                  style={{ width: `${(cat.amount / cat.max) * 100}%`, backgroundColor: cat.color }}
                >
                  <div className="absolute inset-0 bg-white/20 w-full h-full" style={{ mixBlendMode: 'overlay' }} />
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Asset Allocation */}
      {investmentAccounts.length > 0 && (
        <div>
          <h3 className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest mb-4 flex items-center gap-2">
            <TrendingUp className="w-3.5 h-3.5" />
            Asset Allocation
          </h3>
          <div className="space-y-3">
            {investmentAccounts.slice(0, 4).map((account) => (
              <div key={account.id} className="group flex items-center justify-between p-3.5 rounded-xl bg-[var(--color-surface)] border border-[var(--color-border)] hover:border-[var(--color-positive)]/30 hover:bg-[var(--color-surface-hover)] transition-all">
                <div>
                  <p className="text-sm font-medium text-white/90 group-hover:text-white transition-colors">{account.name}</p>
                  <p className="text-xs text-[var(--color-text-muted)] mt-0.5">{account.accountSubtype || account.accountType}</p>
                </div>
                <span className="text-sm font-mono font-medium text-[var(--color-positive)] tracking-tight">
                  Active
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Debt / Liabilities */}
      {liabilityAccounts.length > 0 && (
        <div>
          <h3 className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest mb-4 flex items-center gap-2">
            <AlertCircle className="w-3.5 h-3.5" />
            Debt & Liabilities
          </h3>
          <div className="space-y-3">
            {liabilityAccounts.slice(0, 4).map((account) => {
              const latestSnapshot = account.snapshots && account.snapshots.length > 0 ? account.snapshots[0] : null;
              return (
                <div key={account.id} className="group flex items-center justify-between p-3.5 rounded-xl bg-[var(--color-surface)] border border-[var(--color-border)] hover:border-[var(--color-negative)]/30 hover:bg-[var(--color-surface-hover)] transition-all">
                  <div>
                    <p className="text-sm font-medium text-white/90 group-hover:text-white transition-colors">{account.name}</p>
                    <p className="text-xs text-[var(--color-text-muted)] mt-0.5">{account.accountSubtype || account.accountType}</p>
                  </div>
                  <span className="text-sm font-mono font-medium text-[var(--color-negative)] tracking-tight">
                    {latestSnapshot?.currentBalance ? `$${latestSnapshot.currentBalance.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : 'Active'}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );

  return (
    <PageLayout 
      title="Dashboard" 
      subtitle="Your financial overview and projections."
      rightPanel={rightPanel}
    >
      {apiStatus === "Disconnected" && (
        <div className="mb-6 bg-[var(--color-negative-bg)] border border-[var(--color-negative)] text-[var(--color-negative)] p-4 rounded-lg flex items-center gap-3">
          <AlertCircle className="w-5 h-5" />
          <p className="text-sm font-medium">Failed to load dashboard data. Please ensure the API is running.</p>
        </div>
      )}

      {/* Key Metrics Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-5 mb-10">
        {/* Safe to Spend - Primary Focus */}
        <div className="group relative bg-[var(--color-surface)] p-6 rounded-2xl border border-[var(--color-border)] overflow-hidden transition-all hover:border-[var(--color-primary)]/50 hover:shadow-[0_8px_30px_-12px_rgba(var(--color-primary-rgb),0.3)]">
          <div className="absolute top-0 right-0 -mt-4 -mr-4 w-24 h-24 bg-[var(--color-primary)]/10 rounded-full blur-2xl group-hover:bg-[var(--color-primary)]/20 transition-colors" />
          <p className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest mb-2">Safe to Spend</p>
          <p className="text-4xl font-display font-bold text-white tracking-tight">${safeToSpend.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
          <div className="mt-5 flex items-center text-xs font-medium text-[var(--color-positive)] bg-[var(--color-positive)]/10 border border-[var(--color-positive)]/20 w-fit px-2.5 py-1 rounded-full">
            <TrendingUp className="w-3.5 h-3.5 mr-1.5" />
            +2.4% from last month
          </div>
        </div>
        
        {/* Household Burn */}
        <div className="group bg-[var(--color-surface)] p-6 rounded-2xl border border-[var(--color-border)] transition-all hover:border-white/20 hover:bg-[var(--color-surface-hover)]">
          <p className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest mb-2">Household Burn</p>
          <p className="text-4xl font-display font-bold text-white/90 tracking-tight">${householdBurn.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
          <div className="mt-5 flex items-center text-xs font-medium text-[var(--color-text-muted)] bg-white/5 border border-white/5 w-fit px-2.5 py-1 rounded-full">
            Shared expenses
          </div>
        </div>

        {/* Business Expenses */}
        <div className="group bg-[var(--color-surface)] p-6 rounded-2xl border border-[var(--color-border)] transition-all hover:border-purple-500/30 hover:shadow-[0_8px_30px_-12px_rgba(168,85,247,0.15)] relative overflow-hidden">
          <div className="absolute top-0 right-0 -mt-4 -mr-4 w-24 h-24 bg-purple-500/5 rounded-full blur-2xl group-hover:bg-purple-500/10 transition-colors" />
          <p className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest mb-2">Business Expenses</p>
          <p className="text-4xl font-display font-bold text-white/90 tracking-tight">${businessExpenses.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</p>
          <div className="mt-5 flex items-center text-xs font-medium text-purple-400 bg-purple-500/10 border border-purple-500/20 w-fit px-2.5 py-1 rounded-full">
            Excluded from budget
          </div>
        </div>
      </div>

      {/* Charts Area */}
      <div className="mb-8">
        <DashboardCharts netWorthHistory={netWorthHistory} transactions={transactions} />
      </div>

      {/* Recent Transactions */}
      <div>
        <div className="flex items-center justify-between mb-5">
          <h2 className="text-lg font-display font-semibold text-white tracking-tight">Recent Transactions</h2>
          <Link href="/transactions" className="text-sm text-[var(--color-primary)] hover:text-[var(--color-primary-hover)] font-medium flex items-center gap-1 group">
            View all <ArrowRight className="w-3.5 h-3.5 group-hover:translate-x-0.5 transition-transform" />
          </Link>
        </div>
        
        <div className="bg-[var(--color-surface)] rounded-2xl border border-[var(--color-border)] overflow-hidden shadow-sm">
          {recentTransactions.length === 0 ? (
            <p className="text-[var(--color-text-muted)] text-center py-10 text-sm">No transactions found.</p>
          ) : (
            <div className="divide-y divide-[var(--color-border)]">
              {recentTransactions.map((tx) => (
                <div key={tx.id} className="p-4 sm:p-5 flex items-center justify-between hover:bg-[var(--color-surface-hover)] transition-colors group">
                  <div className="flex items-center gap-4">
                    <div className="w-10 h-10 rounded-xl bg-[var(--color-surface-hover)] border border-[var(--color-border)] flex items-center justify-center text-white font-bold shadow-sm group-hover:scale-105 transition-transform">
                      {tx.description?.charAt(0) || "T"}
                    </div>
                    <div>
                      <p className="text-sm font-medium text-white/90 group-hover:text-white transition-colors">{tx.description}</p>
                      <div className="flex items-center gap-2 mt-1">
                        <span className="text-xs text-[var(--color-text-muted)]">{tx.transactionDate || tx.rawTransactionDate}</span>
                        {tx.excludeFromBudget && (
                          <span className="text-[10px] px-1.5 py-0.5 rounded bg-purple-500/10 text-purple-400 border border-purple-500/20 font-medium">
                            Business
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                  <div className="text-right">
                      <CurrencyDisplay 
                        amount={tx.amount || tx.rawAmount} 
                        isTransfer={tx.excludeFromBudget} 
                        className="text-sm block tracking-tight" 
                      />
                    {(tx.recurring?.isLinked || tx.recurringItemId) && (
                      <span className="text-[10px] text-[var(--color-warning)] mt-1 block font-medium">Recurring</span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </PageLayout>
  );
}
