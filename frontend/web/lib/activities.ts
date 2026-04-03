import {
  buildActivityApiSearchParams,
  defaultActivityFilters,
  type ActivityFilters,
} from "@/lib/activity-filters";

export type Activity = {
  id: string;
  title: string;
  description: string;
  organizer: string;
  location: string;
  city: string;
  ageFrom: number;
  ageTo: number;
  sport: string;
  category: string;
  listingType: string;
  date: string;
  price: number;
  websiteUrl: string;
  signupUrl: string;
  imageUrl: string;
  source: string;
  registrationStatus: string;
  registrationOpenAt: string | null;
  registrationCloseAt: string | null;
  createdAt: string;
};

export type ActivitiesResult = {
  activities: Activity[];
  apiBaseUrl: string;
  errorMessage?: string;
};

const DEFAULT_API_BASE_URL = "http://localhost:5289";

function getApiBaseUrl() {
  const configuredBaseUrl =
    process.env.BARNAKTIV_API_BASE_URL ??
    process.env.NEXT_PUBLIC_BARNAKTIV_API_BASE_URL;

  return configuredBaseUrl?.trim().replace(/\/$/, "") || DEFAULT_API_BASE_URL;
}

export async function getActivities(
  filters: ActivityFilters = defaultActivityFilters,
): Promise<ActivitiesResult> {
  const apiBaseUrl = getApiBaseUrl();
  const queryString = buildActivityApiSearchParams(filters).toString();
  const requestUrl = queryString
    ? `${apiBaseUrl}/api/activities?${queryString}`
    : `${apiBaseUrl}/api/activities`;

  try {
    const response = await fetch(requestUrl, {
      cache: "no-store",
      headers: {
        Accept: "application/json",
      },
    });

    if (!response.ok) {
      return {
        activities: [],
        apiBaseUrl,
        errorMessage: `The API returned ${response.status} ${response.statusText}.`,
      };
    }

    const payload = (await response.json()) as unknown;

    if (!Array.isArray(payload)) {
      return {
        activities: [],
        apiBaseUrl,
        errorMessage: "The API response shape was not an activities array.",
      };
    }

    const activities = payload as Activity[];

    return {
      activities,
      apiBaseUrl,
    };
  } catch (error) {
    return {
      activities: [],
      apiBaseUrl,
      errorMessage:
        error instanceof Error
          ? error.message
          : "An unknown error occurred while loading activities.",
    };
  }
}
