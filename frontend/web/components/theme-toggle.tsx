"use client";

import { useCallback, useLayoutEffect, useState } from "react";

type ThemePreference = "light" | "dark" | "system";

function readStoredTheme(): ThemePreference {
  if (typeof document === "undefined") {
    return "system";
  }
  const attr = document.documentElement.getAttribute("data-theme");
  if (attr === "dark" || attr === "light") {
    return attr;
  }
  try {
    const stored = localStorage.getItem("barnaktiv-theme");
    if (stored === "dark" || stored === "light" || stored === "system") {
      return stored;
    }
  } catch {
    /* ignore */
  }
  return "system";
}

function applyTheme(preference: ThemePreference) {
  const root = document.documentElement;
  if (preference === "system") {
    root.removeAttribute("data-theme");
    try {
      localStorage.removeItem("barnaktiv-theme");
    } catch {
      /* ignore */
    }
    return;
  }
  root.setAttribute("data-theme", preference);
  try {
    localStorage.setItem("barnaktiv-theme", preference);
  } catch {
    /* ignore */
  }
}

export function ThemeToggle() {
  const [preference, setPreference] = useState<ThemePreference>("system");

  useLayoutEffect(() => {
    setPreference(readStoredTheme());
  }, []);

  const cycle = useCallback(() => {
    setPreference((current) => {
      const next: ThemePreference =
        current === "light" ? "dark" : current === "dark" ? "system" : "light";
      applyTheme(next);
      return next;
    });
  }, []);

  const label =
    preference === "light"
      ? "Ljust läge (klicka för mörkt)"
      : preference === "dark"
        ? "Mörkt läge (klicka för system)"
        : "Följ systemets tema (klicka för ljust)";

  return (
    <button
      type="button"
      className="btn-ghost h-10 w-10 shrink-0 rounded-xl border-0 p-0 hover:bg-[color:var(--surface)]"
      onClick={cycle}
      aria-label={label}
      title={label}
    >
      {preference === "light" ? (
        <SunIcon className="h-5 w-5 text-[color:var(--accent)]" />
      ) : preference === "dark" ? (
        <MoonIcon className="h-5 w-5 text-[color:var(--accent)]" />
      ) : (
        <SystemIcon className="h-5 w-5 text-[color:var(--muted)]" />
      )}
    </button>
  );
}

function SunIcon({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="4" stroke="currentColor" strokeWidth="1.75" />
      <path
        d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
      />
    </svg>
  );
}

function MoonIcon({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"
        stroke="currentColor"
        strokeWidth="1.75"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function SystemIcon({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <rect
        x="3"
        y="4"
        width="18"
        height="13"
        rx="2"
        stroke="currentColor"
        strokeWidth="1.75"
      />
      <path d="M8 21h8M12 17v4" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}
