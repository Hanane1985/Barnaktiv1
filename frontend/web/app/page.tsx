import { ActivityExplorer } from "@/components/activity-explorer";
import { parseActivityFiltersFromSearchParams } from "@/lib/activity-filters";
import { getActivities } from "@/lib/activities";

export const dynamic = "force-dynamic";

type HomeProps = {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
};

export default async function Home({ searchParams }: HomeProps) {
  const resolvedSearchParams = (await searchParams) ?? {};
  const filters = parseActivityFiltersFromSearchParams(resolvedSearchParams);
  const { activities, errorMessage } = await getActivities(filters);

  return (
    <ActivityExplorer
      activities={activities}
      errorMessage={errorMessage}
      initialFilters={filters}
    />
  );
}
