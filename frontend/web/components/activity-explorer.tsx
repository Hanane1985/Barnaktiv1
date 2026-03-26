"use client";

import Link from "next/link";
import { useDeferredValue, useState } from "react";

import type { Activity } from "@/lib/activities";

type ActivityExplorerProps = {
  activities: Activity[];
  errorMessage?: string;
};

type AgeGroup = "all" | "0-3" | "4-6" | "7-9" | "10-12" | "13+";
type PriceFilter = "all" | "free" | "paid";
type RegistrationStatus = "Unknown" | "Upcoming" | "Open" | "Closed" | "Full";

const ageGroups: {
  value: AgeGroup;
  label: string;
  min?: number;
  max?: number;
}[] = [
  { value: "all", label: "All ages" },
  { value: "0-3", label: "0-3 years", min: 0, max: 3 },
  { value: "4-6", label: "4-6 years", min: 4, max: 6 },
  { value: "7-9", label: "7-9 years", min: 7, max: 9 },
  { value: "10-12", label: "10-12 years", min: 10, max: 12 },
  { value: "13+", label: "13+ years", min: 13, max: 99 },
];

const priceFilters: { value: PriceFilter; label: string }[] = [
  { value: "all", label: "Any price" },
  { value: "free", label: "Free only" },
  { value: "paid", label: "Paid only" },
];

const dateFormatter = new Intl.DateTimeFormat("sv-SE", {
  weekday: "short",
  day: "numeric",
  month: "short",
});

const dayFormatter = new Intl.DateTimeFormat("sv-SE", {
  day: "2-digit",
});

const monthFormatter = new Intl.DateTimeFormat("sv-SE", {
  month: "short",
});

const priceFormatter = new Intl.NumberFormat("sv-SE", {
  style: "currency",
  currency: "SEK",
  maximumFractionDigits: 0,
});

const dateTimeFormatter = new Intl.DateTimeFormat("sv-SE", {
  dateStyle: "medium",
  timeStyle: "short",
});

function formatPrice(price: number) {
  return price <= 0 ? "Free" : priceFormatter.format(price);
}

function formatAgeRange(activity: Activity) {
  if (activity.ageFrom <= 0 && activity.ageTo <= 0) {
    return "Age not specified";
  }

  if (activity.ageFrom <= 0 && activity.ageTo >= 99) {
    return "All ages";
  }

  if (activity.ageTo >= 99) {
    return `${activity.ageFrom}+ years`;
  }

  if (activity.ageFrom === activity.ageTo) {
    return `${activity.ageFrom} years`;
  }

  return `${activity.ageFrom}-${activity.ageTo} years`;
}

function matchesAgeGroup(activity: Activity, selectedAgeGroup: AgeGroup) {
  if (selectedAgeGroup === "all") {
    return true;
  }

  const ageGroup = ageGroups.find((item) => item.value === selectedAgeGroup);

  if (!ageGroup) {
    return true;
  }

  const minimumAge = ageGroup.min ?? 0;
  const maximumAge = ageGroup.max ?? 99;

  return activity.ageFrom <= maximumAge && activity.ageTo >= minimumAge;
}

function getResultSummary(count: number) {
  if (count === 0) {
    return "No matching activities";
  }

  if (count === 1) {
    return "1 activity";
  }

  return `${count} activities`;
}

function formatRegistrationSummary(activity: Activity) {
  const registrationStatus = activity.registrationStatus as RegistrationStatus;
  const registrationOpenAt = activity.registrationOpenAt
    ? new Date(activity.registrationOpenAt)
    : null;
  const registrationCloseAt = activity.registrationCloseAt
    ? new Date(activity.registrationCloseAt)
    : null;

  switch (registrationStatus) {
    case "Open":
      return registrationCloseAt
        ? `Registration open until ${dateTimeFormatter.format(registrationCloseAt)}`
        : "Registration open now";
    case "Upcoming":
      return registrationOpenAt
        ? `Registration opens ${dateTimeFormatter.format(registrationOpenAt)}`
        : "Registration opens soon";
    case "Closed":
      return registrationCloseAt
        ? `Registration closed ${dateTimeFormatter.format(registrationCloseAt)}`
        : "Registration closed";
    case "Full":
      return "Currently full";
    default:
      return null;
  }
}

function getRegistrationBadgeClassName(status: RegistrationStatus) {
  switch (status) {
    case "Open":
      return "bg-emerald-100 text-emerald-900";
    case "Upcoming":
      return "bg-sky-100 text-sky-900";
    case "Closed":
      return "bg-stone-200 text-stone-800";
    case "Full":
      return "bg-amber-100 text-amber-950";
    default:
      return "bg-white/80 text-[color:var(--muted)]";
  }
}

