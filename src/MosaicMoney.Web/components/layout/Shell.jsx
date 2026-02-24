"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { 
  LayoutDashboard, 
  Receipt, 
  AlertCircle, 
  Link2, 
  Settings,
  PieChart,
  TrendingUp,
  Repeat
} from "lucide-react";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

const navigation = [
  { name: "Dashboard", href: "/", icon: LayoutDashboard },
  { name: "Accounts", href: "/accounts", icon: PieChart },
  { name: "Transactions", href: "/transactions", icon: Receipt },
  { name: "Categories", href: "/categories", icon: TrendingUp },
  { name: "Investments", href: "/investments", icon: TrendingUp },
  { name: "Recurrings", href: "/recurrings", icon: Repeat },
  { name: "Needs Review", href: "/needs-review", icon: AlertCircle },
];

export function Shell({ children }) {
  const pathname = usePathname();

  return (
    <div className="flex h-screen overflow-hidden bg-[var(--color-background)] text-[var(--color-text-main)]">
      {/* Left Sidebar */}
      <aside className="w-64 flex-shrink-0 border-r border-[var(--color-border)] bg-[var(--color-surface)] flex flex-col">
        <div className="h-16 flex items-center px-6 border-b border-[var(--color-border)]">
          <div className="flex items-center gap-2">
            <div className="w-6 h-6 rounded bg-[var(--color-primary)] shadow-[0_0_15px_var(--color-primary)]" />
            <span className="text-lg font-bold tracking-tight font-display text-white">Mosaic</span>
          </div>
        </div>
        
        <nav className="flex-1 overflow-y-auto py-6 px-3 space-y-1">
          <div className="text-xs font-semibold text-[var(--color-text-subtle)] uppercase tracking-wider mb-4 px-3">
            Overview
          </div>
          {navigation.map((item) => {
            const isActive = pathname === item.href;
            return (
              <Link
                key={item.name}
                href={item.href}
                className={cn(
                  "group flex items-center px-3 py-2.5 text-sm font-medium rounded-lg transition-all duration-200",
                  isActive
                    ? "bg-[var(--color-surface-hover)] text-[var(--color-primary)] shadow-sm"
                    : "text-[var(--color-text-muted)] hover:bg-[var(--color-surface-hover)] hover:text-white"
                )}
              >
                <item.icon 
                  className={cn(
                    "mr-3 flex-shrink-0 h-5 w-5 transition-colors duration-200",
                    isActive ? "text-[var(--color-primary)]" : "text-[var(--color-text-subtle)] group-hover:text-white"
                  )} 
                />
                {item.name}
              </Link>
            );
          })}
        </nav>

        <div className="p-4 border-t border-[var(--color-border)]">
          <Link
            href="/settings"
            className="group flex items-center px-3 py-2.5 text-sm font-medium rounded-lg text-[var(--color-text-muted)] hover:bg-[var(--color-surface-hover)] hover:text-white transition-all duration-200"
          >
            <Settings className="mr-3 flex-shrink-0 h-5 w-5 text-[var(--color-text-subtle)] group-hover:text-white transition-colors duration-200" />
            Settings
          </Link>
        </div>
      </aside>

      {/* Main Content Area */}
      <main id="main-content" className="flex-1 flex overflow-hidden relative">
        {children}
      </main>
    </div>
  );
}