"use client";

// Needs pathname access to show the active admin destination.
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ADMIN_NAV } from "@/lib/utils/constants";

export function AdminSidebar() {
  const pathname = usePathname();

  return (
    <aside className="surface-panel min-h-full w-full p-5 lg:max-w-[18rem]">
      <div className="mb-8">
        <p className="eyebrow">Admin portal</p>
        <h2 className="mt-3 font-display text-3xl text-[#1f2933]">Nihome</h2>
        <p className="mt-3 text-sm leading-7 text-[#52606d]">
          Phase 1 shell for internal operations, ready for deeper modules later.
        </p>
      </div>

      <nav aria-label="Admin navigation" className="space-y-6">
        {ADMIN_NAV.map((section) => (
          <div className="space-y-3" key={section.label}>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[#9c6b46]">
              {section.label}
            </p>
            <div className="space-y-2">
              {section.items.map((item) => {
                const isActive = pathname === item.href;

                return (
                  <Link
                    className={`block rounded-[20px] px-4 py-3 transition ${
                      isActive
                        ? "bg-[#1f2933] text-[#fff7ed] shadow-lg"
                        : "bg-white/65 text-[#334e68] hover:bg-white"
                    }`}
                    href={item.href}
                    key={`${section.label}-${item.label}`}
                  >
                    <span className="block font-semibold">{item.label}</span>
                    {item.description ? (
                      <span className="mt-1 block text-xs leading-5 opacity-80">
                        {item.description}
                      </span>
                    ) : null}
                  </Link>
                );
              })}
            </div>
          </div>
        ))}
      </nav>
    </aside>
  );
}
