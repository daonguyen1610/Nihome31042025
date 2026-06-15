import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import {
  CheckCircle2,
  Loader2,
  Minus,
  Pencil,
  Plus,
  Save,
  Sparkles,
  Trash2,
  Upload,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useToast } from "@/hooks/use-toast";
import { useI18n } from "@/lib/i18n";
import {
  ABOUT_ICON_META,
  DEFAULT_STATS_ICON_KEYS,
  DEFAULT_STRATEGY_ICON_KEYS,
  DEFAULT_VALUE_ICON_KEYS,
  parseOrganizationContent,
  resolveAboutIconKey,
  sortItemsBySortOrder,
  type AboutIconKey,
} from "@/lib/aboutSectionContent";
import { adminApi, type AboutSectionAdminResponse, type UpsertAboutSectionRequest } from "@/services/adminApi";

type AboutForm = {
  id: number;
  slug: string;
  itemsJson: string;
  eyebrow: string;
  titleA: string;
  titleB: string;
  paragraph1: string;
  paragraph2: string;
  imageUrl: string;
  isActive: boolean;
  sortOrder: number;
};

type SectionTab = {
  slug: string;
  labelKey: string;
  descriptionKey: string;
  showEyebrow?: boolean;
  showTitleA?: boolean;
  showTitleB?: boolean;
  showParagraph1?: boolean;
  showParagraph2?: boolean;
  showImage?: boolean;
  editor?: "stats" | "values" | "strategy" | "organization" | "timeline" | "certs" | "downloads";
};

type StatItem = {
  id?: string;
  iconKey?: string;
  iconClass?: string;
  num: string;
  label: string;
  isActive?: boolean;
  sortOrder?: number;
};

type IconTextItem = {
  id?: string;
  iconKey?: string;
  iconClass?: string;
  title: string;
  desc: string;
  isActive?: boolean;
  sortOrder?: number;
};

type LeaderItem = {
  id?: string;
  role: string;
  name: string;
  isActive?: boolean;
  sortOrder?: number;
};

type OrganizationItem = {
  board: LeaderItem[];
  directors: LeaderItem[];
  companyChartUrl?: string;
  siteChartUrl?: string;
};

type LeaderItemInput = {
  id?: string;
  role?: string;
  name?: string;
  isActive?: boolean;
  sortOrder?: number;
};

type TimelineItem = {
  id?: string;
  year: string;
  title: string;
  desc: string;
  sortOrder?: number;
};

type CertItem = {
  id?: string;
  name: string;
  desc: string;
  imageUrl?: string;
  sortOrder?: number;
};

type DownloadItem = {
  id?: string;
  name: string;
  size: string;
  type: string;
  url: string;
  sortOrder?: number;
};

type StatDraft = {
  id: string;
  iconKey: AboutIconKey;
  num: string;
  label: string;
  isActive: boolean;
  sortOrder: number;
};

type IconTextDraft = {
  id: string;
  iconKey: AboutIconKey;
  title: string;
  desc: string;
  isActive: boolean;
  sortOrder: number;
};

const SECTION_TABS: SectionTab[] = [
  {
    slug: "about-main",
    labelKey: "aboutAdmin.tab.intro",
    descriptionKey: "aboutAdmin.tabDesc.intro",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showParagraph1: true,
    showParagraph2: true,
    showImage: true,
  },
  {
    slug: "stats-main",
    labelKey: "aboutAdmin.tab.stats",
    descriptionKey: "aboutAdmin.tabDesc.stats",
    editor: "stats",
  },
  {
    slug: "values-main",
    labelKey: "aboutAdmin.tab.values",
    descriptionKey: "aboutAdmin.tabDesc.values",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    editor: "values",
  },
  {
    slug: "strategy-main",
    labelKey: "aboutAdmin.tab.strategy",
    descriptionKey: "aboutAdmin.tabDesc.strategy",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showParagraph1: true,
    showParagraph2: true,
    editor: "strategy",
  },
  {
    slug: "organization-main",
    labelKey: "aboutAdmin.tab.organization",
    descriptionKey: "aboutAdmin.tabDesc.organization",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    editor: "organization",
  },
  {
    slug: "timeline-main",
    labelKey: "aboutAdmin.tab.timeline",
    descriptionKey: "aboutAdmin.tabDesc.timeline",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showImage: true,
    editor: "timeline",
  },
  {
    slug: "certs-main",
    labelKey: "aboutAdmin.tab.certs",
    descriptionKey: "aboutAdmin.tabDesc.certs",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    editor: "certs",
  },
  {
    slug: "downloads-main",
    labelKey: "aboutAdmin.tab.downloads",
    descriptionKey: "aboutAdmin.tabDesc.downloads",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showParagraph1: true,
    editor: "downloads",
  },
];

const emptyForm = (slug: string, sortOrder: number): AboutForm => ({
  id: 0,
  slug,
  itemsJson: "",
  eyebrow: "",
  titleA: "",
  titleB: "",
  paragraph1: "",
  paragraph2: "",
  imageUrl: "",
  isActive: true,
  sortOrder,
});

const toForm = (item: AboutSectionAdminResponse): AboutForm => ({
  id: item.id,
  slug: item.slug,
  itemsJson: item.itemsJson ?? "",
  eyebrow: item.eyebrow,
  titleA: item.titleA,
  titleB: item.titleB,
  paragraph1: item.paragraph1,
  paragraph2: item.paragraph2,
  imageUrl: item.imageUrl,
  isActive: item.isActive,
  sortOrder: item.sortOrder,
});

const parseItems = <T,>(value: string | null | undefined, fallback: T): T => {
  if (!value?.trim()) return fallback;

  try {
    return JSON.parse(value) as T;
  } catch {
    return fallback;
  }
};

const parseItemArray = <T,>(value: string | null | undefined): T[] => {
  const parsed = parseItems<unknown>(value, []);
  return Array.isArray(parsed) ? (parsed as T[]) : [];
};

const serializeItems = (value: unknown) => JSON.stringify(value, null, 2);
const createLocalId = (prefix: string) => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
const normalizeSortOrder = (value: number | undefined, fallback: number) => (Number.isFinite(value) ? value ?? fallback : fallback);
const nextSortOrder = <T extends { sortOrder?: number }>(items: T[]) =>
  items.reduce((max, item) => Math.max(max, normalizeSortOrder(item.sortOrder, 0)), -1) + 1;

const normalizeStatItems = (raw: string | null | undefined): StatItem[] =>
  sortItemsBySortOrder(
    parseItemArray<StatItem>(raw).map((item, index) => ({
      id: item.id ?? createLocalId("stat"),
      iconKey: resolveAboutIconKey(item.iconKey ?? item.iconClass, DEFAULT_STATS_ICON_KEYS[index] ?? "calendar"),
      num: item.num ?? "",
      label: item.label ?? "",
      isActive: item.isActive ?? true,
      sortOrder: normalizeSortOrder(item.sortOrder, index),
    })),
  );

const normalizeIconTextItems = (
  raw: string | null | undefined,
  prefix: string,
  fallbackIcons: AboutIconKey[],
): IconTextItem[] =>
  sortItemsBySortOrder(
    parseItemArray<IconTextItem>(raw).map((item, index) => ({
      id: item.id ?? createLocalId(prefix),
      iconKey: resolveAboutIconKey(item.iconKey ?? item.iconClass, fallbackIcons[index] ?? "star"),
      title: item.title ?? "",
      desc: item.desc ?? "",
      isActive: item.isActive ?? true,
      sortOrder: normalizeSortOrder(item.sortOrder, index),
    })),
  );

const normalizeLeaderItems = (items: LeaderItemInput[], prefix: string) =>
  sortItemsBySortOrder(
    items.map((item, index) => ({
      id: item.id ?? createLocalId(prefix),
      role: item.role ?? "",
      name: item.name ?? "",
      isActive: item.isActive ?? true,
      sortOrder: normalizeSortOrder(item.sortOrder, index),
    })),
  );

const normalizeOrganizationItems = (raw: string | null | undefined): OrganizationItem => {
  const parsed = parseOrganizationContent(raw);
  return {
    board: normalizeLeaderItems(parsed.board ?? [], "board"),
    directors: normalizeLeaderItems(parsed.directors ?? [], "director"),
    companyChartUrl: parsed.companyChartUrl ?? "",
    siteChartUrl: parsed.siteChartUrl ?? "",
  };
};

