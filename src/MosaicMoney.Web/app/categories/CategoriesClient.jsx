"use client";

import { useState } from "react";
import { PageLayout } from "../../components/layout/PageLayout";
import dynamic from 'next/dynamic';
import { getBaseChartOptions, getDonutOptions } from "../../components/charts/ChartConfig";
import { useChartTheme } from "../../components/charts/useChartTheme";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import { Tag, TrendingUp, AlertCircle } from "lucide-react";
import { CurrencyDisplay } from "../../components/ui/CurrencyDisplay";

const Chart = dynamic(() => import('react-apexcharts'), { ssr: false });

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

export function CategoriesClient({ transactions }) {
  const chartTheme = useChartTheme();
  const baseChartOptions = getBaseChartOptions(chartTheme);
  const donutOptions = getDonutOptions(chartTheme);

  // Mock budget data for now, since we don't have a budget API yet
  const mockBudgets = {
    "Groceries": 600,
    "Dining Out": 400,
    "Transportation": 200,
    "Entertainment": 150,
    "Utilities": 300,
    "Shopping": 250,
  };

  // Calculate spending per category
  const categorySpending = transactions
    .filter(tx => !tx.excludeFromBudget && tx.rawAmount < 0)
    .reduce((acc, tx) => {
      const cat = tx.category || "Uncategorized";
      acc[cat] = (acc[cat] || 0) + Math.abs(tx.rawAmount);
      return acc;
    }, {});

  const categories = Object.keys(categorySpending).map(name => ({
    name,
    spent: categorySpending[name],
    budget: mockBudgets[name] || 500, // Default budget if not found
    color: `hsl(var(--chart-${(Object.keys(categorySpending).indexOf(name) % 5) + 1}))`
  })).sort((a, b) => b.spent - a.spent);

  const [selectedCategory, setSelectedCategory] = useState(categories[0] || null);

  const totalSpent = categories.reduce((sum, cat) => sum + cat.spent, 0);
  const totalBudget = categories.reduce((sum, cat) => sum + cat.budget, 0);

  // Mock historical data for the selected category
  const historicalData = [
    { month: "Sep", spent: selectedCategory?.spent * 0.8 || 0 },
    { month: "Oct", spent: selectedCategory?.spent * 1.1 || 0 },
    { month: "Nov", spent: selectedCategory?.spent * 0.9 || 0 },
    { month: "Dec", spent: selectedCategory?.spent * 1.2 || 0 },
    { month: "Jan", spent: selectedCategory?.spent * 0.95 || 0 },
    { month: "Feb", spent: selectedCategory?.spent || 0 },
  ];

  const rightPanel = selectedCategory ? (
    <div className="space-y-8">
      <div>
        <div className="w-12 h-12 rounded-xl bg-[var(--color-surface-hover)] flex items-center justify-center mb-4">
          <Tag className="w-6 h-6 text-[var(--color-primary)]" />
        </div>
        <h2 className="text-xl font-display font-bold text-white">{selectedCategory.name}</h2>
        <div className="flex items-baseline gap-2 mt-2">
          <CurrencyDisplay amount={selectedCategory.spent} className="text-3xl font-mono font-medium" />
          <p className="text-sm text-[var(--color-text-muted)]">/ ${selectedCategory.budget.toFixed(0)}</p>
        </div>
        
        <div className="mt-4">
          <div className="flex justify-between text-xs mb-1">
            <span className="text-[var(--color-text-muted)]">Budget Used</span>
            <span className={cn(
              "font-medium",
              selectedCategory.spent > selectedCategory.budget ? "text-[var(--color-negative)]" : "text-white"
            )}>
              {Math.round((selectedCategory.spent / selectedCategory.budget) * 100)}%
            </span>
          </div>
          <div className="h-2 w-full bg-[var(--color-surface-hover)] rounded-full overflow-hidden">
            <div 
              className={cn(
                "h-full rounded-full transition-all",
                selectedCategory.spent > selectedCategory.budget ? "bg-[var(--color-negative)]" : "bg-[var(--color-primary)]"
              )}
              style={{ width: `${Math.min((selectedCategory.spent / selectedCategory.budget) * 100, 100)}%` }}
            />
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4 flex items-center gap-2">
          <TrendingUp className="w-4 h-4" />
          6-Month History
        </h3>
        <div className="h-48 w-full">
          <Chart 
            options={{
              ...baseChartOptions,
              chart: { ...baseChartOptions.chart, type: 'bar' },
              colors: ['var(--color-primary)'],
              plotOptions: {
                bar: {
                  borderRadius: 4,
                  columnWidth: '60%',
                }
              },
              xaxis: {
                ...baseChartOptions.xaxis,
                categories: historicalData.map(d => d.month)
              }
            }}
            series={[{ name: 'Spent', data: historicalData.map(d => d.spent) }]}
            type="bar"
            width="100%"
            height="100%"
          />
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4">
          Recent Transactions
        </h3>
        <div className="space-y-3">
          {transactions
            .filter(tx => tx.category === selectedCategory.name && tx.rawAmount < 0)
            .slice(0, 5)
            .map(tx => (
              <div key={tx.id} className="flex items-center justify-between p-3 rounded-lg bg-[var(--color-surface-hover)] border border-[var(--color-border)]">
                <div>
                  <p className="text-sm font-medium text-white truncate max-w-[150px]">{tx.description}</p>
                  <p className="text-xs text-[var(--color-text-muted)]">{tx.rawTransactionDate}</p>
                </div>
                <CurrencyDisplay amount={tx.rawAmount} isTransfer={tx.excludeFromBudget} className="text-sm font-mono font-medium" />
              </div>
            ))}
        </div>
      </div>
    </div>
  ) : (
    <div className="flex flex-col items-center justify-center h-full text-center space-y-4">
      <div className="w-16 h-16 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center">
        <Tag className="w-8 h-8 text-[var(--color-text-muted)]" />
      </div>
      <div>
        <p className="text-white font-medium">No category selected</p>
        <p className="text-sm text-[var(--color-text-muted)] mt-1">Select a category to view details</p>
      </div>
    </div>
  );

  return (
    <PageLayout 
      title="Categories & Budgeting" 
      subtitle="Track your spending across different categories."
      rightPanel={rightPanel}
    >
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 mb-8">
        {/* Donut Chart */}
        <div className="lg:col-span-1 bg-[var(--color-surface)] p-6 rounded-xl border border-[var(--color-border)] flex flex-col items-center justify-center">
          <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4 w-full text-left">
            Total Spent vs Budget
          </h3>
          <div className="relative w-48 h-48">
            <Chart 
              options={{
                ...donutOptions,
                labels: categories.map(d => d.name),
                colors: categories.map(d => d.color),
              }}
              series={categories.map(d => d.spent)}
              type="donut"
              width="100%"
              height="100%"
            />
            <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
              <CurrencyDisplay amount={totalSpent} className="text-2xl font-display font-bold" />
              <span className="text-xs text-[var(--color-text-muted)]">of ${totalBudget.toFixed(0)}</span>
            </div>
          </div>
        </div>

        {/* Category List */}
        <div className="lg:col-span-2 space-y-4">
          {categories.map((cat) => {
            const percentUsed = Math.min((cat.spent / cat.budget) * 100, 100);
            const isOverBudget = cat.spent > cat.budget;
            
            return (
              <button
                key={cat.name}
                onClick={() => setSelectedCategory(cat)}
                className={cn(
                  "w-full text-left bg-[var(--color-surface)] p-4 rounded-xl border transition-all",
                  selectedCategory?.name === cat.name 
                    ? "border-[var(--color-primary)] bg-[var(--color-surface-hover)]" 
                    : "border-[var(--color-border)] hover:border-[var(--color-text-muted)]"
                )}
              >
                <div className="flex justify-between items-end mb-2">
                  <div className="flex items-center gap-3">
                    <div 
                      className="w-3 h-3 rounded-full" 
                      style={{ backgroundColor: cat.color }}
                    />
                    <span className="font-medium text-white">{cat.name}</span>
                    {isOverBudget && (
                      <AlertCircle className="w-4 h-4 text-[var(--color-negative)]" />
                    )}
                  </div>
                  <div className="text-right flex items-baseline gap-1">
                    <CurrencyDisplay amount={cat.spent} className="font-mono font-medium" />
                    <span className="text-sm text-[var(--color-text-muted)]">/ ${cat.budget.toFixed(0)}</span>
                  </div>
                </div>
                <div className="h-2 w-full bg-[var(--color-surface-hover)] rounded-full overflow-hidden">
                  <div 
                    className={cn(
                      "h-full rounded-full transition-all",
                      isOverBudget ? "bg-[var(--color-negative)]" : "bg-[var(--color-primary)]"
                    )}
                    style={{ 
                      width: `${percentUsed}%`,
                      backgroundColor: !isOverBudget ? cat.color : undefined
                    }}
                  />
                </div>
              </button>
            );
          })}
        </div>
      </div>
    </PageLayout>
  );
}
