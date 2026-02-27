"use client";

import dynamic from 'next/dynamic';
import { getBaseChartOptions } from '../charts/ChartConfig';
import { useChartTheme } from '../charts/useChartTheme';

const Chart = dynamic(() => import('react-apexcharts'), { ssr: false });

export function DashboardCharts({ netWorthHistory = [], transactions = [] }) {
  const chartTheme = useChartTheme();
  const baseChartOptions = getBaseChartOptions(chartTheme);

  // Process net worth history
  const nwData = netWorthHistory.map(point => {
    const date = new Date(point.date);
    return {
      name: date.toLocaleDateString('en-US', { month: 'short' }),
      netWorth: point.netWorth,
      fullDate: date
    };
  }).sort((a, b) => a.fullDate - b.fullDate);

  const currentNetWorth = nwData.length > 0 ? nwData[nwData.length - 1].netWorth : 0;

  // Process spending history (group transactions by month)
  const spendingByMonth = {};
  transactions.forEach(tx => {
    if (tx.rawAmount < 0 && !tx.excludeFromBudget) {
      const date = new Date(tx.rawTransactionDate);
      const monthKey = date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
      const shortMonth = date.toLocaleDateString('en-US', { month: 'short' });
      
      if (!spendingByMonth[monthKey]) {
        spendingByMonth[monthKey] = { name: shortMonth, spending: 0, fullDate: new Date(date.getFullYear(), date.getMonth(), 1) };
      }
      spendingByMonth[monthKey].spending += Math.abs(tx.rawAmount);
    }
  });

  const spendingData = Object.values(spendingByMonth)
    .sort((a, b) => a.fullDate - b.fullDate)
    .slice(-6); // Last 6 months

  const currentSpending = spendingData.length > 0 ? spendingData[spendingData.length - 1].spending : 0;

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      {/* Net Worth Chart */}
      <div className="group bg-[var(--color-surface)] p-6 rounded-2xl border border-[var(--color-border)] transition-all hover:border-[var(--color-primary)]/30 hover:shadow-[0_8px_30px_-12px_rgba(var(--color-primary-rgb),0.1)] relative overflow-hidden">
        <div className="absolute top-0 left-0 w-full h-full bg-gradient-to-b from-[var(--color-primary)]/5 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none" />
        <div className="mb-6 relative z-10">
          <h3 className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest">Net Worth Trend</h3>
          <div className="flex items-baseline gap-2 mt-1">
            <p className="text-3xl font-display font-bold text-white tracking-tight">
              ${currentNetWorth.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </p>
          </div>
        </div>
        <div className="h-64 w-full relative z-10">
          <Chart 
            options={{
              ...baseChartOptions,
              chart: { ...baseChartOptions.chart, type: 'area' },
              colors: ['var(--color-primary)'],
              fill: {
                type: 'gradient',
                gradient: {
                  shadeIntensity: 1,
                  opacityFrom: 0.4,
                  opacityTo: 0,
                  stops: [0, 100]
                }
              },
              xaxis: {
                ...baseChartOptions.xaxis,
                categories: nwData.length > 0 ? nwData.map(d => d.name) : ['No Data']
              }
            }}
            series={[{ name: 'Net Worth', data: nwData.length > 0 ? nwData.map(d => d.netWorth) : [0] }]}
            type="area"
            width="100%"
            height="100%"
          />
        </div>
      </div>

      {/* Spending Chart */}
      <div className="group bg-[var(--color-surface)] p-6 rounded-2xl border border-[var(--color-border)] transition-all hover:border-[var(--color-warning)]/30 hover:shadow-[0_8px_30px_-12px_rgba(var(--color-warning-rgb),0.1)] relative overflow-hidden">
        <div className="absolute top-0 left-0 w-full h-full bg-gradient-to-b from-[var(--color-warning)]/5 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none" />
        <div className="mb-6 relative z-10">
          <h3 className="text-xs font-semibold text-[var(--color-text-muted)] uppercase tracking-widest">Monthly Spending</h3>
          <div className="flex items-baseline gap-2 mt-1">
            <p className="text-3xl font-display font-bold text-white tracking-tight">
              ${currentSpending.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </p>
          </div>
        </div>
        <div className="h-64 w-full relative z-10">
          <Chart 
            options={{
              ...baseChartOptions,
              chart: { ...baseChartOptions.chart, type: 'line' },
              colors: ['var(--color-warning)'],
              xaxis: {
                ...baseChartOptions.xaxis,
                categories: spendingData.length > 0 ? spendingData.map(d => d.name) : ['No Data']
              }
            }}
            series={[{ name: 'Spending', data: spendingData.length > 0 ? spendingData.map(d => d.spending) : [0] }]}
            type="line"
            width="100%"
            height="100%"
          />
        </div>
      </div>
    </div>
  );
}
