"use client";

// Needs pathname access to render a lightweight breadcrumb placeholder.
import { usePathname } from "next/navigation";
import { ROUTE_LABELS } from "@/lib/utils/constants";

function toBreadcrumbs(pathname: string) {
  const segments = pathname.split("/").filter(Boolean);

  if (segments.length === 0) {
    return ["Home"];
  }

  return segments.map((segment) => ROUTE_LABELS[segment] ?? segment);
}

export function AdminHeader() {
  const pathname = usePathname();
  const breadcrumbs = toBreadcrumbs(pathname);

  return (
    <header className="surface-panel flex items-center justify-between gap-4 px-5 py-4">
      <div>
        <p className="eyebrow">Workspace</p>
        <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-[#52606d]">
          {breadcrumbs.map((crumb, index) => (
            <span className="flex items-center gap-2" key={`${crumb}-${index}`}>
              {index > 0 ? <span>/</span> : null}
              <span>{crumb}</span>
            </span>
          ))}
        </div>
      </div>

      <div className="flex items-center gap-3 rounded-full border border-[#1f2933]/10 bg-white/75 px-3 py-2 text-sm text-[#334e68]">
        <span className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[#1f2933] text-[#fff7ed]">
          NH
        </span>
        <div>
          <p className="font-semibold text-[#1f2933]">Phase 1 Owner</p>
          <p className="text-xs">Admin placeholder</p>
        </div>
      </div>
    </header>
  );
}
