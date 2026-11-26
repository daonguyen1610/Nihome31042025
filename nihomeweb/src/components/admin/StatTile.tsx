import { Link } from "react-router-dom";
import { ArrowUpRight, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

export type StatTileTone = "indigo" | "coral" | "green" | "sky";

/** A headline number on a bright gradient that opens the list behind it. */
export function StatTile({
  tone,
  icon: Icon,
  label,
  value,
  to,
}: {
  tone: StatTileTone;
  icon: LucideIcon;
  label: string;
  value: number | string;
  to: string;
}) {
  return (
    <Link
      to={to}
      className={cn(
        `tile-${tone}`,
        "block rounded-lg p-5 text-white shadow-md transition-[transform,box-shadow] duration-150 ease-out hover:-translate-y-0.5 hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
      )}
    >
      <span className="mb-5 flex items-start justify-between">
        <span className="flex h-11 w-11 items-center justify-center rounded-md bg-white/20">
          <Icon className="h-5 w-5" strokeWidth={1.75} aria-hidden />
        </span>
        <ArrowUpRight className="h-4 w-4" aria-hidden />
      </span>
      <span className="block break-words text-4xl font-bold leading-none tabular-nums">
        {value}
      </span>
      {/* 19px bold counts as large text, which the bright gradients are measured against. */}
      <span className="mt-2 block text-[19px] font-bold leading-snug">{label}</span>
    </Link>
  );
}
