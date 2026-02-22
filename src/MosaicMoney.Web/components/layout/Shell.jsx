"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { LayoutDashboard, Receipt, AlertCircle, Settings } from "lucide-react";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

const navigation = [
  { name: "Dashboard", href: "/", icon: LayoutDashboard },
  { name: "Transactions", href: "/transactions", icon: Receipt },
  { name: "Needs Review", href: "/needs-review", icon: AlertCircle },
];

export function Shell({ children }) {
  const pathname = usePathname();

  return (
    <div className="min-h-screen bg-gray-50 text-gray-900 flex flex-col pb-16 sm:pb-0">
      {/* Accessible Header Landmark */}
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16">
            <div className="flex">
              <div className="flex-shrink-0 flex items-center">
                <span className="text-xl font-bold text-blue-600">Mosaic Money</span>
              </div>
              {/* Accessible Navigation Landmark - Desktop */}
              <nav aria-label="Main Navigation" className="hidden sm:ml-6 sm:flex sm:space-x-8">
                {navigation.map((item) => {
                  const isActive = pathname === item.href;
                  return (
                    <Link
                      key={item.name}
                      href={item.href}
                      className={cn(
                        isActive
                          ? "border-blue-500 text-gray-900"
                          : "border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700",
                        "inline-flex items-center px-1 pt-1 border-b-2 text-sm font-medium"
                      )}
                      aria-current={isActive ? "page" : undefined}
                    >
                      <item.icon className="w-4 h-4 mr-2" aria-hidden="true" />
                      {item.name}
                    </Link>
                  );
                })}
              </nav>
            </div>
            <div className="hidden sm:ml-6 sm:flex sm:items-center">
              <Link
                href="/settings"
                className="p-2 rounded-full text-gray-400 hover:text-gray-500 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
                aria-label="Settings"
              >
                <Settings className="w-6 h-6" aria-hidden="true" />
              </Link>
            </div>
          </div>
        </div>
      </header>

      {/* Accessible Main Content Landmark */}
      <main id="main-content" className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {children}
      </main>

      {/* Accessible Footer Landmark - Desktop */}
      <footer className="hidden sm:block bg-white border-t border-gray-200 mt-auto">
        <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
          <p className="text-center text-sm text-gray-500">
            &copy; {new Date().getFullYear()} Mosaic Money. All rights reserved.
          </p>
        </div>
      </footer>

      {/* Mobile Bottom Navigation */}
      <nav
        aria-label="Mobile Navigation"
        className="sm:hidden fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200 z-20 pb-[env(safe-area-inset-bottom)]"
      >
        <div className="flex justify-around items-center h-16">
          {navigation.map((item) => {
            const isActive = pathname === item.href;
            return (
              <Link
                key={item.name}
                href={item.href}
                className={cn(
                  isActive ? "text-blue-600" : "text-gray-500 hover:text-gray-900",
                  "flex flex-col items-center justify-center w-full h-full space-y-1"
                )}
                aria-current={isActive ? "page" : undefined}
              >
                <item.icon className="w-6 h-6" aria-hidden="true" />
                <span className="text-[10px] font-medium">{item.name}</span>
              </Link>
            );
          })}
          <Link
            href="/settings"
            className={cn(
              pathname === "/settings" ? "text-blue-600" : "text-gray-500 hover:text-gray-900",
              "flex flex-col items-center justify-center w-full h-full space-y-1"
            )}
            aria-current={pathname === "/settings" ? "page" : undefined}
          >
            <Settings className="w-6 h-6" aria-hidden="true" />
            <span className="text-[10px] font-medium">Settings</span>
          </Link>
        </div>
      </nav>
    </div>
  );
}