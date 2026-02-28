"use client";

import { useEffect, useState } from "react";
import { UserProfile } from "@clerk/nextjs";
import { dark } from "@clerk/themes";

export function ClientUserProfile() {
  const [theme, setTheme] = useState("dark"); // Default to dark, client will sync to DOM

  useEffect(() => {
    // Initial sync with DOM
    if (typeof document !== "undefined") {
      setTheme(document.documentElement.dataset.theme === "light" ? "light" : "dark");
      
      // Observer to watch for theme changes set by ThemeSwitcher
      const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
          if (mutation.attributeName === "data-theme") {
            setTheme(document.documentElement.dataset.theme === "light" ? "light" : "dark");
          }
        });
      });
      
      observer.observe(document.documentElement, { attributes: true });
      return () => observer.disconnect();
    }
  }, []);

  return (
    <UserProfile 
      path="/settings/security" 
      routing="path" 
      appearance={{
        baseTheme: theme === "dark" ? dark : undefined,
        variables: {
          colorPrimary: 'var(--color-primary)',
          colorBackground: 'var(--color-surface)',
          colorText: 'var(--color-text-main)',
          colorTextOnPrimaryBackground: 'var(--color-button-ink)',
          colorInputBackground: 'var(--color-surface-hover)',
          colorInputText: 'var(--color-text-main)',
          fontFamily: 'var(--font-body)',
        },
        elements: {
          rootBox: 'w-full',
          cardBox: 'w-full max-w-none border border-[var(--color-border)] shadow-none rounded-2xl bg-[var(--color-surface)]',
          card: 'w-full max-w-none shadow-none',
          navbarButton: 'text-[var(--color-text-muted)] hover:text-[var(--color-text-main)]',
          buttonPrimary: 'bg-[var(--color-primary)] hover:bg-[var(--color-primary-hover)] text-[var(--color-button-ink)]',
        }
      }}
    />
  );
}