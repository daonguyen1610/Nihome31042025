"use client";

// Needs pathname access to highlight the active client navigation item.
import Link from "next/link";
import { usePathname } from "next/navigation";
import { CLIENT_NAV } from "@/lib/utils/constants";
import { PageContainer } from "@/components/layout/page-container";

export function ClientNavbar() {
  const pathname = usePathname();

  return (
    <header className="sticky top-0 z-30 border-b border-white/55 bg-[#fffaf4]/85 backdrop-blur-xl">
      <PageContainer className="flex items-center justify-between gap-4 py-4">
        <Link className="font-display text-2xl text-[#1f2933]" href="/">
          Nihome
        </Link>
        <nav aria-label="Client navigation" className="flex flex-wrap gap-2">
          {CLIENT_NAV.map((item) => {
            const isActive = pathname === item.href;

            return (
              <Link
                className={`shell-link ${isActive ? "shell-link-active" : ""}`}
                href={item.href}
                key={item.href}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
      </PageContainer>
    </header>
  );
}
