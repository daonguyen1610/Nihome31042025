import { useEffect, useMemo, useState } from "react";
import { CheckCircle2, Loader2, Plus, Save, Sparkles, Trash2, Upload, Calendar, Building2, Users, Award, Info } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { adminApi, type AboutSectionAdminResponse, type UpsertAboutSectionRequest } from "@/services/adminApi";
import { useToast } from "@/hooks/use-toast";

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
  label: string;
  description: string;
  showEyebrow?: boolean;
  showTitleA?: boolean;
  showTitleB?: boolean;
  showParagraph1?: boolean;
  showParagraph2?: boolean;
  showImage?: boolean;
  editor?: "stats" | "values" | "strategy" | "organization" | "timeline" | "certs" | "downloads";
};

type StatItem = { num: string; label: string };
type TitleDescItem = { title: string; desc: string };
type LeaderItem = { role: string; name: string };
type OrganizationItem = { board: LeaderItem[]; directors: LeaderItem[] };
type TimelineItem = { year: string; title: string; desc: string };
type CertItem = { name: string; desc: string };
type DownloadItem = { name: string; size: string; type: string; url: string };

// Icons for stats section (matching client-side display)
const STAT_ICONS = [Calendar, Building2, Users, Award];
const STAT_ICON_NAMES = ["Calendar", "Building2", "Users", "Award"];

