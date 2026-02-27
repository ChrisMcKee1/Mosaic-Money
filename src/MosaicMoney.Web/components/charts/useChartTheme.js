"use client";

import { useEffect, useState } from "react";

const getCurrentTheme = () => {
  if (typeof document === "undefined") {
    return "dark";
  }

  return document.documentElement.dataset.theme === "light" ? "light" : "dark";
};

export function useChartTheme() {
  const [theme, setTheme] = useState(getCurrentTheme);

  useEffect(() => {
    const updateTheme = () => {
      setTheme(getCurrentTheme());
    };

    updateTheme();

    const observer = new MutationObserver(updateTheme);
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ["data-theme"],
    });

    window.addEventListener("storage", updateTheme);

    return () => {
      observer.disconnect();
      window.removeEventListener("storage", updateTheme);
    };
  }, []);

  return theme;
}