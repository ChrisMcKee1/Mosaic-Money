import "./globals.css";

export const metadata = {
  title: "Mosaic Money",
  description: "Mosaic Money web skeleton"
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