const SECTION_TABS: SectionTab[] = [
  {
    slug: "about-main",
    label: "Giới thiệu",
    description: "Block mở đầu của trang Về Chúng Tôi.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showParagraph1: true,
    showParagraph2: true,
    showImage: true,
  },
  {
    slug: "stats-main",
    label: "Chỉ số",
    description: "Các chỉ số nổi bật nằm ngay dưới phần giới thiệu.",
    editor: "stats",
  },
  {
    slug: "values-main",
    label: "Giá trị cốt lõi",
    description: "Tiêu đề section và danh sách thẻ giá trị cốt lõi.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    editor: "values",
  },
  {
    slug: "strategy-main",
    label: "Chiến lược",
    description: "Section chiến lược và các lĩnh vực hoạt động.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showParagraph1: true,
    showParagraph2: true,
    editor: "strategy",
  },
  {
    slug: "organization-main",
    label: "Tổ chức",
    description: "Tiêu đề section và danh sách nhân sự điều hành.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    editor: "organization",
  },
  {
    slug: "timeline-main",
    label: "Lịch sử",
    description: "Timeline lịch sử phát triển và hình minh họa.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showImage: true,
    editor: "timeline",
  },
  {
    slug: "certs-main",
    label: "Chứng nhận",
    description: "Tiêu đề và danh sách chứng nhận.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    editor: "certs",
  },
  {
    slug: "downloads-main",
    label: "Tài liệu",
    description: "Section tài liệu tải xuống và danh sách file.",
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

const parseItems = <T,>(value: string, fallback: T): T => {
  if (!value.trim()) return fallback;

  try {
    return JSON.parse(value) as T;
  } catch {
    return fallback;
  }
};

const serializeItems = (value: unknown) => JSON.stringify(value, null, 2);

const EditorSection = ({ title, actionLabel, onAdd, children }: {
  title: string;
  actionLabel: string;
  onAdd: () => void;
  children: React.ReactNode;
}) => (
  <div className="space-y-3">
    <div className="flex items-center justify-between">
      <h3 className="font-bold text-sm">{title}</h3>
      <button type="button" onClick={onAdd} className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted">
        <Plus className="w-4 h-4" />
        {actionLabel}
      </button>
    </div>
    {children}
  </div>
);

const RowCard = ({ children }: { children: React.ReactNode }) => (
  <div className="rounded-2xl border border-border p-4 bg-muted/20 space-y-3">{children}</div>
);

const Field = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <label className="block space-y-1.5">
    <span className="text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </span>
    {children}
  </label>
);

const AboutContent = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [activeSlug, setActiveSlug] = useState("about-main");
  const [forms, setForms] = useState<Record<string, AboutForm>>({});

  useEffect(() => {
    let canceled = false;

    const load = async () => {
      setLoading(true);
      try {
        const { data } = await adminApi.getAboutSections(false);
        if (canceled) return;

        const nextForms = SECTION_TABS.reduce<Record<string, AboutForm>>((acc, tab, index) => {
          const existing = data.find((item) => item.slug === tab.slug);
          acc[tab.slug] = existing ? toForm(existing) : emptyForm(tab.slug, index);
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
  }, [t, toast]);

  const activeTab = useMemo(
    () => SECTION_TABS.find((tab) => tab.slug === activeSlug) ?? SECTION_TABS[0],
    [activeSlug],
  );

  const form = forms[activeSlug] ?? emptyForm(activeSlug, 0);

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

  const statItems = parseItems<StatItem[]>(form.itemsJson, []);
  const valueItems = parseItems<TitleDescItem[]>(form.itemsJson, []);
  const strategyItems = parseItems<TitleDescItem[]>(form.itemsJson, []);
  const organizationItems = parseItems<OrganizationItem>(form.itemsJson, { board: [], directors: [] });
  const timelineItems = parseItems<TimelineItem[]>(form.itemsJson, []);
  const certItems = parseItems<CertItem[]>(form.itemsJson, []);
  const downloadItems = parseItems<DownloadItem[]>(form.itemsJson, []);

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
          [activeSlug]: toForm(response.data),
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

  const renderStructuredEditor = () => {
    if (activeTab.editor === "stats") {
      return (
        <div className="space-y-5">
          {/* Guidelines for non-technical users */}
          <div className="p-4 rounded-xl border space-y-2" style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-primary-soft))" }}>
            <div className="flex gap-2 items-start">
              <Info className="w-5 h-5 flex-shrink-0" style={{ color: "hsl(var(--admin-primary))" }} />
              <div className="text-sm">
                <p className="font-bold mb-1" style={{ color: "hsl(var(--admin-primary))" }}>Hướng dẫn cho người dùng không-kỹ-thuật:</p>
                <ul className="space-y-1 list-disc list-inside" style={{ color: "hsl(var(--admin-sidebar-text))" }}>
                  <li>Mỗi chỉ số bao gồm: Số (18+, 150+, ISO) + Mô tả (Năm kinh nghiệm, ...)</li>
                  <li>Hiển thị tối đa 4 chỉ số trên trang client</li>
                  <li>Các icon được tự động gán theo thứ tự: Lịch, Tòa nhà, Người dùng, Giải thưởng</li>
                  <li>Xem trước bên dưới để kiểm tra cách hiển thị trên trang khách hàng</li>
                </ul>
              </div>
            </div>
          </div>

          {/* Editor Section */}
          <EditorSection
            title="Danh sách chỉ số"
            actionLabel="Thêm chỉ số"
            onAdd={() => statItems.length < 4 ? updateItemsJson([...statItems, { num: "", label: "" }]) : null}
          >
            <div className="space-y-3">
              {statItems.length === 0 && (
                <p className="text-sm italic py-4" style={{ color: "hsl(var(--admin-muted))" }}>Chưa có chỉ số nào. Nhấn "Thêm chỉ số" để thêm.</p>
              )}
              {statItems.map((item, index) => (
                <RowCard key={`stat-${index}`}>
                  <div className="flex gap-3 mb-3 pb-3" style={{ borderBottomColor: "hsl(var(--admin-border))", borderBottomWidth: "1px" }}>
                    <div className="flex-shrink-0">
                      {(() => {
                        const Icon = STAT_ICONS[index] ?? Award;
                        return <Icon className="w-6 h-6" style={{ color: "hsl(var(--admin-primary))" }} />;
                      })()}
                    </div>
                    <div className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
                      <p className="font-semibold">{STAT_ICON_NAMES[index]} icon</p>
                      <p>Vị trí #{index + 1}</p>
                    </div>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    <Field label="Giá trị (ví dụ: 18+, 150+, ISO)">
                      <input
                        className="admin-input"
                        value={item.num}
                        onChange={(e) => {
                          const next = [...statItems];
                          next[index] = { ...item, num: e.target.value };
                          updateItemsJson(next);
                        }}
                        placeholder="18+"
                      />
                    </Field>
                    <Field label="Mô tả (ví dụ: Năm kinh nghiệm)">
                      <input
                        className="admin-input"
                        value={item.label}
                        onChange={(e) => {
                          const next = [...statItems];
                          next[index] = { ...item, label: e.target.value };
                          updateItemsJson(next);
                        }}
                        placeholder="Năm kinh nghiệm"
                      />
                    </Field>
                  </div>
                  <button type="button" onClick={() => updateItemsJson(statItems.filter((_, i) => i !== index))} className="mt-2 px-3 py-2 rounded-xl border text-sm inline-flex items-center gap-2 hover:bg-muted" style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-danger))" }}>
                    <Trash2 className="w-4 h-4" />
                    Xóa
                  </button>
                </RowCard>
              ))}
              {statItems.length >= 4 && (
                <p className="text-xs italic" style={{ color: "hsl(var(--admin-warning))" }}>Đã đạt tối đa 4 chỉ số. Xóa một chỉ số nếu muốn thêm chỉ số mới.</p>
              )}
            </div>
          </EditorSection>

          {/* Live Preview Section */}
          {statItems.some((s) => s.num || s.label) && (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="font-bold text-sm">Xem trước trên trang khách hàng</h3>
                <span className="text-xs px-2 py-1 rounded font-semibold" style={{ background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }}>Trực tiếp</span>
              </div>
              <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 p-4 rounded-xl" style={{ background: "hsl(var(--admin-bg))" }}>
                {statItems.map((item, i) => {
                  const Icon = STAT_ICONS[i] ?? Award;
                  return (
                    <div key={`preview-${i}`} className="rounded-2xl p-4 text-center" style={{ background: "hsl(var(--admin-surface))", borderColor: "hsl(var(--admin-border))", borderWidth: "1px" }}>
                      <Icon className="w-6 h-6 mx-auto mb-3" style={{ color: "hsl(var(--admin-primary))" }} strokeWidth={1.5} />
                      <p className="font-display text-2xl font-extrabold mb-1" style={{ color: "hsl(var(--admin-primary))" }}>{item.num || "—"}</p>
                      <p className="text-xs font-medium leading-snug h-8 flex items-center justify-center" style={{ color: "hsl(var(--admin-muted))" }}>{item.label || "(chưa có)"}</p>
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
      const items = activeTab.editor === "values" ? valueItems : strategyItems;
      const title = activeTab.editor === "values" ? "Danh sách giá trị" : "Danh sách lĩnh vực";
      const actionLabel = activeTab.editor === "values" ? "Thêm giá trị" : "Thêm lĩnh vực";

      return (
        <EditorSection
          title={title}
          actionLabel={actionLabel}
          onAdd={() => updateItemsJson([...items, { title: "", desc: "" }])}
        >
          <div className="space-y-3">
            {items.map((item, index) => (
              <RowCard key={`${activeTab.editor}-${index}`}>
                <Field label="Tiêu đề">
                  <input
                    className="admin-input"
                    value={item.title}
                    onChange={(e) => {
                      const next = [...items];
                      next[index] = { ...item, title: e.target.value };
                      updateItemsJson(next);
                    }}
                  />
                </Field>
                <Field label="Mô tả">
                  <textarea
                    className="admin-input min-h-24"
                    value={item.desc}
                    onChange={(e) => {
                      const next = [...items];
                      next[index] = { ...item, desc: e.target.value };
                      updateItemsJson(next);
                    }}
                  />
                </Field>
                <button type="button" onClick={() => updateItemsJson(items.filter((_, i) => i !== index))} className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted">
                  <Trash2 className="w-4 h-4" />
                  Xóa
                </button>
              </RowCard>
            ))}
          </div>
        </EditorSection>
      );
    }

    if (activeTab.editor === "organization") {
      const updateGroup = (group: "board" | "directors", nextItems: LeaderItem[]) =>
        updateItemsJson({ ...organizationItems, [group]: nextItems });

      const renderGroup = (group: "board" | "directors", title: string) => (
        <EditorSection
          title={title}
          actionLabel="Thêm thành viên"
          onAdd={() => updateGroup(group, [...organizationItems[group], { role: "", name: "" }])}
        >
          <div className="space-y-3">
            {organizationItems[group].map((item, index) => (
              <RowCard key={`${group}-${index}`}>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <Field label="Chức danh">
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
                  <Field label="Họ tên">
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
                </div>
                <button type="button" onClick={() => updateGroup(group, organizationItems[group].filter((_, i) => i !== index))} className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted">
                  <Trash2 className="w-4 h-4" />
                  Xóa
                </button>
              </RowCard>
            ))}
          </div>
        </EditorSection>
      );

      return (
        <div className="space-y-5">
          {renderGroup("board", "Hội đồng quản trị")}
          {renderGroup("directors", "Ban điều hành")}
        </div>
      );
    }

    if (activeTab.editor === "timeline") {
      return (
        <EditorSection
          title="Danh sách mốc thời gian"
          actionLabel="Thêm mốc"
          onAdd={() => updateItemsJson([...timelineItems, { year: "", title: "", desc: "" }])}
        >
          <div className="space-y-3">
            {timelineItems.map((item, index) => (
              <RowCard key={`timeline-${index}`}>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <Field label="Năm">
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
                  <Field label="Tiêu đề">
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
                <Field label="Mô tả">
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
                <button type="button" onClick={() => updateItemsJson(timelineItems.filter((_, i) => i !== index))} className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted">
                  <Trash2 className="w-4 h-4" />
                  Xóa
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
          title="Danh sách chứng nhận"
          actionLabel="Thêm chứng nhận"
          onAdd={() => updateItemsJson([...certItems, { name: "", desc: "" }])}
        >
          <div className="space-y-3">
            {certItems.map((item, index) => (
              <RowCard key={`cert-${index}`}>
                <Field label="Tên chứng nhận">
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
                <Field label="Mô tả">
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
                <button type="button" onClick={() => updateItemsJson(certItems.filter((_, i) => i !== index))} className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted">
                  <Trash2 className="w-4 h-4" />
                  Xóa
                </button>
              </RowCard>
            ))}
          </div>
        </EditorSection>
      );
    }

    if (activeTab.editor === "downloads") {
      return (
        <EditorSection
          title="Danh sách tài liệu"
          actionLabel="Thêm tài liệu"
          onAdd={() => updateItemsJson([...downloadItems, { name: "", size: "", type: "", url: "#" }])}
        >
          <div className="space-y-3">
            {downloadItems.map((item, index) => (
              <RowCard key={`download-${index}`}>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <Field label="Tên tài liệu">
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
                  <Field label="Dung lượng">
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
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <Field label="Loại file">
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
                  <Field label="Đường dẫn">
                    <input
                      className="admin-input"
                      value={item.url}
                      onChange={(e) => {
                        const next = [...downloadItems];
                        next[index] = { ...item, url: e.target.value };
                        updateItemsJson(next);
                      }}
                    />
                  </Field>
                </div>
                <button type="button" onClick={() => updateItemsJson(downloadItems.filter((_, i) => i !== index))} className="px-3 py-2 rounded-xl border border-border text-sm inline-flex items-center gap-2 hover:bg-muted">
                  <Trash2 className="w-4 h-4" />
                  Xóa
                </button>
              </RowCard>
            ))}
          </div>
        </EditorSection>
      );
    }

    return null;
  };

  return (
    <AdminLayout>
      <div
        className="admin-card p-6 lg:p-8 mb-6"
        style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary-soft)), hsl(var(--admin-surface)))" }}
      >
        <p className="text-xs font-semibold uppercase tracking-[0.18em] mb-2" style={{ color: "hsl(var(--admin-primary))" }}>
          {t("nav.about")}
        </p>
        <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("aboutAdmin.title")}</h1>
        <p className="text-sm mt-2" style={{ color: "hsl(var(--admin-muted))" }}>
          Quản lý đầy đủ các block hiển thị trên trang client `/profile` bằng form trực quan cho từng phần.
        </p>
      </div>

      <div className="flex gap-1 p-1 rounded-xl mb-6 overflow-x-auto" style={{ background: "hsl(var(--admin-bg))" }}>
        {SECTION_TABS.map((tab) => (
          <button
            key={tab.slug}
            onClick={() => setActiveSlug(tab.slug)}
            className="px-5 py-2.5 rounded-lg text-sm font-bold transition whitespace-nowrap"
            style={
              activeSlug === tab.slug
                ? { background: "hsl(var(--admin-primary))", color: "white" }
                : { color: "hsl(var(--admin-sidebar-text))" }
            }
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div className="admin-card p-5 space-y-5">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
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
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <Field label="Mã phần">
                <input className="admin-input" value={form.slug} onChange={(e) => updateForm("slug", e.target.value)} placeholder="slug" />
              </Field>
              <Field label="Thứ tự">
                <input className="admin-input" type="number" value={form.sortOrder} onChange={(e) => updateForm("sortOrder", Number(e.target.value) || 0)} placeholder="Sort order" />
              </Field>
              <Field label="Hiển thị">
                <label className="inline-flex items-center gap-2 px-3 py-3 rounded-xl border border-border text-sm">
                  <input type="checkbox" checked={form.isActive} onChange={(e) => updateForm("isActive", e.target.checked)} />
                  Active
                </label>
              </Field>
            </div>

            {activeTab.showEyebrow && (
              <Field label="Nhãn nhỏ">
                <input className="admin-input" value={form.eyebrow} onChange={(e) => updateForm("eyebrow", e.target.value)} placeholder="VỀ CHÚNG TÔI" />
              </Field>
            )}

            {(activeTab.showTitleA || activeTab.showTitleB) && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {activeTab.showTitleA && (
                  <Field label="Tiêu đề dòng 1">
                    <input className="admin-input" value={form.titleA} onChange={(e) => updateForm("titleA", e.target.value)} placeholder="Title A" />
                  </Field>
                )}
                {activeTab.showTitleB && (
                  <Field label="Tiêu đề dòng 2 hoặc phần nhấn mạnh">
                    <input className="admin-input" value={form.titleB} onChange={(e) => updateForm("titleB", e.target.value)} placeholder="Title B" />
                  </Field>
                )}
              </div>
            )}

            {activeTab.showParagraph1 && (
              <Field label="Mô tả 1">
                <textarea className="admin-input min-h-24" value={form.paragraph1} onChange={(e) => updateForm("paragraph1", e.target.value)} placeholder="Đoạn mô tả 1" />
              </Field>
            )}

            {activeTab.showParagraph2 && (
              <Field label="Mô tả 2">
                <textarea className="admin-input min-h-24" value={form.paragraph2} onChange={(e) => updateForm("paragraph2", e.target.value)} placeholder="Đoạn mô tả 2" />
              </Field>
            )}

            {activeTab.showImage && (
              <div className="space-y-2">
                <Field label="Hình ảnh">
                  <div className="flex items-center gap-2">
                    <input className="admin-input" value={form.imageUrl} onChange={(e) => updateForm("imageUrl", e.target.value)} placeholder="/images/upload/..." />
                    <label className="px-3 py-2 rounded-xl border border-border hover:bg-muted inline-flex items-center gap-2 cursor-pointer text-sm">
                      <Upload className="w-4 h-4" />
                      {uploading ? <Loader2 className="w-4 h-4 animate-spin" /> : "Upload"}
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

            {renderStructuredEditor()}

            <div className="text-xs inline-flex items-center gap-1.5" style={{ color: saving ? "hsl(var(--admin-warning))" : "hsl(var(--admin-success))" }}>
              {saving ? <Sparkles className="w-3.5 h-3.5" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
              {saving ? "Đang lưu..." : "Sẵn sàng cập nhật"}
            </div>
          </>
        )}
      </div>
    </AdminLayout>
  );
};

export default AboutContent;
