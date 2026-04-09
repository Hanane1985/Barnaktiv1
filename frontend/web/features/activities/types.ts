export type RegistrationStatus =
  | "Unknown"
  | "Upcoming"
  | "Open"
  | "Closed"
  | "Full";

export type FallbackImage = {
  photoSrc: string;
  backupSrc: string;
  label: string;
};
