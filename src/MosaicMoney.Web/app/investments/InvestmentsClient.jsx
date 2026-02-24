"use client";

import { useState } from "react";
import { PageLayout } from "../../components/layout/PageLayout";
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from "recharts";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import { TrendingUp, TrendingDown, Activity, Briefcase, Bitcoin, Shield } from "lucide-react";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

export function InvestmentsClient({ accounts }) {
  const [selectedAccount, setSelectedAccount] = useState(accounts[0] || null);

  const totalBalance = accounts.reduce((sum, acc) => sum + acc.balance, 0);
  const totalChange1W = accounts.reduce((sum, acc) => sum + acc.change1W, 0);
  const totalChange1WPercent = (totalChange1W / (totalBalance - totalChange1W)) * 100;

  // Mock historical data for the total balance chart
  const historicalData = [
    { date: "Mon", balance: totalBalance - totalChange1W * 0.8 },
    { date: "Tue", balance: totalBalance - totalChange1W * 0.6 },
    { date: "Wed", balance: totalBalance - totalChange1W * 0.4 },
    { date: "Thu", balance: totalBalance - totalChange1W * 0.2 },
    { date: "Fri", balance: totalBalance - totalChange1W * 0.1 },
    { date: "Sat", balance: totalBalance - totalChange1W * 0.05 },
    { date: "Sun", balance: totalBalance },
  ];

  // Mock historical data for the selected account
  const accountHistoricalData = [
    { date: "Mon", balance: selectedAccount?.balance - selectedAccount?.change1W * 0.8 || 0 },
    { date: "Tue", balance: selectedAccount?.balance - selectedAccount?.change1W * 0.6 || 0 },
    { date: "Wed", balance: selectedAccount?.balance - selectedAccount?.change1W * 0.4 || 0 },
    { date: "Thu", balance: selectedAccount?.balance - selectedAccount?.change1W * 0.2 || 0 },
    { date: "Fri", balance: selectedAccount?.balance - selectedAccount?.change1W * 0.1 || 0 },
    { date: "Sat", balance: selectedAccount?.balance - selectedAccount?.change1W * 0.05 || 0 },
    { date: "Sun", balance: selectedAccount?.balance || 0 },
  ];

  // Mock positions for the selected account
  const mockPositions = {
    "1": [
      { symbol: "AAPL", name: "Apple Inc.", shares: 50, price: 175.50, value: 8775.00, change: 1.2 },
      { symbol: "MSFT", name: "Microsoft Corp.", shares: 30, price: 330.20, value: 9906.00, change: -0.5 },
      { symbol: "VOO", name: "Vanguard S&P 500", shares: 60, price: 442.49, value: 26549.50, change: 0.8 },
    ],
    "2": [
      { symbol: "VTTSX", name: "Vanguard Target 2060", shares: 2500, price: 51.38, value: 128450.00, change: -0.35 },
    ],
    "3": [
      { symbol: "BTC", name: "Bitcoin", shares: 0.15, price: 65000.00, value: 9750.00, change: 5.2 },
      { symbol: "ETH", name: "Ethereum", shares: 0.8, price: 3376.00, value: 2700.80, change: 2.1 },
    ],
    "4": [
      { symbol: "DOGE", name: "Dogecoin", shares: 15000, price: 0.15, value: 2250.00, change: -8.5 },
      { symbol: "HOOD", name: "Robinhood Markets", shares: 50, price: 19.00, value: 950.00, change: 1.5 },
    ]
  };

  const positions = selectedAccount ? mockPositions[selectedAccount.id] : [];

  const getIconForType = (type) => {
    switch (type) {
      case "Crypto": return <Bitcoin className="w-5 h-5" />;
      case "Retirement": return <Shield className="w-5 h-5" />;
      default: return <Briefcase className="w-5 h-5" />;
    }
  };

  const rightPanel = selectedAccount ? (
    <div className="space-y-8">
      <div>
        <div className="w-12 h-12 rounded-xl bg-[var(--color-surface-hover)] flex items-center justify-center mb-4 text-[var(--color-primary)]">
          {getIconForType(selectedAccount.type)}
        </div>
        <h2 className="text-xl font-display font-bold text-white">{selectedAccount.name}</h2>
        <p className="text-sm text-[var(--color-text-muted)]">{selectedAccount.type}</p>
        
        <div className="mt-4">
          <p className="text-3xl font-mono font-medium text-white">${selectedAccount.balance.toFixed(2)}</p>
          <div className={cn(
            "flex items-center gap-1 text-sm font-medium mt-1",
            selectedAccount.change1W >= 0 ? "text-[var(--color-positive)]" : "text-[var(--color-negative)]"
          )}>
            {selectedAccount.change1W >= 0 ? <TrendingUp className="w-4 h-4" /> : <TrendingDown className="w-4 h-4" />}
            {selectedAccount.change1W >= 0 ? "+" : ""}${Math.abs(selectedAccount.change1W).toFixed(2)} ({selectedAccount.change1WPercent > 0 ? "+" : ""}{selectedAccount.change1WPercent.toFixed(2)}%)
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4 flex items-center gap-2">
          <Activity className="w-4 h-4" />
          1W Performance
        </h3>
        <div className="h-40 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={accountHistoricalData} margin={{ top: 5, right: 0, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="colorAccountBalance" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor={selectedAccount.change1W >= 0 ? "var(--color-positive)" : "var(--color-negative)"} stopOpacity={0.3}/>
                  <stop offset="95%" stopColor={selectedAccount.change1W >= 0 ? "var(--color-positive)" : "var(--color-negative)"} stopOpacity={0}/>
                </linearGradient>
              </defs>
              <Tooltip 
                contentStyle={{ backgroundColor: 'var(--color-surface)', borderColor: 'var(--color-border)', borderRadius: '8px' }}
                itemStyle={{ color: 'white' }}
                formatter={(value) => `$${value.toFixed(2)}`}
                labelStyle={{ color: 'var(--color-text-muted)' }}
              />
              <Area 
                type="monotone" 
                dataKey="balance" 
                stroke={selectedAccount.change1W >= 0 ? "var(--color-positive)" : "var(--color-negative)"} 
                fillOpacity={1} 
                fill="url(#colorAccountBalance)" 
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4">
          Positions
        </h3>
        <div className="space-y-3">
          {positions.map((pos) => (
            <div key={pos.symbol} className="flex items-center justify-between p-3 rounded-lg bg-[var(--color-surface-hover)] border border-[var(--color-border)]">
              <div>
                <p className="text-sm font-medium text-white">{pos.symbol}</p>
                <p className="text-xs text-[var(--color-text-muted)]">{pos.shares} shares</p>
              </div>
              <div className="text-right">
                <p className="text-sm font-mono font-medium text-white">${pos.value.toFixed(2)}</p>
                <p className={cn(
                  "text-xs font-medium",
                  pos.change >= 0 ? "text-[var(--color-positive)]" : "text-[var(--color-negative)]"
                )}>
                  {pos.change >= 0 ? "+" : ""}{pos.change.toFixed(2)}%
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  ) : (
    <div className="flex flex-col items-center justify-center h-full text-center space-y-4">
      <div className="w-16 h-16 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center">
        <Briefcase className="w-8 h-8 text-[var(--color-text-muted)]" />
      </div>
      <div>
        <p className="text-white font-medium">No account selected</p>
        <p className="text-sm text-[var(--color-text-muted)] mt-1">Select an account to view details</p>
      </div>
    </div>
  );

  return (
    <PageLayout 
      title="Investments" 
      subtitle="Track your portfolio performance and asset allocation."
      rightPanel={rightPanel}
    >
      {/* Total Balance Chart */}
      <div className="bg-[var(--color-surface)] p-6 rounded-xl border border-[var(--color-border)] mb-8">
        <div className="mb-6">
          <p className="text-sm font-medium text-[var(--color-text-muted)] mb-1">Total Portfolio Value</p>
          <div className="flex items-end gap-4">
            <p className="text-4xl font-display font-bold text-white">${totalBalance.toFixed(2)}</p>
            <div className={cn(
              "flex items-center gap-1 text-sm font-medium mb-1 px-2 py-1 rounded-md",
              totalChange1W >= 0 ? "bg-[var(--color-positive)]/10 text-[var(--color-positive)]" : "bg-[var(--color-negative)]/10 text-[var(--color-negative)]"
            )}>
              {totalChange1W >= 0 ? <TrendingUp className="w-4 h-4" /> : <TrendingDown className="w-4 h-4" />}
              {totalChange1W >= 0 ? "+" : ""}${Math.abs(totalChange1W).toFixed(2)} ({totalChange1WPercent > 0 ? "+" : ""}{totalChange1WPercent.toFixed(2)}%) 1W
            </div>
          </div>
        </div>
        
        <div className="h-64 w-full">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={historicalData} margin={{ top: 10, right: 0, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="colorTotalBalance" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="var(--color-primary)" stopOpacity={0.3}/>
                  <stop offset="95%" stopColor="var(--color-primary)" stopOpacity={0}/>
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" vertical={false} />
              <XAxis 
                dataKey="date" 
                stroke="var(--color-text-muted)" 
                fontSize={12} 
                tickLine={false}
                axisLine={false}
              />
              <YAxis 
                stroke="var(--color-text-muted)" 
                fontSize={12}
                tickLine={false}
                axisLine={false}
                tickFormatter={(value) => `$${(value / 1000).toFixed(0)}k`}
                domain={['dataMin - 1000', 'dataMax + 1000']}
              />
              <Tooltip 
                contentStyle={{ backgroundColor: 'var(--color-surface)', borderColor: 'var(--color-border)', borderRadius: '8px' }}
                itemStyle={{ color: 'white' }}
                formatter={(value) => `$${value.toFixed(2)}`}
              />
              <Area 
                type="monotone" 
                dataKey="balance" 
                stroke="var(--color-primary)" 
                strokeWidth={2}
                fillOpacity={1} 
                fill="url(#colorTotalBalance)" 
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Top Movers Widget */}
      <div className="mb-8">
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4">Top Movers</h3>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {[
            { symbol: "BTC", name: "Bitcoin", change: 5.2, price: 65000.00 },
            { symbol: "ETH", name: "Ethereum", change: 2.1, price: 3376.00 },
            { symbol: "AAPL", name: "Apple Inc.", change: 1.2, price: 175.50 },
            { symbol: "DOGE", name: "Dogecoin", change: -8.5, price: 0.15 },
          ].map((asset) => (
            <div key={asset.symbol} className="bg-[var(--color-surface)] p-4 rounded-xl border border-[var(--color-border)]">
              <div className="flex justify-between items-start mb-2">
                <div>
                  <p className="text-sm font-bold text-white">{asset.symbol}</p>
                  <p className="text-xs text-[var(--color-text-muted)] truncate max-w-[80px]">{asset.name}</p>
                </div>
                <div className={cn(
                  "flex items-center gap-1 text-xs font-medium px-1.5 py-0.5 rounded",
                  asset.change >= 0 ? "bg-[var(--color-positive)]/10 text-[var(--color-positive)]" : "bg-[var(--color-negative)]/10 text-[var(--color-negative)]"
                )}>
                  {asset.change >= 0 ? <TrendingUp className="w-3 h-3" /> : <TrendingDown className="w-3 h-3" />}
                  {Math.abs(asset.change).toFixed(1)}%
                </div>
              </div>
              <p className="text-lg font-mono font-medium text-white">${asset.price.toFixed(2)}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Accounts List */}
      <div>
        <h3 className="text-sm font-medium text-[var(--color-text-muted)] uppercase tracking-wider mb-4">Accounts</h3>
        <div className="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] overflow-hidden">
          <div className="divide-y divide-[var(--color-border)]">
            {accounts.map((acc) => (
              <button
                key={acc.id}
                onClick={() => setSelectedAccount(acc)}
                className={cn(
                  "w-full p-4 flex items-center justify-between hover:bg-[var(--color-surface-hover)] transition-colors text-left",
                  selectedAccount?.id === acc.id && "bg-[var(--color-surface-hover)] border-l-2 border-l-[var(--color-primary)]"
                )}
              >
                <div className="flex items-center gap-4">
                  <div className="w-10 h-10 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center text-[var(--color-text-muted)]">
                    {getIconForType(acc.type)}
                  </div>
                  <div>
                    <p className="text-sm font-medium text-white">{acc.name}</p>
                    <p className="text-xs text-[var(--color-text-muted)] mt-0.5">{acc.type}</p>
                  </div>
                </div>
                <div className="text-right">
                  <p className="text-sm font-mono font-medium text-white">${acc.balance.toFixed(2)}</p>
                  <p className={cn(
                    "text-xs font-medium mt-0.5",
                    acc.change1W >= 0 ? "text-[var(--color-positive)]" : "text-[var(--color-negative)]"
                  )}>
                    {acc.change1W >= 0 ? "+" : ""}${Math.abs(acc.change1W).toFixed(2)}
                  </p>
                </div>
              </button>
            ))}
          </div>
        </div>
      </div>
    </PageLayout>
  );
}
