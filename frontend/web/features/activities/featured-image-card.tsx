"use client";

import type { Activity } from "@/lib/activities";

import {
  formatAgeRange,
  getCategoryLabels,
  getFallbackImage,
} from "./activity-domain";
import { useActivityImageSources } from "./hooks/use-activity-image-sources";

type FeaturedImageCardProps = {
  activity?: Activity;
  className?: string;
};

export function FeaturedImageCard({ activity, className = "" }: FeaturedImageCardProps) {
  const imageUrl = activity?.imageUrl?.trim() ?? "";
  const fallbackImage = getFallbackImage(activity);
  const { displayImageSrc, usingOriginalImage, showImage, onImageError } =
    useActivityImageSources(imageUrl, fallbackImage);

  const categoryLabel = activity
    ? activity.sport || getCategoryLabels(activity.category)[0] || "Barnaktiv"
    : "Barnaktiv";
  const cityLabel = activity?.city || activity?.location || "Nära dig";
  const organizerLabel = activity?.organizer || "Flera arrangörer";
  const title = activity?.title || "Aktiviteter som väcker nyfikenhet";
  const supportingText = activity
    ? `${organizerLabel} / ${formatAgeRange(activity)}`
    : "Filtrera på ålder, plats och pris och hitta rätt snabbare.";

  return (
    <article
      className={`relative overflow-hidden rounded-[2rem] border border-white/70 bg-[#fff8f1] shadow-[0_24px_70px_-36px_rgba(15,34,24,0.45)] ${className}`}
    >
      {showImage ? (
        <>
          {/* Activity images come from multiple hosts, so use a plain img instead of broad remote image configuration. */}
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={displayImageSrc}
            alt={usingOriginalImage ? title : `${fallbackImage.label} för ${title}`}
            className="absolute inset-0 h-full w-full object-cover"
            loading="lazy"
            onError={onImageError}
          />
          <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(14,26,21,0.08),rgba(14,26,21,0.72))]" />
        </>
      ) : (
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.9),transparent_35%),linear-gradient(145deg,rgba(222,113,57,0.34),rgba(255,239,224,0.92)_56%,rgba(233,242,235,0.96))]" />
      )}

      <div className="relative flex h-full min-h-[14rem] flex-col justify-between p-5">
        <span className="inline-flex w-fit rounded-full border border-white/40 bg-white px-3 py-1 text-[0.68rem] font-semibold uppercase tracking-[0.22em] text-[color:var(--accent-strong)]">
          {categoryLabel}
        </span>

        <div className="max-w-[18rem] rounded-[1.6rem] border border-white/25 bg-[rgba(16,30,24,0.76)] p-4 text-white shadow-lg">
          <p className="text-xs uppercase tracking-[0.18em] text-white/70">
            {cityLabel}
          </p>
          <h3 className="mt-2 text-xl font-semibold leading-tight">{title}</h3>
          <p className="mt-2 text-sm leading-6 text-white/80">
            {supportingText}
          </p>
        </div>
      </div>
    </article>
  );
}
