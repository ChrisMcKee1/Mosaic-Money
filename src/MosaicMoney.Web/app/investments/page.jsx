import { fetchApi } from "../../lib/api";
import { InvestmentsClient } from "./InvestmentsClient";

export const dynamic = "force-dynamic";

export default async function InvestmentsPage() {
  // Mock data for investments since we don't have an investments API yet
  const mockAccounts = [
    { id: "1", name: "Fidelity Brokerage", type: "Brokerage", balance: 45230.50, change1W: 1250.20, change1WPercent: 2.8 },
    { id: "2", name: "Vanguard 401k", type: "Retirement", balance: 128450.00, change1W: -450.00, change1WPercent: -0.35 },
    { id: "3", name: "Coinbase", type: "Crypto", balance: 12450.80, change1W: 2100.50, change1WPercent: 20.3 },
    { id: "4", name: "Robinhood", type: "Crypto", balance: 3200.00, change1W: -150.00, change1WPercent: -4.5 },
  ];

  return <InvestmentsClient accounts={mockAccounts} />;
}