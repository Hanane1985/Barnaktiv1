import { ActivityExplorer } from "@/components/activity-explorer";
import { getActivities } from "@/lib/activities";

export const dynamic = "force-dynamic";

export default async function Home() {
  const { activities, apiBaseUrl, errorMessage } = await getActivities();

  return (
    <ActivityExplorer
      activities={activities}
      apiBaseUrl={apiBaseUrl}
      errorMessage={errorMessage}
    />
  );
}
