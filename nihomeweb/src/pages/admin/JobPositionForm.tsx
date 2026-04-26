import { useEffect, useState } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { ArrowLeft, Save, Plus, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi } from "@/services/adminApi";
import type { UpsertJobPositionRequest, JobPositionResponse } from "@/services/adminApi";

interface FormData {
  id: number;
  title: string;
  department: string;
  location: string;
  employmentType: string;
  experienceLevel: string;
  description: string;
  requirements: string[];
  isActive: boolean;
  sortOrder: number;
}

const empty: FormData = {
  id: 0,
  title: "",
  department: "",
  location: "",
  employmentType: "full-time",
  experienceLevel: "mid",
  description: "",
  requirements: [],
  isActive: true,
  sortOrder: 0,
};

const JobPositionForm = ({ mode }: { mode: "create" | "edit" }) => {
  const { id: idParam } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const [data, setData] = useState<FormData>(empty);
  const [loading, setLoading] = useState(mode === "edit");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (mode !== "edit" || !idParam) return;
    adminApi.getJobPositions(true).then((res) => {
      const pos = (res.data as JobPositionResponse[]).find((p) => p.id === Number(idParam));
      if (pos) {
        setData({
          id: pos.id,
          title: pos.title,
          department: pos.department,
          location: pos.location,
          employmentType: pos.employmentType,
          experienceLevel: pos.experienceLevel,
          description: pos.description ?? "",
          requirements: pos.requirements ?? [],
          isActive: pos.isActive,
          sortOrder: pos.sortOrder,
        });
      }
      setLoading(false);
    }).catch(() => setLoading(false));
  }, [mode, idParam]);

  const update = <K extends keyof FormData>(key: K, value: FormData[K]) =>
    setData((d) => ({ ...d, [key]: value }));

  const addRequirement = () => update("requirements", [...data.requirements, ""]);
  const updateRequirement = (i: number, val: string) => {
    const next = [...data.requirements];
    next[i] = val;
    update("requirements", next);
  };
  const removeRequirement = (i: number) =>
    update("requirements", data.requirements.filter((_, idx) => idx !== i));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!data.title.trim() || !data.department.trim() || !data.location.trim()) {
      toast({ title: t("form.required"), variant: "destructive" });
      return;
    }
    const payload: UpsertJobPositionRequest = {
      title: data.title,
      department: data.department,
      location: data.location,
      employmentType: data.employmentType,
      experienceLevel: data.experienceLevel,
      description: data.description || undefined,
      requirements: data.requirements.filter((r) => r.trim()),
      isActive: data.isActive,
      sortOrder: data.sortOrder,
    };
    setSubmitting(true);
    try {
      if (mode === "create") {
        await adminApi.createJobPosition(payload);
      } else {
        await adminApi.updateJobPosition(data.id, payload);
      }
      toast({ title: mode === "create" ? t("form.created") : t("form.updated"), description: data.title });
      navigate("/admin/recruitment");
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <AdminLayout>
        <div className="flex items-center justify-center h-64">
          <div className="w-8 h-8 border-4 rounded-full animate-spin" style={{ borderColor: "hsl(var(--admin-primary))", borderTopColor: "transparent" }} />
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="flex items-center gap-3 mb-6">
        <Link
          to="/admin/recruitment"
          className="w-10 h-10 rounded-full bg-white border flex items-center justify-center hover:bg-muted transition"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <ArrowLeft className="w-4 h-4" />
        </Link>
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
            {mode === "create" ? "Đăng vị trí tuyển dụng" : "Chỉnh sửa vị trí tuyển dụng"}
          </h1>
          {mode === "edit" && data.title && (
            <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{data.title}</p>
          )}
        </div>
      </div>

      <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">

          {/* Basic info */}
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">{t("form.basicInfo")}</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <Field label="Chức danh *" className="md:col-span-2">
                <input
                  className="admin-input"
                  value={data.title}
                  onChange={(e) => update("title", e.target.value)}
                  placeholder="Kỹ sư kết cấu, Kiến trúc sư..."
                  required
                />
              </Field>
              <Field label="Phòng ban *">
                <input
                  className="admin-input"
                  value={data.department}
                  onChange={(e) => update("department", e.target.value)}
                  placeholder="Thiết kế, Thi công..."
                  required
                />
              </Field>
              <Field label="Địa điểm *">
                <input
                  className="admin-input"
                  value={data.location}
                  onChange={(e) => update("location", e.target.value)}
                  placeholder="TP.HCM, Toàn quốc..."
                  required
                />
              </Field>
              <Field label="Hình thức làm việc">
                <select
                  className="admin-input"
                  value={data.employmentType}
                  onChange={(e) => update("employmentType", e.target.value)}
                >
                  <option value="full-time">Toàn thời gian</option>
                  <option value="part-time">Bán thời gian</option>
                  <option value="intern">Thực tập sinh</option>
                </select>
              </Field>
              <Field label="Kinh nghiệm yêu cầu">
                <select
                  className="admin-input"
                  value={data.experienceLevel}
                  onChange={(e) => update("experienceLevel", e.target.value)}
                >
                  <option value="student">Sinh viên</option>
                  <option value="junior">Mới ra trường (0–2 năm)</option>
                  <option value="mid">Trung cấp (2–5 năm)</option>
                  <option value="senior">Cao cấp (5+ năm)</option>
                </select>
              </Field>
            </div>
          </div>

          {/* Description */}
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">Mô tả công việc</h2>
            <textarea
              className="admin-input min-h-28"
              value={data.description}
              onChange={(e) => update("description", e.target.value)}
              placeholder="Mô tả chi tiết về vị trí, trách nhiệm công việc..."
            />
          </div>

          {/* Requirements */}
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">Yêu cầu ứng viên</h2>
            <div className="space-y-2">
              {data.requirements.map((req, i) => (
                <div key={i} className="flex gap-2">
                  <input
                    className="admin-input flex-1"
                    value={req}
                    onChange={(e) => updateRequirement(i, e.target.value)}
                    placeholder="Tốt nghiệp đại học chuyên ngành..."
                  />
                  <button
                    type="button"
                    onClick={() => removeRequirement(i)}
                    className="w-9 h-9 flex items-center justify-center rounded-lg border text-destructive hover:bg-destructive/10 transition"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))}
              <button
                type="button"
                onClick={addRequirement}
                className="w-full flex items-center justify-center gap-1.5 py-2 rounded-lg border border-dashed text-sm hover:bg-muted transition"
                style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-muted))" }}
              >
                <Plus className="w-3.5 h-3.5" /> Thêm yêu cầu
              </button>
            </div>
          </div>
        </div>

        {/* Sidebar */}
        <div className="space-y-5">
          <div className="admin-card p-6">
            <h2 className="font-bold mb-4">Cài đặt</h2>
            <div className="space-y-4">
              <label className="flex items-center gap-3 cursor-pointer">
                <input
                  type="checkbox"
                  checked={data.isActive}
                  onChange={(e) => update("isActive", e.target.checked)}
                  className="w-4 h-4 rounded"
                />
                <span className="text-sm font-medium">Đang tuyển dụng</span>
              </label>
              <Field label="Thứ tự hiển thị">
                <input
                  type="number"
                  className="admin-input"
                  value={data.sortOrder}
                  onChange={(e) => update("sortOrder", Number(e.target.value))}
                  min="0"
                />
              </Field>
            </div>
          </div>

          <button
            type="submit"
            disabled={submitting}
            className="admin-btn-primary w-full inline-flex items-center justify-center gap-2 px-5 py-3 text-sm disabled:opacity-50"
          >
            <Save className="w-4 h-4" />
            {mode === "create" ? t("form.create") : t("form.update")}
          </button>
        </div>
      </form>
    </AdminLayout>
  );
};

const Field = ({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) => (
  <label className={["block", className].filter(Boolean).join(" ")}>
    <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </span>
    {children}
  </label>
);

export default JobPositionForm;
