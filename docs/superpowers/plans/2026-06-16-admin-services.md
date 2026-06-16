# Admin Services Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a full-featured admin CRUD page at `/admin/services` so editors can create, edit, and delete `ServiceItem` records that power `/services` and `/services/:slug`.

**Architecture:** Single-file admin page (`admin/Services.tsx`) following the `ProcessList.tsx` pattern — list table + Dialog modal form in one component. The form includes a dynamic sections editor (heading + body bullet list per section). Uses the existing `useServices()` hook for reads; three new `adminApi` methods for writes.

**Tech Stack:** React 18, TypeScript, shadcn/ui Dialog, Tailwind CSS, lucide-react, axios (`api` via `@/lib/api`), `useI18n()` for all strings

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Modify | `nihomebackend/Data/Seeds/header-footer.json` | Add `nav.services` i18n key |
| Modify | `nihomebackend/Data/Seeds/services.json` | Add `svc.admin.*` i18n keys |
| Modify | `nihomeweb/src/services/adminApi.ts` | `UpsertServiceAdminRequest` type + 3 API methods |
| **Create** | `nihomeweb/src/pages/admin/Services.tsx` | List table + create/edit Dialog + delete |
| Modify | `nihomeweb/src/App.tsx` | Register `/admin/services` route |
| Modify | `nihomeweb/src/components/layout/AdminLayout.tsx` | Add Services nav item to content group |

---

## Task 1: i18n seed keys

**Files:**
- Modify: `nihomebackend/Data/Seeds/header-footer.json`
- Modify: `nihomebackend/Data/Seeds/services.json`

- [ ] **Step 1: Add `nav.services` to `header-footer.json`**

Open `nihomebackend/Data/Seeds/header-footer.json`. Find the entry for `nav.posts` (around line 91). Add the following entry **after** `nav.recruitment` (keep alphabetical grouping by feature, not letter):

```json
  {
    "key": "nav.services",
    "category": "nav",
    "vi": "Quản lý dịch vụ",
    "en": "Services",
    "zh": "服务管理",
    "ja": "サービス管理"
  },
```

- [ ] **Step 2: Add `svc.admin.*` keys to `services.json`**

Open `nihomebackend/Data/Seeds/services.json`. Append the following entries to the JSON array (before the closing `]`):

