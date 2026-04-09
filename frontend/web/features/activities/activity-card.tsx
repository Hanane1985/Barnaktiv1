"use client";

import Link from "next/link";

import type { Activity } from "@/lib/activities";

import {
  formatActivityCardDate,
  formatActivityCardTimeLabel,
  formatDescriptionSnippet,
  formatPrice,
  formatAgeRange,
  formatRegistrationSummary,
  getCategoryLabels,
  getFallbackImage,
  getPrimaryLink,
  getRegistrationBadgeClassName,
  parseRegistrationStatus,
  registrationStatusLabelSv,
} from "./activity-domain";
import { useActivityImageSources } from "./hooks/use-activity-image-sources";

type ActivityCardProps = {
  activity: Activity;
};

export function ActivityCard({ activity }: ActivityCardProps) {
  const activityDate = new Date(activity.date);
  const categoryLabels = getCategoryLabels(activity.category);
  const sportLabel = activity.sport || null;
  const cityLabel = activity.city || "Stad saknas";
  const organizerLabel = activity.organizer || "Arrangör kommer snart";
  const sourceLabel = activity.source || "Manuell import";
  const registrationStatus = parseRegistrationStatus(activity.registrationStatus);
  const registrationSummary = formatRegistrationSummary(activity);
  const primaryLink = getPrimaryLink(activity);
  const imageUrl = activity.imageUrl?.trim() || "";
  const fallbackImage = getFallbackImage(activity);
  const { displayImageSrc, usingOriginalImage, showImage, onImageError } =
    useActivityImageSources(imageUrl, fallbackImage);

  const formattedDate = formatActivityCardDate(activityDate);
  const timeLabel = formatActivityCardTimeLabel(activityDate);

  return (
    <article className="group flex h-full flex-col overflow-hidden rounded-3xl border border-[color:var(--border)] bg-[color:var(--surface-strong)] shadow-[var(--card-shadow)] ring-1 ring-black/[0.03] transition duration-300 hover:-translate-y-0.5 hover:shadow-[var(--card-shadow-hover)]">
      <div className="relative aspect-[16/9] overflow-hidden border-b border-[color:var(--border)] bg-[linear-gradient(135deg,#efd4c4,#f8ebe3_50%,#f4f0eb)]">
        {showImage ? (
          <>
            {/* Activity images come from multiple hosts, so use a plain img instead of broad remote image configuration. */}
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={displayImageSrc}
              alt={usingOriginalImage ? activity.title : `${fallbackImage.label} för ${activity.title}`}
              className="h-full w-full object-cover transition duration-500 group-hover:scale-[1.03]"
              loading="lazy"
              onError={onImageError}
            />
            <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(17,29,24,0.08),rgba(17,29,24,0.54))]" />
          </>
        ) : (
          <div className="flex h-full w-full flex-col justify-end bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.95),transparent_38%),linear-gradient(135deg,rgba(223,105,55,0.3),rgba(247,220,205,0.88)_60%,rgba(255,253,248,1))] p-4">
            <div className="max-w-[14rem] rounded-[1.4rem] bg-white p-3.5">
              <p className="text-xs font-semibold uppercase tracking-[0.22em] text-[color:var(--accent-strong)]">
                Barnaktiv
              </p>
              <p className="mt-2 text-sm font-semibold leading-5 text-[color:var(--foreground)]">
                {activity.location || cityLabel}
              </p>
            </div>
          </div>
        )}

        <div className="absolute inset-x-0 top-0 flex items-start justify-between gap-3 p-3.5">
          <div className="flex flex-wrap gap-2">
            {sportLabel ? (
              <span className="rounded-full bg-[color:var(--foreground)] px-2.5 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[color:var(--background)] shadow-sm">
                {sportLabel}
              </span>
            ) : null}
            {categoryLabels.length > 0
              ? categoryLabels.slice(0, 2).map((categoryLabel) => (
                  <span
                    key={categoryLabel}
                    className="rounded-full bg-[color:var(--accent-soft)] px-2.5 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-[color:var(--accent-strong)] shadow-sm"
                  >
                    {categoryLabel}
                  </span>
                ))
              : null}
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            <span className="rounded-full bg-white px-2.5 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.16em] text-[color:var(--foreground)] shadow-sm">
              {formatPrice(activity.price)}
            </span>
            {registrationSummary ? (
              <span
                className={`rounded-full px-2.5 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.14em] shadow-sm ${getRegistrationBadgeClassName(
                  registrationStatus,
                )}`}
              >
                {registrationStatusLabelSv(registrationStatus)}
              </span>
            ) : null}
          </div>
        </div>
      </div>

      <div className="flex flex-1 flex-col p-4 sm:p-5">
        <div className="space-y-3">
          <div className="space-y-1.5">
            <h2 className="font-display text-[1.2rem] font-semibold leading-snug tracking-tight text-[color:var(--foreground)]">
              {activity.title}
            </h2>
            <p className="text-sm font-medium text-[color:var(--muted)]">
              {activity.location || "Plats kommer snart"}
            </p>
            <p className="overflow-hidden text-sm leading-6 text-[color:var(--muted)] [display:-webkit-box] [-webkit-box-orient:vertical] [-webkit-line-clamp:2]">
              {formatDescriptionSnippet(activity.description)}
            </p>
          </div>

          <div className="rounded-2xl border border-[color:var(--border)] bg-[color:var(--surface)] px-4 py-3 text-sm text-[color:var(--foreground)]">
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
              <span className="text-xs uppercase tracking-[0.16em] text-[color:var(--muted)]">
                När
              </span>
              <span className="font-medium">{formattedDate}</span>
              <span className="h-1 w-1 rounded-full bg-[rgba(96,112,99,0.45)]" />
              <span className="font-medium">{timeLabel}</span>
            </div>

            {registrationSummary ? (
              <p className="mt-2 text-sm font-medium text-[color:var(--foreground)]">
                {registrationSummary}
              </p>
            ) : null}
          </div>

          <div className="flex flex-wrap gap-2 text-sm">
            <span className="rounded-full border border-[color:var(--border)] bg-white px-3 py-1.5 font-medium text-[color:var(--foreground)]">
              Stad: {cityLabel}
            </span>
            <span className="rounded-full border border-[color:var(--border)] bg-white px-3 py-1.5 font-medium text-[color:var(--foreground)]">
              {"Ålder: "}
              {formatAgeRange(activity)}
            </span>
          </div>
        </div>

        <div className="mt-auto pt-4">
          <div className="flex flex-wrap items-center gap-2 text-sm text-[color:var(--muted)]">
            <span className="font-medium text-[color:var(--foreground)]">
              {organizerLabel}
            </span>
            <span className="h-1 w-1 rounded-full bg-current" />
            <span>{sourceLabel}</span>
          </div>

          {primaryLink ? (
            <div className="mt-4 flex justify-end">
              <Link
                href={primaryLink.href}
                target="_blank"
                rel="noreferrer"
                className="btn-primary"
              >
                {primaryLink.label}
              </Link>
            </div>
          ) : null}
        </div>
      </div>
    </article>
  );
}
