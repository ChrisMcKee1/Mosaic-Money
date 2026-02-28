"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { 
  LayoutDashboard, 
  Receipt, 
  AlertCircle, 
  Settings,
  PieChart,
  TrendingUp,
  Repeat,
  LogOut
} from "lucide-react";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import { ThemeSwitcher } from "../theme/ThemeSwitcher";
import { SignedIn, SignedOut, SignInButton, UserButton, useClerk } from "@clerk/nextjs";
import { GlobalAgentPanel } from "../assistant/GlobalAgentPanel";

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

function ClerkControls() {
  const { signOut } = useClerk();
  const [isSigningOut, setIsSigningOut] = useState(false);

  const handleSignOut = async () => {
    if (isSigningOut) return;
    setIsSigningOut(true);
    try {
      await signOut({ redirectUrl: "/sign-in" });
    } finally {
      setIsSigningOut(false);
    }
  };

  return (
    <div className="px-3 py-2">
      <SignedIn>
        <div className="space-y-3">
          <div className="flex items-center gap-3">
          <UserButton afterSignOutUrl="/" />
          <span className="text-sm font-medium text-[var(--color-text-main)]">Account</span>
          </div>
          <button
            type="button"
            onClick={handleSignOut}
            disabled={isSigningOut}
            className="w-full flex items-center justify-center gap-2 px-3 py-2 text-sm font-medium rounded-lg border border-[var(--color-border)] text-[var(--color-text-main)] hover:bg-[var(--color-surface-hover)] transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            aria-label="Sign out"
          >
            <LogOut className="w-4 h-4" />
            {isSigningOut ? "Signing out..." : "Sign Out"}
          </button>
        </div>
      </SignedIn>
      <SignedOut>
        <SignInButton mode="modal">
          <button className="w-full flex items-center justify-center px-3 py-2 text-sm font-medium rounded-lg bg-[var(--color-primary)] text-white hover:bg-[var(--color-primary-hover)] transition-colors">
            Sign In
          </button>
        </SignInButton>
      </SignedOut>
    </div>
  );
}

export function Shell({ children, isClerkConfigured }) {
  const pathname = usePathname();
  const showAssistantPanel = !pathname.startsWith("/sign-in") && !pathname.startsWith("/sign-up");

  return (
    <div className="flex h-screen overflow-hidden bg-[var(--color-background)] text-[var(--color-text-main)]">
      {/* Left Sidebar */}
      <aside className="w-64 flex-shrink-0 border-r border-[var(--color-border)] bg-[var(--color-surface)] flex flex-col">
        <div className="h-20 flex items-center px-5 border-b border-[var(--color-border)]">
          <Link href="/" className="flex items-center gap-3 group" aria-label="Mosaic Money home">
            <picture className="h-10 w-10 overflow-hidden rounded-xl ring-1 ring-white/10 shadow-[var(--sidebar-brand-shadow)]">
              <source srcSet="/brand/logo.svg" type="image/svg+xml" />
              <img src="/brand/logo.png" alt="Mosaic Money logo" className="h-10 w-10 object-cover" />
            </picture>
            <div>
              <p className="text-base font-bold tracking-tight font-display text-[var(--color-text-main)]">Mosaic Money</p>
              <p className="text-[11px] uppercase tracking-wider text-[var(--color-text-subtle)]">Financial OS</p>
            </div>
          </Link>
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
                    : "text-[var(--color-text-muted)] hover:bg-[var(--color-surface-hover)] hover:text-[var(--color-text-main)]"
                )}
              >
                <item.icon 
                  className={cn(
                    "mr-3 flex-shrink-0 h-5 w-5 transition-colors duration-200",
                    isActive ? "text-[var(--color-primary)]" : "text-[var(--color-text-subtle)] group-hover:text-[var(--color-text-main)]"
                  )} 
                />
                {item.name}
              </Link>
            );
          })}
        </nav>

        <div className="p-4 border-t border-[var(--color-border)] space-y-3">
          {isClerkConfigured && <ClerkControls />}
          <ThemeSwitcher compact />
          <Link
            href="/settings"
            className="group flex items-center px-3 py-2.5 text-sm font-medium rounded-lg text-[var(--color-text-muted)] hover:bg-[var(--color-surface-hover)] hover:text-[var(--color-text-main)] transition-all duration-200"
          >
            <Settings className="mr-3 flex-shrink-0 h-5 w-5 text-[var(--color-text-subtle)] group-hover:text-[var(--color-text-main)] transition-colors duration-200" />
            Settings
          </Link>
        </div>
      </aside>

      {/* Main Content Area */}
      <main id="main-content" className="flex-1 flex overflow-hidden relative">
        {children}
      </main>

      {showAssistantPanel ? <GlobalAgentPanel /> : null}
    </div>
  );
}