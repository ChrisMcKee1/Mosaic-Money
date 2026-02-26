import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

export function PageLayout({ children, rightPanel, title, subtitle, actions }) {
  return (
    <div className="flex-1 flex w-full h-full overflow-hidden">
      {/* Center Main Content */}
      <div className="flex-1 flex flex-col h-full overflow-y-auto">
        <div className="px-8 py-8 max-w-5xl mx-auto w-full flex-1">
          {(title || subtitle || actions) && (
            <div className="mb-8 flex items-start justify-between">
              <div>
                {title && <h1 className="text-3xl font-display font-bold text-white tracking-tight">{title}</h1>}
                {subtitle && <p className="mt-2 text-sm text-[var(--color-text-muted)]">{subtitle}</p>}
              </div>
              {actions && (
                <div className="flex items-center gap-3">
                  {actions}
                </div>
              )}
            </div>
          )}
          {children}

          {/* Mobile Context Panel Fallback */}
          {rightPanel && (
            <section className="mt-8 lg:hidden">
              <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] p-4">
                <p className="text-xs font-semibold uppercase tracking-wider text-[var(--color-text-muted)] mb-3">
                  Details
                </p>
                {rightPanel}
              </div>
            </section>
          )}
        </div>
      </div>

      {/* Right Contextual Panel */}
      {rightPanel && (
        <aside className="w-80 flex-shrink-0 border-l border-[var(--color-border)] bg-[var(--color-surface)] overflow-y-auto hidden lg:block">
          <div className="p-6">
            {rightPanel}
          </div>
        </aside>
      )}
    </div>
  );
}
