"use client";

import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, AreaChart, Area } from 'recharts';

export function DashboardCharts({ netWorthHistory = [], transactions = [] }) {
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
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
      {/* Net Worth Chart */}
      <div className="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)]">
        <div className="mb-4">
          <h3 className="text-sm font-medium text-[var(--color-text-muted)]">Net Worth Trend</h3>
          <p className="text-2xl font-display font-bold text-white mt-1">
            ${currentNetWorth.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </p>
        </div>
        <div className="h-64 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={nwData.length > 0 ? nwData : [{ name: 'No Data', netWorth: 0 }]} margin={{ top: 5, right: 0, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="colorNetWorth" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="var(--color-primary)" stopOpacity={0.3}/>
                  <stop offset="95%" stopColor="var(--color-primary)" stopOpacity={0}/>
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" vertical={false} />
              <XAxis 
                dataKey="name" 
                stroke="var(--color-text-muted)" 
                fontSize={12} 
                tickLine={false}
                axisLine={false}
                dy={10}
              />
              <YAxis 
                hide 
                domain={['dataMin - 5000', 'dataMax + 5000']} 
              />
              <Tooltip 
                contentStyle={{ 
                  backgroundColor: 'var(--color-surface-hover)', 
                  borderColor: 'var(--color-border)',
                  borderRadius: '8px',
                  color: 'white'
                }}
                itemStyle={{ color: 'var(--color-primary)' }}
                formatter={(value) => [`$${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`, 'Net Worth']}
              />
              <Area 
                type="monotone" 
                dataKey="netWorth" 
                stroke="var(--color-primary)" 
                strokeWidth={3}
                fillOpacity={1} 
                fill="url(#colorNetWorth)" 
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Spending Chart */}
      <div className="bg-[var(--color-surface)] p-5 rounded-xl border border-[var(--color-border)]">
        <div className="mb-4">
          <h3 className="text-sm font-medium text-[var(--color-text-muted)]">Monthly Spending</h3>
          <p className="text-2xl font-display font-bold text-white mt-1">
            ${currentSpending.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </p>
        </div>
        <div className="h-64 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={spendingData.length > 0 ? spendingData : [{ name: 'No Data', spending: 0 }]} margin={{ top: 5, right: 0, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" vertical={false} />
              <XAxis 
                dataKey="name" 
                stroke="var(--color-text-muted)" 
                fontSize={12} 
                tickLine={false}
                axisLine={false}
                dy={10}
              />
              <YAxis 
                hide 
                domain={[0, 'dataMax + 1000']} 
              />
              <Tooltip 
                contentStyle={{ 
                  backgroundColor: 'var(--color-surface-hover)', 
                  borderColor: 'var(--color-border)',
                  borderRadius: '8px',
                  color: 'white'
                }}
                itemStyle={{ color: 'var(--color-warning)' }}
                formatter={(value) => [`$${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`, 'Spending']}
              />
              <Line 
                type="monotone" 
                dataKey="spending" 
                stroke="var(--color-warning)" 
                strokeWidth={3}
                dot={{ r: 4, fill: 'var(--color-surface)', strokeWidth: 2 }}
                activeDot={{ r: 6, fill: 'var(--color-warning)' }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}