```json
  {
    "key": "svc.admin.title",
    "category": "svc",
    "vi": "Quản lý dịch vụ",
    "en": "Services",
    "zh": "服务管理",
    "ja": "サービス管理"
  },
  {
    "key": "svc.admin.add",
    "category": "svc",
    "vi": "Thêm dịch vụ",
    "en": "Add service",
    "zh": "添加服务",
    "ja": "サービスを追加"
  },
  {
    "key": "svc.admin.createTitle",
    "category": "svc",
    "vi": "Tạo dịch vụ mới",
    "en": "Create service",
    "zh": "创建新服务",
    "ja": "新しいサービスを作成"
  },
  {
    "key": "svc.admin.editTitle",
    "category": "svc",
    "vi": "Sửa dịch vụ",
    "en": "Edit service",
    "zh": "编辑服务",
    "ja": "サービスを編集"
  },
  {
    "key": "svc.admin.searchPh",
    "category": "svc",
    "vi": "Tìm theo tên hoặc slug...",
    "en": "Search by name or slug...",
    "zh": "按名称或slug搜索...",
    "ja": "名前またはslugで検索..."
  },
  {
    "key": "svc.admin.empty",
    "category": "svc",
    "vi": "Chưa có dịch vụ nào",
    "en": "No services yet",
    "zh": "暂无服务",
    "ja": "サービスがまだありません"
  },
  {
    "key": "svc.admin.confirmDel",
    "category": "svc",
    "vi": "Xác nhận xóa dịch vụ này?",
    "en": "Delete this service?",
    "zh": "确认删除此服务？",
    "ja": "このサービスを削除しますか？"
  },
  {
    "key": "svc.admin.slug",
    "category": "svc",
    "vi": "Slug (URL)",
    "en": "Slug (URL)",
    "zh": "Slug (URL)",
    "ja": "スラッグ (URL)"
  },
  {
    "key": "svc.admin.shortTitle",
    "category": "svc",
    "vi": "Tên ngắn (eyebrow)",
    "en": "Short title (eyebrow)",
    "zh": "短标题（眉题）",
    "ja": "短いタイトル（アイブロー）"
  },
  {
    "key": "svc.admin.tagline",
    "category": "svc",
    "vi": "Tagline (phụ đề)",
    "en": "Tagline (subtitle)",
    "zh": "标语（副标题）",
    "ja": "タグライン（サブタイトル）"
  },
  {
    "key": "svc.admin.intro",
    "category": "svc",
    "vi": "Giới thiệu",
    "en": "Intro",
    "zh": "简介",
    "ja": "紹介"
  },
  {
    "key": "svc.admin.highlights",
    "category": "svc",
    "vi": "Điểm nổi bật",
    "en": "Highlights",
    "zh": "亮点",
    "ja": "ハイライト"
  },
  {
    "key": "svc.admin.addHighlight",
    "category": "svc",
    "vi": "+ Thêm điểm nổi bật",
    "en": "+ Add highlight",
    "zh": "+ 添加亮点",
    "ja": "+ ハイライトを追加"
  },
  {
    "key": "svc.admin.highlightPh",
    "category": "svc",
    "vi": "VD: Thi công trọn gói",
    "en": "E.g. Turnkey construction",
    "zh": "例：一站式施工",
    "ja": "例：一括施工"
  },
  {
    "key": "svc.admin.sections",
    "category": "svc",
    "vi": "Các mục nội dung",
    "en": "Content sections",
    "zh": "内容板块",
    "ja": "コンテンツセクション"
  },
  {
    "key": "svc.admin.addSection",
    "category": "svc",
    "vi": "+ Thêm mục",
    "en": "+ Add section",
    "zh": "+ 添加板块",
    "ja": "+ セクションを追加"
  },
  {
    "key": "svc.admin.addBullet",
    "category": "svc",
    "vi": "+ Thêm dòng",
    "en": "+ Add bullet",
    "zh": "+ 添加要点",
    "ja": "+ 箇条書きを追加"
  },
  {
    "key": "svc.admin.sectionHeadingPh",
    "category": "svc",
    "vi": "Tiêu đề mục...",
    "en": "Section heading...",
    "zh": "板块标题...",
    "ja": "セクション見出し..."
  },
  {
    "key": "svc.admin.bulletPh",
    "category": "svc",
    "vi": "Nội dung dòng...",
    "en": "Bullet text...",
    "zh": "要点内容...",
    "ja": "箇条書きテキスト..."
  },
  {
    "key": "svc.admin.basicInfo",
    "category": "svc",
    "vi": "Thông tin cơ bản",
    "en": "Basic info",
    "zh": "基本信息",
    "ja": "基本情報"
  },
  {
    "key": "svc.admin.fallbackError",
    "category": "svc",
    "vi": "Đã xảy ra lỗi, vui lòng thử lại",
    "en": "An error occurred, please try again",
    "zh": "发生错误，请重试",
    "ja": "エラーが発生しました。もう一度お試しください"
  }
```

- [ ] **Step 3: Commit seed changes**

```bash
git add nihomebackend/Data/Seeds/header-footer.json nihomebackend/Data/Seeds/services.json
git commit -m "feat(i18n): add nav.services and svc.admin.* seed keys"
```

---

## Task 2: adminApi — types and service methods

**Files:**
- Modify: `nihomeweb/src/services/adminApi.ts`

- [ ] **Step 1: Add `UpsertServiceAdminRequest` interface**

In `adminApi.ts`, after the `UpsertAboutSectionRequest` block (around line 108), add:

```ts
export interface UpsertServiceAdminRequest {
  slug: string;
  title: string;
  shortTitle: string;
  tagline: string;
  intro: string;
  sections: { heading: string; body: string[] }[];
  highlights: string[];
  sortOrder: number;
}
```

- [ ] **Step 2: Add three service methods to `adminApi` object**

Inside the `adminApi = { ... }` object, add after the `// Processes` section:

```ts
  // Services
  createService: (data: UpsertServiceAdminRequest) =>
    api.post('/services', data),
  updateService: (id: number, data: UpsertServiceAdminRequest) =>
    api.put(`/services/${id}`, data),
  deleteService: (id: number) =>
    api.delete(`/services/${id}`),
```

- [ ] **Step 3: Commit**

