import { useEffect, useState } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { ArrowLeft, Save } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { getPost, upsertPost, slugify } from "@/lib/adminStore";
import type { Activity } from "@/data/activities";

const empty: Activity = {
  id: "",
  date: new Date().toLocaleDateString("vi-VN").split("/").join("."),
  img: "",
  category: "",
  title: "",
  excerpt: "",
  content: [],
  author: "",
};

const PostForm = ({ mode }: { mode: "create" | "edit" }) => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const [data, setData] = useState<Activity>(empty);

  useEffect(() => {
    if (mode === "edit" && id) {
      const found = getPost(id);
      if (found) setData(found);
    }
  }, [mode, id]);

  const update = <K extends keyof Activity>(key: K, value: Activity[K]) =>
    setData((d) => ({ ...d, [key]: value }));

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!data.title.trim()) {
      toast({ title: t("form.required"), description: t("posts.field.title"), variant: "destructive" });
      return;
    }
    const final: Activity = {
      ...data,
      id: data.id || slugify(data.title),
      img: data.img || "/placeholder.svg",
    };
    upsertPost(final);
    toast({ title: mode === "create" ? t("form.created") : t("form.updated"), description: final.title });
    navigate("/admin/posts");
  };

  return (
    <AdminLayout>
      <div className="flex items-center gap-3 mb-6">
        <Link
          to="/admin/posts"
          className="w-10 h-10 rounded-full bg-white border flex items-center justify-center hover:bg-muted transition"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <ArrowLeft className="w-4 h-4" />
        </Link>
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
            {mode === "create" ? t("posts.addTitle") : t("posts.editTitle")}
          </h1>
          <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{mode === "edit" && data.id}</p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.basicInfo")}</h2>
            <div className="space-y-4">
              <Field label={t("posts.field.title") + " *"}>
                <input className="admin-input" value={data.title} onChange={(e) => update("title", e.target.value)} required />
              </Field>
              <Field label={t("posts.field.excerpt")}>
                <textarea className="admin-input min-h-20" value={data.excerpt} onChange={(e) => update("excerpt", e.target.value)} />
              </Field>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Field label={t("posts.field.category")}>
                  <input className="admin-input" value={data.category} onChange={(e) => update("category", e.target.value)} />
                </Field>
                <Field label={t("posts.field.author")}>
                  <input className="admin-input" value={data.author ?? ""} onChange={(e) => update("author", e.target.value)} />
                </Field>
                <Field label={t("posts.field.date")}>
                  <input className="admin-input" value={data.date} onChange={(e) => update("date", e.target.value)} placeholder="DD.MM.YYYY" />
                </Field>
              </div>
            </div>
          </div>

          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.content")}</h2>
            <Field label={t("posts.field.content")}>
              <textarea
                className="admin-input min-h-64"
                value={data.content.join("\n")}
                onChange={(e) => update("content", e.target.value.split("\n").filter(Boolean))}
              />
            </Field>
          </div>
        </div>

        <div className="space-y-5">
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.media")}</h2>
            <Field label={t("posts.field.image")}>
              <input className="admin-input" value={data.img} onChange={(e) => update("img", e.target.value)} placeholder="https://..." />
            </Field>
            {data.img && (
              <div className="mt-4 aspect-[16/10] rounded-xl overflow-hidden bg-muted">
                <img src={data.img} alt="" className="w-full h-full object-cover" onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")} />
              </div>
            )}
          </div>

          <button type="submit" className="admin-btn-primary w-full inline-flex items-center justify-center gap-2 px-5 py-3 text-sm">
            <Save className="w-4 h-4" />
            {mode === "create" ? t("form.create") : t("form.update")}
          </button>
        </div>
      </form>
    </AdminLayout>
  );
};

const Field = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <label className="block">
    <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </span>
    {children}
  </label>
);

export default PostForm;
