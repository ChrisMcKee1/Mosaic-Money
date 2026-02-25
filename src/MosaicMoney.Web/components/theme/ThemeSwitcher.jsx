"use client";

import { useEffect, useState } from "react";
import { Moon, Sun } from "lucide-react";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

const THEME_KEY = "mosaic-theme";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

function applyTheme(nextTheme) {
  if (typeof document === "undefined") {
    return;
  }

  document.documentElement.dataset.theme = nextTheme;
  localStorage.setItem(THEME_KEY, nextTheme);
}

export function ThemeSwitcher({ compact = false }) {
  const [theme, setTheme] = useState("dark");

  useEffect(() => {
    const savedTheme = localStorage.getItem(THEME_KEY);
    const nextTheme = savedTheme === "light" ? "light" : "dark";
    setTheme(nextTheme);
    applyTheme(nextTheme);
  }, []);

  const selectTheme = (nextTheme) => {
    setTheme(nextTheme);
    applyTheme(nextTheme);
  };

  return (
    <div className="space-y-2">
      {!compact && (
        <p className="text-xs font-semibold uppercase tracking-wider text-[var(--color-text-subtle)]">
          Appearance
        </p>
      )}
      <div className="grid grid-cols-2 gap-2 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-hover)] p-1">
        <button
          type="button"
          onClick={() => selectTheme("dark")}
          aria-pressed={theme === "dark"}
          className={cn(
            "inline-flex items-center justify-center gap-1.5 rounded-md px-2 py-1.5 text-xs font-medium transition-colors",
            theme === "dark"
              ? "bg-[var(--color-background)] text-[var(--color-text-main)]"
              : "text-[var(--color-text-muted)] hover:text-[var(--color-text-main)]"
          )}
        >
          <Moon className="h-3.5 w-3.5" />
          Dark
        </button>
        <button
          type="button"
          onClick={() => selectTheme("light")}
          aria-pressed={theme === "light"}
          className={cn(
            "inline-flex items-center justify-center gap-1.5 rounded-md px-2 py-1.5 text-xs font-medium transition-colors",
            theme === "light"
              ? "bg-[var(--color-background)] text-[var(--color-text-main)]"
              : "text-[var(--color-text-muted)] hover:text-[var(--color-text-main)]"
          )}
        >
          <Sun className="h-3.5 w-3.5" />
          Light
        </button>
      </div>
    </div>
  );
}
