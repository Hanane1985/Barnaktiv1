import { z } from "zod";

/** Mirrors backend `ActivityDto` JSON (camelCase); validates the activities list at runtime. */
export const activitySchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  description: z.string(),
  organizer: z.string(),
  location: z.string(),
  city: z.string(),
  ageFrom: z.number().int(),
  ageTo: z.number().int(),
  sport: z.string(),
  category: z.string(),
  listingType: z.string(),
  date: z.string(),
  price: z.number(),
  websiteUrl: z.string(),
  signupUrl: z.string(),
  imageUrl: z.string(),
  source: z.string(),
  registrationStatus: z.string(),
  registrationOpenAt: z.union([z.string(), z.null()]),
  registrationCloseAt: z.union([z.string(), z.null()]),
  createdAt: z.string(),
});

export const activitiesArraySchema = z.array(activitySchema);

export type Activity = z.infer<typeof activitySchema>;

export function parseActivitiesResponse(payload: unknown):
  | { ok: true; activities: Activity[] }
  | { ok: false; message: string } {
  const parsed = activitiesArraySchema.safeParse(payload);
  if (!parsed.success) {
    const detail = parsed.error.issues
      .map((issue) => `${issue.path.join(".") || "(root)"}: ${issue.message}`)
      .join("; ");
    return {
      ok: false,
      message: `The API response did not match the activities contract. ${detail}`,
    };
  }

  return { ok: true, activities: parsed.data };
}
