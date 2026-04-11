import {
  buildActivityApiSearchParams,
  defaultActivityFilters,
  type ActivityFilters,
} from "@/lib/activity-filters";
import {
  type Activity,
  parseActivitiesResponse,
} from "@/lib/activity-api-schema";

export type { Activity };

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

    const payload: unknown = await response.json();
    const parsed = parseActivitiesResponse(payload);

    if (!parsed.ok) {
      return {
        activities: [],
        apiBaseUrl,
        errorMessage: parsed.message,
      };
    }

    return {
      activities: parsed.activities,
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
