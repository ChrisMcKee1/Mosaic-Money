import { fetchApi } from "../lib/api";
import { cn } from "../lib/utils";
import { PageLayout } from "../components/layout/PageLayout";
import { DashboardCharts } from "../components/dashboard/DashboardCharts";
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
      <div className="bg-[var(--color-surface-hover)] rounded-xl p-5 border border-[var(--color-border)] relative overflow-hidden">
        <div className="absolute top-0 left-0 w-1 h-full bg-[var(--color-warning)]" />
        <div className="flex items-start justify-between">
          <div>
            <h3 className="text-sm font-medium text-white flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-[var(--color-warning)]" />
              Action Required
            </h3>
            <p className="mt-1 text-2xl font-display font-bold text-white">{needsReviewCount}</p>
            <p className="text-xs text-[var(--color-text-muted)] mt-1">Transactions need review</p>
          </div>
          <Link href="/needs-review" className="p-2 bg-[var(--color-surface)] rounded-lg hover:bg-[var(--color-border)] transition-colors">
            <ArrowRight className="w-4 h-4 text-white" />
          </Link>
        </div>
      </div>

      {/* Upcoming Recurring Widget */}
      <div>
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4 flex items-center gap-2">
          <Calendar className="w-4 h-4" />
          Next 14 Days
        </h3>
        <div className="space-y-3">
          {recurringItems.slice(0, 4).map((item, i) => {
            const daysUntil = item.nextDueDate ? Math.ceil((new Date(item.nextDueDate) - new Date()) / (1000 * 60 * 60 * 24)) : i + 2;
            return (
              <div key={item.id || i} className="flex items-center justify-between p-3 rounded-lg bg-[var(--color-surface-hover)] border border-[var(--color-border)]">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-full bg-[var(--color-surface)] flex items-center justify-center text-xs font-bold text-[var(--color-primary)]">
                    {item.merchantName?.charAt(0) || "R"}
                  </div>
                  <div>
                    <p className="text-sm font-medium text-white">{item.merchantName || "Unknown"}</p>
                    <div className="flex items-center gap-2 mt-0.5">
                      <p className="text-xs text-[var(--color-text-muted)]">
                        {daysUntil === 0 ? "Today" : daysUntil === 1 ? "Tomorrow" : `In ${daysUntil} days`}
                      </p>
                      {item.recurringSource === "plaid" && (
                        <span className="text-[10px] px-1.5 py-0.5 rounded bg-blue-500/10 text-blue-400 border border-blue-500/20">
                          Plaid
                        </span>
                      )}
                    </div>
                  </div>
                </div>
                <span className="text-sm font-mono font-medium text-white">
                  ${item.expectedAmount?.toFixed(2) || "0.00"}
                </span>
              </div>
            );
          })}
          {recurringItems.length === 0 && (
            <div className="text-sm text-[var(--color-text-muted)] p-4 text-center border border-dashed border-[var(--color-border)] rounded-lg">
              No upcoming bills
            </div>
          )}
        </div>
      </div>

      {/* Top Categories Summary */}
      <div>
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4 flex items-center gap-2">
          <TrendingUp className="w-4 h-4" />
          Top Categories
        </h3>
        <div className="space-y-4">
          {[
            { name: "Groceries", amount: 450.20, max: 600, color: "var(--color-primary)" },
            { name: "Dining Out", amount: 320.50, max: 400, color: "var(--color-warning)" },
            { name: "Transportation", amount: 150.00, max: 200, color: "var(--color-positive)" },
          ].map((cat) => (
            <div key={cat.name}>
              <div className="flex justify-between text-sm mb-1">
                <span className="text-white">{cat.name}</span>
                <span className="font-mono text-[var(--color-text-muted)]">${cat.amount.toFixed(0)}</span>
              </div>
              <div className="h-1.5 w-full bg-[var(--color-surface-hover)] rounded-full overflow-hidden">
                <div 
                  className="h-full rounded-full" 
                  style={{ width: `${(cat.amount / cat.max) * 100}%`, backgroundColor: cat.color }}
                />
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Asset Allocation */}
      {investmentAccounts.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4 flex items-center gap-2">
            <TrendingUp className="w-4 h-4" />
            Asset Allocation
          </h3>
          <div className="space-y-3">
            {investmentAccounts.slice(0, 4).map((account) => (
              <div key={account.id} className="flex items-center justify-between p-3 rounded-lg bg-[var(--color-surface-hover)] border border-[var(--color-border)]">
                <div>
                  <p className="text-sm font-medium text-white">{account.name}</p>
                  <p className="text-xs text-[var(--color-text-muted)]">{account.accountSubtype || account.accountType}</p>
                </div>
                <span className="text-sm font-mono font-medium text-white">
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
          <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4 flex items-center gap-2">
            <AlertCircle className="w-4 h-4" />
            Debt & Liabilities
          </h3>
          <div className="space-y-3">
            {liabilityAccounts.slice(0, 4).map((account) => {
              const latestSnapshot = account.snapshots && account.snapshots.length > 0 ? account.snapshots[0] : null;
              return (
                <div key={account.id} className="flex items-center justify-between p-3 rounded-lg bg-[var(--color-surface-hover)] border border-[var(--color-border)]">
                  <div>
                    <p className="text-sm font-medium text-white">{account.name}</p>
                    <p className="text-xs text-[var(--color-text-muted)]">{account.accountSubtype || account.accountType}</p>
                  </div>
                  <span className="text-sm font-mono font-medium text-[var(--color-negative)]">
                    {latestSnapshot?.currentBalance ? `$${latestSnapshot.currentBalance.toFixed(2)}` : 'Active'}
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
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
        <div className="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)]">
          <p className="text-sm font-medium text-[var(--color-text-muted)] mb-1">Safe to Spend</p>
          <p className="text-3xl font-display font-bold text-white">${safeToSpend.toFixed(2)}</p>
          <div className="mt-4 flex items-center text-xs text-[var(--color-positive)] bg-[var(--color-positive-bg)] w-fit px-2 py-1 rounded">
            <TrendingUp className="w-3 h-3 mr-1" />
            +2.4% from last month
          </div>
        </div>
        
        <div className="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)]">
          <p className="text-sm font-medium text-[var(--color-text-muted)] mb-1">Household Burn</p>
          <p className="text-3xl font-display font-bold text-white">${householdBurn.toFixed(2)}</p>
          <div className="mt-4 flex items-center text-xs text-[var(--color-text-muted)] bg-[var(--color-surface-hover)] w-fit px-2 py-1 rounded">
            Shared expenses
          </div>
        </div>

        <div className="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)]">
          <p className="text-sm font-medium text-[var(--color-text-muted)] mb-1">Business Expenses</p>
          <p className="text-3xl font-display font-bold text-white">${businessExpenses.toFixed(2)}</p>
          <div className="mt-4 flex items-center text-xs text-[var(--color-text-muted)] bg-[var(--color-surface-hover)] w-fit px-2 py-1 rounded">
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
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-display font-semibold text-white">Recent Transactions</h2>
          <Link href="/transactions" className="text-sm text-[var(--color-primary)] hover:text-[var(--color-primary-hover)] font-medium">
            View all
          </Link>
        </div>
        
        <div className="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] overflow-hidden">
          {recentTransactions.length === 0 ? (
            <p className="text-[var(--color-text-muted)] text-center py-8 text-sm">No transactions found.</p>
          ) : (
            <div className="divide-y divide-[var(--color-border)]">
              {recentTransactions.map((tx) => (
                <div key={tx.id} className="p-4 flex items-center justify-between hover:bg-[var(--color-surface-hover)] transition-colors">
                  <div className="flex items-center gap-4">
                    <div className="w-10 h-10 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center text-white font-bold">
                      {tx.description?.charAt(0) || "T"}
                    </div>
                    <div>
                      <p className="text-sm font-medium text-white">{tx.description}</p>
                      <div className="flex items-center gap-2 mt-1">
                        <span className="text-xs text-[var(--color-text-muted)]">{tx.transactionDate || tx.rawTransactionDate}</span>
                        {tx.excludeFromBudget && (
                          <span className="text-[10px] px-1.5 py-0.5 rounded bg-purple-500/10 text-purple-400 border border-purple-500/20">
                            Business
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                  <div className="text-right">
                    <p className={cn(
                      "text-sm font-mono font-medium",
                      (tx.amount || tx.rawAmount) < 0 ? "text-white" : "text-[var(--color-positive)]"
                    )}>
                      {(tx.amount || tx.rawAmount) > 0 ? "+" : ""}{(tx.amount || tx.rawAmount) < 0 ? "-" : ""}${Math.abs(tx.amount || tx.rawAmount).toFixed(2)}
                    </p>
                    {(tx.recurring?.isLinked || tx.recurringItemId) && (
                      <span className="text-[10px] text-[var(--color-warning)] mt-1 block">Recurring</span>
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
