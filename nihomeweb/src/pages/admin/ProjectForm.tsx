import { useState } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { ArrowLeft, Save } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, slugify } from "@/services/adminApi";
import type { UpsertProjectRequest } from "@/services/adminApi";
import { useProject } from "@/hooks/useContentApi";
import { PageLoading, PageError } from "@/components/PageState";

interface FormData {
  id: number;
  slug: string;
  imageUrl: string;
  name: string;
  client: string;
  location: string;
  scale: string;
  scope: string;
  status: string;
  year: string;
  category: string;
  description: string;
  challenges: string[];
  solutions: string[];
}

const empty: FormData = {
  id: 0,
  slug: "",
  imageUrl: "",
  name: "",
  client: "",
  location: "",
  scale: "",
  scope: "",
  status: "ongoing",
  year: "",
  category: "",
  description: "",
  challenges: [],
  solutions: [],
};

const ProjectForm = ({ mode }: { mode: "create" | "edit" }) => {
  const { slug } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const { data: existing, loading, error, refetch } = useProject(mode === "edit" ? (slug ?? "") : "");
  const [data, setData] = useState<FormData>(empty);
  const [initialized, setInitialized] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  if (mode === "edit" && existing && !initialized) {
    setData({
      id: existing.id,
      slug: existing.slug,
      imageUrl: existing.imageUrl,
      name: existing.name,
      client: existing.client,
      location: existing.location,
      scale: existing.scale,
      scope: existing.scope,
      status: existing.status,
      year: existing.year ?? "",
      category: existing.category ?? "",
      description: existing.description ?? "",
      challenges: existing.challenges ?? [],
      solutions: existing.solutions ?? [],
    });
    setInitialized(true);
  }

  const update = <K extends keyof FormData>(key: K, value: FormData[K]) =>
    setData((d) => ({ ...d, [key]: value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!data.name.trim()) {
      toast({ title: t("form.required"), description: t("proj.field.name"), variant: "destructive" });
      return;
    }
    const payload: UpsertProjectRequest = {
      slug: data.slug || slugify(data.name),
      imageUrl: data.imageUrl || "/placeholder.svg",
      name: data.name,
      client: data.client,
      location: data.location,
      scale: data.scale,
      scope: data.scope,
      status: data.status,
      year: data.year || undefined,
      category: data.category || undefined,
      description: data.description || undefined,
      challenges: data.challenges.length ? data.challenges : undefined,
      solutions: data.solutions.length ? data.solutions : undefined,
    };
    setSubmitting(true);
    try {
      if (mode === "create") {
        await adminApi.createProject(payload);
      } else {
        await adminApi.updateProject(data.id, payload);
      }
      toast({ title: mode === "create" ? t("form.created") : t("form.updated"), description: data.name });
      navigate("/admin/projects");
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setSubmitting(false);
    }
  };

  if (mode === "edit" && loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (mode === "edit" && error) return <AdminLayout><PageError message={error} onRetry={refetch} /></AdminLayout>;

  return (
    <AdminLayout>
      <div className="flex items-center gap-3 mb-6">
        <Link
          to="/admin/projects"
          className="w-10 h-10 rounded-full bg-white border flex items-center justify-center hover:bg-muted transition"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <ArrowLeft className="w-4 h-4" />
        </Link>
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
            {mode === "create" ? t("proj.addTitle") : t("proj.editTitle")}
          </h1>
          <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>
            {mode === "edit" && data.slug}
          </p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.basicInfo")}</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Field label={t("proj.field.name") + " *"}>
                <input className="admin-input" value={data.name} onChange={(e) => update("name", e.target.value)} required />
              </Field>
              <Field label={t("proj.field.client")}>
                <input className="admin-input" value={data.client} onChange={(e) => update("client", e.target.value)} />
              </Field>
              <Field label={t("proj.field.location")}>
                <input className="admin-input" value={data.location} onChange={(e) => update("location", e.target.value)} />
              </Field>
              <Field label={t("proj.scale")}>
                <input className="admin-input" value={data.scale} onChange={(e) => update("scale", e.target.value)} placeholder="e.g. 15.000 m²" />
              </Field>
              <Field label={t("proj.field.scope")}>
                <input className="admin-input" value={data.scope} onChange={(e) => update("scope", e.target.value)} />
              </Field>
              <Field label={t("proj.field.year")}>
                <input className="admin-input" value={data.year ?? ""} onChange={(e) => update("year", e.target.value)} />
              </Field>
              <Field label={t("proj.field.category")}>
                <input className="admin-input" value={data.category ?? ""} onChange={(e) => update("category", e.target.value)} />
              </Field>
              <Field label={t("proj.field.status")}>
                <select
                  className="admin-input"
                  value={data.status}
                  onChange={(e) => update("status", e.target.value)}
                >
                  <option value="ongoing">{t("proj.ongoing")}</option>
                  <option value="completed">{t("proj.completed")}</option>
                </select>
              </Field>
            </div>
          </div>

          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.content")}</h2>
            <div className="space-y-4">
              <Field label={t("proj.field.description")}>
                <textarea
                  className="admin-input min-h-24"
                  value={data.description ?? ""}
                  onChange={(e) => update("description", e.target.value)}
                />
              </Field>
              <Field label={t("proj.field.challenges")}>
                <textarea
                  className="admin-input min-h-28"
                  value={(data.challenges ?? []).join("\n")}
                  onChange={(e) => update("challenges", e.target.value.split("\n").filter(Boolean))}
                />
              </Field>
              <Field label={t("proj.field.solutions")}>
                <textarea
                  className="admin-input min-h-28"
                  value={(data.solutions ?? []).join("\n")}
                  onChange={(e) => update("solutions", e.target.value.split("\n").filter(Boolean))}
                />
              </Field>
            </div>
          </div>
        </div>

        <div className="space-y-5">
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.media")}</h2>
            <Field label={t("proj.field.image")}>
              <input className="admin-input" value={data.imageUrl} onChange={(e) => update("imageUrl", e.target.value)} placeholder="https://..." />
            </Field>
            {data.imageUrl && (
              <div className="mt-4 aspect-[16/10] rounded-xl overflow-hidden bg-muted">
                <img src={data.imageUrl} alt="" className="w-full h-full object-cover" onError={(e) => ((e.target as HTMLImageElement).src = "/placeholder.svg")} />
              </div>
            )}
          </div>

          <button type="submit" disabled={submitting} className="admin-btn-primary w-full inline-flex items-center justify-center gap-2 px-5 py-3 text-sm disabled:opacity-50">
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

export default ProjectForm;
