import "./globals.css";
import { Shell } from "../components/layout/Shell";

export const metadata = {
  title: "Mosaic Money",
  description: "Mosaic Money web skeleton"
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>
        <a href="#main-content" className="sr-only focus:not-sr-only focus:absolute focus:p-4 focus:bg-white focus:text-blue-600 focus:z-50">
          Skip to main content
        </a>
        <Shell>{children}</Shell>
      </body>
    </html>
  );
}
