"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  useDeferredValue,
  useEffect,
  useRef,
  useState,
  useTransition,
} from "react";

import { SiteFooter, SiteHeader } from "@/components/site-chrome";
import { ActivityCard } from "@/features/activities/activity-card";
import { ageGroups, priceFilters, sortOptions } from "@/features/activities/constants";
import {
  getCategoryLabels,
  getResultSummary,
  getSortedOptions,
} from "@/features/activities/activity-domain";
import { HeroCollage } from "@/features/activities/hero-collage";
import {
  buildActivityRouteSearchParams,
  defaultActivityFilters,
  type ActivityFilters,
  type AgeGroup,
  type PriceFilter,
  type SortOption,
} from "@/lib/activity-filters";
import type { Activity } from "@/lib/activities";

type ActivityExplorerProps = {
  activities: Activity[];
  initialFilters: ActivityFilters;
  errorMessage?: string;
};

function IconActivities({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
    >
      <path d="M4.5 12.5 9 7l3 3 4.5-4.5L20 8.5" />
      <path d="M4 19h16" />
      <path d="M8 15h.01M12 15h4" />
    </svg>
  );
}

function IconMap({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
    >
      <path d="M9 20.5 3 17.5V5.5l6 3 6-3 6 3v12l-6-3-6 3z" />
      <path d="M9 5.5v15M15 3.5v15" />
    </svg>
  );
}

function IconUsers({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
    >
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
  );
}