```bash
git add nihomeweb/src/services/adminApi.ts
git commit -m "feat(admin): add service CRUD methods to adminApi"
```

---

## Task 3: Services.tsx — list table

**Files:**
- Create: `nihomeweb/src/pages/admin/Services.tsx`

- [ ] **Step 1: Create the file with list table (no form yet)**

Create `nihomeweb/src/pages/admin/Services.tsx`:

```tsx
import { useMemo, useState } from "react";
import { Plus, Search, Pencil, Trash2, ExternalLink } from "lucide-react";
import { Link } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useServices } from "@/hooks/useContentApi";
import type { ServiceResponse, ServiceSection } from "@/services/contentApi";
import { adminApi, slugify, type UpsertServiceAdminRequest } from "@/services/adminApi";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

// ─── Types ───────────────────────────────────────────────────

type SectionDraft = { heading: string; body: string[] };

type FormState = {
  id: number | null;
  slug: string;
  title: string;
  shortTitle: string;
  tagline: string;
  intro: string;
  highlights: string[];
  sections: SectionDraft[];
  sortOrder: number;
};

const emptyForm: FormState = {
  id: null,
  slug: "",
  title: "",
  shortTitle: "",
  tagline: "",
  intro: "",
  highlights: [],
  sections: [],
  sortOrder: 0,
};

// ─── Error helper ────────────────────────────────────────────

function getErrorMessage(error: unknown): string | null {
  if (typeof error === "object" && error !== null) {
    const e = error as {
      message?: unknown;
      response?: { data?: { message?: unknown; title?: unknown; errors?: Record<string, unknown> } };
    };
    const data = e.response?.data;
    if (typeof data?.message === "string") return data.message;
    if (data?.errors) {
      for (const v of Object.values(data.errors)) {
        if (typeof v === "string" && v.trim()) return v;
        if (Array.isArray(v)) {
          const first = v.find((x) => typeof x === "string" && x.trim());
          if (typeof first === "string") return first;
        }
      }
    }
    if (typeof data?.title === "string") return data.title;
    if (typeof e.message === "string") return e.message;
  }
  return null;
}

// ─── Main component ──────────────────────────────────────────

const AdminServices = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [q, setQ] = useState("");
  const [openModal, setOpenModal] = useState(false);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [submitting, setSubmitting] = useState(false);

  const { data, loading, error, refetch } = useServices();
  const items = useMemo(() => data ?? [], [data]);
  const filtered = useMemo(
    () =>
      items.filter(
        (s) =>
          s.title.toLowerCase().includes(q.toLowerCase()) ||
          s.slug.toLowerCase().includes(q.toLowerCase()) ||
          s.shortTitle.toLowerCase().includes(q.toLowerCase()),
      ),
    [items, q],
  );

  const isEditing = form.id != null;

  // ── Handlers ──

  const startCreate = () => {
    const maxSort = items.reduce((m, s) => Math.max(m, 0), 0);
    setForm({ ...emptyForm, sortOrder: maxSort + 1 });
    setOpenModal(true);
  };

  const startEdit = (s: ServiceResponse) => {
    setForm({
      id: s.id,
      slug: s.slug,
      title: s.title,
      shortTitle: s.shortTitle,
      tagline: s.tagline,
      intro: s.intro,
      highlights: [...s.highlights],
      sections: s.sections.map((sec: ServiceSection) => ({
        heading: sec.heading,
        body: [...sec.body],
      })),
      sortOrder: 0,
    });
    setOpenModal(true);
  };

  const remove = async (s: ServiceResponse) => {
    if (!window.confirm(`${t("svc.admin.confirmDel")}\n\n${s.title}`)) return;
    try {
      await adminApi.deleteService(s.id);
      toast({ title: t("form.deleted"), description: s.title });
      await refetch();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err) ?? t("svc.admin.fallbackError"),
        variant: "destructive",
      });
    }
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title.trim() || !form.slug.trim() || !form.shortTitle.trim() || !form.tagline.trim() || !form.intro.trim()) {
      toast({ title: t("form.required"), variant: "destructive" });
      return;
    }
    const payload: UpsertServiceAdminRequest = {
      slug: form.slug.trim(),
      title: form.title.trim(),
      shortTitle: form.shortTitle.trim(),
      tagline: form.tagline.trim(),
      intro: form.intro.trim(),
      highlights: form.highlights.filter((h) => h.trim()),
      sections: form.sections
        .filter((s) => s.heading.trim())
        .map((s) => ({
          heading: s.heading.trim(),
          body: s.body.filter((b) => b.trim()).map((b) => b.trim()),
        })),
      sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
    };
    setSubmitting(true);
    try {
      if (isEditing && form.id != null) {
        await adminApi.updateService(form.id, payload);
        toast({ title: t("form.updated"), description: form.title.trim() });
      } else {
        await adminApi.createService(payload);
        toast({ title: t("form.created"), description: form.title.trim() });
      }
      setOpenModal(false);
      setForm(emptyForm);
      await refetch();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err) ?? t("svc.admin.fallbackError"),
        variant: "destructive",
      });
    } finally {
      setSubmitting(false);
    }
  };

  // ── Render ──

  return (
    <AdminLayout>
      {/* Page header */}
      <div className="mb-6 flex items-start gap-3">
        <div className="flex-1">
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
            {t("svc.admin.title")}
          </h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} / {items.length}
          </p>
        </div>
        <button
          onClick={startCreate}
          className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg font-bold text-sm shrink-0"
          style={{ background: "hsl(var(--admin-primary))", color: "white" }}
        >
          <Plus className="w-4 h-4" />
          {t("svc.admin.add")}
        </button>
      </div>

      {/* Search bar */}
      <div className="admin-card p-5 mb-5">
        <div className="flex items-center gap-2 max-w-md">
          <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t("svc.admin.searchPh")}
            className="admin-input flex-1"
          />
        </div>
      </div>

      {/* Table */}
      {loading ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("common.loading")}
        </div>
      ) : error ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-danger))" }}>
          {error}
        </div>
      ) : filtered.length === 0 ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("svc.admin.empty")}
        </div>
      ) : (
        <div className="admin-card overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead style={{ background: "hsl(var(--admin-bg))" }}>
                <tr className="text-left">
                  <th className="px-5 py-4 font-bold text-xs uppercase tracking-wider w-12">#</th>
                  <th className="px-5 py-4 font-bold text-xs uppercase tracking-wider">{t("svc.admin.shortTitle")}</th>
                  <th className="px-5 py-4 font-bold text-xs uppercase tracking-wider">{t("form.title")}</th>
                  <th className="px-5 py-4 font-bold text-xs uppercase tracking-wider hidden lg:table-cell">{t("svc.admin.tagline")}</th>
                  <th className="px-5 py-4 font-bold text-xs uppercase tracking-wider text-right">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((s) => (
                  <tr
                    key={s.id}
                    className="border-t hover:bg-muted/30 transition"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <td className="px-5 py-4 text-xs font-mono" style={{ color: "hsl(var(--admin-muted))" }}>
                      {s.id}
                    </td>
                    <td className="px-5 py-4">
                      <span className="admin-chip" style={{ background: "hsl(var(--admin-primary-soft))", color: "hsl(var(--admin-primary))" }}>
                        {s.shortTitle}
                      </span>
                    </td>
                    <td className="px-5 py-4">
                      <p className="font-semibold">{s.title}</p>
                      <p className="text-xs mt-0.5 font-mono" style={{ color: "hsl(var(--admin-muted))" }}>{s.slug}</p>
                    </td>
                    <td className="px-5 py-4 hidden lg:table-cell">
                      <p className="text-sm line-clamp-1 max-w-xs" style={{ color: "hsl(var(--admin-muted))" }}>{s.tagline}</p>
                    </td>
                    <td className="px-5 py-4 text-right">
                      <div className="inline-flex items-center gap-1">
                        <Link
                          to={`/services/${s.slug}`}
                          target="_blank"
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center transition"
                          style={{ color: "hsl(var(--admin-info))" }}
                          title={t("common.view")}
                        >
                          <ExternalLink className="w-4 h-4" />
                        </Link>
                        <button
                          onClick={() => startEdit(s)}
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center transition"
                          style={{ color: "hsl(var(--admin-primary))" }}
                          title={t("svc.admin.editTitle")}
                        >
                          <Pencil className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => remove(s)}
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center transition"
                          style={{ color: "hsl(var(--admin-danger))" }}
                          title={t("common.delete")}
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Dialog placeholder — added in Task 4 */}
    </AdminLayout>
  );
};

export default AdminServices;
```

