import { fetchApi } from "../lib/api";

export default async function HomePage() {
  let apiStatus = "Unknown";
  try {
    const health = await fetchApi("/api/health", { cache: "no-store" });
    apiStatus = health.status === "ok" ? "Connected" : "Error";
  } catch (error) {
    console.error("Failed to fetch API health:", error);
    apiStatus = "Disconnected";
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900">Dashboard</h1>
        <p className="mt-1 text-sm text-gray-500">
          Welcome to Mosaic Money. Your financial overview will appear here.
        </p>
        <p className="mt-2 text-xs text-gray-400">
          API Status: <span className="font-medium">{apiStatus}</span>
        </p>
      </div>
      
      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {/* Placeholder Cards */}
        <div className="bg-white overflow-hidden shadow rounded-lg border border-gray-200">
          <div className="p-5">
            <div className="flex items-center">
              <div className="w-0 flex-1">
                <dl>
                  <dt className="text-sm font-medium text-gray-500 truncate">Total Balance</dt>
                  <dd className="text-lg font-medium text-gray-900">$0.00</dd>
                </dl>
              </div>
            </div>
          </div>
        </div>
        
        <div className="bg-white overflow-hidden shadow rounded-lg border border-gray-200">
          <div className="p-5">
            <div className="flex items-center">
              <div className="w-0 flex-1">
                <dl>
                  <dt className="text-sm font-medium text-gray-500 truncate">Recent Transactions</dt>
                  <dd className="text-lg font-medium text-gray-900">0</dd>
                </dl>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