function getPrimaryLink(activity: Activity) {
  if (activity.signupUrl) {
    return {
      href: activity.signupUrl,
      label: "Register",
    };
  }

  if (activity.websiteUrl) {
    return {
      href: activity.websiteUrl,
      label: "Visit site",
    };
  }

  return null;
}

function ActivityCard({ activity }: { activity: Activity }) {
  const activityDate = new Date(activity.date);
  const categoryLabel = activity.category || "General";
  const sportLabel = activity.sport || null;
  const cityLabel = activity.city || "Unknown city";
  const organizerLabel = activity.organizer || "Organizer to be confirmed";
  const sourceLabel = activity.source || "Manual import";
  const registrationStatus = activity.registrationStatus as RegistrationStatus;
  const registrationSummary = formatRegistrationSummary(activity);
  const primaryLink = getPrimaryLink(activity);
  const dateLabel =
    activity.listingType === "Program" && activity.registrationOpenAt
      ? "Primary date"
      : "Date";

  return (
    <article className="flex h-full flex-col rounded-[2rem] border border-[color:var(--border)] bg-[color:var(--surface-strong)] p-5 shadow-[var(--card-shadow)] shadow-black/5">
      <div className="flex items-start justify-between gap-4">
        <div className="space-y-3">
          <div className="flex flex-wrap gap-2">
            {sportLabel ? (
              <span className="rounded-full bg-[color:var(--foreground)] px-3 py-1 text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--background)]">
                {sportLabel}
              </span>
            ) : null}
            <span className="rounded-full bg-[color:var(--accent-soft)] px-3 py-1 text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--accent-strong)]">
              {categoryLabel}
            </span>
            {registrationSummary ? (
              <span
                className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] ${getRegistrationBadgeClassName(
                  registrationStatus,
                )}`}
              >
                {registrationStatus}
              </span>
            ) : null}
            <span className="rounded-full border border-[color:var(--border)] px-3 py-1 text-xs font-medium text-[color:var(--muted)]">
              {cityLabel}
            </span>
          </div>
          <div className="space-y-2">
            <h2 className="text-xl font-semibold tracking-tight text-[color:var(--foreground)]">
              {activity.title}
            </h2>
            <p className="text-sm leading-6 text-[color:var(--muted)]">
              {activity.description || "No description available yet."}
            </p>
          </div>
        </div>

        <div className="min-w-[5rem] rounded-[1.5rem] bg-[color:var(--foreground)] px-3 py-4 text-center text-[color:var(--background)]">
          <div className="text-3xl font-semibold leading-none">
            {dayFormatter.format(activityDate)}
          </div>
          <div className="mt-1 text-xs uppercase tracking-[0.3em] text-white/70">
            {monthFormatter.format(activityDate)}
          </div>
        </div>
      </div>

      <dl className="mt-6 grid gap-3 text-sm text-[color:var(--foreground)] sm:grid-cols-2">
        <div className="rounded-2xl bg-white/70 px-4 py-3">
          <dt className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
            {dateLabel}
          </dt>
          <dd className="mt-1 font-medium">{dateFormatter.format(activityDate)}</dd>
        </div>
        <div className="rounded-2xl bg-white/70 px-4 py-3">
          <dt className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
            Age
          </dt>
          <dd className="mt-1 font-medium">{formatAgeRange(activity)}</dd>
        </div>
        <div className="rounded-2xl bg-white/70 px-4 py-3">
          <dt className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
            Place
          </dt>
          <dd className="mt-1 font-medium">
            {activity.location || "Location to be confirmed"}
          </dd>
        </div>
        <div className="rounded-2xl bg-white/70 px-4 py-3">
          <dt className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
            Price
          </dt>
          <dd className="mt-1 font-medium">{formatPrice(activity.price)}</dd>
        </div>
        {registrationSummary ? (
          <div className="rounded-2xl bg-white/70 px-4 py-3 sm:col-span-2">
            <dt className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
              Registration
            </dt>
            <dd className="mt-1 font-medium">{registrationSummary}</dd>
          </div>
        ) : null}
      </dl>

      <div className="mt-6 flex flex-wrap items-center gap-3 text-sm text-[color:var(--muted)]">
        <span>{organizerLabel}</span>
        <span className="h-1 w-1 rounded-full bg-current" />
        <span>{sourceLabel}</span>
      </div>

      <div className="mt-6 flex items-center justify-between gap-3">
        <p className="text-sm text-[color:var(--muted)]">
          Added {new Date(activity.createdAt).toLocaleDateString("sv-SE")}
        </p>
        {primaryLink ? (
          <Link
            href={primaryLink.href}
            target="_blank"
            rel="noreferrer"
            className="rounded-full bg-[color:var(--accent)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[color:var(--accent-strong)]"
          >
            {primaryLink.label}
          </Link>
        ) : null}
      </div>
    </article>
  );
}

export function ActivityExplorer({
  activities,
  errorMessage,
}: ActivityExplorerProps) {
  const [search, setSearch] = useState("");
  const [selectedCity, setSelectedCity] = useState("all");
  const [selectedSport, setSelectedSport] = useState("all");
  const [selectedCategory, setSelectedCategory] = useState("all");
  const [selectedAgeGroup, setSelectedAgeGroup] = useState<AgeGroup>("all");
  const [selectedPrice, setSelectedPrice] = useState<PriceFilter>("all");
  const deferredSearch = useDeferredValue(search);
  const searchTerm = deferredSearch.trim().toLowerCase();

  const cities = Array.from(new Set(activities.map((activity) => activity.city).filter(Boolean))).sort(
    (left, right) => left.localeCompare(right),
  );
  const sports = Array.from(new Set(activities.map((activity) => activity.sport).filter(Boolean))).sort(
    (left, right) => left.localeCompare(right),
  );
  const categories = Array.from(
    new Set(activities.map((activity) => activity.category).filter(Boolean)),
  ).sort((left, right) => left.localeCompare(right));

  const filteredActivities = activities.filter((activity) => {
    const matchesSearch =
      searchTerm.length === 0 ||
      [
        activity.title,
        activity.description,
        activity.organizer,
        activity.location,
        activity.city,
        activity.sport,
        activity.category,
      ]
        .join(" ")
        .toLowerCase()
        .includes(searchTerm);

    const matchesCity = selectedCity === "all" || activity.city === selectedCity;
    const matchesSport = selectedSport === "all" || activity.sport === selectedSport;
    const matchesCategory =
      selectedCategory === "all" || activity.category === selectedCategory;
    const matchesPrice =
      selectedPrice === "all" ||
      (selectedPrice === "free" ? activity.price <= 0 : activity.price > 0);

    return (
      matchesSearch &&
      matchesCity &&
      matchesSport &&
      matchesCategory &&
      matchesPrice &&
      matchesAgeGroup(activity, selectedAgeGroup)
    );
  });

  const clearFilters = () => {
    setSearch("");
    setSelectedCity("all");
    setSelectedSport("all");
    setSelectedCategory("all");
    setSelectedAgeGroup("all");
    setSelectedPrice("all");
  };

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-7xl flex-col gap-8 px-4 py-6 sm:px-6 lg:px-8 lg:py-10">
      <section className="overflow-hidden rounded-[2.5rem] border border-white/50 bg-[linear-gradient(135deg,rgba(255,248,235,0.95),rgba(247,226,212,0.92))] p-6 shadow-[var(--card-shadow)] shadow-black/5 sm:p-8">
        <div className="grid gap-8 lg:grid-cols-[1.6fr_1fr] lg:items-end">
          <div className="space-y-5">
            <span className="inline-flex rounded-full bg-white/80 px-4 py-2 text-xs font-semibold uppercase tracking-[0.24em] text-[color:var(--accent-strong)]">
              Live API connection
            </span>
            <div className="space-y-4">
              <h1 className="max-w-3xl text-4xl font-semibold tracking-tight text-[color:var(--foreground)] sm:text-5xl">
                Explore kids activities from the Barnaktiv backend.
              </h1>
              <p className="max-w-2xl text-base leading-7 text-[color:var(--muted)] sm:text-lg">
                lets you filter by search, city, category, age, and price.
              </p>
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-3 lg:grid-cols-1">
            <div className="rounded-[1.75rem] bg-[color:var(--foreground)] px-5 py-4 text-[color:var(--background)]">
              <div className="text-sm uppercase tracking-[0.18em] text-white/70">
                Available now
              </div>
              <div className="mt-3 text-4xl font-semibold">{activities.length}</div>
            </div>
            <div className="rounded-[1.75rem] border border-[color:var(--border)] bg-white/80 px-5 py-4">
              <div className="text-sm uppercase tracking-[0.18em] text-[color:var(--muted)]">
                Cities
              </div>
              <div className="mt-3 text-4xl font-semibold text-[color:var(--foreground)]">
                {cities.length}
              </div>
            </div>
            <div className="rounded-[1.75rem] border border-[color:var(--border)] bg-white/80 px-5 py-4">
              <div className="text-sm uppercase tracking-[0.18em] text-[color:var(--muted)]">
                Categories
              </div>
              <div className="mt-3 text-4xl font-semibold text-[color:var(--foreground)]">
                {categories.length}
              </div>
            </div>
          </div>
        </div>
      </section>

      {errorMessage ? (
        <section className="rounded-[2rem] border border-amber-300 bg-amber-50 px-5 py-4 text-sm text-amber-950">
          <p className="font-semibold">Activities could not be loaded from the backend.</p>
          <p className="mt-1">
            {errorMessage} Start <code>Barnaktiv.API</code> or set <code>BARNAKTIV_API_BASE_URL</code> to the correct backend URL.
          </p>
        </section>
      ) : null}

      <section className="rounded-[2.25rem] border border-[color:var(--border)] bg-[color:var(--surface)] p-5 shadow-[var(--card-shadow)] shadow-black/5 sm:p-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <h2 className="text-2xl font-semibold tracking-tight text-[color:var(--foreground)]">
              Filters
            </h2>
            <p className="mt-2 text-sm text-[color:var(--muted)]">
              Narrow the activity list before adding more ingestion sources.
            </p>
          </div>
          <button
            type="button"
            onClick={clearFilters}
            className="rounded-full border border-[color:var(--border)] px-4 py-2 text-sm font-semibold text-[color:var(--foreground)] transition hover:bg-white/80"
          >
            Clear filters
          </button>
        </div>

        <div className="mt-6 grid gap-4 lg:grid-cols-[1.7fr_repeat(5,minmax(0,1fr))]">
          <label className="space-y-2">
            <span className="text-sm font-medium text-[color:var(--foreground)]">Search</span>
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Title, place, organizer..."
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none ring-0 transition placeholder:text-[color:var(--muted)] focus:border-[color:var(--accent)]"
            />
          </label>

          <label className="space-y-2">
            <span className="text-sm font-medium text-[color:var(--foreground)]">City</span>
            <select
              value={selectedCity}
              onChange={(event) => setSelectedCity(event.target.value)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
            >
              <option value="all">All cities</option>
              {cities.map((city) => (
                <option key={city} value={city}>
                  {city}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-2">
            <span className="text-sm font-medium text-[color:var(--foreground)]">Sport</span>
            <select
              value={selectedSport}
              onChange={(event) => setSelectedSport(event.target.value)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
            >
              <option value="all">All sports</option>
              {sports.map((sport) => (
                <option key={sport} value={sport}>
                  {sport}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-2">
            <span className="text-sm font-medium text-[color:var(--foreground)]">Category</span>
            <select
              value={selectedCategory}
              onChange={(event) => setSelectedCategory(event.target.value)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
            >
              <option value="all">All categories</option>
              {categories.map((category) => (
                <option key={category} value={category}>
                  {category}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-2">
            <span className="text-sm font-medium text-[color:var(--foreground)]">Age</span>
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
            <span className="text-sm font-medium text-[color:var(--foreground)]">Price</span>
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

      <section className="space-y-5">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2 className="text-2xl font-semibold tracking-tight text-[color:var(--foreground)]">
              Activity cards
            </h2>
            <p className="mt-2 text-sm text-[color:var(--muted)]">
              {getResultSummary(filteredActivities.length)} after filtering.
            </p>
          </div>
          <p className="text-sm text-[color:var(--muted)]">
            Source ready for incremental scraping expansion.
          </p>
        </div>

        {filteredActivities.length > 0 ? (
          <div className="grid gap-5 lg:grid-cols-2 xl:grid-cols-3">
            {filteredActivities.map((activity) => (
              <ActivityCard key={activity.id} activity={activity} />
            ))}
          </div>
        ) : (
          <div className="rounded-[2rem] border border-dashed border-[color:var(--border)] bg-white/60 px-6 py-12 text-center">
            <h3 className="text-xl font-semibold text-[color:var(--foreground)]">
              No activities matched the current filters.
            </h3>
            <p className="mt-3 text-sm leading-6 text-[color:var(--muted)]">
              Reset the filters or start the backend if the API is not returning data yet.
            </p>
          </div>
        )}
      </section>
    </main>
  );
}
