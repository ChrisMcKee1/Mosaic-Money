"use client";

import { Wallet, CreditCard, Building, Landmark, TrendingUp, MoreHorizontal } from "lucide-react";
import { LineChart, Line, ResponsiveContainer } from 'recharts';

const generateSparklineData = (base, isPositive) => {
  return Array.from({ length: 10 }).map((_, i) => ({
    value: base + (Math.random() * 100 - 50) * (isPositive ? 1 : -1)
  }));
};

export function AccountsList({ accounts }) {
  const groupedAccounts = accounts.reduce((acc, account) => {
    if (!acc[account.type]) acc[account.type] = [];
    acc[account.type].push(account);
    return acc;
  }, {});

  const getIcon = (type) => {
    switch (type) {
      case "Depository": return <Wallet className="w-5 h-5 text-[var(--color-primary)]" />;
      case "Credit Card": return <CreditCard className="w-5 h-5 text-[var(--color-warning)]" />;
      case "Investment": return <TrendingUp className="w-5 h-5 text-[var(--color-positive)]" />;
      case "Loan": return <Landmark className="w-5 h-5 text-[var(--color-negative)]" />;
      case "Real Estate": return <Building className="w-5 h-5 text-purple-400" />;
      default: return <Wallet className="w-5 h-5 text-[var(--color-text-muted)]" />;
    }
  };

  return (
    <div className="space-y-8">
      {Object.entries(groupedAccounts).map(([type, typeAccounts]) => {
        const total = typeAccounts.reduce((sum, a) => sum + a.balance, 0);
        const isPositive = total >= 0;

        return (
          <div key={type}>
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-display font-semibold text-white flex items-center gap-2">
                {getIcon(type)}
                {type}
              </h2>
              <span className={`font-mono font-medium ${isPositive ? 'text-[var(--color-positive)]' : 'text-[var(--color-negative)]'}`}>
                {isPositive ? '' : '-'}${Math.abs(total).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </span>
            </div>

            <div className="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] overflow-hidden">
              <div className="divide-y divide-[var(--color-border)]">
                {typeAccounts.map((account) => {
                  const isAccPositive = account.balance >= 0;
                  const sparklineData = generateSparklineData(Math.abs(account.balance), isAccPositive);
                  
                  return (
                    <div key={account.id} className="p-4 flex items-center justify-between hover:bg-[var(--color-surface-hover)] transition-colors cursor-pointer group">
                      <div className="flex items-center gap-4 flex-1">
                        <div className="w-10 h-10 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center text-white font-bold border border-[var(--color-border)]">
                          {account.institution.charAt(0)}
                        </div>
                        <div>
                          <p className="text-sm font-medium text-white group-hover:text-[var(--color-primary)] transition-colors">{account.name}</p>
                          <div className="flex items-center gap-2 mt-1">
                            <span className="text-xs text-[var(--color-text-muted)]">{account.institution}</span>
                            {account.mask && (
                              <span className="text-[10px] px-1.5 py-0.5 rounded bg-[var(--color-surface-hover)] text-[var(--color-text-muted)] border border-[var(--color-border)]">
                                •••• {account.mask}
                              </span>
                            )}
                          </div>
                        </div>
                      </div>

                      {/* Sparkline */}
                      <div className="hidden md:block w-24 h-8 mx-4 opacity-50 group-hover:opacity-100 transition-opacity">
                        <ResponsiveContainer width="100%" height="100%">
                          <LineChart data={sparklineData}>
                            <Line 
                              type="monotone" 
                              dataKey="value" 
                              stroke={isAccPositive ? "var(--color-positive)" : "var(--color-negative)"} 
                              strokeWidth={2} 
                              dot={false} 
                              isAnimationActive={false}
                            />
                          </LineChart>
                        </ResponsiveContainer>
                      </div>

                      <div className="text-right flex items-center gap-4">
                        <p className={`text-sm font-mono font-medium ${isAccPositive ? 'text-white' : 'text-[var(--color-negative)]'}`}>
                          {isAccPositive ? '' : '-'}${Math.abs(account.balance).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </p>
                        <button className="p-1.5 text-[var(--color-text-muted)] hover:text-white rounded-md hover:bg-[var(--color-surface)] transition-colors">
                          <MoreHorizontal className="w-4 h-4" />
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
