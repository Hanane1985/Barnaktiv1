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
const ACTIVITY_PAGE_SIZE = 300;
const MAX_ACTIVITY_PAGES = 20;

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
  const baseParams = buildActivityApiSearchParams(filters);
  const activities: Activity[] = [];

  try {
    for (let page = 0; page < MAX_ACTIVITY_PAGES; page++) {
      const params = new URLSearchParams(baseParams);
      params.set("skip", (page * ACTIVITY_PAGE_SIZE).toString());
      params.set("take", ACTIVITY_PAGE_SIZE.toString());

      const response = await fetch(`${apiBaseUrl}/api/activities?${params.toString()}`, {
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

      activities.push(...parsed.activities);

      if (parsed.activities.length < ACTIVITY_PAGE_SIZE) {
        break;
      }
    }

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
