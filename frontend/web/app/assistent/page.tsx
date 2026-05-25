import { ActivityAssistant } from "@/components/activity-assistant";

export const metadata = {
  title: "Assistent | Barnaktiv",
  description:
    "Ställ frågor på svenska och få förslag på barnaktiviteter från Barnaktivs databas.",
};

export default function AssistantPage() {
  return <ActivityAssistant />;
}
