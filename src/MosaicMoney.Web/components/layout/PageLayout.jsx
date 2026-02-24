import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

export function PageLayout({ children, rightPanel, title, subtitle }) {
  return (
    <div className="flex-1 flex w-full h-full overflow-hidden">
      {/* Center Main Content */}
      <div className="flex-1 flex flex-col h-full overflow-y-auto">
        <div className="px-8 py-8 max-w-5xl mx-auto w-full flex-1">
          {(title || subtitle) && (
            <div className="mb-8">
              {title && <h1 className="text-3xl font-display font-bold text-white tracking-tight">{title}</h1>}
              {subtitle && <p className="mt-2 text-sm text-[var(--color-text-muted)]">{subtitle}</p>}
            </div>
          )}
          {children}
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
