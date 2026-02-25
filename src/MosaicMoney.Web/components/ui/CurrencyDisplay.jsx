import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

/**
 * Centralized component for displaying currency values.
 * Handles Plaid's convention where positive amounts are expenses (money out)
 * and negative amounts are income (money in).
 * 
 * @param {Object} props
 * @param {number} props.amount - The raw amount from the backend.
 * @param {boolean} [props.isTransfer=false] - Whether the transaction is a transfer (neutral).
 * @param {string} [props.className] - Additional CSS classes.
 * @param {boolean} [props.showSign=true] - Whether to show the +/- sign.
 * @param {string} [props.type="transaction"] - "transaction" (Plaid convention) or "balance" (standard convention).
 */
export function CurrencyDisplay({ amount, isTransfer = false, className, showSign = true, type = "transaction" }) {
  // Plaid convention (transaction):
  // amount > 0 -> Expense (money out)
  // amount < 0 -> Income (money in)
  // Standard convention (balance):
  // amount > 0 -> Asset/Positive (money in)
  // amount < 0 -> Debt/Negative (money out)
  
  const isIncome = type === "transaction" ? amount < 0 : amount > 0;
  const isExpense = type === "transaction" ? amount > 0 : amount < 0;
  const isZero = amount === 0;

  const absAmount = Math.abs(amount);
  const formattedAmount = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(absAmount);

  let colorClass = "text-white"; // Default
  let sign = "";

  if (isTransfer || isZero) {
    colorClass = "text-[var(--color-text-muted)]"; // Grayish for transfers/neutral
    sign = "";
  } else if (isIncome) {
    colorClass = "text-[var(--color-positive)]"; // Green for income/asset
    sign = type === "transaction" ? "+" : ""; // Don't show + for balances unless requested
  } else if (isExpense) {
    colorClass = "text-[var(--color-negative)]"; // Red for expense/debt
    sign = "-";
  }

  return (
    <span className={cn("font-mono font-medium", colorClass, className)}>
      {showSign && sign}{formattedAmount}
    </span>
  );
}
