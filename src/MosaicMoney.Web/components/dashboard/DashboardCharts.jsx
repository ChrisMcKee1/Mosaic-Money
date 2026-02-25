"use client";

import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, AreaChart, Area } from 'recharts';

const CustomTooltip = ({ active, payload, label, prefix = "$" }) => {
  if (active && payload && payload.length) {
    return (
      <div className="bg-black/60 backdrop-blur-md border border-white/10 p-3 rounded-xl shadow-xl">
        <p className="text-white/60 text-xs font-medium mb-1 uppercase tracking-wider">{label}</p>
        <p className="text-white font-display font-bold text-lg">
          {prefix}{payload[0].value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </p>
      </div>
    );
  }
  return null;
};

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
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={nwData.length > 0 ? nwData : [{ name: 'No Data', netWorth: 0 }]} margin={{ top: 10, right: 0, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="colorNetWorth" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="var(--color-primary)" stopOpacity={0.4}/>
                  <stop offset="95%" stopColor="var(--color-primary)" stopOpacity={0}/>
                </linearGradient>
                <filter id="glow">
                  <feGaussianBlur stdDeviation="4" result="coloredBlur"/>
                  <feMerge>
                    <feMergeNode in="coloredBlur"/>
                    <feMergeNode in="SourceGraphic"/>
                  </feMerge>
                </filter>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" vertical={false} opacity={0.5} />
              <XAxis 
                dataKey="name" 
                stroke="var(--color-text-muted)" 
                fontSize={11} 
                fontWeight={500}
                tickLine={false}
                axisLine={false}
                dy={10}
              />
              <YAxis hide domain={['dataMin - 5000', 'dataMax + 5000']} />
              <Tooltip content={<CustomTooltip />} cursor={{ stroke: 'var(--color-border)', strokeWidth: 1, strokeDasharray: '4 4' }} />
              <Area 
                type="monotone" 
                dataKey="netWorth" 
                stroke="var(--color-primary)" 
                strokeWidth={3}
                fillOpacity={1} 
                fill="url(#colorNetWorth)" 
                activeDot={{ r: 6, fill: 'var(--color-surface)', stroke: 'var(--color-primary)', strokeWidth: 2, filter: 'url(#glow)' }}
              />
            </AreaChart>
          </ResponsiveContainer>
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
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={spendingData.length > 0 ? spendingData : [{ name: 'No Data', spending: 0 }]} margin={{ top: 10, right: 0, left: 0, bottom: 0 }}>
              <defs>
                <filter id="glowWarning">
                  <feGaussianBlur stdDeviation="4" result="coloredBlur"/>
                  <feMerge>
                    <feMergeNode in="coloredBlur"/>
                    <feMergeNode in="SourceGraphic"/>
                  </feMerge>
                </filter>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" vertical={false} opacity={0.5} />
              <XAxis 
                dataKey="name" 
                stroke="var(--color-text-muted)" 
                fontSize={11} 
                fontWeight={500}
                tickLine={false}
                axisLine={false}
                dy={10}
              />
              <YAxis hide domain={[0, 'dataMax + 1000']} />
              <Tooltip content={<CustomTooltip />} cursor={{ stroke: 'var(--color-border)', strokeWidth: 1, strokeDasharray: '4 4' }} />
              <Line 
                type="monotone" 
                dataKey="spending" 
                stroke="var(--color-warning)" 
                strokeWidth={3}
                dot={{ r: 4, fill: 'var(--color-surface)', stroke: 'var(--color-warning)', strokeWidth: 2 }}
                activeDot={{ r: 6, fill: 'var(--color-warning)', stroke: 'var(--color-surface)', strokeWidth: 2, filter: 'url(#glowWarning)' }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}
