import { Outfit, Plus_Jakarta_Sans, JetBrains_Mono } from "next/font/google";
import { ClerkProvider } from '@clerk/nextjs';
import "./globals.css";
import { Shell } from "../components/layout/Shell";
import { isClerkConfiguredCorrectly } from "../lib/clerk-server-utils";

const outfit = Outfit({
  subsets: ["latin"],
  variable: "--font-outfit",
  display: "swap",
});

const jakarta = Plus_Jakarta_Sans({
  subsets: ["latin"],
  variable: "--font-jakarta",
  display: "swap",
});

const jetbrains = JetBrains_Mono({
  subsets: ["latin"],
  variable: "--font-jetbrains",
  display: "swap",
});

export const metadata = {
  title: "Mosaic Money",
  description: "Refined financial dashboard"
};

export default function RootLayout({ children }) {
  const isClerkConfigured = isClerkConfiguredCorrectly();

  const content = (
    <html
      lang="en"
      data-theme="dark"
      suppressHydrationWarning
      className={`${outfit.variable} ${jakarta.variable} ${jetbrains.variable}`}
    >
      <head>
        <script
          dangerouslySetInnerHTML={{
            __html: `
              (() => {
                try {
                  const key = 'mosaic-theme';
                  const saved = localStorage.getItem(key);
                  const theme = saved === 'light' ? 'light' : 'dark';
                  document.documentElement.dataset.theme = theme;
                } catch {}
              })();
            `,
          }}
        />
      </head>
      <body className="bg-background text-text-main antialiased selection:bg-primary selection:text-background">
        <a href="#main-content" className="sr-only focus:not-sr-only focus:absolute focus:p-4 focus:bg-surface focus:text-primary focus:z-50">
          Skip to main content
        </a>
        <Shell isClerkConfigured={isClerkConfigured}>{children}</Shell>
      </body>
    </html>
  );

  if (!isClerkConfigured) {
    return content;
  }

  return (
    <ClerkProvider appearance={{ cssLayerName: 'clerk' }}>
      {content}
    </ClerkProvider>
  );
}
