export type AgeGroup = "all" | "0-3" | "4-6" | "7-9" | "10-12" | "13+";
export type PriceFilter = "all" | "free" | "paid";
export type SortOption =
  | "date-asc"
  | "date-desc"
  | "created-desc"
  | "registration"
  | "price-asc"
  | "price-desc"
  | "title-asc";

export type ActivityFilters = {
  search: string;
  city: string;
  organizer: string;
  sport: string;
  category: string;
  ageGroup: AgeGroup;
  price: PriceFilter;
  sort: SortOption;
};

export const defaultActivityFilters: ActivityFilters = {
  search: "",
  city: "all",
  organizer: "all",
  sport: "all",
  category: "all",
  ageGroup: "all",
  price: "all",
  sort: "date-asc",
};

export const ageGroupRanges: Record<Exclude<AgeGroup, "all">, { min: number; max: number }> = {
  "0-3": { min: 0, max: 3 },
  "4-6": { min: 4, max: 6 },
  "7-9": { min: 7, max: 9 },
  "10-12": { min: 10, max: 12 },
  "13+": { min: 13, max: 99 },
};

type SearchParamValue = string | string[] | undefined;

const validAgeGroups = new Set<AgeGroup>([
  "all",
  "0-3",
  "4-6",
  "7-9",
  "10-12",
  "13+",
]);

const validPriceFilters = new Set<PriceFilter>(["all", "free", "paid"]);
const validSortOptions = new Set<SortOption>([
  "date-asc",
  "date-desc",
  "created-desc",
  "registration",
  "price-asc",
  "price-desc",
  "title-asc",
]);

function getSingleValue(value: SearchParamValue) {
  if (Array.isArray(value)) {
    return value[0] ?? "";
  }

  return value ?? "";
}

function normalizeFilterValue(value: string) {
  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : "";
}

export function parseActivityFiltersFromSearchParams(
  searchParams: Record<string, SearchParamValue>,
): ActivityFilters {
  const search = normalizeFilterValue(getSingleValue(searchParams.search));
  const city = normalizeFilterValue(getSingleValue(searchParams.city));
  const organizer = normalizeFilterValue(getSingleValue(searchParams.organizer));
  const sport = normalizeFilterValue(getSingleValue(searchParams.sport));
  const category = normalizeFilterValue(getSingleValue(searchParams.category));
  const ageGroupValue = normalizeFilterValue(getSingleValue(searchParams.age));
  const priceValue = normalizeFilterValue(getSingleValue(searchParams.price));
  const sortValue = normalizeFilterValue(getSingleValue(searchParams.sort));

  return {
    search,
    city: city || defaultActivityFilters.city,
    organizer: organizer || defaultActivityFilters.organizer,
    sport: sport || defaultActivityFilters.sport,
    category: category || defaultActivityFilters.category,
    ageGroup: validAgeGroups.has(ageGroupValue as AgeGroup)
      ? (ageGroupValue as AgeGroup)
      : defaultActivityFilters.ageGroup,
    price: validPriceFilters.has(priceValue as PriceFilter)
      ? (priceValue as PriceFilter)
      : defaultActivityFilters.price,
    sort: validSortOptions.has(sortValue as SortOption)
      ? (sortValue as SortOption)
      : defaultActivityFilters.sort,
  };
}

export function buildActivityRouteSearchParams(filters: ActivityFilters) {
  const params = new URLSearchParams();

  if (filters.search.trim()) {
    params.set("search", filters.search.trim());
  }

  setIfNonDefault(params, "city", filters.city, defaultActivityFilters.city);
  setIfNonDefault(
    params,
    "organizer",
    filters.organizer,
    defaultActivityFilters.organizer,
  );
  setIfNonDefault(params, "sport", filters.sport, defaultActivityFilters.sport);
  setIfNonDefault(
    params,
    "category",
    filters.category,
    defaultActivityFilters.category,
  );
  setIfNonDefault(params, "age", filters.ageGroup, defaultActivityFilters.ageGroup);
  setIfNonDefault(params, "price", filters.price, defaultActivityFilters.price);
  setIfNonDefault(params, "sort", filters.sort, defaultActivityFilters.sort);

  return params;
}

export function buildActivityApiSearchParams(filters: ActivityFilters) {
  const params = new URLSearchParams();

  if (filters.search.trim()) {
    params.set("search", filters.search.trim());
  }

  setIfNonDefault(params, "city", filters.city, defaultActivityFilters.city);
  setIfNonDefault(
    params,
    "organizer",
    filters.organizer,
    defaultActivityFilters.organizer,
  );
  setIfNonDefault(params, "sport", filters.sport, defaultActivityFilters.sport);
  setIfNonDefault(
    params,
    "category",
    filters.category,
    defaultActivityFilters.category,
  );
  setIfNonDefault(params, "price", filters.price, defaultActivityFilters.price);
  setIfNonDefault(params, "sort", filters.sort, defaultActivityFilters.sort);

  if (filters.ageGroup !== defaultActivityFilters.ageGroup) {
    const ageGroup = filters.ageGroup as Exclude<AgeGroup, "all">;
    const ageRange = ageGroupRanges[ageGroup];
    params.set("minAge", ageRange.min.toString());
    params.set("maxAge", ageRange.max.toString());
  }

  return params;
}

function setIfNonDefault(
  params: URLSearchParams,
  key: string,
  value: string,
  defaultValue: string,
) {
  const trimmedValue = value.trim();

  if (trimmedValue.length === 0 || trimmedValue === defaultValue) {
    return;
  }

  params.set(key, trimmedValue);
}
