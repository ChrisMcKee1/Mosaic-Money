"use client";

import { Wallet, CreditCard, Building, Landmark, TrendingUp, MoreHorizontal, Users, User, Eye } from "lucide-react";
import dynamic from 'next/dynamic';
import { getSparklineOptions } from "../charts/ChartConfig";
import { useChartTheme } from "../charts/useChartTheme";
import { CurrencyDisplay } from "../ui/CurrencyDisplay";

const Chart = dynamic(() => import('react-apexcharts'), { ssr: false });

const generateSparklineData = (base, isPositive, seedSource) => {
  let seed = String(seedSource)
    .split("")
    .reduce((acc, char) => acc + char.charCodeAt(0), 17);

  return Array.from({ length: 10 }).map(() => {
    seed = (seed * 48271) % 2147483647;
    const normalized = seed / 2147483647;
    const delta = (normalized - 0.5) * 100;

    return {
      value: base + delta * (isPositive ? 1 : -1),
    };
  });
};

const VisibilityBadge = ({ visibility }) => {
  if (!visibility) return null;
  
  const config = {
    "Joint": { icon: Users, className: "bg-blue-500/10 text-blue-400 border-blue-500/20" },
    "Mine": { icon: User, className: "bg-emerald-500/10 text-emerald-400 border-emerald-500/20" },
    "Shared": { icon: Eye, className: "bg-purple-500/10 text-purple-400 border-purple-500/20" }
  };
  
  const { icon: Icon, className } = config[visibility] || config["Mine"];
  
  return (
    <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium border ${className}`}>
      <Icon className="w-3 h-3" />
      {visibility}
    </span>
  );
};

export function AccountsList({ accounts, onSelectAccount, selectedAccountId }) {
  const chartTheme = useChartTheme();

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
              <CurrencyDisplay amount={total} type="balance" className="font-mono font-medium" />
            </div>

            <div className="bg-[var(--color-surface)] rounded-xl border border-[var(--color-border)] overflow-hidden">
              <div className="divide-y divide-[var(--color-border)]">
                {typeAccounts.map((account) => {
                  const isAccPositive = account.balance >= 0;
                  const sparklineData = generateSparklineData(Math.abs(account.balance), isAccPositive, account.id);
                  
                  return (
                    <div 
                      key={account.id} 
                      onClick={() => onSelectAccount?.(account)}
                      className={`p-4 flex items-center justify-between hover:bg-[var(--color-surface-hover)] transition-colors cursor-pointer group border-l-2 ${selectedAccountId === account.id ? 'bg-[var(--color-surface-hover)] border-l-[var(--color-primary)]' : 'border-l-transparent'}`}
                    >
                      <div className="flex items-center gap-4 flex-1">
                        <div className="w-10 h-10 rounded-full bg-[var(--color-surface-hover)] flex items-center justify-center text-white font-bold border border-[var(--color-border)]">
                          {account.institution.charAt(0)}
                        </div>
                        <div>
                          <div className="flex items-center gap-2">
                            <p className="text-sm font-medium text-white group-hover:text-[var(--color-primary)] transition-colors">{account.name}</p>
                            <VisibilityBadge visibility={account.visibility} />
                          </div>
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
                        <Chart 
                          options={getSparklineOptions(isAccPositive, chartTheme)} 
                          series={[{ data: sparklineData.map(d => d.value) }]} 
                          type="line" 
                          width="100%" 
                          height="100%" 
                        />
                      </div>

                      <div className="text-right flex items-center gap-4">
                        <CurrencyDisplay amount={account.balance} type="balance" className="text-sm font-mono font-medium" />
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
