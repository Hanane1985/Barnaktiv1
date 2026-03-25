export type Activity = {
  id: string;
  title: string;
  description: string;
  organizer: string;
  location: string;
  city: string;
  ageFrom: number;
  ageTo: number;
  category: string;
  date: string;
  price: number;
  websiteUrl: string;
  imageUrl: string;
  source: string;
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

export async function getActivities(): Promise<ActivitiesResult> {
  const apiBaseUrl = getApiBaseUrl();

  try {
    const response = await fetch(`${apiBaseUrl}/api/activities`, {
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

    activities.sort((left, right) => {
      const dateDifference =
        new Date(left.date).getTime() - new Date(right.date).getTime();

      if (dateDifference !== 0) {
        return dateDifference;
      }

      return left.title.localeCompare(right.title);
    });

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
