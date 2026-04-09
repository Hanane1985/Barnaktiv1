import Link from "next/link";

import { ThemeToggle } from "@/components/theme-toggle";

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-[color:var(--border)] bg-[color:var(--surface-strong)]/85 backdrop-blur-md">
      <div className="mx-auto flex h-14 max-w-[86rem] items-center justify-between gap-4 px-4 sm:h-16 sm:px-6 lg:px-8">
        <Link
          href="/"
          className="font-display text-lg font-semibold tracking-tight text-[color:var(--foreground)] transition hover:opacity-90 sm:text-xl"
        >
          Barnaktiv
        </Link>

        <div className="flex items-center gap-1 sm:gap-2">
          <nav
            className="flex items-center gap-1 text-sm font-medium text-[color:var(--muted)]"
            aria-label="Huvudnavigation"
          >
            <a
              href="#utforska"
              className="rounded-full px-3 py-2 transition hover:bg-[color:var(--surface)] hover:text-[color:var(--foreground)]"
            >
              Utforska
            </a>
            <a
              href="#aktiviteter"
              className="rounded-full px-3 py-2 transition hover:bg-[color:var(--surface)] hover:text-[color:var(--foreground)]"
            >
              Aktiviteter
            </a>
          </nav>
          <ThemeToggle />
        </div>
      </div>
    </header>
  );
}

export function SiteFooter() {
  return (
    <footer className="mt-4 border-t border-[color:var(--border)] bg-[color:var(--surface-strong)]/60 py-10">
      <div className="mx-auto flex max-w-[86rem] flex-col gap-6 px-4 sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
        <div>
          <p className="font-display text-base font-semibold text-[color:var(--foreground)]">
            Barnaktiv
          </p>
          <p className="mt-1 max-w-md text-sm leading-relaxed text-[color:var(--muted)]">
            Vi samlar barnaktiviteter från flera källor så att du snabbare hittar något som passar
            er familj.
          </p>
        </div>
        <p className="text-xs text-[color:var(--muted-foreground)]">
          © {new Date().getFullYear()} Barnaktiv · Göteborg och närområde
        </p>
      </div>
    </footer>
  );
}
