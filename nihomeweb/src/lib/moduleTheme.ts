/** Each part of the workspace has one colour; the values live in index.css (`.tone-*`). */
export type ModuleTone =
  | "indigo"
  | "violet"
  | "blue"
  | "cyan"
  | "teal"
  | "emerald"
  | "amber"
  | "orange"
  | "rose"
  | "pink"
  | "fuchsia"
  | "slate";

/** Menu group ids and the top-level routes that sit outside any group. */
const TONES: Record<string, ModuleTone> = {
  crm: "blue",
  design: "violet",
  procurement: "orange",
  finance: "emerald",
  permits: "amber",
  "construction-bch": "cyan",
  "construction-cm": "teal",
  analytics: "indigo",
  content: "pink",
  branding: "fuchsia",
  processes: "slate",
  users: "indigo",
  config: "slate",
  "/admin": "indigo",
  "/admin/notifications": "amber",
  "/admin/my-work": "rose",
  "/admin/daily-reports": "teal",
  "/admin/work-packages": "violet",
};

export const moduleTone = (key: string | undefined): ModuleTone =>
  (key ? TONES[key] : undefined) ?? "indigo";
