import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Barnaktiv | Hitta barnaktiviteter i din stad",
  description:
    "Upptäck barnaktiviteter, prova-på-pass och lovaktiviteter med en varm och inspirerande översikt.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="sv">
      <body>{children}</body>
    </html>
  );
}
