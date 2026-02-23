import { fetchApi } from "../../lib/api";

export const dynamic = "force-dynamic";

export default async function TransactionsPage() {
  let transactions = [];
  let error = null;

  try {
    transactions = await fetchApi("/api/v1/transactions/projection-metadata?pageSize=200");
  } catch (e) {
    error = e.message;
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900">Transactions</h1>
        <p className="mt-1 text-sm text-gray-500">
          View your financial transactions. Read-only ledger truth with projection context.
        </p>
      </div>

      {error ? (
        <div data-testid="transactions-error-banner" className="bg-red-50 border border-red-200 text-red-700 p-4 rounded-md">
          Failed to load transactions: {error}
        </div>
      ) : (
        <div data-testid="transactions-table-wrapper" className="bg-white shadow rounded-lg border border-gray-200 overflow-hidden">
          {transactions.length === 0 ? (
            <p data-testid="transactions-empty-state" className="text-gray-500 text-center py-8">No transactions found.</p>
          ) : (
            <div className="overflow-x-auto">
              <table data-testid="transactions-table" className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                    <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Description</th>
                    <th scope="col" className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
                    <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                    <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Context</th>
                    <th scope="col" className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Notes</th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {transactions.map((tx) => (
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
                      <td className="px-6 py-4 whitespace-nowrap text-sm">
                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                          tx.reviewStatus === 'NeedsReview' ? 'bg-yellow-100 text-yellow-800' :
                          tx.reviewStatus === 'Reviewed' ? 'bg-green-100 text-green-800' :
                          'bg-gray-100 text-gray-800'
                        }`}>
                          {tx.reviewStatus}
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
                      <td className="px-6 py-4 text-sm text-gray-500">
                        {tx.userNote && (
                          <div className="mb-1">
                            <span className="font-semibold text-gray-700 text-xs uppercase">User:</span> {tx.userNote}
                          </div>
                        )}
                        {tx.agentNote && (
                          <div>
                            <span className="font-semibold text-blue-700 text-xs uppercase">Agent:</span> {tx.agentNote}
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