- [ ] **Step 2: Commit**

```bash
git add nihomeweb/src/pages/admin/Services.tsx
git commit -m "feat(admin): Services list table scaffold"
```

---

## Task 4: Services.tsx — dialog form (basic info + intro + highlights)

**Files:**
- Modify: `nihomeweb/src/pages/admin/Services.tsx`

- [ ] **Step 1: Add missing imports at the top of the file**

Add `Loader2, Save, X, GripVertical` to the lucide imports line:

```tsx
import { Plus, Search, Pencil, Trash2, ExternalLink, Loader2, Save, X } from "lucide-react";
```

- [ ] **Step 2: Replace the `{/* Dialog placeholder */}` comment with the full Dialog**

Replace:
```tsx
      {/* Dialog placeholder — added in Task 4 */}
```

With the complete Dialog below. Place it just before the closing `</AdminLayout>`:

```tsx
      <Dialog open={openModal} onOpenChange={setOpenModal}>
        <DialogContent className="admin-scope max-w-2xl max-h-[92vh] flex flex-col p-0 gap-0 overflow-hidden rounded-2xl border-0 shadow-2xl">
          {/* Gradient header */}
          <DialogHeader className="relative px-6 py-5 text-left space-y-1 bg-gradient-to-br from-rose-500 via-rose-500 to-orange-500 text-white shrink-0">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-white/15 backdrop-blur flex items-center justify-center shrink-0">
                {isEditing ? <Pencil className="w-5 h-5" /> : <Plus className="w-5 h-5" />}
              </div>
              <div className="min-w-0">
                <DialogTitle className="text-lg sm:text-xl font-extrabold tracking-tight">
                  {isEditing ? t("svc.admin.editTitle") : t("svc.admin.createTitle")}
                </DialogTitle>
                <DialogDescription className="text-white/85 text-xs sm:text-sm mt-0.5">
                  {form.title || t("svc.admin.title")}
                </DialogDescription>
              </div>
            </div>
          </DialogHeader>

          <form onSubmit={save} className="flex flex-col flex-1 overflow-hidden bg-slate-50">
            <div className="flex-1 overflow-y-auto px-6 py-5 space-y-5">

              {/* ── Basic info ── */}
              <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
                <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
                  <span className="w-1 h-4 rounded-full bg-rose-500" />
                  {t("svc.admin.basicInfo")}
                </h3>
                <div className="space-y-4">
                  {/* Title */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      {t("form.title")} <span className="text-rose-500">*</span>
                    </label>
                    <input
                      value={form.title}
                      onChange={(e) => {
                        const title = e.target.value;
                        setForm((f) => ({
                          ...f,
                          title,
                          slug: f.id == null ? slugify(title) : f.slug,
                        }));
                      }}
                      placeholder={t("form.title")}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      required
                      autoFocus
                    />
                  </div>

                  {/* Short title + Slug (2-col) */}
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                        {t("svc.admin.shortTitle")} <span className="text-rose-500">*</span>
                      </label>
                      <input
                        value={form.shortTitle}
                        onChange={(e) => setForm((f) => ({ ...f, shortTitle: e.target.value }))}
                        placeholder="Design & Build"
                        className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                        required
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                        {t("svc.admin.slug")} <span className="text-rose-500">*</span>
                      </label>
                      <input
                        value={form.slug}
                        onChange={(e) => setForm((f) => ({ ...f, slug: e.target.value }))}
                        placeholder="design-and-build"
                        className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm font-mono text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                        required
                      />
                    </div>
                  </div>

                  {/* Tagline + SortOrder (3+1 col) */}
                  <div className="grid grid-cols-4 gap-3">
                    <div className="col-span-3">
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                        {t("svc.admin.tagline")} <span className="text-rose-500">*</span>
                      </label>
                      <input
                        value={form.tagline}
                        onChange={(e) => setForm((f) => ({ ...f, tagline: e.target.value }))}
                        placeholder={t("svc.admin.tagline")}
                        className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                        required
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-1.5">{t("proc.fieldSortOrder")}</label>
                      <input
                        type="number"
                        value={form.sortOrder}
                        onChange={(e) => setForm((f) => ({ ...f, sortOrder: Number(e.target.value) }))}
                        className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      />
                    </div>
                  </div>
                </div>
              </section>

              {/* ── Intro ── */}
              <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
                <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
                  <span className="w-1 h-4 rounded-full bg-rose-500" />
                  {t("svc.admin.intro")}
                </h3>
                <textarea
                  value={form.intro}
                  onChange={(e) => setForm((f) => ({ ...f, intro: e.target.value }))}
                  placeholder={t("svc.admin.intro")}
                  rows={4}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition resize-none"
                  required
                />
              </section>

              {/* ── Highlights ── */}
              <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
                <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
                  <span className="w-1 h-4 rounded-full bg-rose-500" />
                  {t("svc.admin.highlights")}
                  <span className="text-slate-400 normal-case font-normal">({form.highlights.length})</span>
                </h3>
                <div className="space-y-2">
                  {form.highlights.map((h, i) => (
                    <div key={i} className="flex items-center gap-2">
                      <input
                        value={h}
                        onChange={(e) =>
                          setForm((f) => ({
                            ...f,
                            highlights: f.highlights.map((x, j) => (j === i ? e.target.value : x)),
                          }))
                        }
                        placeholder={t("svc.admin.highlightPh")}
                        className="flex-1 px-3.5 py-2 rounded-xl border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                      />
                      <button
                        type="button"
                        onClick={() =>
                          setForm((f) => ({
                            ...f,
                            highlights: f.highlights.filter((_, j) => j !== i),
                          }))
                        }
                        className="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition shrink-0"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  ))}
                  <button
                    type="button"
                    onClick={() => setForm((f) => ({ ...f, highlights: [...f.highlights, ""] }))}
                    className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl border border-dashed border-slate-300 text-sm font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/50 transition"
                  >
                    {t("svc.admin.addHighlight")}
                  </button>
                </div>
              </section>

              {/* ── Sections — added in Task 5 ── */}
              <SectionsEditor form={form} setForm={setForm} t={t} />

            </div>

            {/* Footer actions */}
            <div className="px-6 py-4 border-t border-slate-200 bg-white flex items-center justify-end gap-2 shrink-0">
              <button
                type="button"
                onClick={() => setOpenModal(false)}
                disabled={submitting}
                className="inline-flex items-center gap-1.5 px-5 py-2.5 rounded-xl font-semibold text-sm border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-50 transition"
              >
                <X className="w-4 h-4" />
                {t("common.cancel")}
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl font-semibold text-sm text-white bg-gradient-to-br from-rose-500 to-orange-500 shadow-md shadow-rose-500/30 hover:shadow-lg hover:shadow-rose-500/40 hover:brightness-105 disabled:opacity-50 disabled:shadow-none transition"
              >
                {submitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                {submitting ? t("common.loading") : t("common.save")}
              </button>
            </div>
          </form>
        </DialogContent>
      </Dialog>
```

