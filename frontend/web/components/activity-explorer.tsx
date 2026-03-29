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

const detailedDateFormatter = new Intl.DateTimeFormat("sv-SE", {
  weekday: "long",
  day: "numeric",
  month: "long",
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

const timeFormatter = new Intl.DateTimeFormat("sv-SE", {
  hour: "2-digit",
  minute: "2-digit",
});

function formatPrice(price: number) {
  return price <= 0 ? "Gratis" : priceFormatter.format(price);
}

function formatAgeRange(activity: Activity) {
  if (activity.ageFrom <= 0 && activity.ageTo <= 0) {
    return "Ej angivet";
  }

  if (activity.ageFrom <= 0 && activity.ageTo >= 99) {
    return "Alla åldrar";
  }

  if (activity.ageTo >= 99) {
    return `${activity.ageFrom}+ år`;
  }

  if (activity.ageFrom === activity.ageTo) {
    return `${activity.ageFrom} år`;
  }

  return `${activity.ageFrom}-${activity.ageTo} år`;
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
        ? `Öppen till ${dateTimeFormatter.format(registrationCloseAt)}`
        : "Öppen nu";
    case "Upcoming":
      return registrationOpenAt
        ? `Öppnar ${dateTimeFormatter.format(registrationOpenAt)}`
        : "Öppnar snart";
    case "Closed":
      return registrationCloseAt
        ? `Stängde ${dateTimeFormatter.format(registrationCloseAt)}`
        : "Stängd";
    case "Full":
      return "Fullbokad";
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
      label: "Anmäl dig",
    };
  }

  if (activity.websiteUrl) {
    return {
      href: activity.websiteUrl,
      label: "Besök sida",
    };
  }

  return null;
}

function hasSpecifiedTime(date: Date) {
  return date.getHours() !== 0 || date.getMinutes() !== 0;
}

function capitalizeFirstLetter(value: string) {
  if (!value) {
    return value;
  }

  return value.charAt(0).toUpperCase() + value.slice(1);
}

