import { fetchApi } from "../lib/api";

export const dynamic = "force-dynamic";

export default async function HomePage() {
  let apiStatus = "Unknown";
  let transactions = [];
  let recurringItems = [];
  let reimbursements = [];

  try {
    const health = await fetchApi("/api/health", { cache: "no-store" });
    apiStatus = health.status === "ok" ? "Connected" : "Error";

    // Fetch projection metadata for recent transactions
    transactions = await fetchApi("/api/v1/transactions/projection-metadata?pageSize=200", { cache: "no-store" });
    
    // Fetch active recurring items
    recurringItems = await fetchApi("/api/v1/recurring?isActive=true", { cache: "no-store" });

    // Fetch pending reimbursements
    reimbursements = await fetchApi("/api/v1/reimbursements?status=PendingApproval", { cache: "no-store" });
  } catch (error) {
    console.error("Failed to fetch dashboard data:", error);
    apiStatus = "Disconnected";
  }

  // Calculate totals
  // Note: In a real app, total liquidity would come from an account balance endpoint.
  // Here we sum the recent transactions as a proxy for the dashboard.
  const totalLiquidity = transactions.reduce((sum, tx) => sum + tx.rawAmount, 0);
  
  // Household budget burn: sum of negative amounts not excluded from budget
  const householdBurn = transactions
    .filter(tx => !tx.excludeFromBudget && tx.rawAmount < 0)
    .reduce((sum, tx) => sum + Math.abs(tx.rawAmount), 0);

  // Business expenses: sum of negative amounts excluded from budget
  const businessExpenses = transactions
    .filter(tx => tx.excludeFromBudget && tx.rawAmount < 0)
    .reduce((sum, tx) => sum + Math.abs(tx.rawAmount), 0);

  // Upcoming recurring: sum of expected amounts
  const upcomingRecurring = recurringItems.reduce((sum, item) => sum + item.expectedAmount, 0);

  // Pending reimbursements: sum of proposed amounts
  const pendingReimbursements = reimbursements.reduce((sum, item) => sum + item.proposedAmount, 0);

  // Safe to spend: Total Liquidity - Upcoming Recurring + Pending Reimbursements
  // (Assuming pending reimbursements will be paid back)
  const safeToSpend = totalLiquidity - upcomingRecurring + pendingReimbursements;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900">Dashboard</h1>
        <p className="mt-1 text-sm text-gray-500">
          Welcome to Mosaic Money. Your financial overview and projections.
        </p>
        <p className="mt-2 text-xs text-gray-400">
          API Status: <span className={apiStatus === "Connected" ? "font-medium text-green-600" : "font-medium text-red-600"}>{apiStatus}</span>
        </p>
      </div>

      {apiStatus === "Disconnected" && (
        <div className="bg-red-50 border border-red-200 text-red-700 p-4 rounded-md">
          Failed to load dashboard data. Please ensure the API is running.
        </div>
      )}
      
      {/* FE-06: Business vs Household Isolation Visuals */}
      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <div className="bg-white overflow-hidden shadow rounded-lg border border-gray-200">
          <div className="p-5">
            <div className="flex items-center">
              <div className="w-0 flex-1">
                <dl>
                  <dt className="text-sm font-medium text-gray-500 truncate">Total Liquidity</dt>
                  <dd className="text-2xl font-semibold text-gray-900">${totalLiquidity.toFixed(2)}</dd>
                  <dd className="text-xs text-gray-500 mt-1">All accounts combined</dd>
                </dl>
              </div>
            </div>
          </div>
        </div>
        
        <div className="bg-white overflow-hidden shadow rounded-lg border border-green-200">
          <div className="p-5">
            <div className="flex items-center">
              <div className="w-0 flex-1">
                <dl>
                  <dt className="text-sm font-medium text-green-600 truncate">Household Budget Burn</dt>
                  <dd className="text-2xl font-semibold text-gray-900">${householdBurn.toFixed(2)}</dd>
                  <dd className="text-xs text-gray-500 mt-1">Shared expenses</dd>
                </dl>
              </div>
            </div>
          </div>
        </div>

        <div className="bg-white overflow-hidden shadow rounded-lg border border-purple-200">
          <div className="p-5">
            <div className="flex items-center">
              <div className="w-0 flex-1">
                <dl>
                  <dt className="text-sm font-medium text-purple-600 truncate">Business Expenses</dt>
                  <dd className="text-2xl font-semibold text-gray-900">${businessExpenses.toFixed(2)}</dd>
                  <dd className="text-xs text-gray-500 mt-1">Excluded from household budget</dd>
                </dl>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* FE-07: Recurring + Safe-to-Spend Projection UI */}
      <h2 className="text-lg font-medium text-gray-900 mt-8">Projections</h2>
      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <div className="bg-white overflow-hidden shadow rounded-lg border border-blue-200">
          <div className="p-5">
            <div className="flex items-center">
              <div className="w-0 flex-1">
                <dl>
                  <dt className="text-sm font-medium text-blue-600 truncate">Safe to Spend</dt>
                  <dd className="text-2xl font-semibold text-gray-900">${safeToSpend.toFixed(2)}</dd>
                  <dd className="text-xs text-gray-500 mt-1">Liquidity - Upcoming + Pending</dd>
                </dl>
              </div>
            </div>
          </div>
        </div>

        <div className="bg-white overflow-hidden shadow rounded-lg border border-orange-200">
          <div className="p-5">
            <div className="flex items-center">
              <div className="w-0 flex-1">
                <dl>
                  <dt className="text-sm font-medium text-orange-600 truncate">Upcoming Recurring</dt>
                  <dd className="text-2xl font-semibold text-gray-900">${upcomingRecurring.toFixed(2)}</dd>
                  <dd className="text-xs text-gray-500 mt-1">{recurringItems.length} active items</dd>
                </dl>
              </div>
            </div>
          </div>
        </div>

        <div className="bg-white overflow-hidden shadow rounded-lg border border-teal-200">
          <div className="p-5">
            <div className="flex items-center">
              <div className="w-0 flex-1">
                <dl>
                  <dt className="text-sm font-medium text-teal-600 truncate">Pending Reimbursements</dt>
                  <dd className="text-2xl font-semibold text-gray-900">${pendingReimbursements.toFixed(2)}</dd>
                  <dd className="text-xs text-gray-500 mt-1">{reimbursements.length} pending proposals</dd>
                </dl>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Recent Transactions with Projection Metadata */}
      <h2 className="text-lg font-medium text-gray-900 mt-8">Recent Transactions (Projection Metadata)</h2>
      <div className="bg-white shadow rounded-lg border border-gray-200 overflow-hidden">
        {transactions.length === 0 ? (
          <p className="text-gray-500 text-center py-8">No transactions found.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Description</th>
                  <th scope="col" className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Context</th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {transactions.slice(0, 10).map((tx) => (
                  <tr key={tx.id} className="hover:bg-gray-50">
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                      {tx.rawTransactionDate}
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-900">
                      <div className="font-medium">{tx.description}</div>
                      {tx.excludeFromBudget && (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-purple-100 text-purple-800 mt-1">
                          Business Expense
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-right font-medium">
                      <span className={tx.rawAmount < 0 ? "text-red-600" : "text-green-600"}>
                        ${Math.abs(tx.rawAmount).toFixed(2)}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-sm text-gray-500">
                      <div className="flex flex-col gap-1">
                        {tx.recurring?.isLinked && (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-orange-100 text-orange-800 w-fit">
                            Recurring: {tx.recurring.frequency}
                          </span>
                        )}
                        {tx.reimbursement?.hasProposals && (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-teal-100 text-teal-800 w-fit">
                            Reimbursement: {tx.reimbursement.latestStatus}
                          </span>
                        )}
                        {tx.splits?.length > 0 && (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-800 w-fit">
                            Amortized: {tx.splits.length} splits
                          </span>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
