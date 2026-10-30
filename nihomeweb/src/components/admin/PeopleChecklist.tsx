import { useId, useMemo, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { personOption, type PersonIdentity } from "@/lib/people";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { X } from "lucide-react";

type Person = PersonIdentity & {
  userId: number;
  name: string;
  email?: string;
  selectable?: boolean;
};

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
      people.map((person) => ({
        ...personOption(person, t),
        selectable: person.selectable !== false,
      })),
    [people, t],
  );
  const departments = Array.from(
    new Map(options.map((option) => [option.department!.value, option.department!])).values(),
  ).sort((a, b) => a.label.localeCompare(b.label));
  const normalize = (value: string) =>
    value
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/đ/g, "d")
      .toLocaleLowerCase();
  const visible = options.filter(
    (option) =>
      option.selectable &&
      (department === "all" || option.department?.value === department) &&
      normalize(`${option.label} ${option.hint} ${option.keywords}`).includes(
        normalize(search.trim()),
      ),
  );
  return (
    <div className="mt-2 min-w-0 space-y-3" data-testid="people-checklist">
      <div className="grid min-w-0 grid-cols-1 gap-2 sm:grid-cols-2">
        <div className="min-w-0 space-y-1">
          <label htmlFor={`${id}-department`} className="text-xs font-medium text-muted-foreground">
            {t("people.departmentFilter")}
          </label>
          <select
            id={`${id}-department`}
            value={department}
            onChange={(event) => setDepartment(event.target.value)}
            className="h-11 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <option value="all">{t("people.allDepartments")}</option>
            {departments.map((item) => (
              <option key={item.value} value={item.value}>
                {item.label}
              </option>
            ))}
          </select>
        </div>
        <div className="min-w-0 space-y-1">
          <label htmlFor={`${id}-search`} className="text-xs font-medium text-muted-foreground">
            {t("common.search")}
          </label>
          <Input
            id={`${id}-search`}
            className="h-11"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t("people.search")}
          />
        </div>
      </div>
      {selected.length > 0 && (
        <div className="flex flex-wrap items-center gap-2" aria-label={t("people.selected")}>
          <span className="text-xs text-muted-foreground">
            {t("people.selected")}: {selected.length}
          </span>
          {options
            .filter((option) => selected.includes(Number(option.value)))
            .map((option) => (
              <Button
                key={option.value}
                size="sm"
                variant="secondary"
                type="button"
                className="h-auto min-h-11 min-w-0 max-w-full whitespace-normal text-left"
                onClick={() => onChange(selected.filter((value) => String(value) !== option.value))}
                aria-label={`${t("people.deselect")}: ${option.label}`}
              >
                <span className="min-w-0 flex-1 break-words">
                  {option.label} · {option.hint}
                  {!option.selectable && <> · {t("people.inactiveSelected")}</>}
                </span>
                <X className="ml-2 h-4 w-4 shrink-0" aria-hidden="true" />
              </Button>
            ))}
        </div>
      )}
      <div className="max-h-56 overflow-y-auto rounded-md border p-1">
        {visible.length === 0 && (
          <p role="status" className="p-3 text-sm text-muted-foreground">
            {t(
              options.some((option) => option.selectable)
                ? "people.noResults"
                : "people.noneEligible",
            )}
          </p>
        )}
        {visible.map((option) => (
          <label
            key={option.value}
            className="flex min-h-12 cursor-pointer items-center gap-3 rounded-sm px-3 py-2 hover:bg-accent"
          >
            <Checkbox
              checked={selected.includes(Number(option.value))}
              onCheckedChange={(checked) =>
                onChange(
                  checked
                    ? [...selected, Number(option.value)]
                    : selected.filter((value) => String(value) !== option.value),
                )
              }
            />
            <span className="min-w-0 break-words text-sm">
              <span className="block font-medium">{option.label}</span>
              <span className="block text-xs text-muted-foreground">{option.hint}</span>
            </span>
          </label>
        ))}
      </div>
      <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
        {t("people.resultCount", { count: visible.length, selected: selected.length })}
      </p>
    </div>
  );
}