function ActivityCard({ activity }: { activity: Activity }) {
  const activityDate = new Date(activity.date);
  const [imageFailed, setImageFailed] = useState(false);
  const categoryLabel = activity.category || "General";
  const sportLabel = activity.sport || null;
  const cityLabel = activity.city || "Stad saknas";
  const organizerLabel = activity.organizer || "Arrangör kommer snart";
  const sourceLabel = activity.source || "Manuell import";
  const registrationStatus = activity.registrationStatus as RegistrationStatus;
  const registrationSummary = formatRegistrationSummary(activity);
  const primaryLink = getPrimaryLink(activity);
  const imageUrl = activity.imageUrl?.trim() || "";
  const showImage = imageUrl.length > 0 && !imageFailed;
  const formattedDate = capitalizeFirstLetter(
    detailedDateFormatter.format(activityDate),
  );
  const timeLabel = hasSpecifiedTime(activityDate)
    ? timeFormatter.format(activityDate)
    : "Tid ej angiven";

  return (
    <article className="group flex h-full flex-col overflow-hidden rounded-[2rem] border border-[color:var(--border)] bg-[color:var(--surface-strong)] shadow-[var(--card-shadow)] shadow-black/5">
      <div className="relative aspect-[16/10] overflow-hidden border-b border-[color:var(--border)] bg-[linear-gradient(135deg,#f4b18f,#f8e9d9_55%,#fffaf4)]">
        {showImage ? (
          // The scraped image hosts vary, so keep a plain img here instead of broad remote image allow-lists.
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={imageUrl}
            alt={activity.title}
            className="h-full w-full object-cover transition duration-500 group-hover:scale-[1.02]"
            loading="lazy"
            onError={() => setImageFailed(true)}
          />
        ) : (
          <div className="flex h-full w-full flex-col justify-end bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.95),transparent_38%),linear-gradient(135deg,rgba(223,105,55,0.3),rgba(247,220,205,0.88)_60%,rgba(255,253,248,1))] p-5">
            <div className="max-w-[14rem] rounded-[1.5rem] bg-white/78 p-4 backdrop-blur-sm">
              <p className="text-xs font-semibold uppercase tracking-[0.22em] text-[color:var(--accent-strong)]">
                Barnaktiv
              </p>
              <p className="mt-2 text-base font-semibold leading-5 text-[color:var(--foreground)]">
                {activity.location || cityLabel}
              </p>
            </div>
          </div>
        )}

        <div className="absolute inset-x-0 top-0 flex items-start justify-between gap-3 p-4">
          <div className="flex flex-wrap gap-2">
            {sportLabel ? (
              <span className="rounded-full bg-[color:var(--foreground)] px-3 py-1 text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--background)] shadow-sm">
                {sportLabel}
              </span>
            ) : null}
            <span className="rounded-full bg-[color:var(--accent-soft)] px-3 py-1 text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--accent-strong)] shadow-sm">
              {categoryLabel}
            </span>
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            <span className="rounded-full bg-white/88 px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-[color:var(--foreground)] shadow-sm backdrop-blur-sm">
              {formatPrice(activity.price)}
            </span>
            {registrationSummary ? (
              <span
                className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] shadow-sm backdrop-blur-sm ${getRegistrationBadgeClassName(
                  registrationStatus,
                )}`}
              >
                {registrationStatus}
              </span>
            ) : null}
          </div>
        </div>
      </div>

      <div className="flex flex-1 flex-col p-5">
        <div className="space-y-4">
          <div className="space-y-2">
            <h2 className="text-[1.35rem] font-semibold leading-tight tracking-tight text-[color:var(--foreground)]">
              {activity.title}
            </h2>
            <p className="text-sm font-medium text-[color:var(--muted)]">
              {activity.location || "Plats kommer snart"}
            </p>
          </div>

          <dl className="grid gap-3 text-sm text-[color:var(--foreground)] sm:grid-cols-2">
            <div className="rounded-2xl bg-white/70 px-4 py-3">
              <dt className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
                Datum
              </dt>
              <dd className="mt-1 font-medium">{formattedDate}</dd>
            </div>
            <div className="rounded-2xl bg-white/70 px-4 py-3">
              <dt className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
                Tid
              </dt>
              <dd className="mt-1 font-medium">{timeLabel}</dd>
            </div>
          </dl>

          <div className="flex flex-wrap gap-2 text-sm">
            <span className="rounded-full border border-[color:var(--border)] bg-white px-3 py-2 font-medium text-[color:var(--foreground)]">
              Stad: {cityLabel}
            </span>
            <span className="rounded-full border border-[color:var(--border)] bg-white px-3 py-2 font-medium text-[color:var(--foreground)]">
              {"Ålder: "}{formatAgeRange(activity)}
            </span>
            <span className="rounded-full border border-[color:var(--border)] bg-white px-3 py-2 font-medium text-[color:var(--foreground)]">
              Pris: {formatPrice(activity.price)}
            </span>
          </div>

          {registrationSummary ? (
            <div className="rounded-2xl bg-white/70 px-4 py-3 text-sm">
              <p className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
                {"Anmälan"}
              </p>
              <p className="mt-1 font-medium text-[color:var(--foreground)]">
                {registrationSummary}
              </p>
            </div>
          ) : null}
        </div>

        <div className="mt-auto pt-6">
          <div className="flex flex-wrap items-center gap-3 text-sm text-[color:var(--muted)]">
            <span>{organizerLabel}</span>
            <span className="h-1 w-1 rounded-full bg-current" />
            <span>{sourceLabel}</span>
          </div>

          <div className="mt-5 flex items-center justify-between gap-3">
            <p className="text-sm text-[color:var(--muted)]">
              Tillagd {new Date(activity.createdAt).toLocaleDateString("sv-SE")}
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
        </div>
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
  const [selectedOrganizer, setSelectedOrganizer] = useState("all");
  const [selectedSport, setSelectedSport] = useState("all");
  const [selectedCategory, setSelectedCategory] = useState("all");
  const [selectedAgeGroup, setSelectedAgeGroup] = useState<AgeGroup>("all");
  const [selectedPrice, setSelectedPrice] = useState<PriceFilter>("all");
  const deferredSearch = useDeferredValue(search);
  const searchTerm = deferredSearch.trim().toLowerCase();

  const cities = Array.from(new Set(activities.map((activity) => activity.city).filter(Boolean))).sort(
    (left, right) => left.localeCompare(right),
  );
  const organizers = Array.from(
    new Set(activities.map((activity) => activity.organizer).filter(Boolean)),
  ).sort((left, right) => left.localeCompare(right));
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
    const matchesOrganizer =
      selectedOrganizer === "all" || activity.organizer === selectedOrganizer;
    const matchesSport = selectedSport === "all" || activity.sport === selectedSport;
    const matchesCategory =
      selectedCategory === "all" || activity.category === selectedCategory;
    const matchesPrice =
      selectedPrice === "all" ||
      (selectedPrice === "free" ? activity.price <= 0 : activity.price > 0);

    return (
      matchesSearch &&
      matchesCity &&
      matchesOrganizer &&
      matchesSport &&
      matchesCategory &&
      matchesPrice &&
      matchesAgeGroup(activity, selectedAgeGroup)
    );
  });

  const clearFilters = () => {
    setSearch("");
    setSelectedCity("all");
    setSelectedOrganizer("all");
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
                lets you filter by search, city, organizer, category, age, and price.
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

        <div className="mt-6 grid gap-4 lg:grid-cols-[1.7fr_repeat(6,minmax(0,1fr))]">
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
            <span className="text-sm font-medium text-[color:var(--foreground)]">
              Organizer / club
            </span>
            <select
              value={selectedOrganizer}
              onChange={(event) => setSelectedOrganizer(event.target.value)}
              className="w-full rounded-2xl border border-[color:var(--border)] bg-white px-4 py-3 text-sm outline-none transition focus:border-[color:var(--accent)]"
            >
              <option value="all">All organizers</option>
              {organizers.map((organizer) => (
                <option key={organizer} value={organizer}>
                  {organizer}
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
