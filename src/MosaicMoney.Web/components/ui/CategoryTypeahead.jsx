"use client";

import { useState, useEffect, useRef } from "react";
import { Search, Check } from "lucide-react";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";

import { searchCategories } from "../../app/actions";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

export function CategoryTypeahead({ value, onChange, placeholder = "Search categories..." }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState([]);
  const [isOpen, setIsOpen] = useState(false);
  const [isSearching, setIsSearching] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState(null);
  const wrapperRef = useRef(null);

  useEffect(() => {
    function handleClickOutside(event) {
      if (wrapperRef.current && !wrapperRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  useEffect(() => {
    const normalizedQuery = query.trim();
    if (!normalizedQuery) {
      setResults([]);
      setIsSearching(false);
      return;
    }

    setIsSearching(true);
    const timeoutId = setTimeout(async () => {
      try {
        const data = await searchCategories(normalizedQuery, 10);
        setResults(data);
      } catch (e) {
        console.error("Category search failed", e);
        setResults([]);
      } finally {
        setIsSearching(false);
      }
    }, 300);

    return () => clearTimeout(timeoutId);
  }, [query]);

  return (
    <div ref={wrapperRef} className="relative w-full">
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--color-text-muted)]" />
        <input
          type="text"
          placeholder={selectedCategory ? `${selectedCategory.categoryName} > ` : placeholder}
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setIsOpen(true);
            if (selectedCategory) {
              setSelectedCategory(null);
              onChange("");
            }
          }}
          onFocus={() => setIsOpen(true)}
          className="w-full bg-[var(--color-background)] border border-[var(--color-border)] rounded-md pl-9 pr-8 py-2 text-sm text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] focus:outline-none focus:border-[var(--color-primary)] focus:ring-1 focus:ring-[var(--color-primary)] transition-all"
        />
        {isSearching && (
          <div className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 border-2 border-[var(--color-primary)] border-t-transparent rounded-full animate-spin" />
        )}
      </div>

      {isOpen && query.trim() && (
        <div className="absolute z-10 w-full mt-1 bg-[var(--color-surface)] border border-[var(--color-border)] rounded-md shadow-lg max-h-60 overflow-auto">
          {results.length === 0 && !isSearching ? (
            <div className="p-3 text-sm text-[var(--color-text-muted)] text-center">
              No categories found.
            </div>
          ) : (
            <ul className="py-1">
              {results.map((cat) => (
                <li
                  key={cat.id}
                  onClick={() => {
                    setSelectedCategory(cat);
                    setQuery("");
                    setIsOpen(false);
                    onChange(cat.id);
                  }}
                  className={cn(
                    "px-3 py-2 text-sm cursor-pointer hover:bg-[var(--color-surface-hover)] flex items-center justify-between",
                    value === cat.id ? "bg-[var(--color-primary)]/10 text-[var(--color-primary)]" : "text-[var(--color-text-main)]"
                  )}
                >
                  <div>
                    <span className="font-medium">{cat.name}</span>
                    <span className="text-[var(--color-text-muted)] ml-2 text-xs">{cat.categoryName}</span>
                  </div>
                  {value === cat.id && <Check className="w-4 h-4" />}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
