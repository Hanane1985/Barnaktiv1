import type { Metadata } from "next";
import Script from "next/script";

import "./globals.css";

const themeInitScript = `(function(){try{var t=localStorage.getItem("barnaktiv-theme");if(t==="dark")document.documentElement.setAttribute("data-theme","dark");else if(t==="light")document.documentElement.setAttribute("data-theme","light");}catch(e){}})();`;

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
    <html lang="sv" suppressHydrationWarning>
      <body>
        <Script id="barnaktiv-theme-init" strategy="beforeInteractive">
          {themeInitScript}
        </Script>
        {children}
      </body>
    </html>
  );
}