export function ActivityExplorer({
  activities,
  initialFilters,
  errorMessage,
}: ActivityExplorerProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [isPending, startTransition] = useTransition();
  const [search, setSearch] = useState(initialFilters.search);
  const [selectedCity, setSelectedCity] = useState(initialFilters.city);
  const [selectedOrganizer, setSelectedOrganizer] = useState(initialFilters.organizer);
  const [selectedSport, setSelectedSport] = useState(initialFilters.sport);
  const [selectedCategory, setSelectedCategory] = useState(initialFilters.category);
  const [selectedAgeGroup, setSelectedAgeGroup] = useState<AgeGroup>(
    initialFilters.ageGroup,
  );
  const [selectedPrice, setSelectedPrice] = useState<PriceFilter>(initialFilters.price);
  const [selectedSort, setSelectedSort] = useState<SortOption>(initialFilters.sort);
  const [isSortMenuOpen, setIsSortMenuOpen] = useState(false);
  const deferredSearch = useDeferredValue(search);
  const sortMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const nextQueryString = buildActivityRouteSearchParams({
      search: deferredSearch,
      city: selectedCity,
      organizer: selectedOrganizer,
      sport: selectedSport,
      category: selectedCategory,
      ageGroup: selectedAgeGroup,
      price: selectedPrice,
      sort: selectedSort,
    }).toString();
    const currentQueryString = searchParams.toString();

    if (nextQueryString === currentQueryString) {
      return;
    }

    startTransition(() => {
      router.replace(
        nextQueryString ? `${pathname}?${nextQueryString}` : pathname,
        { scroll: false },
      );
    });
  }, [
    deferredSearch,
    pathname,
    router,
    searchParams,
    selectedAgeGroup,
    selectedCategory,
    selectedCity,
    selectedOrganizer,
    selectedPrice,
    selectedSort,
    selectedSport,
  ]);

  useEffect(() => {
    if (!isSortMenuOpen) {
      return;
    }

    const onPointerDown = (event: MouseEvent) => {
      const node = sortMenuRef.current;
      if (node && !node.contains(event.target as Node)) {
        setIsSortMenuOpen(false);
      }
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsSortMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", onPointerDown);
    window.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [isSortMenuOpen]);

  const cities = getSortedOptions(
    activities.map((activity) => activity.city),
    selectedCity,
  );
  const organizers = getSortedOptions(
    activities.map((activity) => activity.organizer),
    selectedOrganizer,
  );
  const sports = getSortedOptions(
    activities.map((activity) => activity.sport),
    selectedSport,
  );
  const categories = getSortedOptions(
    activities.flatMap((activity) => getCategoryLabels(activity.category)),
    selectedCategory,
  );
  const selectedSortLabel =
    sortOptions.find((option) => option.value === selectedSort)?.label ??
    sortOptions[0].label;

  const openActivitiesCount = activities.filter(
    (activity) => activity.registrationStatus === "Open",
  ).length;
  const freeActivitiesCount = activities.filter(
    (activity) => activity.price <= 0,
  ).length;
  const featuredCities = cities.slice(0, 4);
  const featuredCategories = categories.slice(0, 5);

  const clearFilters = () => {
    setSearch(defaultActivityFilters.search);
    setSelectedCity(defaultActivityFilters.city);
    setSelectedOrganizer(defaultActivityFilters.organizer);
    setSelectedSport(defaultActivityFilters.sport);
    setSelectedCategory(defaultActivityFilters.category);
    setSelectedAgeGroup(defaultActivityFilters.ageGroup);
    setSelectedPrice(defaultActivityFilters.price);
    setSelectedSort(defaultActivityFilters.sort);
    setIsSortMenuOpen(false);
  };

  const statIconMuted = "h-5 w-5 shrink-0 text-[color:var(--accent)]";
  const statIconOnDark = "h-5 w-5 shrink-0 text-white/85";

  return (
    <>
      <SiteHeader />
      <main className="relative mx-auto flex min-h-screen w-full max-w-[86rem] flex-col gap-10 overflow-hidden px-4 pb-8 pt-6 sm:px-6 lg:gap-12 lg:px-8 lg:pb-12 lg:pt-8">
        <div className="hero-page-glow pointer-events-none absolute inset-x-0 top-0 -z-10 h-[32rem] opacity-95" />

        <section className="relative overflow-hidden rounded-[2rem] border border-[color:var(--border)] bg-[color:var(--surface-strong)] px-6 py-8 shadow-[var(--card-shadow)] sm:rounded-[2.25rem] sm:px-8 sm:py-10 lg:px-10 lg:py-12">
          <div
            className="pointer-events-none absolute -right-20 -top-20 h-64 w-64 rounded-full bg-[color:var(--accent-soft)]/40 blur-3xl"
            aria-hidden
          />
          <div className="relative grid gap-10 lg:grid-cols-[1.02fr_0.98fr] lg:items-center lg:gap-12">
            <div className="space-y-8">
              <span className="inline-flex items-center gap-2 rounded-full border border-[color:var(--border)] bg-[color:var(--surface)] px-4 py-2 text-[0.7rem] font-semibold uppercase tracking-[0.2em] text-[color:var(--sage)]">
                <span className="h-1.5 w-1.5 rounded-full bg-[color:var(--accent)]" />
                Barnaktiviteter samlade på ett ställe
              </span>

              <div className="space-y-5">
                <h1 className="font-display max-w-[20ch] text-[2.25rem] font-semibold leading-[1.08] tracking-tight text-[color:var(--foreground)] sm:text-5xl xl:text-[3.25rem]">
                  Ge varje ledig dag något att längta till.
                </h1>
                <p className="max-w-xl text-base leading-relaxed text-[color:var(--muted)] sm:text-lg">
                  Barnaktiv samlar prova-på-pass, lovaktiviteter, kurser och
                  föreningsträffar så att du snabbt hittar rätt aktivitet för ditt
                  barn. Filtrera på stad, ålder, pris och anmälan utan att hoppa
                  mellan olika sidor.
                </p>
              </div>

              <div className="grid gap-3 sm:grid-cols-3">
                <div className="flex flex-col justify-between rounded-2xl bg-[color:var(--sage)] p-5 text-white shadow-md">
                  <div className="flex items-center gap-2 text-white/80">
                    <IconActivities className={statIconOnDark} />
                    <span className="text-[0.7rem] font-semibold uppercase tracking-[0.16em]">
                      Aktiviteter
                    </span>
                  </div>
                  <p className="mt-4 font-display text-3xl font-semibold tabular-nums sm:text-4xl">
                    {activities.length}
                  </p>
                </div>
                <div className="card-surface flex flex-col justify-between rounded-2xl p-5">
                  <div className="flex items-center gap-2 text-[color:var(--muted)]">
                    <IconMap className={statIconMuted} />
                    <span className="text-[0.7rem] font-semibold uppercase tracking-[0.16em]">
                      Städer
                    </span>
                  </div>
                  <p className="mt-4 font-display text-3xl font-semibold tabular-nums text-[color:var(--foreground)] sm:text-4xl">
                    {cities.length}
                  </p>
                </div>
                <div className="card-surface flex flex-col justify-between rounded-2xl p-5">
                  <div className="flex items-center gap-2 text-[color:var(--muted)]">
                    <IconUsers className={statIconMuted} />
                    <span className="text-[0.7rem] font-semibold uppercase tracking-[0.16em]">
                      Arrangörer
                    </span>
                  </div>
                  <p className="mt-4 font-display text-3xl font-semibold tabular-nums text-[color:var(--foreground)] sm:text-4xl">
                    {organizers.length}
                  </p>
                </div>
              </div>

              <div className="flex flex-wrap items-center gap-2 text-sm text-[color:var(--muted)]">
                <span className="font-medium text-[color:var(--foreground)]">
                  Populärt just nu:
                </span>
                {featuredCategories.length > 0 ? (
                  featuredCategories.map((category) => (
                    <span
                      key={category}
                      className="rounded-full border border-[color:var(--border)] bg-[color:var(--surface)] px-3 py-1.5 text-[color:var(--foreground)] shadow-sm"
                    >
                      {category}
                    </span>
                  ))
                ) : (
                  <span className="rounded-full border border-[color:var(--border)] bg-[color:var(--surface)] px-3 py-1.5 shadow-sm">
                    Nya aktiviteter laddas in löpande
                  </span>
                )}
              </div>

              <div className="flex flex-wrap gap-3">
                <a
                  href="#utforska"
                  className="btn-primary inline-flex px-6 py-2.5"
                >
                  Börja utforska
                </a>
                <a href="#aktiviteter" className="btn-ghost inline-flex px-6 py-2.5">
                  Se alla aktiviteter
                </a>
              </div>
            </div>

            <HeroCollage
              activities={activities}
              openActivitiesCount={openActivitiesCount}
              freeActivitiesCount={freeActivitiesCount}
            />
          </div>
        </section>

        {errorMessage ? (
          <section className="alert-banner px-5 py-4 text-sm" role="alert">
            <p className="font-semibold">Kunde inte hämta aktiviteter just nu.</p>
            <p className="mt-2 leading-relaxed">
              {errorMessage} Starta <code>Barnaktiv.API</code> eller sätt{" "}
              <code>BARNAKTIV_API_BASE_URL</code> till rätt backendadress.
            </p>
          </section>
        ) : null}

        <section className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
          <div className="card-surface rounded-[1.75rem] p-6 sm:p-8">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--sage)]">
              Enklare att välja rätt
            </p>
            <h2 className="font-display mt-4 text-2xl font-semibold tracking-tight text-[color:var(--foreground)] sm:text-3xl">
              En startsida som inspirerar innan du filtrerar.
            </h2>
            <p className="mt-4 max-w-2xl text-base leading-relaxed text-[color:var(--muted)]">
              I stället för en torr lista får du en tydlig översikt med bilder,
              siffror och snabbvägar till det som betyder mest: plats, ålder och om
              det fortfarande går att anmäla sig.
            </p>
          </div>

          <div className="rounded-[1.75rem] border border-[color:var(--border)] bg-[color:var(--surface)] p-6 shadow-[var(--card-shadow)] sm:p-8">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--sage)]">
              Lokalt och levande
            </p>
            <div className="mt-4 flex flex-wrap gap-2">
              {featuredCities.length > 0 ? (
                featuredCities.map((city) => (
                  <span
                    key={city}
                    className="rounded-full border border-[color:var(--border)] bg-[color:var(--surface-strong)] px-3 py-2 text-sm font-medium text-[color:var(--foreground)] shadow-sm"
                  >
                    {city}
                  </span>
                ))
              ) : (
                <span className="rounded-full border border-[color:var(--border)] bg-[color:var(--surface-strong)] px-3 py-2 text-sm font-medium shadow-sm">
                  Fler städer fylls på när datan laddas
                </span>
              )}
            </div>

            <div className="mt-6 grid gap-3 sm:grid-cols-2">
              <div className="rounded-xl border border-[color:var(--border)] bg-[color:var(--surface-strong)] p-4">
                <p className="text-xs font-semibold uppercase tracking-[0.14em] text-[color:var(--muted)]">
                  För familjer
                </p>
                <p className="mt-2 text-sm leading-relaxed text-[color:var(--foreground)]">
                  Hitta aktiviteter som passar barnets ålder och er vardag.
                </p>
              </div>
              <div className="rounded-xl border border-[color:var(--border)] bg-[color:var(--surface-strong)] p-4">
                <p className="text-xs font-semibold uppercase tracking-[0.14em] text-[color:var(--muted)]">
                  För arrangörer
                </p>
                <p className="mt-2 text-sm leading-relaxed text-[color:var(--foreground)]">
                  Visa utbudet där föräldrar faktiskt letar.
                </p>
              </div>
            </div>
          </div>
        </section>

        <section
          id="utforska"
          aria-busy={isPending}
          className={`card-surface overflow-hidden rounded-[1.75rem] sm:rounded-[2rem] transition-opacity duration-200 ${isPending ? "opacity-[0.92]" : "opacity-100"}`}
        >
          <div className="border-b border-[color:var(--border)] bg-[color:var(--surface)] px-5 py-5 sm:px-7 sm:py-6">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <h2 className="font-display text-2xl font-semibold tracking-tight text-[color:var(--foreground)]">
                  Filtrera smart
                </h2>
                <p className="mt-2 max-w-xl text-sm leading-relaxed text-[color:var(--muted)]">
                  Snäva in efter plats, arrangör, sport, kategori, ålder och pris.
                </p>
              </div>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center" ref={sortMenuRef}>
                <div className="relative">
                  <button
                    type="button"
                    aria-expanded={isSortMenuOpen}
                    aria-haspopup="menu"
                    aria-controls="sort-menu"
                    id="sort-trigger"
                    onClick={() => setIsSortMenuOpen((current) => !current)}
                    className="btn-ghost w-full justify-between sm:w-auto"
                  >
                    Sortera: {selectedSortLabel}
                    <span className="text-[color:var(--muted)]" aria-hidden>
                      ▾
                    </span>
                  </button>

                  {isSortMenuOpen ? (
                    <div
                      id="sort-menu"
                      role="menu"
                      aria-labelledby="sort-trigger"
                      className="absolute right-0 z-30 mt-2 w-full min-w-[16rem] overflow-hidden rounded-2xl border border-[color:var(--border)] bg-[color:var(--surface-strong)] p-1.5 shadow-[var(--card-shadow-hover)] sm:w-64"
                    >
                      <div className="space-y-0.5">
                        {sortOptions.map((sortOption) => {
                          const isSelected = sortOption.value === selectedSort;

                          return (
                            <button
                              key={sortOption.value}
                              type="button"
                              role="menuitem"
                              onClick={() => {
                                setSelectedSort(sortOption.value);
                                setIsSortMenuOpen(false);
                              }}
                              className={`flex w-full items-center justify-between rounded-xl px-3 py-2.5 text-left text-sm transition ${
                                isSelected
                                  ? "bg-[color:var(--sage)] font-semibold text-white"
                                  : "text-[color:var(--foreground)] hover:bg-[color:var(--surface)]"
                              }`}
                            >
                              <span>{sortOption.label}</span>
                              {isSelected ? (
                                <span className="text-xs opacity-90">Vald</span>
                              ) : null}
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  ) : null}
                </div>

                <button type="button" onClick={clearFilters} className="btn-ghost">
                  Rensa filter
                </button>
              </div>
            </div>
          </div>

          <div className="grid gap-4 p-5 sm:grid-cols-2 sm:p-7 xl:grid-cols-[1.7fr_repeat(6,minmax(0,1fr))]">
            <label className="space-y-2">
              <span className="label-barnaktiv">Sök</span>
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Titel, plats, arrangör..."
                className="input-barnaktiv"
              />
            </label>

            <label className="space-y-2">
              <span className="label-barnaktiv">Stad</span>
              <select
                value={selectedCity}
                onChange={(event) => setSelectedCity(event.target.value)}
                className="input-barnaktiv"
              >
                <option value="all">Alla städer</option>
                {cities.map((city) => (
                  <option key={city} value={city}>
                    {city}
                  </option>
                ))}
              </select>
            </label>

            <label className="space-y-2">
              <span className="label-barnaktiv">Arrangör</span>
              <select
                value={selectedOrganizer}
                onChange={(event) => setSelectedOrganizer(event.target.value)}
                className="input-barnaktiv"
              >
                <option value="all">Alla arrangörer</option>
                {organizers.map((organizer) => (
                  <option key={organizer} value={organizer}>
                    {organizer}
                  </option>
                ))}
              </select>
            </label>

            <label className="space-y-2">
              <span className="label-barnaktiv">Sport</span>
              <select
                value={selectedSport}
                onChange={(event) => setSelectedSport(event.target.value)}
                className="input-barnaktiv"
              >
                <option value="all">Alla sporter</option>
                {sports.map((sport) => (
                  <option key={sport} value={sport}>
                    {sport}
                  </option>
                ))}
              </select>
            </label>

            <label className="space-y-2">
              <span className="label-barnaktiv">Kategori</span>
              <select
                value={selectedCategory}
                onChange={(event) => setSelectedCategory(event.target.value)}
                className="input-barnaktiv"
              >
                <option value="all">Alla kategorier</option>
                {categories.map((category) => (
                  <option key={category} value={category}>
                    {category}
                  </option>
                ))}
              </select>
            </label>

            <label className="space-y-2">
              <span className="label-barnaktiv">Ålder</span>
              <select
                value={selectedAgeGroup}
                onChange={(event) => setSelectedAgeGroup(event.target.value as AgeGroup)}
                className="input-barnaktiv"
              >
                {ageGroups.map((ageGroup) => (
                  <option key={ageGroup.value} value={ageGroup.value}>
                    {ageGroup.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="space-y-2">
              <span className="label-barnaktiv">Pris</span>
              <select
                value={selectedPrice}
                onChange={(event) => setSelectedPrice(event.target.value as PriceFilter)}
                className="input-barnaktiv"
              >
                {priceFilters.map((priceFilter) => (
                  <option key={priceFilter.value} value={priceFilter.value}>
                    {priceFilter.label}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </section>

        <section id="aktiviteter" className="space-y-6">
          <div className="flex flex-col gap-3 border-b border-[color:var(--border)] pb-5 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <h2 className="font-display text-2xl font-semibold tracking-tight text-[color:var(--foreground)] sm:text-3xl">
                Aktiviteter att upptäcka
              </h2>
              <p className="mt-2 text-sm text-[color:var(--muted)]">
                {isPending
                  ? "Uppdaterar resultat..."
                  : `${getResultSummary(activities.length)} efter dina val.`}
              </p>
            </div>
            <div className="text-sm text-[color:var(--muted)]">
              <p>
                Sortering:{" "}
                <span className="font-semibold text-[color:var(--foreground)]">
                  {selectedSortLabel}
                </span>
              </p>
              <p className="mt-1 text-[color:var(--muted-foreground)]">
                Kort med bild, pris, ålder och anmälningsläge.
              </p>
            </div>
          </div>

          {activities.length > 0 ? (
            <div className="grid gap-6 sm:gap-7 lg:grid-cols-2 xl:grid-cols-3">
              {activities.map((activity) => (
                <ActivityCard key={activity.id} activity={activity} />
              ))}
            </div>
          ) : (
            <div className="rounded-2xl border border-dashed border-[color:var(--border-strong)] bg-[color:var(--surface-strong)] px-6 py-14 text-center shadow-sm">
              <h3 className="font-display text-xl font-semibold text-[color:var(--foreground)]">
                Inga aktiviteter matchade filtren.
              </h3>
              <p className="mx-auto mt-3 max-w-md text-sm leading-relaxed text-[color:var(--muted)]">
                Rensa filtren eller kontrollera att backend körs om listan är tom.
              </p>
              <button type="button" onClick={clearFilters} className="btn-primary mt-6">
                Rensa alla filter
              </button>
            </div>
          )}
        </section>
      </main>
      <SiteFooter />
    </>
  );
}
