"use client";

import { useState } from "react";
import { PageLayout } from "../../components/layout/PageLayout";
import dynamic from 'next/dynamic';
import { getDonutOptions } from "../../components/charts/ChartConfig";
import { useChartTheme } from "../../components/charts/useChartTheme";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import { Calendar, CheckCircle2, Clock, AlertCircle, RefreshCw, FileText } from "lucide-react";
import { CurrencyDisplay } from "../../components/ui/CurrencyDisplay";

const Chart = dynamic(() => import('react-apexcharts'), { ssr: false });

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

export function RecurringsClient({ items }) {
  const chartTheme = useChartTheme();
  const donutOptions = getDonutOptions(chartTheme);

  // Mock status for items since API doesn't provide it yet
  const itemsWithStatus = items.map((item, i) => ({
    ...item,
    status: i % 3 === 0 ? "paid" : i % 5 === 0 ? "overdue" : "upcoming",
    dueDate: new Date(Date.now() + (i * 2 - 5) * 24 * 60 * 60 * 1000).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
  })).sort((a, b) => {
    const statusOrder = { overdue: 0, upcoming: 1, paid: 2 };
    return statusOrder[a.status] - statusOrder[b.status];
  });

  const [selectedItem, setSelectedItem] = useState(itemsWithStatus[0] || null);

  const totalExpected = itemsWithStatus.reduce((sum, item) => sum + item.expectedAmount, 0);
  const totalPaid = itemsWithStatus.filter(i => i.status === "paid").reduce((sum, item) => sum + item.expectedAmount, 0);
  const totalLeft = totalExpected - totalPaid;

  const donutData = [
    { name: "Paid", value: totalPaid, color: "var(--color-positive)" },
    { name: "Left to Pay", value: totalLeft, color: "var(--color-surface-hover)" },
  ];

  const getStatusIcon = (status) => {
    switch (status) {
      case "paid": return <CheckCircle2 className="w-4 h-4 text-[var(--color-positive)]" />;
      case "overdue": return <AlertCircle className="w-4 h-4 text-[var(--color-negative)]" />;
      case "upcoming": return <Clock className="w-4 h-4 text-[var(--color-warning)]" />;
      default: return null;
    }
  };

  const getStatusColor = (status) => {
    switch (status) {
      case "paid": return "bg-[var(--color-positive)]/10 text-[var(--color-positive)] border-[var(--color-positive)]/20";
      case "overdue": return "bg-[var(--color-negative)]/10 text-[var(--color-negative)] border-[var(--color-negative)]/20";
      case "upcoming": return "bg-[var(--color-warning)]/10 text-[var(--color-warning)] border-[var(--color-warning)]/20";
      default: return "bg-[var(--color-surface-hover)] text-[var(--color-text-muted)] border-[var(--color-border)]";
    }
  };

  const rightPanel = selectedItem ? (
    <div className="space-y-8">
      <div>
        <div className="w-12 h-12 rounded-xl bg-[var(--color-surface-hover)] flex items-center justify-center mb-4">
          <RefreshCw className="w-6 h-6 text-[var(--color-primary)]" />
        </div>
        <h2 className="text-xl font-display font-bold text-white">{selectedItem.merchantName || "Unknown Merchant"}</h2>
        <CurrencyDisplay amount={selectedItem.expectedAmount} className="text-3xl font-mono font-medium mt-2 block" />
        
        <div className="flex items-center gap-2 mt-4">
          <span className={cn(
            "inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium border",
            getStatusColor(selectedItem.status)
          )}>
            {getStatusIcon(selectedItem.status)}
            <span className="capitalize">{selectedItem.status}</span>
          </span>
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium bg-[var(--color-surface-hover)] text-white border border-[var(--color-border)]">
            <Calendar className="w-3.5 h-3.5 text-[var(--color-text-muted)]" />
            Due {selectedItem.dueDate}
          </span>
        </div>
      </div>

      <div className="space-y-4 pt-4 border-t border-[var(--color-border)]">
        <h3 className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-2">Rule Details</h3>
        
        <div className="grid grid-cols-2 gap-4">
          <div className="bg-[var(--color-surface-hover)] p-3 rounded-lg border border-[var(--color-border)]">
            <p className="text-xs text-[var(--color-text-muted)] mb-1">Frequency</p>
            <p className="text-sm font-medium text-white capitalize">{selectedItem.frequency || "Monthly"}</p>
          </div>
          <div className="bg-[var(--color-surface-hover)] p-3 rounded-lg border border-[var(--color-border)]">
            <p className="text-xs text-[var(--color-text-muted)] mb-1">Category</p>
            <p className="text-sm font-medium text-white">{selectedItem.category || "Uncategorized"}</p>
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-xs font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4">
          Recent History
        </h3>
        <div className="space-y-3">
          {[1, 2, 3].map((i) => {
            const pastDate = new Date(Date.now() - i * 30 * 24 * 60 * 60 * 1000).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
            return (
              <div key={i} className="flex items-center justify-between p-3 rounded-lg bg-[var(--color-surface-hover)] border border-[var(--color-border)]">
                <div className="flex items-center gap-3">
                  <CheckCircle2 className="w-4 h-4 text-[var(--color-positive)]" />
                  <div>
                    <p className="text-sm font-medium text-white">Paid</p>
                    <p className="text-xs text-[var(--color-text-muted)]">{pastDate}</p>
                  </div>
                </div>
                <CurrencyDisplay amount={selectedItem.expectedAmount} className="text-sm font-mono font-medium" />
              </div>
            );
          })}
        </div>
      </div>
    </div>
  ) : (
    <div className="flex flex-col items-center justify-center h-full text-center space-y-4">
      <div className="w-16 h-16 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center">
        <FileText className="w-8 h-8 text-[var(--color-text-muted)]" />
      </div>
      <div>
        <p className="text-white font-medium">No recurring item selected</p>
        <p className="text-sm text-[var(--color-text-muted)] mt-1">Select an item to view details</p>
      </div>
    </div>
  );

  return (
    <PageLayout 
      title="Recurring Bills & Subscriptions" 
      subtitle="Manage your upcoming payments and subscriptions."
      rightPanel={rightPanel}
    >
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 mb-8">
        {/* Donut Chart */}
        <div className="lg:col-span-1 bg-[var(--color-surface)] p-6 rounded-xl border border-[var(--color-border)] flex flex-col items-center justify-center">
          <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4 w-full text-left">
            This Month
          </h3>
          <div className="relative w-48 h-48">
            <Chart 
              options={{
                ...donutOptions,
                labels: donutData.map(d => d.name),
                colors: ['var(--color-positive)', 'var(--color-surface-hover)'],
              }}
              series={donutData.map(d => d.value)}
              type="donut"
              width="100%"
              height="100%"
            />
            <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
              <CurrencyDisplay amount={totalLeft} className="text-2xl font-display font-bold" />
              <span className="text-xs text-[var(--color-text-muted)]">Left to Pay</span>
            </div>
          </div>
          <div className="w-full mt-6 space-y-2">
            <div className="flex justify-between text-sm">
              <span className="text-[var(--color-text-muted)] flex items-center gap-2">
                <div className="w-2 h-2 rounded-full bg-[var(--color-positive)]" /> Paid
              </span>
              <CurrencyDisplay amount={totalPaid} className="font-mono" />
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-[var(--color-text-muted)] flex items-center gap-2">
                <div className="w-2 h-2 rounded-full bg-[var(--color-surface-hover)]" /> Left
              </span>
              <CurrencyDisplay amount={totalLeft} className="font-mono" />
            </div>
          </div>
        </div>

        {/* Recurring List */}
        <div className="lg:col-span-2 space-y-4">
          <div className="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] overflow-hidden">
            <div className="divide-y divide-[var(--color-border)]">
              {itemsWithStatus.map((item) => (
                <button
                  key={item.id}
                  onClick={() => setSelectedItem(item)}
                  className={cn(
                    "w-full p-4 flex items-center justify-between hover:bg-[var(--color-surface-hover)] transition-colors text-left",
                    selectedItem?.id === item.id && "bg-[var(--color-surface-hover)] border-l-2 border-l-[var(--color-primary)]"
                  )}
                >
                  <div className="flex items-center gap-4">
                    <div className="w-10 h-10 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center text-white font-bold">
                      {item.merchantName?.charAt(0) || "R"}
                    </div>
                    <div>
                      <p className="text-sm font-medium text-white">{item.merchantName || "Unknown Merchant"}</p>
                      <div className="flex items-center gap-2 mt-1">
                        <span className={cn(
                          "text-[10px] px-1.5 py-0.5 rounded border uppercase tracking-wider font-semibold",
                          getStatusColor(item.status)
                        )}>
                          {item.status}
                        </span>
                        <span className="text-xs text-[var(--color-text-muted)] flex items-center gap-1">
                          <Calendar className="w-3 h-3" /> {item.dueDate}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="text-right">
                    <CurrencyDisplay amount={item.expectedAmount} className="text-sm font-mono font-medium" />
                    <p className="text-xs text-[var(--color-text-muted)] mt-1 capitalize">{item.frequency || "Monthly"}</p>
                  </div>
                </button>
              ))}
              {itemsWithStatus.length === 0 && (
                <div className="text-center py-12">
                  <p className="text-[var(--color-text-muted)]">No recurring items found.</p>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </PageLayout>
  );
}
