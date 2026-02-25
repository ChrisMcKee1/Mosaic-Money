import { ThemeSwitcher } from "../../components/theme/ThemeSwitcher";

export const metadata = {
  title: "Settings | Mosaic Money",
};

export default function SettingsPage() {
  return (
    <div className="p-6 md:p-10 max-w-3xl w-full overflow-y-auto">
      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-6 md:p-8">
        <h1 className="text-2xl md:text-3xl font-display font-bold text-[var(--color-text-main)]">
          Settings
        </h1>
        <p className="mt-2 text-sm text-[var(--color-text-muted)]">
          Configure how Mosaic Money looks across dashboard, accounts, transactions, and review workflows.
        </p>

        <div className="mt-8 rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-hover)] p-4 md:p-5">
          <ThemeSwitcher />
          <p className="mt-3 text-xs text-[var(--color-text-subtle)]">
            Theme preference is saved locally in your browser.
          </p>
        </div>
      </div>
    </div>
  );
}
