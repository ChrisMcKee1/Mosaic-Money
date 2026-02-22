import Link from "next/link";
import { LayoutDashboard, Receipt, PieChart, Settings } from "lucide-react";

export function Shell({ children }) {
  return (
    <div className="min-h-screen bg-gray-50 text-gray-900 flex flex-col">
      {/* Accessible Header Landmark */}
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16">
            <div className="flex">
              <div className="flex-shrink-0 flex items-center">
                <span className="text-xl font-bold text-blue-600">Mosaic Money</span>
              </div>
              {/* Accessible Navigation Landmark */}
              <nav aria-label="Main Navigation" className="hidden sm:ml-6 sm:flex sm:space-x-8">
                <Link
                  href="/"
                  className="border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 inline-flex items-center px-1 pt-1 border-b-2 text-sm font-medium"
                >
                  <LayoutDashboard className="w-4 h-4 mr-2" aria-hidden="true" />
                  Dashboard
                </Link>
                <Link
                  href="/transactions"
                  className="border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 inline-flex items-center px-1 pt-1 border-b-2 text-sm font-medium"
                >
                  <Receipt className="w-4 h-4 mr-2" aria-hidden="true" />
                  Transactions
                </Link>
                <Link
                  href="/budget"
                  className="border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 inline-flex items-center px-1 pt-1 border-b-2 text-sm font-medium"
                >
                  <PieChart className="w-4 h-4 mr-2" aria-hidden="true" />
                  Budget
                </Link>
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

      {/* Accessible Footer Landmark */}
      <footer className="bg-white border-t border-gray-200 mt-auto">
        <div className="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
          <p className="text-center text-sm text-gray-500">
            &copy; {new Date().getFullYear()} Mosaic Money. All rights reserved.
          </p>
        </div>
      </footer>
    </div>
  );
}