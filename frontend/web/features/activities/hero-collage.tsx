"use client";

import type { Activity } from "@/lib/activities";

import { getHeroActivities } from "./activity-domain";
import { FeaturedImageCard } from "./featured-image-card";

type HeroCollageProps = {
  activities: Activity[];
  openActivitiesCount: number;
  freeActivitiesCount: number;
};

export function HeroCollage({
  activities,
  openActivitiesCount,
  freeActivitiesCount,
}: HeroCollageProps) {
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
