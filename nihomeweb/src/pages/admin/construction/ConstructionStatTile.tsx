import type { ReactNode } from "react";

/** Consistent, readable construction totals without decorative status colors. */
export default function ConstructionStatTile({
  label,
  value,
  icon,
  note,
  testId,
}: {
  label: string;
  value: ReactNode;
  icon?: ReactNode;
  note?: ReactNode;
  testId?: string;
}) {
  return (
    <div className="min-w-0 rounded-xl border bg-card p-4" data-testid={testId}>
      <div className="flex items-start justify-between gap-2 text-sm text-muted-foreground">
        <span>{label}</span>
        {icon ? (
          <span aria-hidden className="shrink-0 [&>svg]:h-4 [&>svg]:w-4">
            {icon}
          </span>
        ) : null}
      </div>
      <div className="mt-2 text-2xl font-semibold tabular-nums text-foreground">{value}</div>
      {note ? <p className="mt-1 text-xs text-muted-foreground">{note}</p> : null}
    </div>
  );
}
