import type { AgeGroup, PriceFilter, SortOption } from "@/lib/activity-filters";

export const ageGroups: {
  value: AgeGroup;
  label: string;
  min?: number;
  max?: number;
}[] = [
  { value: "all", label: "Alla åldrar" },
  { value: "0-3", label: "0-3 år", min: 0, max: 3 },
  { value: "4-6", label: "4-6 år", min: 4, max: 6 },
  { value: "7-9", label: "7-9 år", min: 7, max: 9 },
  { value: "10-12", label: "10-12 år", min: 10, max: 12 },
  { value: "13+", label: "13+ år", min: 13, max: 99 },
];

export const priceFilters: { value: PriceFilter; label: string }[] = [
  { value: "all", label: "Alla priser" },
  { value: "free", label: "Gratis" },
  { value: "paid", label: "Betalaktiviteter" },
];

export const sortOptions: { value: SortOption; label: string }[] = [
  { value: "date-asc", label: "Äldst datum först" },
  { value: "date-desc", label: "Nyast datum först" },
  { value: "created-desc", label: "Senast tillagda" },
  { value: "registration", label: "Öppen anmälan först" },
  { value: "price-asc", label: "Billigast först" },
  { value: "price-desc", label: "Dyrast först" },
  { value: "title-asc", label: "A-Ö" },
];
