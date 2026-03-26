import { ActivityExplorer } from "@/components/activity-explorer";
import { getActivities } from "@/lib/activities";

export const dynamic = "force-dynamic";

export default async function Home() {
  const { activities, errorMessage } = await getActivities();

  return (
    <ActivityExplorer
      activities={activities}
      errorMessage={errorMessage}
    />
  );
}
