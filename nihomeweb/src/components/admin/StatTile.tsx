import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, type LucideIcon } from "lucide-react";
import type { ModuleTone } from "@/lib/moduleTheme";
import { cn } from "@/lib/utils";

/** A headline number on a module gradient; opens the list behind it when `to` is given. */
export function StatTile({
  tone,
  icon: Icon,
  label,
  value,
  to,
}: {
  tone: ModuleTone;
  icon: LucideIcon;
  label: string;
  value: ReactNode;
  to?: string;
}) {
  const body = (
    <>
      <span className="mb-4 flex items-start justify-between">
        <span className="flex h-11 w-11 items-center justify-center rounded-md bg-white/20">
          <Icon className="h-5 w-5" strokeWidth={1.75} aria-hidden />
        </span>
        {to && <ArrowUpRight className="h-4 w-4" aria-hidden />}
      </span>
      <span className="block break-words text-3xl font-bold leading-tight tabular-nums">
        {value}
      </span>
      <span className="mt-1 block text-sm font-medium">{label}</span>
    </>
  );
  const className = cn(`tone-${tone} tone-gradient block rounded-lg p-4 shadow-md sm:p-5`);
  return to ? (
    <Link
      to={to}
      className={cn(
        className,
        "transition-[transform,box-shadow] duration-150 hover:-translate-y-0.5 hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
      )}
    >
      {body}
    </Link>
  ) : (
    <div className={className}>{body}</div>
  );
}