- [ ] **Step 3: Commit**

```bash
git add nihomeweb/src/pages/admin/Services.tsx
git commit -m "feat(admin): Services dialog form - basic info, intro, highlights"
```

---

## Task 5: Services.tsx — SectionsEditor sub-component

**Files:**
- Modify: `nihomeweb/src/pages/admin/Services.tsx`

The `SectionsEditor` component referenced in Task 4 must be defined in the same file **before** `AdminServices`. The sections editor allows adding/removing sections, editing each section's heading, and adding/removing bullet items per section.

- [ ] **Step 1: Add `SectionsEditor` component before `AdminServices` in the file**

Insert the following before `const AdminServices = () => {`:

```tsx
// ─── SectionsEditor sub-component ────────────────────────────

type TFunction = (key: string) => string;

function SectionsEditor({
  form,
  setForm,
  t,
}: {
  form: FormState;
  setForm: React.Dispatch<React.SetStateAction<FormState>>;
  t: TFunction;
}) {
  const addSection = () =>
    setForm((f) => ({
      ...f,
      sections: [...f.sections, { heading: "", body: [""] }],
    }));

  const removeSection = (si: number) =>
    setForm((f) => ({
      ...f,
      sections: f.sections.filter((_, i) => i !== si),
    }));

  const updateHeading = (si: number, value: string) =>
    setForm((f) => ({
      ...f,
      sections: f.sections.map((s, i) => (i === si ? { ...s, heading: value } : s)),
    }));

  const addBullet = (si: number) =>
    setForm((f) => ({
      ...f,
      sections: f.sections.map((s, i) =>
        i === si ? { ...s, body: [...s.body, ""] } : s,
      ),
    }));

  const updateBullet = (si: number, bi: number, value: string) =>
    setForm((f) => ({
      ...f,
      sections: f.sections.map((s, i) =>
        i === si
          ? { ...s, body: s.body.map((b, j) => (j === bi ? value : b)) }
          : s,
      ),
    }));

  const removeBullet = (si: number, bi: number) =>
    setForm((f) => ({
      ...f,
      sections: f.sections.map((s, i) =>
        i === si ? { ...s, body: s.body.filter((_, j) => j !== bi) } : s,
      ),
    }));

  return (
    <section className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-4 flex items-center gap-2">
        <span className="w-1 h-4 rounded-full bg-rose-500" />
        {t("svc.admin.sections")}
        <span className="text-slate-400 normal-case font-normal">({form.sections.length})</span>
      </h3>

      <div className="space-y-4">
        {form.sections.map((sec, si) => (
          <div
            key={si}
            className="rounded-xl border border-slate-200 bg-slate-50/60 p-4 space-y-3"
          >
            {/* Section heading row */}
            <div className="flex items-center gap-2">
              <span className="w-6 h-6 rounded-lg bg-rose-100 text-rose-600 flex items-center justify-center text-xs font-extrabold shrink-0">
                {si + 1}
              </span>
              <input
                value={sec.heading}
                onChange={(e) => updateHeading(si, e.target.value)}
                placeholder={t("svc.admin.sectionHeadingPh")}
                className="flex-1 px-3.5 py-2 rounded-xl border border-slate-200 bg-white text-sm font-semibold text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
              />
              <button
                type="button"
                onClick={() => removeSection(si)}
                className="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition shrink-0"
                title={t("common.delete")}
              >
                <Trash2 className="w-4 h-4" />
              </button>
            </div>

            {/* Body bullets */}
            <div className="pl-8 space-y-2">
              {sec.body.map((bullet, bi) => (
                <div key={bi} className="flex items-center gap-2">
                  <span className="w-1.5 h-1.5 rounded-full bg-rose-400 shrink-0" />
                  <input
                    value={bullet}
                    onChange={(e) => updateBullet(si, bi, e.target.value)}
                    placeholder={t("svc.admin.bulletPh")}
                    className="flex-1 px-3 py-1.5 rounded-lg border border-slate-200 bg-white text-sm text-slate-800 placeholder:text-slate-400 outline-none focus:border-rose-500 focus:ring-4 focus:ring-rose-500/15 transition"
                  />
                  <button
                    type="button"
                    onClick={() => removeBullet(si, bi)}
                    className="w-7 h-7 rounded-lg flex items-center justify-center text-slate-400 hover:bg-rose-50 hover:text-rose-600 transition shrink-0"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))}
              <button
                type="button"
                onClick={() => addBullet(si)}
                className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-dashed border-slate-300 text-xs font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/50 transition"
              >
                {t("svc.admin.addBullet")}
              </button>
            </div>
          </div>
        ))}

        <button
          type="button"
          onClick={addSection}
          className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl border border-dashed border-slate-300 text-sm font-semibold text-slate-500 hover:border-rose-400 hover:text-rose-600 hover:bg-rose-50/50 transition"
        >
          {t("svc.admin.addSection")}
        </button>
      </div>
    </section>
  );
}
```

