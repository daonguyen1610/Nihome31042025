import { ReactNode } from "react";

/* Section card wrapper */
export const SettingSection = ({ title, children }: { title: string; children: ReactNode }) => (
  <div className="admin-card overflow-hidden">
    <div
      className="px-6 py-3 border-b text-sm font-bold uppercase tracking-wider"
      style={{
        background: "hsl(var(--admin-bg))",
        borderColor: "hsl(var(--admin-border))",
        color: "hsl(var(--admin-muted))",
      }}
    >
      {title}
    </div>
    <div className="divide-y" style={{ borderColor: "hsl(var(--admin-border))" }}>
      {children}
    </div>
  </div>
);

/* Single setting row */
export const SettingRow = ({ label, children }: { label: string; children: ReactNode }) => (
  <div className="grid grid-cols-1 md:grid-cols-12 gap-3 md:gap-6 px-6 py-4 items-center">
    <label className="md:col-span-5 lg:col-span-4 text-sm font-semibold md:text-right">
      {label}
    </label>
    <div className="md:col-span-7 lg:col-span-8">{children}</div>
  </div>
);

/* Inputs */
export const TextInput = (props: React.InputHTMLAttributes<HTMLInputElement>) => (
  <input
    {...props}
    className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none focus:ring-2 transition"
    style={{
      borderColor: "hsl(var(--admin-border))",
      // @ts-expect-error css var
      "--tw-ring-color": "hsl(var(--admin-primary) / 0.4)",
    }}
  />
);

export const NumberInput = (props: React.InputHTMLAttributes<HTMLInputElement>) => (
  <TextInput type="number" {...props} />
);

export const SelectInput = ({
  options,
  ...rest
}: React.SelectHTMLAttributes<HTMLSelectElement> & { options: { value: string; label: string }[] }) => (
  <select
    {...rest}
    className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none focus:ring-2 transition"
    style={{
      borderColor: "hsl(var(--admin-border))",
      // @ts-expect-error css var
      "--tw-ring-color": "hsl(var(--admin-primary) / 0.4)",
    }}
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
}: {
  on: boolean;
  onChange: (v: boolean) => void;
}) => (
  <button
    type="button"
    onClick={() => onChange(!on)}
    className="w-11 h-6 rounded-full relative transition shrink-0"
    style={{ background: on ? "hsl(var(--admin-primary))" : "hsl(var(--admin-border))" }}
    aria-pressed={on}
  >
    <span
      className="absolute top-0.5 w-5 h-5 rounded-full bg-white transition shadow"
      style={{ left: on ? "calc(100% - 22px)" : "2px" }}
    />
  </button>
);

export const TextArea = (props: React.TextareaHTMLAttributes<HTMLTextAreaElement>) => (
  <textarea
    {...props}
    className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none focus:ring-2 transition min-h-24"
    style={{
      borderColor: "hsl(var(--admin-border))",
      // @ts-expect-error css var
      "--tw-ring-color": "hsl(var(--admin-primary) / 0.4)",
    }}
  />
);
