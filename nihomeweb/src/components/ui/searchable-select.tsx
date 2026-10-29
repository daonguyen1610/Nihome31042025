import * as React from "react";
import { Check, ChevronsUpDown } from "lucide-react";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";

export interface SearchableSelectOption {
  value: string;
  label: string;
  /** Optional secondary line rendered muted below <c>label</c>. */
  hint?: string;
  /** Extra text used only for the fuzzy filter — never rendered. */
  keywords?: string;
  /** Optional classes for a multi-line secondary description. */
  hintClassName?: string;
  department?: { value: string; label: string };
}

export interface SearchableSelectProps {
  value?: string | null;
  onChange: (value: string) => void;
  options: SearchableSelectOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  ariaLabel?: string;
  disabled?: boolean;
  className?: string;
  searchValue?: string;
  onSearchValueChange?: (value: string) => void;
  shouldFilter?: boolean;
  loading?: boolean;
  loadingText?: string;
  /** Fixed pixel width for the popover; defaults to the trigger width. */
  popoverWidthPx?: number;
  id?: string;
  required?: boolean;
  "data-testid"?: string;
}

/**
 * Trigger + Popover + Command wrapper — same look as a shadcn Select but
 * with type-to-filter over an arbitrarily long option list. Filtering defaults
 * to cmdk's client-side matcher over <c>label</c>, <c>hint</c> and
 * <c>keywords</c>; controlled search props allow server-backed pickers.
 */
export function SearchableSelect({
  value,
  onChange,
  options,
  placeholder = "—",
  searchPlaceholder,
  emptyText,
  ariaLabel,
  disabled,
  className,
  searchValue,
  onSearchValueChange,
  shouldFilter = true,
  loading = false,
  loadingText = "…",
  popoverWidthPx,
  id,
  required,
  "data-testid": testId,
}: SearchableSelectProps) {
  const { t } = useI18n();
  const [open, setOpen] = React.useState(false);
  const [department, setDepartment] = React.useState("all");
  const filterId = React.useId();
  const searchInput = React.useRef<HTMLInputElement>(null);
  const departments = React.useMemo(
    () =>
      Array.from(
        new Map(
          options.flatMap((option) =>
            option.department ? [[option.department.value, option.department] as const] : [],
          ),
        ).values(),
      ).sort((a, b) => a.label.localeCompare(b.label)),
    [options],
  );
  const visibleOptions =
    department === "all"
      ? options
      : options.filter((option) => !option.department || option.department.value === department);
  const selected = React.useMemo(
    () => options.find((o) => o.value === value) ?? null,
    [options, value],
  );

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) setDepartment("all");
      }}
    >
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          id={id}
          data-testid={testId}
          aria-required={required}
          aria-label={ariaLabel ?? placeholder}
          aria-expanded={open}
          disabled={disabled}
          className={cn(
            "w-full justify-between font-normal",
            selected?.department && "h-auto min-h-12 py-2",
            !selected && "text-muted-foreground",
            className,
          )}
        >
          <span className="min-w-0 flex-1 text-left">
            <span className="block truncate">{selected ? selected.label : placeholder}</span>
            {selected?.department && selected.hint && (
              <span className="block truncate text-xs text-muted-foreground">{selected.hint}</span>
            )}
          </span>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        className="max-w-[calc(100vw-2rem)] p-0"
        style={
          popoverWidthPx
            ? { width: popoverWidthPx }
            : { width: "var(--radix-popover-trigger-width)" }
        }
        align="start"
        data-people-picker={departments.length ? "true" : undefined}
        onOpenAutoFocus={(event) => {
          if (departments.length > 0) {
            event.preventDefault();
            searchInput.current?.focus();
          }
        }}
      >
        {departments.length > 0 && (
          <div className="space-y-1 border-b p-3">
            <label htmlFor={filterId} className="text-xs font-medium text-muted-foreground">
              {t("people.departmentFilter")}
            </label>
            <select
              id={filterId}
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
        )}
        <Command shouldFilter={shouldFilter}>
          <CommandInput
            ref={searchInput}
            placeholder={
              searchPlaceholder ?? (departments.length ? t("people.search") : placeholder)
            }
            value={searchValue}
            onValueChange={onSearchValueChange}
          />
          <CommandList>
            {!loading && (
              <CommandEmpty>
                {emptyText ?? (departments.length ? t("people.noResults") : "—")}
              </CommandEmpty>
            )}
            {loading ? (
              <div className="py-6 text-center text-sm text-muted-foreground">{loadingText}</div>
            ) : (
              <CommandGroup>
                {visibleOptions.map((o) => (
                  <CommandItem
                    key={o.value}
                    value={`${o.value} ${o.label} ${o.hint ?? ""} ${o.keywords ?? ""}`}
                    className="min-h-11"
                    onSelect={() => {
                      onChange(o.value);
                      setOpen(false);
                      setDepartment("all");
                    }}
                  >
                    <Check
                      className={cn(
                        "mr-2 h-4 w-4",
                        selected?.value === o.value ? "opacity-100" : "opacity-0",
                      )}
                    />
                    <div className="flex min-w-0 flex-col">
                      <span className="truncate">{o.label}</span>
                      {o.hint && (
                        <span
                          className={cn(
                            "whitespace-normal break-words text-xs text-muted-foreground",
                            o.hintClassName,
                          )}
                        >
                          {o.hint}
                        </span>
                      )}
                    </div>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