Note: `Trash2` and `X` are already imported from lucide-react in Task 4.

- [ ] **Step 2: Commit**

```bash
git add nihomeweb/src/pages/admin/Services.tsx
git commit -m "feat(admin): Services dynamic sections editor"
```

---

## Task 6: Route + nav item

**Files:**
- Modify: `nihomeweb/src/App.tsx`
- Modify: `nihomeweb/src/components/layout/AdminLayout.tsx`

- [ ] **Step 1: Add route to App.tsx**

In `nihomeweb/src/App.tsx`:
1. Add import near the other admin imports:
   ```tsx
   import AdminServices from "./pages/admin/Services.tsx";
   ```
2. Add the route inside the admin routes section, near the other content routes (after `/admin/projects`):
   ```tsx
   <Route path="/admin/services" element={<AdminServices />} />
   ```

- [ ] **Step 2: Add nav item to AdminLayout.tsx**

In `nihomeweb/src/components/layout/AdminLayout.tsx`:

1. Add `ConciergeBell` to the lucide import (it fits "services"):
   ```tsx
   import {
     // ... existing icons ...
     ConciergeBell,
   } from "lucide-react";
   ```

2. In the `groups` array, find the `content` group (id: `"content"`). Add the services item after the projects item:
   ```tsx
   { to: "/admin/services", label: t("nav.services"), icon: ConciergeBell },
   ```
   
   The full content group items should look like:
   ```tsx
   items: [
     { to: "/admin/posts", label: t("nav.posts"), icon: FileText },
     { to: "/admin/projects", label: t("nav.projects"), icon: Building2 },
     { to: "/admin/services", label: t("nav.services"), icon: ConciergeBell },
     { to: "/admin/recruitment", label: t("nav.recruitment"), icon: Briefcase },
     { to: "/admin/contacts", label: t("nav.contacts"), icon: Inbox },
     { to: "/admin/categories", label: t("nav.categories"), icon: FolderTree },
   ],
   ```

