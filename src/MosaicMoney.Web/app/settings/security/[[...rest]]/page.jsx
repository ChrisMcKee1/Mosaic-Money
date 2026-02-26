import { UserProfile } from "@clerk/nextjs";

export const metadata = {
  title: "Security | Settings | Mosaic Money",
};

export default function SecuritySettingsPage() {
  const isClerkConfigured = !!process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY;

  if (!isClerkConfigured) {
    return (
      <div className="p-6 md:p-10 max-w-3xl w-full overflow-y-auto">
        <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-6 md:p-8">
          <h1 className="text-2xl md:text-3xl font-display font-bold text-[var(--color-text-main)]">
            Security & Authentication
          </h1>
          <p className="mt-4 text-sm text-[var(--color-text-muted)]">
            Authentication is currently disabled in this environment.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-6 md:p-10 max-w-4xl w-full overflow-y-auto">
      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-6 md:p-8">
        <h1 className="text-2xl md:text-3xl font-display font-bold text-[var(--color-text-main)] mb-6">
          Security & Authentication
        </h1>
        <div className="clerk-profile-container">
          <UserProfile path="/settings/security" routing="path" />
        </div>
      </div>
    </div>
  );
}