const normalizeTimelineItems = (raw: string | null | undefined): TimelineItem[] =>
  sortItemsBySortOrder(
    parseItemArray<TimelineItem>(raw).map((item, index) => ({
      id: item.id ?? createLocalId("timeline"),
      year: item.year ?? "",
      title: item.title ?? "",
      desc: item.desc ?? "",
      sortOrder: normalizeSortOrder(item.sortOrder, index),
    })),
  );

const normalizeCertItems = (raw: string | null | undefined): CertItem[] =>
  sortItemsBySortOrder(
    parseItemArray<CertItem>(raw).map((item, index) => ({
      id: item.id ?? createLocalId("cert"),
      name: item.name ?? "",
      desc: item.desc ?? "",
      imageUrl: item.imageUrl ?? "",
      sortOrder: normalizeSortOrder(item.sortOrder, index),
    })),
  );

const normalizeDownloadItems = (raw: string | null | undefined): DownloadItem[] =>
  sortItemsBySortOrder(
    parseItemArray<DownloadItem>(raw).map((item, index) => ({
      id: item.id ?? createLocalId("download"),
      name: item.name ?? "",
      size: item.size ?? "",
      type: item.type ?? "",
      url: item.url ?? "#",
      sortOrder: normalizeSortOrder(item.sortOrder, index),
    })),
  );

const normalizeItemsJsonBySlug = (slug: string, raw: string | null | undefined) => {
  switch (slug) {
    case "stats-main":
      return serializeItems(normalizeStatItems(raw));
    case "values-main":
      return serializeItems(normalizeIconTextItems(raw, "value", DEFAULT_VALUE_ICON_KEYS));
    case "strategy-main":
      return serializeItems(normalizeIconTextItems(raw, "strategy", DEFAULT_STRATEGY_ICON_KEYS));
    case "organization-main":
      return serializeItems(normalizeOrganizationItems(raw));
    case "timeline-main":
      return serializeItems(normalizeTimelineItems(raw));
    case "certs-main":
      return serializeItems(normalizeCertItems(raw));
    case "downloads-main":
      return serializeItems(normalizeDownloadItems(raw));
    default:
      return raw?.trim() ?? "";
  }
};

const normalizeFormForTab = (form: AboutForm) => ({
  ...form,
  itemsJson: normalizeItemsJsonBySlug(form.slug, form.itemsJson),
});

const EditorSection = ({
  title,
  actionLabel,
  onAdd,
  children,
}: {
  title: string;
  actionLabel: string;
  onAdd: () => void;
  children: ReactNode;
}) => (
  <div className="space-y-3">
    <div className="flex items-center justify-between gap-3">
      <h3 className="font-bold text-sm">{title}</h3>
      <button
        type="button"
        onClick={onAdd}
        className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted"
      >
        <Plus className="w-4 h-4" />
        {actionLabel}
      </button>
    </div>
    {children}
  </div>
);

const RowCard = ({ children }: { children: ReactNode }) => (
  <div className="rounded-2xl border border-border p-4 bg-muted/20 space-y-3">{children}</div>
);

const Field = ({ label, children }: { label: string; children: ReactNode }) => (
  <label className="block space-y-1.5">
    <span className="text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </span>
    {children}
  </label>
);

const SortOrderField = ({
  label,
  value,
  onChange,
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
}) => {
  const safeValue = Math.max(0, Number.isFinite(value) ? value : 0);

  return (
    <div className="about-sort-field">
      <div className="about-sort-badge">
        <span className="about-sort-badge-label">{label}</span>
        <strong className="about-sort-badge-value">#{safeValue}</strong>
      </div>

      <div className="about-sort-stepper">
        <button type="button" className="about-sort-stepper-btn" onClick={() => onChange(Math.max(0, safeValue - 1))} aria-label={`${label} -1`}>
          <Minus className="w-4 h-4" />
        </button>
        <input
          className="about-sort-input"
          type="number"
          min={0}
          value={safeValue}
          onChange={(e) => onChange(Math.max(0, Number(e.target.value) || 0))}
        />
        <button type="button" className="about-sort-stepper-btn" onClick={() => onChange(safeValue + 1)} aria-label={`${label} +1`}>
          <Plus className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
};

const VisibilityField = ({
  label,
  checked,
  onChange,
  activeLabel,
  hiddenLabel,
  t,
}: {
  label: string;
  checked: boolean;
  onChange: (value: boolean) => void;
  activeLabel: string;
  hiddenLabel: string;
  t: (key: string) => string;
}) => (
  <div className="about-visibility-field">
    <div className="about-visibility-badge">
      <span className="about-visibility-badge-label">{label}</span>
      <strong className={`about-visibility-badge-value ${checked ? "is-active" : "is-hidden"}`}>
        {checked ? activeLabel : hiddenLabel}
      </strong>
    </div>

    <button
      type="button"
      className={`about-visibility-toggle ${checked ? "is-active" : ""}`}
      aria-pressed={checked}
      onClick={() => onChange(!checked)}
    >
      <span className="about-visibility-toggle-track">
        <span className="about-visibility-toggle-thumb" />
      </span>
      <span className="about-visibility-toggle-copy">
        <span className="about-visibility-toggle-title">{checked ? activeLabel : hiddenLabel}</span>
        <span className="about-visibility-toggle-subtitle">
          {checked ? t("aboutAdmin.visibleHint") : t("aboutAdmin.hiddenHint")}
        </span>
      </span>
    </button>
  </div>
);

const IconPicker = ({
  value,
  onChange,
  t,
}: {
  value: AboutIconKey;
  onChange: (value: AboutIconKey) => void;
  t: (key: string) => string;
}) => {
  const PreviewIcon = ABOUT_ICON_META[value].icon;

  return (
    <div className="about-icon-picker space-y-4">
      <div className="about-icon-current">
        <div className="about-icon-current-mark">
          <PreviewIcon className="w-6 h-6" style={{ color: "white" }} />
        </div>
        <div className="text-sm min-w-0 flex-1">
          <p className="font-display text-lg font-extrabold">{ABOUT_ICON_META[value].label}</p>
          <p style={{ color: "hsl(var(--admin-muted))" }}>{t("aboutAdmin.iconCurrentLabel")}</p>
        </div>
        <div className="about-icon-current-badge">NICON</div>
      </div>

      <div className="about-icon-grid">
        {Object.entries(ABOUT_ICON_META).map(([iconKey, meta]) => {
          const Icon = meta.icon;
          const isSelected = value === iconKey;

          return (
            <button
              key={iconKey}
              type="button"
              onClick={() => onChange(iconKey as AboutIconKey)}
              className={`about-icon-option ${isSelected ? "is-active" : ""}`}
              aria-label={meta.label}
              title={meta.label}
            >
              <div className="flex flex-col items-center justify-center gap-2">
                <div className="about-icon-option-mark">
                  <Icon className="w-5 h-5" />
                </div>
                <span className="about-icon-option-label">{meta.label}</span>
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
};

const StatListCard = ({
  item,
  onEdit,
  onDelete,
  t,
}: {
  item: StatItem;
  onEdit: () => void;
  onDelete: () => void;
  t: (key: string) => string;
}) => {
  const iconKey = resolveAboutIconKey(item.iconKey ?? item.iconClass, "calendar");
  const Icon = ABOUT_ICON_META[iconKey].icon;

  return (
    <div
      className="rounded-2xl border p-4"
      style={{ background: "hsl(var(--admin-surface))", borderColor: "hsl(var(--admin-border))" }}
    >
      <div className="flex items-center gap-3">
        <div
          className="w-14 h-14 rounded-xl flex items-center justify-center flex-shrink-0"
          style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary-soft)), rgba(255,255,255,0.92))" }}
        >
          <Icon className="w-6 h-6" style={{ color: "hsl(var(--admin-primary))" }} strokeWidth={2} />
        </div>
        <div className="min-w-0 flex-1">
          <div className="text-3xl font-extrabold leading-none truncate" style={{ color: "hsl(var(--admin-sidebar-text))" }}>
            {item.num || t("aboutAdmin.defaultStatValue")}
          </div>
          <div className="mt-1 text-base truncate" style={{ color: "hsl(var(--admin-muted))" }}>
            {item.label || t("aboutAdmin.defaultStatLabel")}
          </div>
          <div className="mt-2 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("aboutAdmin.fieldSortOrder")} #{normalizeSortOrder(item.sortOrder, 0)} • {ABOUT_ICON_META[iconKey].label}
          </div>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0">
          <span
            className="px-3 py-1.5 rounded-full text-sm font-semibold"
            style={
              item.isActive !== false
                ? { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }
                : { background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-muted))" }
            }
          >
            {item.isActive !== false ? t("aboutAdmin.statusActive") : t("aboutAdmin.statusHidden")}
          </span>
          <button
            type="button"
            onClick={onEdit}
            className="w-11 h-11 rounded-xl border inline-flex items-center justify-center hover:bg-muted/40"
            style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-primary))" }}
          >
            <Pencil className="w-4 h-4" />
          </button>
          <button
            type="button"
            onClick={onDelete}
            className="w-11 h-11 rounded-xl border inline-flex items-center justify-center hover:bg-muted/40"
            style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-danger))" }}
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
};

