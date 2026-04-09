"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useDeferredValue, useEffect, useState, useTransition } from "react";

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

  useEffect(() => {
    setSearch(initialFilters.search);
    setSelectedCity(initialFilters.city);
    setSelectedOrganizer(initialFilters.organizer);
    setSelectedSport(initialFilters.sport);
    setSelectedCategory(initialFilters.category);
    setSelectedAgeGroup(initialFilters.ageGroup);
    setSelectedPrice(initialFilters.price);
    setSelectedSort(initialFilters.sort);
  }, [initialFilters]);

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

  return (
    <main className="relative mx-auto flex min-h-screen w-full max-w-[86rem] flex-col gap-8 overflow-hidden px-4 py-6 sm:px-6 lg:px-8 lg:py-10">
      <div className="pointer-events-none absolute inset-x-0 top-0 -z-10 h-[30rem] bg-[linear-gradient(180deg,rgba(255,255,255,0.35),transparent)]" />

      <section className="relative overflow-hidden rounded-[2.8rem] border border-white/70 bg-[#fff9f2] px-6 py-7 shadow-[0_30px_90px_-54px_rgba(15,34,24,0.42)] sm:px-8 sm:py-10">
        <div className="relative grid gap-8 lg:grid-cols-[1.05fr_0.95fr] lg:items-center">
          <div className="space-y-7">
            <span className="inline-flex rounded-full border border-white/70 bg-white px-4 py-2 text-xs font-semibold uppercase tracking-[0.24em] text-[color:var(--accent-strong)] shadow-sm">
              Barnaktiviteter samlade på ett ställe
            </span>

            <div className="space-y-5">
              <h1 className="max-w-3xl text-4xl font-semibold tracking-tight text-[color:var(--foreground)] sm:text-5xl xl:text-6xl">
                Ge varje ledig dag något att längta till.
              </h1>
              <p className="max-w-2xl text-base leading-8 text-[color:var(--muted)] sm:text-lg">
                Barnaktiv samlar prova-på-pass, lovaktiviteter, kurser och
                föreningsträffar så att du snabbt hittar rätt aktivitet för ditt
                barn. Filtrera på stad, ålder, pris och anmälan utan att hoppa
                mellan olika sidor.
              </p>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <div className="rounded-[1.75rem] bg-[color:var(--foreground)] px-5 py-4 text-[color:var(--background)] shadow-[0_24px_60px_-38px_rgba(15,34,24,0.7)]">
                <div className="text-[0.72rem] uppercase tracking-[0.18em] text-white/70">
                  Aktiviteter
                </div>
                <div className="mt-3 text-4xl font-semibold">{activities.length}</div>
              </div>
              <div className="rounded-[1.75rem] border border-[color:var(--border)] bg-white px-5 py-4">
                <div className="text-[0.72rem] uppercase tracking-[0.18em] text-[color:var(--muted)]">
                  Städer
                </div>
                <div className="mt-3 text-4xl font-semibold text-[color:var(--foreground)]">
                  {cities.length}
                </div>
              </div>
              <div className="rounded-[1.75rem] border border-[color:var(--border)] bg-white px-5 py-4">
                <div className="text-[0.72rem] uppercase tracking-[0.18em] text-[color:var(--muted)]">
                  Arrangörer
                </div>
                <div className="mt-3 text-4xl font-semibold text-[color:var(--foreground)]">
                  {organizers.length}
                </div>
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
                    className="rounded-full border border-white/70 bg-white px-3 py-1.5 shadow-sm"
                  >
                    {category}
                  </span>
                ))
              ) : (
                <span className="rounded-full border border-white/70 bg-white px-3 py-1.5 shadow-sm">
                  Nya aktiviteter laddas in löpande
                </span>
              )}
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
        <section className="rounded-[2rem] border border-amber-300 bg-amber-50/90 px-5 py-4 text-sm text-amber-950 shadow-sm">
          <p className="font-semibold">
            Aktiviteterna kunde inte hämtas från backend just nu.
          </p>
          <p className="mt-1">
            {errorMessage} Starta <code>Barnaktiv.API</code> eller sätt{" "}
            <code>BARNAKTIV_API_BASE_URL</code> till rätt backendadress.
          </p>
        </section>
      ) : null}

      <section className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
        <div className="rounded-[2.25rem] border border-[color:var(--border)] bg-white p-6 shadow-[var(--card-shadow)] shadow-black/5 sm:p-7">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--accent-strong)]">
            Enklare att välja rätt
          </p>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-[color:var(--foreground)]">
            En startsida som inspirerar innan du ens börjar filtrera.
          </h2>
          <p className="mt-4 max-w-2xl text-base leading-7 text-[color:var(--muted)]">
            I stället för en torr lista får du en varm, visuell översikt med
            riktiga aktivitetsbilder, tydliga siffror och snabbvägar till det
            som faktiskt betyder något för familjer: plats, ålder och om det
            fortfarande finns chans att anmäla sig.
          </p>
        </div>

        <div className="rounded-[2.25rem] border border-[color:var(--border)] bg-[#fffaf4] p-6 shadow-[var(--card-shadow)] shadow-black/5 sm:p-7">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--accent-strong)]">
            Lokalt och levande
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            {featuredCities.length > 0 ? (
              featuredCities.map((city) => (
                <span
                  key={city}
                  className="rounded-full bg-white px-3 py-2 text-sm font-medium text-[color:var(--foreground)] shadow-sm"
                >
                  {city}
                </span>
              ))
            ) : (
              <span className="rounded-full bg-white px-3 py-2 text-sm font-medium text-[color:var(--foreground)] shadow-sm">
                Fler städer fylls på när datan laddas
              </span>
            )}
          </div>

          <div className="mt-6 grid gap-3 sm:grid-cols-2">
            <div className="rounded-[1.5rem] border border-white/70 bg-white p-4">
              <p className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
                För familjer
              </p>
              <p className="mt-2 text-sm leading-6 text-[color:var(--foreground)]">
                Hitta snabbt aktiviteter som passar barnets ålder och er vardag.
              </p>
            </div>
            <div className="rounded-[1.5rem] border border-white/70 bg-white p-4">
              <p className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
                För arrangörer
              </p>
              <p className="mt-2 text-sm leading-6 text-[color:var(--foreground)]">
                Visa upp utbudet i en miljö som gör det enkelt att bli vald.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section
        id="utforska"
        className="rounded-[2.35rem] border border-[color:var(--border)] bg-white p-5 shadow-[var(--card-shadow)] shadow-black/5 sm:p-6"
      >
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <h2 className="text-2xl font-semibold tracking-tight text-[color:var(--foreground)]">
              Filtrera smart
            </h2>
            <p className="mt-2 text-sm leading-6 text-[color:var(--muted)]">
              Snäva in listan efter plats, arrangör, sport, kategori, ålder och
              pris så du slipper gissa dig fram.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="relative">
              <button
                type="button"
                aria-expanded={isSortMenuOpen}
                aria-haspopup="menu"
                onClick={() => setIsSortMenuOpen((current) => !current)}
                className="rounded-full border border-[color:var(--border)] bg-white px-4 py-2 text-sm font-semibold text-[color:var(--foreground)] transition hover:bg-[#fffaf5]"
              >
                Sortera: {selectedSortLabel}
              </button>

              {isSortMenuOpen ? (
                <div className="absolute right-0 z-20 mt-2 w-64 overflow-hidden rounded-[1.5rem] border border-[color:var(--border)] bg-white p-2 shadow-[0_24px_60px_-36px_rgba(15,34,24,0.36)]">
                  <div className="space-y-1">
                    {sortOptions.map((sortOption) => {
                      const isSelected = sortOption.value === selectedSort;

                      return (
                        <button
                          key={sortOption.value}
                          type="button"
                          onClick={() => {
                            setSelectedSort(sortOption.value);
                            setIsSortMenuOpen(false);
                          }}
                          className={`flex w-full items-center justify-between rounded-[1.15rem] px-3 py-2.5 text-left text-sm transition ${
                            isSelected
                              ? "bg-[color:var(--foreground)] font-semibold text-[color:var(--background)]"
                              : "text-[color:var(--foreground)] hover:bg-[#fffaf5]"
                          }`}
                        >
                          <span>{sortOption.label}</span>
                          <span className="text-xs">
                            {isSelected ? "Vald" : ""}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </div>
              ) : null}
            </div>

            <button
              type="button"
              onClick={clearFilters}
              className="rounded-full border border-[color:var(--border)] bg-white px-4 py-2 text-sm font-semibold text-[color:var(--foreground)] transition hover:bg-[#fffaf5]"
            >
              Rensa filter
            </button>
          </div>
        </div>

        <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-[1.7fr_repeat(6,minmax(0,1fr))]">
          <label className="space-y-2">
            <span className="text-sm font-medium text-[color:var(--foreground)]">
              Sök
            </span>
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Titel, plats, arrangör..."
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none ring-0 transition placeholder:text-[color:var(--muted)] focus:border-[color:var(--accent)]"
            />
          </label>

          <label className="space-y-2">
            <span className="text-sm font-medium text-[color:var(--foreground)]">
              Stad
            </span>
            <select
              value={selectedCity}
              onChange={(event) => setSelectedCity(event.target.value)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
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
            <span className="text-sm font-medium text-[color:var(--foreground)]">
              Arrangör
            </span>
            <select
              value={selectedOrganizer}
              onChange={(event) => setSelectedOrganizer(event.target.value)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
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
            <span className="text-sm font-medium text-[color:var(--foreground)]">
              Sport
            </span>
            <select
              value={selectedSport}
              onChange={(event) => setSelectedSport(event.target.value)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
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
            <span className="text-sm font-medium text-[color:var(--foreground)]">
              Kategori
            </span>
            <select
              value={selectedCategory}
              onChange={(event) => setSelectedCategory(event.target.value)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
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
            <span className="text-sm font-medium text-[color:var(--foreground)]">
              Ålder
            </span>
            <select
              value={selectedAgeGroup}
              onChange={(event) => setSelectedAgeGroup(event.target.value as AgeGroup)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
            >
              {ageGroups.map((ageGroup) => (
                <option key={ageGroup.value} value={ageGroup.value}>
                  {ageGroup.label}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-2">
            <span className="text-sm font-medium text-[color:var(--foreground)]">
              Pris
            </span>
            <select
              value={selectedPrice}
              onChange={(event) => setSelectedPrice(event.target.value as PriceFilter)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
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

      <section id="aktiviteter" className="space-y-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2 className="text-2xl font-semibold tracking-tight text-[color:var(--foreground)]">
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
              <span className="font-medium text-[color:var(--foreground)]">
                {selectedSortLabel}
              </span>
            </p>
            <p className="mt-1">Visar kort med bild, pris, ålder och anmälningsläge.</p>
          </div>
        </div>

        {activities.length > 0 ? (
          <div className="grid gap-5 lg:grid-cols-2 xl:grid-cols-3">
            {activities.map((activity) => (
              <ActivityCard key={activity.id} activity={activity} />
            ))}
          </div>
        ) : (
          <div className="rounded-[2rem] border border-dashed border-[color:var(--border)] bg-white px-6 py-12 text-center">
            <h3 className="text-xl font-semibold text-[color:var(--foreground)]">
              Inga aktiviteter matchade filtren.
            </h3>
            <p className="mt-3 text-sm leading-6 text-[color:var(--muted)]">
              Rensa filtren eller starta backend om API:t inte levererar data än.
            </p>
          </div>
        )}
      </section>
    </main>
  );
}
