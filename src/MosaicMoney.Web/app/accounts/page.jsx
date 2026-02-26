import { AccountsClient } from "./AccountsClient";

export const metadata = {
  title: "Accounts | Mosaic Money",
};

export default function AccountsPage() {
  // Mock data for accounts
  const accounts = [
    { id: 1, name: "Chase Checking", type: "Depository", balance: 4500.00, mask: "1234", institution: "Chase", visibility: "Joint" },
    { id: 2, name: "Ally Savings", type: "Depository", balance: 12500.00, mask: "5678", institution: "Ally Bank", visibility: "Joint" },
    { id: 3, name: "Amex Sapphire", type: "Credit Card", balance: -1250.00, mask: "9012", institution: "American Express", visibility: "Mine" },
    { id: 4, name: "Fidelity Brokerage", type: "Investment", balance: 45000.00, mask: "3456", institution: "Fidelity", visibility: "Mine" },
    { id: 5, name: "Vanguard 401k", type: "Investment", balance: 120000.00, mask: "7890", institution: "Vanguard", visibility: "Shared" },
    { id: 6, name: "Auto Loan", type: "Loan", balance: -15000.00, mask: "1122", institution: "Capital One", visibility: "Joint" },
    { id: 7, name: "Primary Residence", type: "Real Estate", balance: 450000.00, mask: null, institution: "Zillow Estimate", visibility: "Joint" },
    { id: 8, name: "Mortgage", type: "Loan", balance: -320000.00, mask: "3344", institution: "Rocket Mortgage", visibility: "Joint" },
  ];

  return <AccountsClient initialAccounts={accounts} />;
}
