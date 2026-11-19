import { useId, useMemo, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { personOption, type PersonIdentity } from "@/lib/people";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Search, X } from "lucide-react";

type Person = PersonIdentity & {
  userId: number;
  name: string;
  email?: string;
  selectable?: boolean;
};

const normalize = (value: string) =>
  value.normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/đ/g, "d").toLocaleLowerCase();

// "Cung ứng · Cung ứng" (role named after its department) reads as one fact.
const dedupeHint = (hint = "") =>
  Array.from(new Set(hint.split(" · ").filter(Boolean))).join(" · ");

/**
 * Picks several people: one search field with a department filter beside it,
 * the chosen people as compact removable chips, and a checklist that can take
 * everyone it currently shows in one click (e.g. a whole department).
 */
export function PeopleChecklist({
  people,
  selected,
  onChange,
}: {
  people: Person[];
  selected: number[];
  onChange: (ids: number[]) => void;
}) {
  const { t } = useI18n();
  const id = useId();
  const [department, setDepartment] = useState("all");
  const [search, setSearch] = useState("");
  const options = useMemo(
    () =>
      people.map((person) => {
        const option = personOption(person, t);
        return {
          ...option,
          hint: dedupeHint(option.hint),
          selectable: person.selectable !== false,
        };
      }),
    [people, t],
  );
  const departments = Array.from(
    new Map(options.map((option) => [option.department!.value, option.department!])).values(),
  ).sort((a, b) => a.label.localeCompare(b.label));
  const query = normalize(search.trim());
  const visible = options.filter(
    (option) =>
      option.selectable &&
      (department === "all" || option.department?.value === department) &&
      normalize(`${option.label} ${option.hint} ${option.keywords}`).includes(query),
  );
  const chosen = options.filter((option) => selected.includes(Number(option.value)));
  const unselectedVisible = visible.filter((option) => !selected.includes(Number(option.value)));
  const toggle = (value: string, checked: boolean) =>
    onChange(
      checked ? [...selected, Number(value)] : selected.filter((item) => String(item) !== value),
    );

  return (
    <div className="mt-2 min-w-0 space-y-3" data-testid="people-checklist">
      <div className="flex min-w-0 flex-col gap-2 sm:flex-row">
        <div className="relative min-w-0 flex-1">
          <label htmlFor={`${id}-search`} className="sr-only">
            {t("common.search")}
          </label>
          <Search
            className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
          <Input
            id={`${id}-search`}
            type="search"
            className="pl-9"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t("people.search")}
            autoComplete="off"
          />
        </div>
        <label htmlFor={`${id}-department`} className="sr-only">
          {t("people.departmentFilter")}
        </label>
        <select
          id={`${id}-department`}
          value={department}
          onChange={(event) => setDepartment(event.target.value)}
          className="h-10 w-full min-w-0 rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 sm:w-56"
        >
          <option value="all">{t("people.allDepartments")}</option>
          {departments.map((item) => (
            <option key={item.value} value={item.value}>
              {item.label}
            </option>
          ))}
        </select>
      </div>

      {chosen.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center justify-between gap-3 text-xs">
            <span className="font-medium text-muted-foreground">
              {t("people.selected")}: {chosen.length}
            </span>
            {chosen.length > 1 && (
              <button
                type="button"
                className="rounded-sm font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                onClick={() => onChange([])}
              >
                {t("people.clearAll")}
              </button>
            )}
          </div>
          <ul className="flex flex-wrap gap-1.5" aria-label={t("people.selected")}>
            {chosen.map((option) => (
              <li key={option.value} className="min-w-0 max-w-full">
                <button
                  type="button"
                  title={option.hint}
                  onClick={() => toggle(option.value, false)}
                  aria-label={`${t("people.deselect")}: ${option.label}`}
                  className={cn(
                    "inline-flex min-h-8 max-w-full items-center gap-1.5 rounded-full border bg-muted/60 py-1 pl-3 pr-2 text-left text-sm transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    !option.selectable && "border-dashed text-muted-foreground",
                  )}
                >
                  <span className="min-w-0 truncate">
                    <span className="font-medium">{option.label}</span>
                    <span className="text-muted-foreground">
                      {" · "}
                      {option.selectable ? option.hint : t("people.inactiveSelected")}
                    </span>
                  </span>
                  <X className="h-3.5 w-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="overflow-hidden rounded-md border">
        {unselectedVisible.length > 1 && (
          <div className="flex items-center justify-between gap-3 border-b bg-muted/40 px-3 py-1.5 text-xs">
            <span className="text-muted-foreground">
              {t("people.visibleCount", { count: visible.length })}
            </span>
            <button
              type="button"
              className="rounded-sm font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              onClick={() =>
                onChange([...selected, ...unselectedVisible.map((option) => Number(option.value))])
              }
            >
              {t("people.selectAllVisible", { count: unselectedVisible.length })}
            </button>
          </div>
        )}
        <div className="max-h-60 overflow-y-auto p-1">
          {visible.length === 0 && (
            <p role="status" className="p-3 text-sm text-muted-foreground">
              {t(
                options.some((option) => option.selectable)
                  ? "people.noResults"
                  : "people.noneEligible",
              )}
            </p>
          )}
          {visible.map((option) => {
            const checked = selected.includes(Number(option.value));
            return (
              <label
                key={option.value}
                className={cn(
                  "flex min-h-11 cursor-pointer items-center gap-3 rounded-sm px-3 py-1.5 hover:bg-accent",
                  checked && "bg-primary/5",
                )}
              >
                <Checkbox
                  checked={checked}
                  onCheckedChange={(next) => toggle(option.value, next === true)}
                />
                <span className="flex min-w-0 flex-1 flex-col text-sm sm:flex-row sm:items-baseline sm:gap-2">
                  <span className="truncate font-medium">{option.label}</span>
                  <span className="truncate text-xs text-muted-foreground">{option.hint}</span>
                </span>
              </label>
            );
          })}
        </div>
      </div>
      <p role="status" aria-live="polite" className="sr-only">
        {t("people.resultCount", { count: visible.length, selected: selected.length })}
      </p>
    </div>
  );
}
