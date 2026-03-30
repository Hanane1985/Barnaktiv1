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
type FallbackImage = {
  photoSrc: string;
  backupSrc: string;
  label: string;
};

const ageGroups: {
  value: AgeGroup;
  label: string;
  min?: number;
  max?: number;
}[] = [
  { value: "all", label: "Alla åldrar" },
  { value: "0-3", label: "0-3 år", min: 0, max: 3 },
  { value: "4-6", label: "4-6 år", min: 4, max: 6 },
  { value: "7-9", label: "7-9 år", min: 7, max: 9 },
  { value: "10-12", label: "10-12 år", min: 10, max: 12 },
  { value: "13+", label: "13+ år", min: 13, max: 99 },
];

const priceFilters: { value: PriceFilter; label: string }[] = [
  { value: "all", label: "Alla priser" },
  { value: "free", label: "Gratis" },
  { value: "paid", label: "Betalaktiviteter" },
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
    return "Inga aktiviteter matchar";
  }

  if (count === 1) {
    return "1 aktivitet";
  }

  return `${count} aktiviteter`;
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
      label: "Anmäl nu",
    };
  }

  if (activity.websiteUrl) {
    return {
      href: activity.websiteUrl,
      label: "Läs mer",
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

function getCategoryLabels(categoryValue: string) {
  return categoryValue
    .split(",")
    .map((category) => category.trim())
    .filter(Boolean);
}

function normalizeMatchingText(value: string) {
  return value
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");
}

function getFallbackImage(activity?: Activity): FallbackImage {
  const haystack = normalizeMatchingText(
    [
      activity?.sport,
      activity?.category,
      activity?.title,
      activity?.description,
      activity?.location,
      activity?.city,
    ]
      .filter(Boolean)
      .join(" "),
  );

  if (/(fotboll|football|soccer)/.test(haystack)) {
    return {
      photoSrc:
        "https://images.pexels.com/photos/8941573/pexels-photo-8941573.jpeg?auto=compress&cs=tinysrgb&w=1200",
      backupSrc: "/images/fallbacks/football.svg",
      label: "Fotbollsillustration",
    };
  }

  if (/(sim|swim|vatten|bad|pool|aqua)/.test(haystack)) {
    return {
      photoSrc:
        "https://images.pexels.com/photos/3099220/pexels-photo-3099220.jpeg?auto=compress&cs=tinysrgb&w=1200",
      backupSrc: "/images/fallbacks/water.svg",
      label: "Vattenaktivitet",
    };
  }

  if (
    /(musik|teater|dans|konst|art|mala|skapa|skapande|kultur|drama|piano|gitarr)/.test(
      haystack,
    )
  ) {
    return {
      photoSrc:
        "https://images.pexels.com/photos/6719007/pexels-photo-6719007.jpeg?auto=compress&cs=tinysrgb&w=1200",
      backupSrc: "/images/fallbacks/creative.svg",
      label: "Kreativ aktivitet",
    };
  }

  return {
    photoSrc:
      "https://images.pexels.com/photos/7671335/pexels-photo-7671335.jpeg?auto=compress&cs=tinysrgb&w=1200",
    backupSrc: "/images/fallbacks/movement.svg",
    label: "Aktivitetsillustration",
  };
}

function formatDescriptionSnippet(description: string) {
  const cleanDescription = description.replace(/\s+/g, " ").trim();

  if (cleanDescription.length === 0) {
    return "Rörelse, gemenskap och nya favoriter för barn som vill testa något nytt.";
  }

  if (cleanDescription.length <= 150) {
    return cleanDescription;
  }

  return `${cleanDescription.slice(0, 147).trimEnd()}...`;
}

function getHeroActivities(activities: Activity[]) {
  const uniqueActivities = new Map<string, Activity>();

  for (const activity of activities) {
    if (!uniqueActivities.has(activity.id)) {
      uniqueActivities.set(activity.id, activity);
    }
  }

  const allActivities = Array.from(uniqueActivities.values());
  const withImages = allActivities.filter((activity) => activity.imageUrl?.trim());
  const withoutImages = allActivities.filter(
    (activity) => !activity.imageUrl?.trim(),
  );
  const selectedActivities: Activity[] = [];
  const selectedIds = new Set<string>();
  const usedOrganizers = new Set<string>();

  const tryAddActivity = (activity: Activity) => {
    if (selectedIds.has(activity.id)) {
      return false;
    }

    const organizerKey =
      normalizeMatchingText(activity.organizer || "").trim() || "__unknown__";

    if (usedOrganizers.has(organizerKey)) {
      return false;
    }

    selectedActivities.push(activity);
    selectedIds.add(activity.id);
    usedOrganizers.add(organizerKey);

    return selectedActivities.length >= 3;
  };

  for (const activity of [...withImages, ...withoutImages]) {
    if (tryAddActivity(activity)) {
      return selectedActivities;
    }
  }

  for (const activity of [...withImages, ...withoutImages]) {
    if (selectedIds.has(activity.id)) {
      continue;
    }

    selectedActivities.push(activity);
    selectedIds.add(activity.id);

    if (selectedActivities.length >= 3) {
      break;
    }
  }

  return selectedActivities;
}

function FeaturedImageCard({
  activity,
  className = "",
}: {
  activity?: Activity;
  className?: string;
}) {
  const imageUrl = activity?.imageUrl?.trim() ?? "";
  const fallbackImage = getFallbackImage(activity);
  const imageSources = [
    imageUrl.length > 0 ? imageUrl : null,
    fallbackImage.photoSrc,
    fallbackImage.backupSrc,
  ].filter(Boolean) as string[];
  const [sourceIndex, setSourceIndex] = useState(0);
  const displayImageSrc = imageSources[sourceIndex];
  const usingOriginalImage = displayImageSrc === imageUrl && imageUrl.length > 0;
  const showImage = Boolean(displayImageSrc);
  const categoryLabel = activity
    ? activity.sport || getCategoryLabels(activity.category)[0] || "Barnaktiv"
    : "Barnaktiv";
  const cityLabel = activity?.city || activity?.location || "Nära dig";
  const organizerLabel = activity?.organizer || "Flera arrangörer";
  const title = activity?.title || "Aktiviteter som väcker nyfikenhet";
  const supportingText = activity
    ? `${organizerLabel} / ${formatAgeRange(activity)}`
    : "Filtrera på ålder, plats och pris och hitta rätt snabbare.";

  return (
    <article
      className={`relative overflow-hidden rounded-[2rem] border border-white/70 bg-[#fff8f1] shadow-[0_24px_70px_-36px_rgba(15,34,24,0.45)] ${className}`}
    >
      {showImage ? (
        <>
          {/* Activity images come from multiple hosts, so use a plain img instead of broad remote image configuration. */}
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={displayImageSrc}
            alt={usingOriginalImage ? title : `${fallbackImage.label} för ${title}`}
            className="absolute inset-0 h-full w-full object-cover"
            loading="lazy"
            onError={() => setSourceIndex((current) => current + 1)}
          />
          <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(14,26,21,0.08),rgba(14,26,21,0.72))]" />
        </>
      ) : (
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.9),transparent_35%),linear-gradient(145deg,rgba(222,113,57,0.34),rgba(255,239,224,0.92)_56%,rgba(233,242,235,0.96))]" />
      )}

      <div className="relative flex h-full min-h-[14rem] flex-col justify-between p-5">
        <span className="inline-flex w-fit rounded-full border border-white/40 bg-white px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.22em] text-[color:var(--accent-strong)]">
          {categoryLabel}
        </span>

        <div className="max-w-[18rem] rounded-[1.6rem] border border-white/25 bg-[rgba(16,30,24,0.76)] p-4 text-white shadow-lg">
          <p className="text-xs uppercase tracking-[0.18em] text-white/70">
            {cityLabel}
          </p>
          <h3 className="mt-2 text-xl font-semibold leading-tight">{title}</h3>
          <p className="mt-2 text-sm leading-6 text-white/80">
            {supportingText}
          </p>
        </div>
      </div>
    </article>
  );
}

