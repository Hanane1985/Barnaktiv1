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
      <div
        className="pointer-events-none absolute -left-8 top-10 h-28 w-28 rounded-full blur-3xl"
        style={{ background: "var(--hero-blur-light)" }}
      />
      <div className="pointer-events-none absolute right-0 top-0 h-40 w-40 rounded-full bg-[color:var(--accent-soft)]/60 blur-3xl" />

      <div className="grid h-full gap-4 sm:grid-cols-2">
        {collageActivities.map((activity, index) => (
          <FeaturedImageCard
            key={activity?.id ?? `hero-card-${index}`}
            activity={activity}
            className="min-h-[18rem] sm:min-h-[31rem]"
          />
        ))}
      </div>

      <div className="absolute -bottom-5 left-5 right-5 rounded-2xl border border-[color:var(--border)] bg-[color:var(--surface-strong)]/95 p-4 shadow-[var(--card-shadow)] backdrop-blur-md sm:left-auto sm:right-8 sm:w-[18rem]">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[color:var(--sage)]">
          Just nu i Barnaktiv
        </p>
        <div className="mt-4 grid grid-cols-2 gap-3">
          <div className="rounded-xl bg-[color:var(--sage)] px-3 py-3 text-white">
            <p className="text-[0.68rem] uppercase tracking-[0.18em] text-white/75">
              Öppet nu
            </p>
            <p className="mt-2 font-display text-2xl font-semibold tabular-nums">
              {openActivitiesCount}
            </p>
          </div>
          <div className="rounded-xl border border-[color:var(--border)] bg-[color:var(--surface)] px-3 py-3">
            <p className="text-[0.68rem] uppercase tracking-[0.18em] text-[color:var(--muted)]">
              Gratis
            </p>
            <p className="mt-2 font-display text-2xl font-semibold tabular-nums text-[color:var(--foreground)]">
              {freeActivitiesCount}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
