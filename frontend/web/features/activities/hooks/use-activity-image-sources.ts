"use client";

import { useCallback, useMemo, useState } from "react";

import type { FallbackImage } from "../types";

export function useActivityImageSources(imageUrl: string, fallbackImage: FallbackImage) {
  const trimmed = imageUrl.trim();
  const imageSources = useMemo(
    () =>
      [
        trimmed.length > 0 ? trimmed : null,
        fallbackImage.photoSrc,
        fallbackImage.backupSrc,
      ].filter(Boolean) as string[],
    [trimmed, fallbackImage.photoSrc, fallbackImage.backupSrc],
  );

  const [sourceIndex, setSourceIndex] = useState(0);
  const displayImageSrc = imageSources[sourceIndex];
  const usingOriginalImage = displayImageSrc === trimmed && trimmed.length > 0;
  const showImage = Boolean(displayImageSrc);

  const onImageError = useCallback(() => {
    setSourceIndex((current) => current + 1);
  }, []);

  return { displayImageSrc, usingOriginalImage, showImage, onImageError };
}