const IconTextListCard = ({
  item,
  onEdit,
  onDelete,
  t,
}: {
  item: IconTextItem;
  onEdit: () => void;
  onDelete: () => void;
  t: (key: string) => string;
}) => {
  const iconKey = resolveAboutIconKey(item.iconKey ?? item.iconClass, "star");
  const Icon = ABOUT_ICON_META[iconKey].icon;

  return (
    <div
      className="rounded-2xl border p-4"
      style={{ background: "hsl(var(--admin-surface))", borderColor: "hsl(var(--admin-border))" }}
    >
      <div className="flex items-start gap-3">
        <div
          className="w-14 h-14 rounded-xl flex items-center justify-center flex-shrink-0"
          style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary-soft)), rgba(255,255,255,0.92))" }}
        >
          <Icon className="w-6 h-6" style={{ color: "hsl(var(--admin-primary))" }} strokeWidth={2} />
        </div>
        <div className="min-w-0 flex-1">
          <div className="font-bold text-base truncate">{item.title || t("aboutAdmin.defaultItemTitle")}</div>
          <p className="mt-1 text-sm line-clamp-2" style={{ color: "hsl(var(--admin-muted))" }}>
            {item.desc || t("aboutAdmin.defaultItemDesc")}
          </p>
          <div className="mt-2 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("aboutAdmin.fieldSortOrder")} #{normalizeSortOrder(item.sortOrder, 0)} • {ABOUT_ICON_META[iconKey].label}
          </div>
        </div>
        <div className="flex items-center gap-2 flex-shrink-0">
          <span
            className="px-3 py-1.5 rounded-full text-sm font-semibold"
            style={
              item.isActive !== false
                ? { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }
                : { background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-muted))" }
            }
          >
            {item.isActive !== false ? t("aboutAdmin.statusActive") : t("aboutAdmin.statusHidden")}
          </span>
          <button
            type="button"
            onClick={onEdit}
            className="w-11 h-11 rounded-xl border inline-flex items-center justify-center hover:bg-muted/40"
            style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-primary))" }}
          >
            <Pencil className="w-4 h-4" />
          </button>
          <button
            type="button"
            onClick={onDelete}
            className="w-11 h-11 rounded-xl border inline-flex items-center justify-center hover:bg-muted/40"
            style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-danger))" }}
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
};

const PreviewIconProxy = ({ iconKey }: { iconKey: AboutIconKey }) => {
  const Icon = ABOUT_ICON_META[iconKey].icon;
  return <Icon className="w-7 h-7 text-white" strokeWidth={1.8} />;
};

