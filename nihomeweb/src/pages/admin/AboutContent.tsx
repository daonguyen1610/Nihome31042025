import { useEffect, useMemo, useState } from "react";
import { CheckCircle2, Loader2, Save, Sparkles, Upload } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type AboutSectionAdminResponse,
  type UpsertAboutSectionRequest,
} from "@/services/adminApi";
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
  showItemsJson?: boolean;
  itemsHint?: string;
};

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
    showItemsJson: true,
    itemsHint: '[{"num":"18+","label":"Năm kinh nghiệm"}]',
  },
  {
    slug: "values-main",
    label: "Giá trị cốt lõi",
    description: "Tiêu đề section và danh sách thẻ giá trị cốt lõi.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showItemsJson: true,
    itemsHint: '[{"title":"Mục tiêu rõ ràng","desc":"..."}]',
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
    showItemsJson: true,
    itemsHint: '[{"title":"Thiết kế - thi công tổng thể","desc":"..."}]',
  },
  {
    slug: "organization-main",
    label: "Tổ chức",
    description: "Tiêu đề section và sơ đồ nhân sự điều hành.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showItemsJson: true,
    itemsHint: '{"board":[{"role":"Chủ tịch HĐQT","name":"..."}],"directors":[{"role":"Tổng giám đốc","name":"..."}]}',
  },
  {
    slug: "timeline-main",
    label: "Lịch sử",
    description: "Timeline lịch sử phát triển và hình minh họa.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showImage: true,
    showItemsJson: true,
    itemsHint: '[{"year":"2006","title":"Thành lập","desc":"..."}]',
  },
  {
    slug: "certs-main",
    label: "Chứng nhận",
    description: "Tiêu đề và danh sách chứng nhận.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showItemsJson: true,
    itemsHint: '[{"name":"ISO 9001:2015","desc":"..."}]',
  },
  {
    slug: "downloads-main",
    label: "Tài liệu",
    description: "Section tài liệu tải xuống và danh sách file.",
    showEyebrow: true,
    showTitleA: true,
    showTitleB: true,
    showParagraph1: true,
    showItemsJson: true,
    itemsHint: '[{"name":"Company Profile","size":"12 MB","type":"PDF","url":"#"}]',
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
        ...(prev[activeSlug] ?? emptyForm(activeSlug, activeTab ? SECTION_TABS.indexOf(activeTab) : 0)),
        [key]: value,
      },
    }));
  };

  const saveSection = async () => {
    if (activeTab.showItemsJson && form.itemsJson.trim()) {
      try {
        JSON.parse(form.itemsJson);
      } catch {
        toast({
          title: t("aboutAdmin.missingDataTitle"),
          description: "Items JSON không hợp lệ.",
          variant: "destructive",
        });
        return;
      }
    }

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
        const created = response.data;
        setForms((prev) => ({
          ...prev,
          [activeSlug]: toForm(created),
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
          Quản lý đầy đủ các block hiển thị trên trang client `/profile`.
        </p>
      </div>

      <div
        className="flex gap-1 p-1 rounded-xl mb-6 overflow-x-auto"
        style={{ background: "hsl(var(--admin-bg))" }}
      >
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

      <div className="admin-card p-5 space-y-4">
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
              <input
                className="admin-input"
                value={form.slug}
                onChange={(e) => updateForm("slug", e.target.value)}
                placeholder="slug"
              />
              <input
                className="admin-input"
                type="number"
                value={form.sortOrder}
                onChange={(e) => updateForm("sortOrder", Number(e.target.value) || 0)}
                placeholder="Sort order"
              />
              <label className="inline-flex items-center gap-2 px-3 py-2 rounded-xl border border-border text-sm">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={(e) => updateForm("isActive", e.target.checked)}
                />
                Active
              </label>
            </div>

            {activeTab.showEyebrow && (
              <input
                className="admin-input"
                value={form.eyebrow}
                onChange={(e) => updateForm("eyebrow", e.target.value)}
                placeholder="Eyebrow"
              />
            )}

            {(activeTab.showTitleA || activeTab.showTitleB) && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {activeTab.showTitleA && (
                  <input
                    className="admin-input"
                    value={form.titleA}
                    onChange={(e) => updateForm("titleA", e.target.value)}
                    placeholder="Title A"
                  />
                )}
                {activeTab.showTitleB && (
                  <input
                    className="admin-input"
                    value={form.titleB}
                    onChange={(e) => updateForm("titleB", e.target.value)}
                    placeholder="Title B"
                  />
                )}
              </div>
            )}

            {activeTab.showParagraph1 && (
              <textarea
                className="admin-input min-h-24"
                value={form.paragraph1}
                onChange={(e) => updateForm("paragraph1", e.target.value)}
                placeholder="Đoạn mô tả 1"
              />
            )}

            {activeTab.showParagraph2 && (
              <textarea
                className="admin-input min-h-24"
                value={form.paragraph2}
                onChange={(e) => updateForm("paragraph2", e.target.value)}
                placeholder="Đoạn mô tả 2"
              />
            )}

            {activeTab.showImage && (
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <input
                    className="admin-input"
                    value={form.imageUrl}
                    onChange={(e) => updateForm("imageUrl", e.target.value)}
                    placeholder="/images/upload/..."
                  />
                  <label className="px-3 py-2 rounded-xl border border-border hover:bg-muted inline-flex items-center gap-2 cursor-pointer text-sm">
                    <Upload className="w-4 h-4" />
                    {uploading ? <Loader2 className="w-4 h-4 animate-spin" /> : "Upload"}
                    <input type="file" accept="image/*" onChange={onSelectFile} disabled={uploading} className="hidden" />
                  </label>
                </div>
                {form.imageUrl && (
                  <div className="rounded-2xl overflow-hidden border border-border bg-muted/30">
                    <img src={form.imageUrl} alt={activeTab.label} className="w-full h-56 object-cover" />
                  </div>
                )}
              </div>
            )}

            {activeTab.showItemsJson && (
              <div className="space-y-2">
                <textarea
                  className="admin-input min-h-64 font-mono text-xs"
                  value={form.itemsJson}
                  onChange={(e) => updateForm("itemsJson", e.target.value)}
                  placeholder={activeTab.itemsHint}
                />
                <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
                  JSON mẫu: <code>{activeTab.itemsHint}</code>
                </p>
              </div>
            )}

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
