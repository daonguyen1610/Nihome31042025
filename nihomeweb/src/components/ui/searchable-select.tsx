import * as React from "react";
import { Check, ChevronsUpDown } from "lucide-react";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";

export interface SearchableSelectOption {
  value: string;
  label: string;
  /** Optional secondary line rendered muted below <c>label</c>. */
  hint?: string;
  /** Extra text used only for the fuzzy filter — never rendered. */
  keywords?: string;
  /** Optional classes for a multi-line secondary description. */
  hintClassName?: string;
}

export interface SearchableSelectProps {
  value: string | null;
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
}: SearchableSelectProps) {
  const [open, setOpen] = React.useState(false);
  const selected = React.useMemo(
    () => options.find((o) => o.value === value) ?? null,
    [options, value],
  );

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-label={ariaLabel}
          aria-expanded={open}
          disabled={disabled}
          className={cn(
            "w-full justify-between font-normal",
            !selected && "text-muted-foreground",
            className,
          )}
        >
          <span className="truncate text-left">
            {selected ? selected.label : placeholder}
          </span>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        className="p-0"
        style={popoverWidthPx ? { width: popoverWidthPx } : { width: "var(--radix-popover-trigger-width)" }}
        align="start"
      >
        <Command shouldFilter={shouldFilter}>
          <CommandInput
            placeholder={searchPlaceholder ?? placeholder}
            value={searchValue}
            onValueChange={onSearchValueChange}
          />
          <CommandList>
            {!loading && <CommandEmpty>{emptyText ?? "—"}</CommandEmpty>}
            {loading ? (
              <div className="py-6 text-center text-sm text-muted-foreground">{loadingText}</div>
            ) : (
              <CommandGroup>
                {options.map((o) => (
                  <CommandItem
                    key={o.value}
                    value={`${o.label} ${o.hint ?? ""} ${o.keywords ?? ""}`}
                    onSelect={() => {
                      onChange(o.value);
                      setOpen(false);
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
                        <span className={cn("truncate text-xs text-muted-foreground", o.hintClassName)}>
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