const AboutContent = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const localizedTabs = useMemo(
    () => SECTION_TABS.map((tab) => ({ ...tab, label: t(tab.labelKey), description: t(tab.descriptionKey) })),
    [t],
  );
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadingCompanyChart, setUploadingCompanyChart] = useState(false);
  const [uploadingSiteChart, setUploadingSiteChart] = useState(false);
  const [uploadingCertIds, setUploadingCertIds] = useState<Set<string>>(new Set());
  const [uploadingDownloadIds, setUploadingDownloadIds] = useState<Set<string>>(new Set());
  const [activeSlug, setActiveSlug] = useState("about-main");
  const [forms, setForms] = useState<Record<string, AboutForm>>({});
  const [statDialogOpen, setStatDialogOpen] = useState(false);
  const [statDraft, setStatDraft] = useState<StatDraft | null>(null);
  const [iconDialogOpen, setIconDialogOpen] = useState(false);
  const [iconDialogEditor, setIconDialogEditor] = useState<"values" | "strategy">("values");
  const [iconDraft, setIconDraft] = useState<IconTextDraft | null>(null);
  const editorRef = useRef<HTMLDivElement | null>(null);
  const previousActiveSlugRef = useRef(activeSlug);

  useEffect(() => {
    let canceled = false;

    const load = async () => {
      setLoading(true);
      try {
        const { data } = await adminApi.getAboutSections(false);
        if (canceled) return;

        const nextForms = localizedTabs.reduce<Record<string, AboutForm>>((acc, tab, index) => {
          const existing = data.find((item) => item.slug === tab.slug);
          acc[tab.slug] = normalizeFormForTab(existing ? toForm(existing) : emptyForm(tab.slug, index));
          return acc;
        }, {});

        setForms(nextForms);
      } catch {
        if (canceled) return;
        toast({
          title: t("auth.error"),
          description: t("aboutAdmin.loadError"),
          variant: "destructive",
        });
      } finally {
        if (!canceled) setLoading(false);
      }
    };

    void load();

    return () => {
      canceled = true;
    };
  }, [localizedTabs, t, toast]);

  const activeTab = useMemo(
    () => localizedTabs.find((tab) => tab.slug === activeSlug) ?? localizedTabs[0],
    [activeSlug, localizedTabs],
  );

  useEffect(() => {
    if (loading) return;
    if (previousActiveSlugRef.current === activeSlug) return;

    previousActiveSlugRef.current = activeSlug;
    editorRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [activeSlug, loading]);

  const form = forms[activeSlug] ?? emptyForm(activeSlug, 0);
  const shouldShowStructuredEditorFirst = activeTab.editor === "organization";
  const statItems = useMemo(() => normalizeStatItems(form.itemsJson), [form.itemsJson]);
  const valueItems = useMemo(() => normalizeIconTextItems(form.itemsJson, "value", DEFAULT_VALUE_ICON_KEYS), [form.itemsJson]);
  const strategyItems = useMemo(() => normalizeIconTextItems(form.itemsJson, "strategy", DEFAULT_STRATEGY_ICON_KEYS), [form.itemsJson]);
  const organizationItems = useMemo(() => normalizeOrganizationItems(form.itemsJson), [form.itemsJson]);
  const timelineItems = useMemo(() => normalizeTimelineItems(form.itemsJson), [form.itemsJson]);
  const certItems = useMemo(() => normalizeCertItems(form.itemsJson), [form.itemsJson]);
  const downloadItems = useMemo(() => normalizeDownloadItems(form.itemsJson), [form.itemsJson]);

  const updateForm = <K extends keyof AboutForm>(key: K, value: AboutForm[K]) => {
    setForms((prev) => ({
      ...prev,
      [activeSlug]: {
        ...(prev[activeSlug] ?? emptyForm(activeSlug, 0)),
        [key]: value,
      },
    }));
  };

  const updateItemsJson = (value: unknown) => {
    updateForm("itemsJson", serializeItems(value));
  };

  const saveSection = async () => {
    const payload: UpsertAboutSectionRequest = {
      slug: form.slug,
      itemsJson: form.itemsJson.trim() || null,
      eyebrow: form.eyebrow.trim(),
      titleA: form.titleA.trim(),
      titleB: form.titleB.trim(),
      paragraph1: form.paragraph1.trim(),
      paragraph2: form.paragraph2.trim(),
      imageUrl: form.imageUrl.trim(),
      isActive: form.isActive,
      sortOrder: form.sortOrder,
    };

    setSaving(true);
    try {
      if (form.id > 0) {
        await adminApi.updateAboutSection(form.id, payload);
      } else {
        const response = await adminApi.createAboutSection(payload);
        setForms((prev) => ({
          ...prev,
          [activeSlug]: normalizeFormForTab(toForm(response.data)),
        }));
      }

      toast({ title: t("form.updated") });
    } catch {
      toast({
        title: t("auth.error"),
        description: t("aboutAdmin.saveError"),
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const handleImageUpload = async (file: File) => {
    setUploading(true);
    try {
      const previousImageUrl = form.id > 0 ? form.imageUrl : undefined;
      const response = await adminApi.uploadImage(file, previousImageUrl);
      updateForm("imageUrl", response.data.imageUrl);
      toast({ title: t("form.updated") });
    } catch {
      toast({ title: t("auth.error"), variant: "destructive" });
    } finally {
      setUploading(false);
    }
  };

  const onSelectFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    await handleImageUpload(file);
    e.target.value = "";
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const handleCertImageUpload = async (file: File, certId: string) => {
    setUploadingCertIds((prev) => new Set([...prev, certId]));
    try {
      const response = await adminApi.uploadImage(file);
      const next = certItems.map((item) =>
        item.id === certId ? { ...item, imageUrl: response.data.imageUrl } : item,
      );
      updateItemsJson(sortItemsBySortOrder(next));
    } catch {
      toast({ title: t("auth.error"), variant: "destructive" });
    } finally {
      setUploadingCertIds((prev) => {
        const next = new Set(prev);
        next.delete(certId);
        return next;
      });
    }
  };

  const handleDocumentUpload = async (file: File, downloadId: string) => {
    setUploadingDownloadIds((prev) => new Set([...prev, downloadId]));
    try {
      const response = await adminApi.uploadDocument(file);
      const ext = file.name.split(".").pop()?.toUpperCase() ?? "";
      const next = downloadItems.map((item) =>
        item.id === downloadId
          ? { ...item, url: response.data.cvUrl, size: formatFileSize(file.size), type: ext }
          : item,
      );
      updateItemsJson(sortItemsBySortOrder(next));
    } catch {
      toast({ title: t("auth.error"), variant: "destructive" });
    } finally {
      setUploadingDownloadIds((prev) => {
        const next = new Set(prev);
        next.delete(downloadId);
        return next;
      });
    }
  };

  const onSelectOrgChart = async (
    e: React.ChangeEvent<HTMLInputElement>,
    field: "companyChartUrl" | "siteChartUrl",
    setUploading: (v: boolean) => void,
  ) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    try {
      const response = await adminApi.uploadImage(file);
      updateItemsJson({ ...organizationItems, [field]: response.data.imageUrl });
    } catch {
      toast({ title: t("auth.error"), variant: "destructive" });
    } finally {
      setUploading(false);
      e.target.value = "";
    }
  };

  const updateStatItems = (nextItems: StatItem[]) => updateItemsJson(sortItemsBySortOrder(nextItems));
  const updateIconTextCollection = (nextItems: IconTextItem[]) => updateItemsJson(sortItemsBySortOrder(nextItems));

  const openStatDialog = (item?: StatItem) => {
    if (!item && statItems.length >= 4) return;

    const fallbackIcon = item
      ? resolveAboutIconKey(item.iconKey ?? item.iconClass, "calendar")
      : DEFAULT_STATS_ICON_KEYS[Math.min(statItems.length, DEFAULT_STATS_ICON_KEYS.length - 1)] ?? "calendar";

    setStatDraft({
      id: item?.id ?? createLocalId("stat"),
      iconKey: fallbackIcon,
      num: item?.num ?? "",
      label: item?.label ?? "",
      isActive: item?.isActive ?? true,
      sortOrder: item?.sortOrder ?? nextSortOrder(statItems),
    });
    setStatDialogOpen(true);
  };

  const saveStatDialog = () => {
    if (!statDraft) return;
    if (!statDraft.num.trim() || !statDraft.label.trim()) {
      toast({
        title: t("form.required"),
        description: t("aboutAdmin.validationStat"),
        variant: "destructive",
      });
      return;
    }

    const nextItem: StatItem = {
      id: statDraft.id,
      iconKey: statDraft.iconKey,
      num: statDraft.num.trim(),
      label: statDraft.label.trim(),
      isActive: statDraft.isActive,
      sortOrder: statDraft.sortOrder,
    };

    const exists = statItems.some((item) => item.id === statDraft.id);
    updateStatItems(exists ? statItems.map((item) => (item.id === statDraft.id ? nextItem : item)) : [...statItems, nextItem]);
    setStatDialogOpen(false);
    setStatDraft(null);
  };

  const openIconDialog = (editor: "values" | "strategy", item?: IconTextItem) => {
    const items = editor === "values" ? valueItems : strategyItems;
    const fallbackIcons = editor === "values" ? DEFAULT_VALUE_ICON_KEYS : DEFAULT_STRATEGY_ICON_KEYS;

    setIconDialogEditor(editor);
    setIconDraft({
      id: item?.id ?? createLocalId(editor),
      iconKey: resolveAboutIconKey(item?.iconKey ?? item?.iconClass, fallbackIcons[Math.min(items.length, fallbackIcons.length - 1)] ?? "star"),
      title: item?.title ?? "",
      desc: item?.desc ?? "",
      isActive: item?.isActive ?? true,
      sortOrder: item?.sortOrder ?? nextSortOrder(items),
    });
    setIconDialogOpen(true);
  };

  const saveIconDialog = () => {
    if (!iconDraft) return;
    if (!iconDraft.title.trim() || !iconDraft.desc.trim()) {
      toast({
        title: t("form.required"),
        description: t("aboutAdmin.validationItem"),
        variant: "destructive",
      });
      return;
    }

    const nextItem: IconTextItem = {
      id: iconDraft.id,
      iconKey: iconDraft.iconKey,
      title: iconDraft.title.trim(),
      desc: iconDraft.desc.trim(),
      isActive: iconDraft.isActive,
      sortOrder: iconDraft.sortOrder,
    };

    const items = iconDialogEditor === "values" ? valueItems : strategyItems;
    const exists = items.some((item) => item.id === iconDraft.id);
    updateIconTextCollection(exists ? items.map((item) => (item.id === iconDraft.id ? nextItem : item)) : [...items, nextItem]);
    setIconDialogOpen(false);
    setIconDraft(null);
  };

  const renderStructuredEditor = () => {
    if (activeTab.editor === "stats") {
      return (
        <div className="space-y-5">
          <EditorSection title={t("aboutAdmin.statsList")} actionLabel={t("aboutAdmin.addStat")} onAdd={() => openStatDialog()}>
            <div className="space-y-3">
              {statItems.length === 0 && (
                <p className="text-sm italic py-4" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("aboutAdmin.noStats")}
                </p>
              )}
              {statItems.map((item) => (
                <StatListCard
                  key={item.id}
                  item={item}
                  t={t}
                  onEdit={() => openStatDialog(item)}
                  onDelete={() => updateStatItems(statItems.filter((current) => current.id !== item.id))}
                />
              ))}
              {statItems.length >= 4 && (
                <p className="text-xs italic" style={{ color: "hsl(var(--admin-warning))" }}>
                  {t("aboutAdmin.maxStats")}
                </p>
              )}
            </div>
          </EditorSection>

          {statItems.some((item) => item.isActive !== false && (item.num || item.label)) && (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="font-bold text-sm">{t("aboutAdmin.previewTitle")}</h3>
                <span
                  className="text-xs px-2 py-1 rounded font-semibold"
                  style={{ background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }}
                >
                  {t("aboutAdmin.previewLive")}
                </span>
              </div>
              <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 p-4 rounded-xl" style={{ background: "hsl(var(--admin-bg))" }}>
                {statItems
                  .filter((item) => item.isActive !== false)
                  .map((item) => {
                    const iconKey = resolveAboutIconKey(item.iconKey ?? item.iconClass, "calendar");
                    const Icon = ABOUT_ICON_META[iconKey].icon;

                    return (
                      <div
                        key={item.id}
                        className="rounded-2xl p-4 text-center"
                        style={{ background: "hsl(var(--admin-surface))", borderColor: "hsl(var(--admin-border))", borderWidth: "1px" }}
                      >
                        <Icon className="w-6 h-6 mx-auto mb-3" style={{ color: "hsl(var(--admin-primary))" }} strokeWidth={1.5} />
                        <p className="font-display text-2xl font-extrabold mb-1" style={{ color: "hsl(var(--admin-primary))" }}>
                          {item.num || "—"}
                        </p>
                        <p className="text-xs font-medium leading-snug h-8 flex items-center justify-center" style={{ color: "hsl(var(--admin-muted))" }}>
                          {item.label || t("aboutAdmin.emptyValue")}
                        </p>
                      </div>
                    );
                  })}
              </div>
            </div>
          )}
        </div>
      );
    }

    if (activeTab.editor === "values" || activeTab.editor === "strategy") {
      const iconEditor: "values" | "strategy" = activeTab.editor;
      const items = iconEditor === "values" ? valueItems : strategyItems;
      const title = iconEditor === "values" ? t("aboutAdmin.valuesList") : t("aboutAdmin.strategyList");
      const actionLabel = iconEditor === "values" ? t("aboutAdmin.addValue") : t("aboutAdmin.addStrategy");

      return (
        <div className="space-y-4">
          <div className="p-3 rounded-xl border text-sm" style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-primary-soft))" }}>
            {t("aboutAdmin.iconDialogHint")}
          </div>
          <EditorSection title={title} actionLabel={actionLabel} onAdd={() => openIconDialog(iconEditor)}>
            <div className="space-y-3">
              {items.length === 0 && (
                <p className="text-sm italic py-4" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("aboutAdmin.noItems")}
                </p>
              )}
              {items.map((item) => (
                <IconTextListCard
                  key={item.id}
                  item={item}
                  t={t}
                  onEdit={() => openIconDialog(iconEditor, item)}
                  onDelete={() => updateIconTextCollection(items.filter((current) => current.id !== item.id))}
                />
              ))}
            </div>
          </EditorSection>
        </div>
      );
    }

    if (activeTab.editor === "organization") {
      const updateGroup = (group: "board" | "directors", nextItems: LeaderItem[]) =>
        updateItemsJson({ ...organizationItems, [group]: sortItemsBySortOrder(nextItems) });

      const renderGroup = (group: "board" | "directors", title: string) => (
        <EditorSection
          title={title}
          actionLabel={t("aboutAdmin.addMember")}
          onAdd={() =>
            updateGroup(group, [
              ...organizationItems[group],
              { id: createLocalId(group), role: "", name: "", isActive: true, sortOrder: nextSortOrder(organizationItems[group]) },
            ])
          }
        >
          <div className="space-y-3">
            {organizationItems[group].length === 0 && (
              <RowCard>
                <p className="text-sm italic py-2" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("aboutAdmin.noItems")}
                </p>
              </RowCard>
            )}
            {organizationItems[group].map((item, index) => (
              <RowCard key={item.id ?? `${group}-${index}`}>
                <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1fr)_minmax(0,1fr)_minmax(260px,0.95fr)] gap-3 items-start">
                  <SortOrderField
                    label={t("aboutAdmin.fieldSortOrder")}
                    value={normalizeSortOrder(item.sortOrder, index)}
                    onChange={(value) => {
                      const next = [...organizationItems[group]];
                      next[index] = { ...item, sortOrder: value };
                      updateGroup(group, next);
                    }}
                  />
                  <Field label={t("aboutAdmin.fieldRole")}>
                    <input
                      className="admin-input"
                      value={item.role}
                      onChange={(e) => {
                        const next = [...organizationItems[group]];
                        next[index] = { ...item, role: e.target.value };
                        updateGroup(group, next);
                      }}
                    />
                  </Field>
                  <Field label={t("aboutAdmin.fieldName")}>
                    <input
                      className="admin-input"
                      value={item.name}
                      onChange={(e) => {
                        const next = [...organizationItems[group]];
                        next[index] = { ...item, name: e.target.value };
                        updateGroup(group, next);
                      }}
                    />
                  </Field>
                  <VisibilityField
                    label={t("aboutAdmin.fieldVisible")}
                    checked={item.isActive !== false}
                    onChange={(value) => {
                      const next = [...organizationItems[group]];
                      next[index] = { ...item, isActive: value };
                      updateGroup(group, next);
                    }}
                    activeLabel={t("aboutAdmin.statusActive")}
                    hiddenLabel={t("aboutAdmin.statusHidden")}
                    t={t}
                  />
                </div>
                <button
                  type="button"
                  onClick={() => updateGroup(group, organizationItems[group].filter((_, i) => i !== index))}
                  className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted"
                >
                  <Trash2 className="w-4 h-4" />
                  {t("common.delete")}
                </button>
              </RowCard>
            ))}
          </div>
        </EditorSection>
      );

      const renderChartField = (
        field: "companyChartUrl" | "siteChartUrl",
        label: string,
        isUploading: boolean,
        setIsUploading: (v: boolean) => void,
      ) => (
        <div className="space-y-2">
          <Field label={label}>
            <div className="flex items-center gap-2">
              <input
                className="admin-input"
                value={organizationItems[field] ?? ""}
                onChange={(e) => updateItemsJson({ ...organizationItems, [field]: e.target.value })}
                placeholder="https://..."
              />
              <label className="px-3 py-2 rounded-xl border border-border hover:bg-muted inline-flex items-center gap-2 cursor-pointer text-sm shrink-0">
                <Upload className="w-4 h-4" />
                {isUploading ? <Loader2 className="w-4 h-4 animate-spin" /> : t("aboutAdmin.upload")}
                <input
                  type="file"
                  accept="image/*"
                  className="hidden"
                  disabled={isUploading}
                  onChange={(e) => void onSelectOrgChart(e, field, setIsUploading)}
                />
              </label>
            </div>
          </Field>
          {organizationItems[field] && (
            <div className="rounded-2xl overflow-hidden border border-border bg-muted/30">
              <img src={organizationItems[field]} alt={label} className="w-full object-contain max-h-72" />
            </div>
          )}
        </div>
      );

      return (
        <div className="space-y-5">
          <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
            {renderChartField("companyChartUrl", t("aboutAdmin.org.companyChart"), uploadingCompanyChart, setUploadingCompanyChart)}
            {renderChartField("siteChartUrl", t("aboutAdmin.org.siteChart"), uploadingSiteChart, setUploadingSiteChart)}
          </div>
          {renderGroup("board", t("aboutAdmin.boardList"))}
          {renderGroup("directors", t("aboutAdmin.directorsList"))}
        </div>
      );
    }

    if (activeTab.editor === "timeline") {
      return (
        <EditorSection
          title={t("aboutAdmin.timelineList")}
          actionLabel={t("aboutAdmin.addMilestone")}
          onAdd={() =>
            updateItemsJson([
              ...timelineItems,
              { id: createLocalId("timeline"), year: "", title: "", desc: "", sortOrder: nextSortOrder(timelineItems) },
            ])
          }
        >
          <div className="space-y-3">
            {timelineItems.map((item, index) => (
              <RowCard key={item.id ?? `timeline-${index}`}>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                  <Field label={t("aboutAdmin.fieldSortOrder")}>
                    <input
                      className="admin-input"
                      type="number"
                      value={normalizeSortOrder(item.sortOrder, index)}
                      onChange={(e) => {
                        const next = [...timelineItems];
                        next[index] = { ...item, sortOrder: Number(e.target.value) || 0 };
                        updateItemsJson(sortItemsBySortOrder(next));
                      }}
                    />
                  </Field>
                  <Field label={t("aboutAdmin.fieldYear")}>
                    <input
                      className="admin-input"
                      value={item.year}
                      onChange={(e) => {
                        const next = [...timelineItems];
                        next[index] = { ...item, year: e.target.value };
                        updateItemsJson(next);
                      }}
                    />
                  </Field>
                  <Field label={t("aboutAdmin.fieldTitle")}>
                    <input
                      className="admin-input"
                      value={item.title}
                      onChange={(e) => {
                        const next = [...timelineItems];
                        next[index] = { ...item, title: e.target.value };
                        updateItemsJson(next);
                      }}
                    />
                  </Field>
                </div>
                <Field label={t("aboutAdmin.fieldDesc")}>
                  <textarea
                    className="admin-input min-h-24"
                    value={item.desc}
                    onChange={(e) => {
                      const next = [...timelineItems];
                      next[index] = { ...item, desc: e.target.value };
                      updateItemsJson(next);
                    }}
                  />
                </Field>
                <button
                  type="button"
                  onClick={() => updateItemsJson(timelineItems.filter((_, i) => i !== index))}
                  className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted"
                >
                  <Trash2 className="w-4 h-4" />
                  {t("common.delete")}
                </button>
              </RowCard>
            ))}
          </div>
        </EditorSection>
      );
    }

    if (activeTab.editor === "certs") {
      return (
        <EditorSection
          title={t("aboutAdmin.certsList")}
          actionLabel={t("aboutAdmin.addCert")}
          onAdd={() =>
            updateItemsJson([...certItems, { id: createLocalId("cert"), name: "", desc: "", imageUrl: "", sortOrder: nextSortOrder(certItems) }])
          }
        >
          <div className="space-y-3">
            {certItems.map((item, index) => {
              const certId = item.id ?? `cert-${index}`;
              const isUploadingCert = uploadingCertIds.has(certId);
              return (
                <RowCard key={certId}>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    <Field label={t("aboutAdmin.fieldSortOrder")}>
                      <input
                        className="admin-input"
                        type="number"
                        value={normalizeSortOrder(item.sortOrder, index)}
                        onChange={(e) => {
                          const next = [...certItems];
                          next[index] = { ...item, sortOrder: Number(e.target.value) || 0 };
                          updateItemsJson(sortItemsBySortOrder(next));
                        }}
                      />
                    </Field>
                    <Field label={t("aboutAdmin.fieldCertName")}>
                      <input
                        className="admin-input"
                        value={item.name}
                        onChange={(e) => {
                          const next = [...certItems];
                          next[index] = { ...item, name: e.target.value };
                          updateItemsJson(next);
                        }}
                      />
                    </Field>
                  </div>
                  <Field label={t("aboutAdmin.fieldDesc")}>
                    <textarea
                      className="admin-input min-h-24"
                      value={item.desc}
                      onChange={(e) => {
                        const next = [...certItems];
                        next[index] = { ...item, desc: e.target.value };
                        updateItemsJson(next);
                      }}
                    />
                  </Field>
                  <Field label={t("aboutAdmin.fieldCertImage")}>
                    <div className="flex items-center gap-2">
                      <input
                        className="admin-input"
                        value={item.imageUrl ?? ""}
                        onChange={(e) => {
                          const next = [...certItems];
                          next[index] = { ...item, imageUrl: e.target.value };
                          updateItemsJson(next);
                        }}
                        placeholder="/images/upload/..."
                      />
                      <label className="px-3 py-2 rounded-xl border border-border hover:bg-muted inline-flex items-center gap-2 cursor-pointer text-sm shrink-0">
                        <Upload className="w-4 h-4" />
                        {isUploadingCert ? <Loader2 className="w-4 h-4 animate-spin" /> : t("aboutAdmin.upload")}
                        <input
                          type="file"
                          accept="image/*"
                          className="hidden"
                          disabled={isUploadingCert}
                          onChange={(e) => {
                            const file = e.target.files?.[0];
                            if (file) void handleCertImageUpload(file, certId);
                            e.target.value = "";
                          }}
                        />
                      </label>
                    </div>
                    {item.imageUrl && (
                      <div className="mt-2 rounded-xl overflow-hidden border border-border bg-muted/30 max-h-40">
                        <img src={item.imageUrl} alt={item.name} className="w-full object-contain max-h-40" />
                      </div>
                    )}
                  </Field>
                  <button
                    type="button"
                    onClick={() => updateItemsJson(certItems.filter((_, i) => i !== index))}
                    className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted"
                  >
                    <Trash2 className="w-4 h-4" />
                    {t("common.delete")}
                  </button>
                </RowCard>
              );
            })}
          </div>
        </EditorSection>
      );
    }

    if (activeTab.editor === "downloads") {
      return (
        <EditorSection
          title={t("aboutAdmin.downloadsList")}
          actionLabel={t("aboutAdmin.addDownload")}
          onAdd={() =>
            updateItemsJson([
              ...downloadItems,
              { id: createLocalId("download"), name: "", size: "", type: "", url: "", sortOrder: nextSortOrder(downloadItems) },
            ])
          }
        >
          <div className="space-y-3">
            {downloadItems.map((item, index) => {
              const dlId = item.id ?? `download-${index}`;
              const isUploadingDoc = uploadingDownloadIds.has(dlId);
              return (
                <RowCard key={dlId}>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    <Field label={t("aboutAdmin.fieldSortOrder")}>
                      <input
                        className="admin-input"
                        type="number"
                        value={normalizeSortOrder(item.sortOrder, index)}
                        onChange={(e) => {
                          const next = [...downloadItems];
                          next[index] = { ...item, sortOrder: Number(e.target.value) || 0 };
                          updateItemsJson(sortItemsBySortOrder(next));
                        }}
                      />
                    </Field>
                    <Field label={t("aboutAdmin.fieldDocName")}>
                      <input
                        className="admin-input"
                        value={item.name}
                        onChange={(e) => {
                          const next = [...downloadItems];
                          next[index] = { ...item, name: e.target.value };
                          updateItemsJson(next);
                        }}
                      />
                    </Field>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <Field label={t("aboutAdmin.fieldFileSize")}>
                      <input
                        className="admin-input"
                        value={item.size}
                        onChange={(e) => {
                          const next = [...downloadItems];
                          next[index] = { ...item, size: e.target.value };
                          updateItemsJson(next);
                        }}
                      />
                    </Field>
                    <Field label={t("aboutAdmin.fieldFileType")}>
                      <input
                        className="admin-input"
                        value={item.type}
                        onChange={(e) => {
                          const next = [...downloadItems];
                          next[index] = { ...item, type: e.target.value };
                          updateItemsJson(next);
                        }}
                      />
                    </Field>
                    <Field label={t("aboutAdmin.fieldUrl")}>
                      <div className="flex items-center gap-2">
                        <input
                          className="admin-input"
                          value={item.url}
                          onChange={(e) => {
                            const next = [...downloadItems];
                            next[index] = { ...item, url: e.target.value };
                            updateItemsJson(next);
                          }}
                          placeholder="/files/cv/..."
                        />
                        <label className="px-3 py-2 rounded-xl border border-border hover:bg-muted inline-flex items-center gap-2 cursor-pointer text-sm shrink-0">
                          <Upload className="w-4 h-4" />
                          {isUploadingDoc ? <Loader2 className="w-4 h-4 animate-spin" /> : t("aboutAdmin.uploadDoc")}
                          <input
                            type="file"
                            accept=".pdf,.doc,.docx,.xls,.xlsx,image/*"
                            className="hidden"
                            disabled={isUploadingDoc}
                            onChange={(e) => {
                              const file = e.target.files?.[0];
                              if (file) void handleDocumentUpload(file, dlId);
                              e.target.value = "";
                            }}
                          />
                        </label>
                      </div>
                    </Field>
                  </div>
                  <button
                    type="button"
                    onClick={() => updateItemsJson(downloadItems.filter((_, i) => i !== index))}
                    className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted"
                  >
                    <Trash2 className="w-4 h-4" />
                    {t("common.delete")}
                  </button>
                </RowCard>
              );
            })}
          </div>
        </EditorSection>
      );
    }

    return null;
  };

  return (
    <AdminLayout>
      <div className="admin-about-page space-y-6">
      <div className="admin-card about-hero p-6 lg:p-8">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] mb-2" style={{ color: "hsl(var(--admin-primary))" }}>
          {t("nav.about")}
        </p>
        <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("aboutAdmin.title")}</h1>
        <p className="text-sm mt-2 max-w-3xl" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("aboutAdmin.pageDesc")}
        </p>
      </div>

      <div className="about-tabs">
        {localizedTabs.map((tab) => (
          <button
            type="button"
            key={tab.slug}
            onClick={() => setActiveSlug(tab.slug)}
            className={`about-tab ${activeSlug === tab.slug ? "is-active" : ""}`}
            aria-pressed={activeSlug === tab.slug}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div ref={editorRef} className="admin-card about-editor p-5 lg:p-7 space-y-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h2 className="font-display text-xl font-extrabold">{activeTab.label}</h2>
            <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
              {activeTab.description}
            </p>
          </div>
          <button
            onClick={() => void saveSection()}
            disabled={saving || loading}
            className="admin-btn-primary px-4 py-2 text-sm inline-flex items-center gap-2 disabled:opacity-50"
          >
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            {t("common.save")}
          </button>
        </div>

        {loading ? (
          <div className="py-10 flex justify-center">
            <Loader2 className="w-6 h-6 animate-spin" />
          </div>
        ) : (
          <>
            {shouldShowStructuredEditorFirst && renderStructuredEditor()}

            <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,1fr)_minmax(280px,0.7fr)] gap-4 items-start">
              <Field label={t("aboutAdmin.fieldSlug")}>
                <input className="admin-input" value={form.slug} onChange={(e) => updateForm("slug", e.target.value)} placeholder={t("aboutAdmin.placeholderSlug")} />
              </Field>
              <SortOrderField
                label={t("aboutAdmin.fieldSortOrder")}
                value={form.sortOrder}
                onChange={(value) => updateForm("sortOrder", value)}
              />
              <VisibilityField
                label={t("aboutAdmin.fieldVisible")}
                checked={form.isActive}
                onChange={(value) => updateForm("isActive", value)}
                activeLabel={t("aboutAdmin.statusActive")}
                hiddenLabel={t("aboutAdmin.statusHidden")}
                t={t}
              />
            </div>

            {activeTab.showEyebrow && (
              <Field label={t("aboutAdmin.fieldEyebrow")}>
                <input className="admin-input" value={form.eyebrow} onChange={(e) => updateForm("eyebrow", e.target.value)} placeholder={t("aboutAdmin.placeholderEyebrow")} />
              </Field>
            )}

            {(activeTab.showTitleA || activeTab.showTitleB) && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {activeTab.showTitleA && (
                  <Field label={t("aboutAdmin.fieldTitleA")}>
                    <input className="admin-input" value={form.titleA} onChange={(e) => updateForm("titleA", e.target.value)} placeholder={t("aboutAdmin.placeholderTitleA")} />
                  </Field>
                )}
                {activeTab.showTitleB && (
                  <Field label={t("aboutAdmin.fieldTitleB")}>
                    <input className="admin-input" value={form.titleB} onChange={(e) => updateForm("titleB", e.target.value)} placeholder={t("aboutAdmin.placeholderTitleB")} />
                  </Field>
                )}
              </div>
            )}

            {activeTab.showParagraph1 && (
              <Field label={t("aboutAdmin.fieldParagraph1")}>
                <textarea className="admin-input min-h-24" value={form.paragraph1} onChange={(e) => updateForm("paragraph1", e.target.value)} placeholder={t("aboutAdmin.placeholderParagraph1")} />
              </Field>
            )}

            {activeTab.showParagraph2 && (
              <Field label={t("aboutAdmin.fieldParagraph2")}>
                <textarea className="admin-input min-h-24" value={form.paragraph2} onChange={(e) => updateForm("paragraph2", e.target.value)} placeholder={t("aboutAdmin.placeholderParagraph2")} />
              </Field>
            )}

            {activeTab.showImage && (
              <div className="space-y-2">
                <Field label={t("aboutAdmin.fieldImage")}>
                  <div className="flex items-center gap-2">
                    <input className="admin-input" value={form.imageUrl} onChange={(e) => updateForm("imageUrl", e.target.value)} placeholder={t("aboutAdmin.placeholderImagePath")} />
                    <label className="px-3 py-2 rounded-xl border border-border hover:bg-muted inline-flex items-center gap-2 cursor-pointer text-sm">
                      <Upload className="w-4 h-4" />
                      {uploading ? <Loader2 className="w-4 h-4 animate-spin" /> : t("aboutAdmin.upload")}
                      <input type="file" accept="image/*" onChange={onSelectFile} disabled={uploading} className="hidden" />
                    </label>
                  </div>
                </Field>
                {form.imageUrl && (
                  <div className="rounded-2xl overflow-hidden border border-border bg-muted/30">
                    <img src={form.imageUrl} alt={activeTab.label} className="w-full h-56 object-cover" />
                  </div>
                )}
              </div>
            )}

            {!shouldShowStructuredEditorFirst && renderStructuredEditor()}

            <div className="text-xs inline-flex items-center gap-1.5" style={{ color: saving ? "hsl(var(--admin-warning))" : "hsl(var(--admin-success))" }}>
              {saving ? <Sparkles className="w-3.5 h-3.5" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
              {saving ? t("aboutAdmin.saving") : t("aboutAdmin.ready")}
            </div>
          </>
        )}
      </div>
      </div>

      <Dialog
        open={statDialogOpen}
        onOpenChange={(open) => {
          setStatDialogOpen(open);
          if (!open) setStatDraft(null);
        }}
      >
        <DialogContent
          className="admin-scope admin-about-dialog sm:max-w-6xl p-0 overflow-hidden gap-0 rounded-[2rem] border shadow-2xl"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <DialogHeader
            className="admin-about-dialog-header px-5 sm:px-7 pt-5 sm:pt-6 pb-5 border-b"
            style={{
              borderColor: "hsl(var(--admin-border))",
              background:
                "radial-gradient(at 90% 10%, hsl(var(--admin-primary) / 0.18) 0px, transparent 42%), linear-gradient(135deg, hsl(var(--admin-bg)), hsl(var(--admin-primary-soft) / 0.44))",
            }}
          >
            <p className="text-[11px] font-bold uppercase tracking-[0.18em] mb-3" style={{ color: "hsl(var(--admin-primary))" }}>
              NICON • {t("nav.about")}
            </p>
            <DialogTitle className="font-display text-[2rem] leading-none tracking-tight">
              {statDraft ? t("aboutAdmin.editStatDialog") : t("aboutAdmin.addStatDialog")}
            </DialogTitle>
            <DialogDescription className="text-base mt-2 max-w-2xl" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("aboutAdmin.editStatDialogDesc")}
            </DialogDescription>
          </DialogHeader>

          {statDraft && (
            <>
              <div className="about-dialog-body grid grid-cols-1 xl:grid-cols-[minmax(0,1fr)_360px]">
                <div className="about-dialog-main px-5 sm:px-7 py-5 sm:py-6 space-y-5">
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <div className="about-form-card">
                      <SortOrderField
                        label={t("aboutAdmin.fieldSortOrder")}
                        value={statDraft.sortOrder}
                        onChange={(value) => setStatDraft((prev) => (prev ? { ...prev, sortOrder: value } : prev))}
                      />
                    </div>
                    <div className="about-form-card">
                      <Field label={t("aboutAdmin.statValue")}>
                        <input
                          className="admin-input text-xl font-extrabold"
                          value={statDraft.num}
                          onChange={(e) => setStatDraft((prev) => (prev ? { ...prev, num: e.target.value } : prev))}
                          placeholder={t("aboutAdmin.placeholderStatValue")}
                        />
                      </Field>
                    </div>
                    <div className="about-form-card">
                      <Field label={t("aboutAdmin.fieldStatus")}>
                        <select
                          className="admin-input"
                          value={statDraft.isActive ? "true" : "false"}
                          onChange={(e) => setStatDraft((prev) => (prev ? { ...prev, isActive: e.target.value === "true" } : prev))}
                        >
                          <option value="true">{t("aboutAdmin.statusActive")}</option>
                          <option value="false">{t("aboutAdmin.statusHidden")}</option>
                        </select>
                      </Field>
                    </div>
                  </div>

                  <div className="about-form-card about-form-card-accent">
                    <Field label={t("aboutAdmin.statLabel")}>
                      <input
                        className="admin-input"
                        value={statDraft.label}
                        onChange={(e) => setStatDraft((prev) => (prev ? { ...prev, label: e.target.value } : prev))}
                        placeholder={t("aboutAdmin.placeholderStatLabel")}
                      />
                    </Field>
                  </div>

                  <div className="about-form-card about-form-card-accent p-4 sm:p-5">
                    <Field label={t("aboutAdmin.chooseIcon")}>
                      <IconPicker
                        value={statDraft.iconKey}
                        t={t}
                        onChange={(iconKey) => setStatDraft((prev) => (prev ? { ...prev, iconKey } : prev))}
                      />
                    </Field>
                  </div>
                </div>

                <aside
                  className="about-dialog-side border-t xl:border-t-0 xl:border-l px-5 sm:px-7 py-5 sm:py-6 space-y-5"
                  style={{
                    borderColor: "hsl(var(--admin-border))",
                    background: "linear-gradient(180deg, hsl(var(--admin-primary-soft) / 0.55), hsl(var(--admin-bg)))",
                  }}
                >
                  <div>
                    <p
                      className="text-[11px] font-bold uppercase tracking-[0.18em] mb-2"
                      style={{ color: "hsl(var(--admin-primary))" }}
                    >
                      {t("aboutAdmin.previewTitle")}
                    </p>
                    <h3 className="font-display text-xl font-extrabold leading-tight">
                      {statDraft.num || t("aboutAdmin.defaultStatValue")}
                    </h3>
                    <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
                      {statDraft.label || t("aboutAdmin.defaultStatLabel")}
                    </p>
                  </div>

                  <div
                    className="admin-stat-card"
                    style={{
                      background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))",
                    }}
                  >
                    <div className="w-14 h-14 rounded-2xl bg-white/15 backdrop-blur-sm flex items-center justify-center mb-6">
                      <PreviewIconProxy iconKey={statDraft.iconKey} />
                    </div>
                    <div className="space-y-2">
                      <p className="text-sm font-semibold text-white/80">{ABOUT_ICON_META[statDraft.iconKey].label}</p>
                      <p className="font-display text-5xl font-extrabold leading-none">{statDraft.num || t("aboutAdmin.defaultStatValue")}</p>
                      <p className="text-sm text-white/85 leading-relaxed">
                        {statDraft.label || t("aboutAdmin.defaultStatLabel")}
                      </p>
                    </div>
                  </div>

                  <div
                    className="rounded-[1.5rem] border p-4 space-y-3"
                    style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-surface) / 0.88)" }}
                  >
                    <div className="flex items-center justify-between text-sm">
                      <span style={{ color: "hsl(var(--admin-muted))" }}>{t("aboutAdmin.fieldSortOrder")}</span>
                      <span className="font-bold">#{statDraft.sortOrder}</span>
                    </div>
                    <div className="flex items-center justify-between text-sm">
                      <span style={{ color: "hsl(var(--admin-muted))" }}>{t("aboutAdmin.fieldStatus")}</span>
                      <span
                        className="inline-flex items-center rounded-full px-3 py-1 text-xs font-bold uppercase tracking-[0.14em]"
                        style={
                          statDraft.isActive
                            ? { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }
                            : { background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-muted))" }
                        }
                      >
                        {statDraft.isActive ? t("aboutAdmin.statusActive") : t("aboutAdmin.statusHidden")}
                      </span>
                    </div>
                  </div>
                </aside>
              </div>

              <div
                className="about-dialog-footer px-5 sm:px-7 py-4 border-t flex flex-col-reverse sm:flex-row sm:items-center sm:justify-end gap-2"
                style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-bg))" }}
              >
                <button type="button" className="about-secondary-btn" onClick={() => setStatDialogOpen(false)}>
                  {t("common.cancel")}
                </button>
                <button type="button" className="admin-btn-primary px-5 py-2.5 sm:min-w-[152px]" onClick={saveStatDialog}>
                  {t("aboutAdmin.saveStat")}
                </button>
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>

      <Dialog
        open={iconDialogOpen}
        onOpenChange={(open) => {
          setIconDialogOpen(open);
          if (!open) setIconDraft(null);
        }}
      >
        <DialogContent className="admin-scope admin-about-dialog sm:max-w-5xl p-0 gap-0 overflow-hidden rounded-[2rem] border shadow-2xl" style={{ borderColor: "hsl(var(--admin-border))" }}>
          <DialogHeader
            className="admin-about-dialog-header px-5 sm:px-7 pt-5 sm:pt-6 pb-5 border-b"
            style={{
              borderColor: "hsl(var(--admin-border))",
              background:
                "radial-gradient(at 90% 10%, hsl(var(--admin-primary) / 0.18) 0px, transparent 42%), linear-gradient(135deg, hsl(var(--admin-bg)), hsl(var(--admin-primary-soft) / 0.44))",
            }}
          >
            <p className="text-[11px] font-bold uppercase tracking-[0.18em] mb-3" style={{ color: "hsl(var(--admin-primary))" }}>
              NICON • {t("nav.about")}
            </p>
            <DialogTitle className="font-display text-[1.75rem] leading-none tracking-tight">
              {iconDialogEditor === "values" ? t("aboutAdmin.editValueDialog") : t("aboutAdmin.editStrategyDialog")}
            </DialogTitle>
            <DialogDescription className="text-base mt-2 max-w-2xl" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("aboutAdmin.editItemDialogDesc")}
            </DialogDescription>
          </DialogHeader>

          {iconDraft && (
            <>
              <div className="about-dialog-body grid grid-cols-1 xl:grid-cols-[minmax(0,1fr)_340px]">
                <div className="about-dialog-main px-5 sm:px-7 py-5 sm:py-6 space-y-5">
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <div className="about-form-card">
                      <SortOrderField
                        label={t("aboutAdmin.fieldSortOrder")}
                        value={iconDraft.sortOrder}
                        onChange={(value) => setIconDraft((prev) => (prev ? { ...prev, sortOrder: value } : prev))}
                      />
                    </div>
                    <div className="about-form-card">
                      <Field label={t("aboutAdmin.fieldStatus")}>
                        <select
                          className="admin-input"
                          value={iconDraft.isActive ? "true" : "false"}
                          onChange={(e) => setIconDraft((prev) => (prev ? { ...prev, isActive: e.target.value === "true" } : prev))}
                        >
                          <option value="true">{t("aboutAdmin.statusActive")}</option>
                          <option value="false">{t("aboutAdmin.statusHidden")}</option>
                        </select>
                      </Field>
                    </div>
                    <div className="about-form-card">
                      <Field label={t("aboutAdmin.fieldTitle")}>
                        <input
                          className="admin-input"
                          value={iconDraft.title}
                          onChange={(e) => setIconDraft((prev) => (prev ? { ...prev, title: e.target.value } : prev))}
                        />
                      </Field>
                    </div>
                  </div>

                  <div className="about-form-card about-form-card-accent">
                    <Field label={t("aboutAdmin.fieldDesc")}>
                      <textarea
                        className="admin-input min-h-28"
                        value={iconDraft.desc}
                        onChange={(e) => setIconDraft((prev) => (prev ? { ...prev, desc: e.target.value } : prev))}
                      />
                    </Field>
                  </div>

                  <div className="about-form-card about-form-card-accent p-4 sm:p-5">
                    <Field label={t("aboutAdmin.chooseIcon")}>
                      <IconPicker
                        value={iconDraft.iconKey}
                        t={t}
                        onChange={(iconKey) => setIconDraft((prev) => (prev ? { ...prev, iconKey } : prev))}
                      />
                    </Field>
                  </div>
                </div>

                <aside
                  className="about-dialog-side border-t xl:border-t-0 xl:border-l px-5 sm:px-7 py-5 sm:py-6 space-y-5"
                  style={{
                    borderColor: "hsl(var(--admin-border))",
                    background: "linear-gradient(180deg, hsl(var(--admin-primary-soft) / 0.55), hsl(var(--admin-bg)))",
                  }}
                >
                  <div>
                    <p
                      className="text-[11px] font-bold uppercase tracking-[0.18em] mb-2"
                      style={{ color: "hsl(var(--admin-primary))" }}
                    >
                      {t("aboutAdmin.previewTitle")}
                    </p>
                    <h3 className="font-display text-xl font-extrabold leading-tight">
                      {iconDraft.title || t("aboutAdmin.defaultItemTitle")}
                    </h3>
                    <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
                      {iconDraft.desc || t("aboutAdmin.defaultItemDesc")}
                    </p>
                  </div>

                  <div className="about-icon-preview-card">
                    <div className="about-icon-preview-mark">
                      <PreviewIconProxy iconKey={iconDraft.iconKey} />
                    </div>
                    <div className="space-y-2">
                      <p className="text-sm font-semibold" style={{ color: "hsl(var(--admin-primary))" }}>
                        {ABOUT_ICON_META[iconDraft.iconKey].label}
                      </p>
                      <p className="font-display text-2xl font-extrabold leading-tight">
                        {iconDraft.title || t("aboutAdmin.defaultItemTitle")}
                      </p>
                      <p className="text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>
                        {iconDraft.desc || t("aboutAdmin.defaultItemDesc")}
                      </p>
                    </div>
                  </div>

                  <div
                    className="rounded-[1.5rem] border p-4 space-y-3"
                    style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-surface) / 0.88)" }}
                  >
                    <div className="flex items-center justify-between text-sm">
                      <span style={{ color: "hsl(var(--admin-muted))" }}>{t("aboutAdmin.fieldSortOrder")}</span>
                      <span className="font-bold">#{iconDraft.sortOrder}</span>
                    </div>
                    <div className="flex items-center justify-between text-sm">
                      <span style={{ color: "hsl(var(--admin-muted))" }}>{t("aboutAdmin.fieldStatus")}</span>
                      <span
                        className="inline-flex items-center rounded-full px-3 py-1 text-xs font-bold uppercase tracking-[0.14em]"
                        style={
                          iconDraft.isActive
                            ? { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }
                            : { background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-muted))" }
                        }
                      >
                        {iconDraft.isActive ? t("aboutAdmin.statusActive") : t("aboutAdmin.statusHidden")}
                      </span>
                    </div>
                  </div>
                </aside>
              </div>

              <div
                className="about-dialog-footer px-5 sm:px-7 py-4 border-t flex flex-col-reverse sm:flex-row sm:items-center sm:justify-end gap-2"
                style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-bg))" }}
              >
                <button type="button" className="about-secondary-btn" onClick={() => setIconDialogOpen(false)}>
                  {t("common.cancel")}
                </button>
                <button type="button" className="admin-btn-primary px-4 py-2.5 sm:min-w-[152px]" onClick={saveIconDialog}>
                  {t("aboutAdmin.saveItem")}
                </button>
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default AboutContent;
