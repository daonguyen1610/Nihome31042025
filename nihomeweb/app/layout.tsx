import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Nihome",
  description: "Modern property management for owners, staff, and tenants.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
