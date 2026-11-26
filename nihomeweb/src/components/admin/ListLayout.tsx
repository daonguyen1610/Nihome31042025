import { useState, type ReactNode } from "react";
import { Columns3, SlidersHorizontal } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/** The one page heading: name, how many records, one line of help and the page's actions. */
export function PageHeader({
  title,
  count,
  description,
  actions,
}: {
  title: string;
  count?: number;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <header className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
      <div className="min-w-0">
        {/* The count sits beside the heading, so the heading is still just the page's name. */}
        <div className="flex flex-wrap items-baseline gap-x-2">
          <h1 className="text-2xl font-semibold">{title}</h1>
          {count !== undefined && (
            <span className="tabular-nums text-muted-foreground">({count})</span>
          )}
        </div>
        {description && (
          <p className="mt-1 max-w-[75ch] text-sm text-muted-foreground">{description}</p>
        )}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap gap-2">{actions}</div>}
    </header>
  );
}

export type ListTab<T extends string> = {
  value: T;
  label: string;
  count?: number;
  /** Work that is late or blocked stands out even while another tab is open. */
  urgent?: boolean;
};

/** Quick views over one list. Toggle buttons, not tabs: they filter, they don't swap panels. */
export function ListTabs<T extends string>({
  label,
  tabs,
  value,
  onChange,
}: {
  label: string;
  tabs: ListTab<T>[];
  value: T;
  onChange: (next: T) => void;
}) {
  return (
    <div className="flex gap-1 overflow-x-auto border-b" role="group" aria-label={label}>
      {tabs.map((tab) => {
        const active = tab.value === value;
        return (
          <button
            key={tab.value}
            type="button"
            aria-pressed={active}
            onClick={() => onChange(tab.value)}
            className={cn(
              "-mb-px flex shrink-0 items-center gap-2 whitespace-nowrap border-b-2 px-3 py-2.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring pointer-coarse:min-h-11",
              active
                ? "border-primary text-primary"
                : "border-transparent text-muted-foreground hover:text-foreground",
            )}
          >
            {tab.label}
            {tab.count !== undefined && (
              <span
                className={cn(
                  "rounded-full px-2 text-xs tabular-nums leading-5",
                  tab.urgent && tab.count > 0
                    ? "bg-danger-soft text-danger-strong"
                    : active
                      ? "bg-primary text-primary-foreground"
                      : "bg-muted text-muted-foreground",
                )}
              >
                {tab.count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}

/**
 * "All" plus one quick view per status that holds records. An empty status adds nothing
 * to choose, unless it is the one being viewed.
 */
export function statusTabs<T extends string>(
  allLabel: string,
  statuses: readonly T[],
  counts: Record<string, number>,
  selected: T | "",
  labelOf: (status: T) => string,
): ListTab<T | "">[] {
  return [
    {
      value: "",
      label: allLabel,
      count: Object.values(counts).reduce((sum, value) => sum + value, 0),
    },
    ...statuses
      .filter((status) => (counts[status] ?? 0) > 0 || status === selected)
      .map((status) => ({ value: status, label: labelOf(status), count: counts[status] ?? 0 })),
  ];
}

/**
 * One bordered surface for a list: search and filters on top, the records below.
 * Filters stay mounted-on-demand so the table starts high on the screen.
 */
export function ListPanel({
  search,
  filters,
  filterCount,
  filtersOpen,
  onFiltersOpenChange,
  onClear,
  tools,
  children,
  footer,
}: {
  search: ReactNode;
  filters: ReactNode;
  /** Filters in effect besides the search box and the quick views. */
  filterCount: number;
  filtersOpen: boolean;
  onFiltersOpenChange: (open: boolean) => void;
  /** Present while anything narrows the list. */
  onClear?: () => void;
  tools?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <section className="overflow-hidden rounded-lg border bg-card">
      <div className="flex flex-wrap items-center gap-2 border-b p-3">
        <div className="min-w-0 flex-1 basis-56 sm:max-w-md">{search}</div>
        <Button
          type="button"
          variant="outline"
          aria-expanded={filtersOpen}
          onClick={() => onFiltersOpenChange(!filtersOpen)}
        >
          <SlidersHorizontal />
          {t("list.filters")}
          {filterCount > 0 && (
            <span className="rounded-full bg-primary px-2 text-xs leading-5 text-primary-foreground tabular-nums">
              {filterCount}
            </span>
          )}
        </Button>
        {onClear && (
          <Button type="button" variant="ghost" onClick={onClear}>
            {t("common.clearFilters")}
          </Button>
        )}
        {tools && <div className="ml-auto flex items-center gap-2">{tools}</div>}
      </div>
      {filtersOpen && (
        <div className="grid gap-3 border-b bg-muted/30 p-3 sm:grid-cols-2 lg:grid-cols-4">
          {filters}
        </div>
      )}
      {children}
      {footer && <div className="border-t p-3">{footer}</div>}
    </section>
  );
}

/** Columns this person chose to hide, remembered on this device. */
export function useHiddenColumns(storageKey: string) {
  const [hidden, setHidden] = useState<string[]>(() => {
    try {
      const saved = JSON.parse(localStorage.getItem(storageKey) ?? "[]");
      return Array.isArray(saved) ? saved : [];
    } catch {
      return [];
    }
  });
  const toggle = (id: string) =>
    setHidden((current) => {
      const next = current.includes(id) ? current.filter((x) => x !== id) : [...current, id];
      try {
        localStorage.setItem(storageKey, JSON.stringify(next));
      } catch {
        // The choice then lasts until the page is reloaded.
      }
      return next;
    });
  return { isShown: (id: string) => !hidden.includes(id), toggle };
}

/** Table-only control; cards below xl always show every fact. */
export function ColumnMenu({
  columns,
  isShown,
  onToggle,
}: {
  columns: { id: string; label: string }[];
  isShown: (id: string) => boolean;
  onToggle: (id: string) => void;
}) {
  const { t } = useI18n();
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" variant="outline" className="hidden xl:inline-flex">
          <Columns3 />
          {t("list.columns")}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {columns.map((column) => (
          <DropdownMenuCheckboxItem
            key={column.id}
            checked={isShown(column.id)}
            onCheckedChange={() => onToggle(column.id)}
            onSelect={(event) => event.preventDefault()}
          >
            {column.label}
          </DropdownMenuCheckboxItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
