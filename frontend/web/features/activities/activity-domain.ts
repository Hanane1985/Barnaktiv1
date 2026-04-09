import { defaultActivityFilters } from "@/lib/activity-filters";
import type { Activity } from "@/lib/activities";

import type { FallbackImage, RegistrationStatus } from "./types";

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

const registrationStatuses = new Set<RegistrationStatus>([
  "Unknown",
  "Upcoming",
  "Open",
  "Closed",
  "Full",
]);

export function parseRegistrationStatus(raw: string): RegistrationStatus {
  return registrationStatuses.has(raw as RegistrationStatus)
    ? (raw as RegistrationStatus)
    : "Unknown";
}

export function registrationStatusLabelSv(status: RegistrationStatus): string {
  switch (status) {
    case "Open":
      return "Öppen";
    case "Upcoming":
      return "Kommande";
    case "Closed":
      return "Stängd";
    case "Full":
      return "Fullbokad";
    default:
      return "Okänt";
  }
}

export function formatPrice(price: number) {
  return price <= 0 ? "Gratis" : priceFormatter.format(price);
}

export function formatAgeRange(activity: Activity) {
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

export function getResultSummary(count: number) {
  if (count === 0) {
    return "Inga aktiviteter matchar";
  }

  if (count === 1) {
    return "1 aktivitet";
  }

  return `${count} aktiviteter`;
}

export function formatRegistrationSummary(activity: Activity) {
  const registrationStatus = parseRegistrationStatus(activity.registrationStatus);
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

export function getRegistrationBadgeClassName(status: RegistrationStatus) {
  switch (status) {
    case "Open":
      return "badge-reg badge-reg--open";
    case "Upcoming":
      return "badge-reg badge-reg--upcoming";
    case "Closed":
      return "badge-reg badge-reg--closed";
    case "Full":
      return "badge-reg badge-reg--full";
    default:
      return "badge-reg badge-reg--unknown";
  }
}

export function getPrimaryLink(activity: Activity) {
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

export function hasSpecifiedTime(date: Date) {
  return date.getHours() !== 0 || date.getMinutes() !== 0;
}

export function capitalizeFirstLetter(value: string) {
  if (!value) {
    return value;
  }

  return value.charAt(0).toUpperCase() + value.slice(1);
}

export function getCanonicalCategoryLabel(category: string) {
  const trimmedCategory = category.trim();
  const normalizedCategory = trimmedCategory
    .toLowerCase()
    .replace(/\s*\/\s*/g, "/")
    .replace(/\s+/g, " ");

  switch (normalizedCategory) {
    case "bad":
    case "bad/simning":
      return "Bad/Simning";
    default:
      return trimmedCategory;
  }
}

export function getCategoryLabels(categoryValue: string) {
  return Array.from(
    new Set(
      categoryValue
        .split(",")
        .map((category) => getCanonicalCategoryLabel(category))
        .filter(Boolean),
    ),
  );
}

export function getSortedOptions(values: string[], selectedValue: string) {
  const options = Array.from(new Set(values.filter(Boolean)));

  if (
    selectedValue !== defaultActivityFilters.city &&
    selectedValue.length > 0 &&
    !options.includes(selectedValue)
  ) {
    options.push(selectedValue);
  }

  return options.sort((left, right) => left.localeCompare(right, "sv-SE"));
}

export function normalizeMatchingText(value: string) {
  return value
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");
}

export function getFallbackImage(activity?: Activity): FallbackImage {
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

export function formatDescriptionSnippet(description: string) {
  const cleanDescription = description.replace(/\s+/g, " ").trim();

  if (cleanDescription.length === 0) {
    return "Rörelse, gemenskap och nya favoriter för barn som vill testa något nytt.";
  }

  if (cleanDescription.length <= 150) {
    return cleanDescription;
  }

  return `${cleanDescription.slice(0, 147).trimEnd()}...`;
}

export function getHeroActivities(activities: Activity[]) {
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

export function formatActivityCardDate(activityDate: Date) {
  return capitalizeFirstLetter(detailedDateFormatter.format(activityDate));
}

export function formatActivityCardTimeLabel(activityDate: Date) {
  return hasSpecifiedTime(activityDate)
    ? timeFormatter.format(activityDate)
    : "Tid ej angiven";
}