function HeroCollage({
  activities,
  openActivitiesCount,
  freeActivitiesCount,
}: {
  activities: Activity[];
  openActivitiesCount: number;
  freeActivitiesCount: number;
}) {
  const heroActivities = getHeroActivities(activities);
  const collageActivities =
    heroActivities.length >= 3 ? heroActivities.slice(1, 3) : heroActivities.slice(0, 2);

  return (
    <div className="relative min-h-[24rem] lg:min-h-[31rem]">
      <div className="pointer-events-none absolute -left-8 top-10 h-24 w-24 rounded-full bg-white/45 blur-3xl" />
      <div className="pointer-events-none absolute right-0 top-0 h-36 w-36 rounded-full bg-[rgba(224,116,58,0.14)] blur-3xl" />

      <div className="grid h-full gap-4 sm:grid-cols-2">
        {collageActivities.map((activity, index) => (
          <FeaturedImageCard
            key={activity?.id ?? `hero-card-${index}`}
            activity={activity}
            className="min-h-[18rem] sm:min-h-[31rem]"
          />
        ))}
      </div>

      <div className="absolute -bottom-5 left-5 right-5 rounded-[1.7rem] border border-white/70 bg-white p-4 shadow-[0_18px_40px_-30px_rgba(15,34,24,0.38)] sm:left-auto sm:right-8 sm:w-[18rem]">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[color:var(--accent-strong)]">
          Just nu i Barnaktiv
        </p>
        <div className="mt-4 grid grid-cols-2 gap-3">
          <div className="rounded-[1.25rem] bg-[color:var(--foreground)] px-3 py-3 text-[color:var(--background)]">
            <p className="text-[0.68rem] uppercase tracking-[0.18em] text-white/70">
              Öppet nu
            </p>
            <p className="mt-2 text-2xl font-semibold">{openActivitiesCount}</p>
          </div>
          <div className="rounded-[1.25rem] border border-[color:var(--border)] bg-[color:var(--surface-strong)] px-3 py-3">
            <p className="text-[0.68rem] uppercase tracking-[0.18em] text-[color:var(--muted)]">
              Gratis
            </p>
            <p className="mt-2 text-2xl font-semibold text-[color:var(--foreground)]">
              {freeActivitiesCount}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

function ActivityCard({ activity }: { activity: Activity }) {
  const activityDate = new Date(activity.date);
  const categoryLabels = getCategoryLabels(activity.category);
  const sportLabel = activity.sport || null;
  const cityLabel = activity.city || "Stad saknas";
  const organizerLabel = activity.organizer || "Arrangör kommer snart";
  const sourceLabel = activity.source || "Manuell import";
  const registrationStatus = activity.registrationStatus as RegistrationStatus;
  const registrationSummary = formatRegistrationSummary(activity);
  const primaryLink = getPrimaryLink(activity);
  const imageUrl = activity.imageUrl?.trim() || "";
  const fallbackImage = getFallbackImage(activity);
  const imageSources = [
    imageUrl.length > 0 ? imageUrl : null,
    fallbackImage.photoSrc,
    fallbackImage.backupSrc,
  ].filter(Boolean) as string[];
  const [sourceIndex, setSourceIndex] = useState(0);
  const displayImageSrc = imageSources[sourceIndex];
  const usingOriginalImage = displayImageSrc === imageUrl && imageUrl.length > 0;
  const showImage = Boolean(displayImageSrc);
  const formattedDate = capitalizeFirstLetter(
    detailedDateFormatter.format(activityDate),
  );
  const timeLabel = hasSpecifiedTime(activityDate)
    ? timeFormatter.format(activityDate)
    : "Tid ej angiven";

  return (
    <article className="group flex h-full flex-col overflow-hidden rounded-[2rem] border border-[color:var(--border)] bg-[color:var(--surface-strong)] shadow-[var(--card-shadow)] shadow-black/8 transition duration-300 hover:-translate-y-1">
      <div className="relative aspect-[16/10] overflow-hidden border-b border-[color:var(--border)] bg-[linear-gradient(135deg,#f4b18f,#f8e9d9_55%,#f6f0e6)]">
        {showImage ? (
          <>
            {/* Activity images come from multiple hosts, so use a plain img instead of broad remote image configuration. */}
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={displayImageSrc}
              alt={usingOriginalImage ? activity.title : `${fallbackImage.label} för ${activity.title}`}
              className="h-full w-full object-cover transition duration-500 group-hover:scale-[1.03]"
              loading="lazy"
              onError={() => setSourceIndex((current) => current + 1)}
            />
            <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(17,29,24,0.08),rgba(17,29,24,0.54))]" />
          </>
        ) : (
          <div className="flex h-full w-full flex-col justify-end bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.95),transparent_38%),linear-gradient(135deg,rgba(223,105,55,0.3),rgba(247,220,205,0.88)_60%,rgba(255,253,248,1))] p-5">
            <div className="max-w-[14rem] rounded-[1.5rem] bg-white p-4">
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
            {categoryLabels.length > 0
              ? categoryLabels.slice(0, 2).map((categoryLabel) => (
                  <span
                    key={categoryLabel}
                    className="rounded-full bg-[color:var(--accent-soft)] px-3 py-1 text-xs font-semibold uppercase tracking-[0.2em] text-[color:var(--accent-strong)] shadow-sm"
                  >
                    {categoryLabel}
                  </span>
                ))
              : null}
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            <span className="rounded-full bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-[color:var(--foreground)] shadow-sm">
              {formatPrice(activity.price)}
            </span>
            {registrationSummary ? (
              <span
                className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] shadow-sm ${getRegistrationBadgeClassName(
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
            <p className="text-sm leading-6 text-[color:var(--muted)]">
              {formatDescriptionSnippet(activity.description)}
            </p>
          </div>

          <dl className="grid gap-3 text-sm text-[color:var(--foreground)] sm:grid-cols-2">
            <div className="rounded-2xl bg-[rgba(255,255,255,0.78)] px-4 py-3">
              <dt className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
                Datum
              </dt>
              <dd className="mt-1 font-medium">{formattedDate}</dd>
            </div>
            <div className="rounded-2xl bg-[rgba(255,255,255,0.78)] px-4 py-3">
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
            <div className="rounded-2xl bg-[rgba(255,255,255,0.78)] px-4 py-3 text-sm">
              <p className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
                Anmälan
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

          {primaryLink ? (
            <div className="mt-5 flex justify-end">
              <Link
                href={primaryLink.href}
                target="_blank"
                rel="noreferrer"
                className="rounded-full bg-[color:var(--accent)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[color:var(--accent-strong)]"
              >
                {primaryLink.label}
              </Link>
            </div>
          ) : null}
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
    new Set(
      activities.flatMap((activity) => getCategoryLabels(activity.category)),
    ),
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
      selectedCategory === "all" ||
      getCategoryLabels(activity.category).includes(selectedCategory);
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

  const openActivitiesCount = activities.filter(
    (activity) => activity.registrationStatus === "Open",
  ).length;
  const freeActivitiesCount = activities.filter(
    (activity) => activity.price <= 0,
  ).length;
  const featuredCities = cities.slice(0, 4);
  const featuredCategories = categories.slice(0, 5);

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

            <div className="flex flex-wrap gap-3">
              <Link
                href="#utforska"
                className="rounded-full bg-[color:var(--foreground)] px-5 py-3 text-sm font-semibold text-[color:var(--background)] transition hover:translate-y-[-1px] hover:bg-[#16271d]"
              >
                Utforska aktiviteter
              </Link>
              <Link
                href="#aktiviteter"
                className="rounded-full border border-[color:var(--border)] bg-white px-5 py-3 text-sm font-semibold text-[color:var(--foreground)] transition hover:bg-[#fffaf5]"
              >
                Se alla kort
              </Link>
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
          <button
            type="button"
            onClick={clearFilters}
            className="rounded-full border border-[color:var(--border)] bg-white px-4 py-2 text-sm font-semibold text-[color:var(--foreground)] transition hover:bg-[#fffaf5]"
          >
            Rensa filter
          </button>
        </div>

        <div className="mt-6 grid gap-4 lg:grid-cols-[1.7fr_repeat(6,minmax(0,1fr))]">
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
              {getResultSummary(filteredActivities.length)} efter dina val.
            </p>
          </div>
          <p className="text-sm text-[color:var(--muted)]">
            Visar kort med bild, pris, ålder och anmälningsläge.
          </p>
        </div>

        {filteredActivities.length > 0 ? (
          <div className="grid gap-5 lg:grid-cols-2 xl:grid-cols-3">
            {filteredActivities.map((activity) => (
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
