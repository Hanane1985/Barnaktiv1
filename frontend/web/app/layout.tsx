import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Barnaktiv",
  description: "Browse children's activities from the Barnaktiv API.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
