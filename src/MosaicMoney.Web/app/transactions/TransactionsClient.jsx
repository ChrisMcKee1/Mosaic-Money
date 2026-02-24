"use client";

import { useState } from "react";
import { PageLayout } from "../../components/layout/PageLayout";
import { Search, Filter, ArrowDownRight, ArrowUpRight, Tag, Calendar, FileText, MessageSquare } from "lucide-react";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

export function TransactionsClient({ initialTransactions }) {
  const [selectedTx, setSelectedTx] = useState(initialTransactions[0] || null);
  const [searchQuery, setSearchQuery] = useState("");

  // Group transactions by date
  const groupedTransactions = initialTransactions.reduce((acc, tx) => {
    const date = tx.rawTransactionDate || "Unknown Date";
    if (!acc[date]) acc[date] = [];
    acc[date].push(tx);
    return acc;
  }, {});

  const rightPanel = selectedTx ? (
    <div className="space-y-6">
      <div>
        <div className="w-12 h-12 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center text-white font-bold text-xl mb-4">
          {selectedTx.description?.charAt(0) || "T"}
        </div>
        <h2 className="text-xl font-display font-bold text-white">{selectedTx.description}</h2>
        <p className={cn(
          "text-3xl font-mono font-medium mt-2",
          selectedTx.rawAmount < 0 ? "text-white" : "text-[var(--color-positive)]"
        )}>
          {selectedTx.rawAmount > 0 ? "+" : ""}{selectedTx.rawAmount < 0 ? "-" : ""}${Math.abs(selectedTx.rawAmount).toFixed(2)}
        </p>
        <p className="text-sm text-[var(--color-text-muted)] mt-1">{selectedTx.rawTransactionDate}</p>
      </div>

      <div className="space-y-4 pt-4 border-t border-[var(--color-border)]">
        <div>
          <h3 className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-2">Categorization</h3>
          <div className="flex items-center gap-2">
            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-sm font-medium bg-[var(--color-surface-hover)] text-white border border-[var(--color-border)]">
              <Tag className="w-3.5 h-3.5 text-[var(--color-primary)]" />
              {selectedTx.category || "Uncategorized"}
            </span>
            {selectedTx.excludeFromBudget && (
              <span className="inline-flex items-center px-2.5 py-1 rounded-md text-sm font-medium bg-purple-500/10 text-purple-400 border border-purple-500/20">
                Business
              </span>
            )}
          </div>
        </div>

        <div>
          <h3 className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-2">Status</h3>
          <span className={cn(
            "inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium",
            selectedTx.reviewStatus === 'NeedsReview' ? 'bg-[var(--color-warning)]/10 text-[var(--color-warning)] border border-[var(--color-warning)]/20' :
            selectedTx.reviewStatus === 'Reviewed' ? 'bg-[var(--color-positive)]/10 text-[var(--color-positive)] border border-[var(--color-positive)]/20' :
            'bg-[var(--color-surface-hover)] text-[var(--color-text-muted)] border border-[var(--color-border)]'
          )}>
            {selectedTx.reviewStatus || "Pending"}
          </span>
        </div>

        {(selectedTx.userNote || selectedTx.agentNote) && (
          <div>
            <h3 className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-2">Notes</h3>
            <div className="space-y-2">
              {selectedTx.userNote && (
                <div className="bg-[var(--color-surface-hover)] p-3 rounded-lg border border-[var(--color-border)]">
                  <p className="text-xs font-medium text-white mb-1 flex items-center gap-1.5">
                    <MessageSquare className="w-3 h-3" /> User Note
                  </p>
                  <p className="text-sm text-[var(--color-text-muted)]">{selectedTx.userNote}</p>
                </div>
              )}
              {selectedTx.agentNote && (
                <div className="bg-[var(--color-primary)]/10 p-3 rounded-lg border border-[var(--color-primary)]/20">
                  <p className="text-xs font-medium text-[var(--color-primary)] mb-1 flex items-center gap-1.5">
                    <MessageSquare className="w-3 h-3" /> Agent Note
                  </p>
                  <p className="text-sm text-[var(--color-text-muted)]">{selectedTx.agentNote}</p>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  ) : (
    <div className="flex flex-col items-center justify-center h-full text-center space-y-4">
      <div className="w-16 h-16 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center">
        <FileText className="w-8 h-8 text-[var(--color-text-muted)]" />
      </div>
      <div>
        <p className="text-white font-medium">No transaction selected</p>
        <p className="text-sm text-[var(--color-text-muted)] mt-1">Select a transaction to view details</p>
      </div>
    </div>
  );

  return (
    <PageLayout 
      title="Transactions" 
      subtitle="View and manage your financial transactions."
      rightPanel={rightPanel}
    >
      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row gap-4 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-muted)]" />
          <input 
            type="text" 
            placeholder="Search transactions..." 
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full bg-[var(--color-surface)] border border-[var(--color-border)] rounded-lg pl-10 pr-4 py-2 text-sm text-white placeholder:text-[var(--color-text-muted)] focus:outline-none focus:border-[var(--color-primary)] focus:ring-1 focus:ring-[var(--color-primary)] transition-all"
          />
        </div>
        <button className="flex items-center gap-2 px-4 py-2 bg-[var(--color-surface)] border border-[var(--color-border)] rounded-lg text-sm font-medium text-white hover:bg-[var(--color-surface-hover)] transition-colors">
          <Filter className="w-4 h-4" />
          Filter
        </button>
      </div>

      {/* Transactions List */}
      <div className="space-y-8">
        {Object.entries(groupedTransactions).map(([date, txs]) => (
          <div key={date}>
            <h3 className="text-sm font-medium text-[var(--color-text-muted)] mb-3 sticky top-0 bg-[var(--color-background)] py-2 z-10">
              {date}
            </h3>
            <div className="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] overflow-hidden">
              <div className="divide-y divide-[var(--color-border)]">
                {txs.map((tx) => (
                  <button
                    key={tx.id}
                    onClick={() => setSelectedTx(tx)}
                    className={cn(
                      "w-full p-4 flex items-center justify-between hover:bg-[var(--color-surface-hover)] transition-colors text-left",
                      selectedTx?.id === tx.id && "bg-[var(--color-surface-hover)] border-l-2 border-l-[var(--color-primary)]"
                    )}
                  >
                    <div className="flex items-center gap-4">
                      <div className={cn(
                        "w-10 h-10 rounded-full flex items-center justify-center",
                        tx.rawAmount < 0 ? "bg-[var(--color-surface-hover)] text-white" : "bg-[var(--color-positive)]/10 text-[var(--color-positive)]"
                      )}>
                        {tx.rawAmount < 0 ? <ArrowDownRight className="w-5 h-5" /> : <ArrowUpRight className="w-5 h-5" />}
                      </div>
                      <div>
                        <p className="text-sm font-medium text-white">{tx.description}</p>
                        <div className="flex items-center gap-2 mt-1">
                          <span className="text-xs text-[var(--color-text-muted)]">{tx.category || "Uncategorized"}</span>
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
                        tx.rawAmount < 0 ? "text-white" : "text-[var(--color-positive)]"
                      )}>
                        {tx.rawAmount > 0 ? "+" : ""}{tx.rawAmount < 0 ? "-" : ""}${Math.abs(tx.rawAmount).toFixed(2)}
                      </p>
                      {tx.reviewStatus === 'NeedsReview' && (
                        <span className="text-[10px] text-[var(--color-warning)] mt-1 block">Needs Review</span>
                      )}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          </div>
        ))}
        {Object.keys(groupedTransactions).length === 0 && (
          <div className="text-center py-12 bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)]">
            <p className="text-[var(--color-text-muted)]">No transactions found.</p>
          </div>
        )}
      </div>
    </PageLayout>
  );
}
