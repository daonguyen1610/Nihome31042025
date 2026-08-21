import { Fragment, useMemo } from "react";
import { cn } from "@/lib/utils";

export interface TimelineEntry {
  id: number;
  /** Translated label for the entry kind, e.g. "Gọi điện". */
  typeLabel: string;
  content: string;
  createdByName?: string | null;
  /** ISO timestamp. Leads use createdAt, customers use occurredAt. */
  at: string;
}

const dayKey = (iso: string) => new Date(iso).toDateString();

/**
 * Entries in today's group show only a time; older ones would be ambiguous that
 * way, so they keep their date.
 */
const stamp = (iso: string, locale: string) => {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  const isToday = dayKey(iso) === new Date().toDateString();
  return isToday
    ? date.toLocaleTimeString(locale, { hour: "2-digit", minute: "2-digit" })
    : date.toLocaleString(locale, {
        day: "2-digit",
        month: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
      });
};

const dayHeading = (iso: string, locale: string, todayLabel: string) => {
  const date = new Date(iso);
  if (dayKey(iso) === new Date().toDateString()) return todayLabel;
  return date.toLocaleDateString(locale, { day: "2-digit", month: "2-digit", year: "numeric" });
};

interface ActivityTimelineProps {
  entries: TimelineEntry[];
  locale: string;
  todayLabel: string;
  className?: string;
}

/**
 * Care history shared by the lead and customer detail dialogs — the two were
 * byte-for-byte the same markup before this.
 *
 * The list scrolls inside its own box: a customer with a long history used to
 * push the dialog's action buttons off the bottom of the screen.
 */
export const ActivityTimeline = ({
  entries,
  locale,
  todayLabel,
  className,
}: ActivityTimelineProps) => {
  const grouped = useMemo(() => {
    const out: { day: string; items: TimelineEntry[] }[] = [];
    for (const entry of entries) {
      const key = dayKey(entry.at);
      const last = out[out.length - 1];
      if (last && last.day === key) last.items.push(entry);
      else out.push({ day: key, items: [entry] });
    }
    return out;
  }, [entries]);

  return (
    <div className={cn("max-h-96 overflow-y-auto pr-1", className)}>
      {grouped.map((group) => (
        <Fragment key={group.day}>
          <div className="sticky top-0 z-10 bg-background py-1 text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
            {dayHeading(group.items[0].at, locale, todayLabel)}
          </div>
          <ol className="mb-2 space-y-2 border-l pl-4">
            {group.items.map((entry) => (
              <li key={entry.id} className="relative">
                <span className="absolute -left-[19px] top-1 h-2.5 w-2.5 rounded-full bg-primary" />
                <div className="text-xs text-muted-foreground">
                  {entry.typeLabel} · {stamp(entry.at, locale)}
                  {entry.createdByName ? ` · ${entry.createdByName}` : ""}
                </div>
                <div className="whitespace-pre-wrap text-sm">{entry.content}</div>
              </li>
            ))}
          </ol>
        </Fragment>
      ))}
    </div>
  );
};