- [ ] **Step 3: Commit**

```bash
git add nihomeweb/src/App.tsx nihomeweb/src/components/layout/AdminLayout.tsx
git commit -m "feat(admin): register /admin/services route and nav item"
```

---

## Task 7: Quality gates

**Files:** (no changes — verification only)

- [ ] **Step 1: Run frontend lint**

```bash
cd nihomeweb && npm run lint
```

Expected: no errors. If lint errors appear, fix them before proceeding.

Common issues to watch for:
- `t` function typed as `(key: string) => string` in `SectionsEditor` — if TypeScript complains, replace `TFunction` with the return type from `useI18n()`:
  ```tsx
  const { t } = useI18n();
  type TFunction = typeof t;
  ```
- Unused imports — remove any that weren't needed.

- [ ] **Step 2: Run frontend build**

```bash
npm run build
```

Expected: no TypeScript or build errors.

- [ ] **Step 3: Final commit (if any lint/build fixes were made)**

```bash
git add -p   # stage only relevant changes
git commit -m "fix(admin): lint and type fixes for Services page"
```

- [ ] **Step 4: Note for backend restart**

After deploying or running the backend locally, restart it so `TranslationSeeder` picks up the new seed keys from `header-footer.json` and `services.json`. No migration is needed — the seed files update the `Translations` table via the existing seeder.

