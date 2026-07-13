import { type ButtonHTMLAttributes, type ReactNode } from "react";
import { cn } from "@/lib/utils";

/* Section card wrapper */
export const SettingSection = ({ title, children }: { title: string; children: ReactNode }) => (
  <div className="overflow-hidden rounded-lg border bg-card">
    <div className="border-b bg-muted/50 px-4 py-2.5 text-xs font-medium uppercase tracking-wide text-muted-foreground">
      {title}
    </div>
    <div className="divide-y">
      {children}
    </div>
  </div>
);

/* Single setting row */
export const SettingRow = ({ label, children }: { label: string; children: ReactNode }) => (
  <div className="grid grid-cols-1 items-center gap-3 px-4 py-3 md:grid-cols-12 md:gap-6">
    <label className="text-sm font-medium md:col-span-5 md:text-right lg:col-span-4">
      {label}
    </label>
    <div className="md:col-span-7 lg:col-span-8">{children}</div>
  </div>
);

/* Inputs */
export const TextInput = ({ className, ...props }: React.InputHTMLAttributes<HTMLInputElement>) => (
  <input
    {...props}
    className={cn(
      "flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50",
      className,
    )}
  />
);

export const NumberInput = (props: React.InputHTMLAttributes<HTMLInputElement>) => (
  <TextInput type="number" {...props} />
);

export const SelectInput = ({
  options,
  className,
  ...rest
}: React.SelectHTMLAttributes<HTMLSelectElement> & { options: { value: string; label: string }[] }) => (
  <select
    {...rest}
    className={cn(
      "flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50",
      className,
    )}
  >
    {options.map((o) => (
      <option key={o.value} value={o.value}>
        {o.label}
      </option>
    ))}
  </select>
);

export const Toggle = ({
  on,
  onChange,
  disabled = false,
  ariaLabel,
  ...buttonProps
}: {
  on: boolean;
  onChange: (v: boolean) => void;
  disabled?: boolean;
  ariaLabel?: string;
} & Omit<ButtonHTMLAttributes<HTMLButtonElement>, "type" | "onClick" | "onChange" | "aria-pressed">) => (
  <button
    {...buttonProps}
    type="button"
    onClick={() => onChange(!on)}
    disabled={disabled}
    className={cn(
      "relative h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60",
      on ? "bg-primary" : "bg-muted",
    )}
    aria-pressed={on}
    aria-label={ariaLabel ?? "Toggle setting"}
  >
    <span
      className={cn(
        "pointer-events-none block h-5 w-5 rounded-full bg-background shadow-lg ring-0 transition-transform",
        on ? "translate-x-5" : "translate-x-0",
      )}
    />
  </button>
);

export const TextArea = ({ className, ...props }: React.TextareaHTMLAttributes<HTMLTextAreaElement>) => (
  <textarea
    {...props}
    className={cn(
      "flex min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50",
      className,
    )}
  />
);