```
docker compose restart backend
# or: dotnet run inside the backend container
```

---

## Self-Review Checklist

**Spec coverage:**
- [x] `adminApi.ts` — `UpsertServiceAdminRequest` + 3 methods (Task 2)
- [x] `admin/Services.tsx` — list table with search (Task 3)
- [x] `admin/Services.tsx` — dialog: basic info, intro, highlights (Task 4)
- [x] `admin/Services.tsx` — dialog: sections dynamic editor (Task 5)
- [x] `admin/Services.tsx` — save (create/update) and delete handlers (in Task 3 file)
- [x] Route `/admin/services` (Task 6)
- [x] Nav item with `ConciergeBell` icon in content group (Task 6)
- [x] i18n: `nav.services` → `header-footer.json` (Task 1)
- [x] i18n: `svc.admin.*` → `services.json` (Task 1)
- [x] All four languages: vi/en/zh/ja (Task 1)
- [x] Quality gates: lint + build (Task 7)

**Key type consistency:**
- `UpsertServiceAdminRequest.sections` → `{ heading: string; body: string[] }[]` — matches `SectionDraft` and `ServiceSection` from `contentApi`
- `adminApi.createService` / `updateService` / `deleteService` — use `id: number` from `ServiceResponse`
- `slugify` imported from `adminApi.ts` — already exported there
- `proc.fieldSortOrder` i18n key — already exists in `admin-system.json` (reused from ProcessList pattern)
- `form.title` → `t("form.title")` key — check it exists. If not, use `t("svc.admin.basicInfo")` label inline or add `form.title` to `common.json`. Looking at codebase, `form.title` is used in `PostForm.tsx` so it exists.
