import { useState } from "react";
import { Plus, Briefcase, MapPin, Eye, CheckCircle2, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

const positions = [
  { title: "Kiến trúc sư trưởng", dept: "Thiết kế", location: "TP.HCM", apps: 12 },
  { title: "Kỹ sư kết cấu", dept: "Thiết kế", location: "TP.HCM", apps: 8 },
  { title: "Kỹ sư M&E", dept: "Thiết kế", location: "TP.HCM", apps: 15 },
  { title: "Chỉ huy trưởng công trình", dept: "Thi công", location: "Bình Dương", apps: 6 },
  { title: "Cán bộ QA/QC", dept: "Quản lý CL", location: "Toàn quốc", apps: 9 },
];

type App = {
  id: string;
  name: string;
  position: string;
  exp: string;
  date: string;
  status: "new" | "interview" | "rejected";
};

const initialApps: App[] = [
  { id: "a1", name: "Lê Văn Hùng", position: "Kỹ sư M&E", exp: "5 năm", date: "2 giờ trước", status: "new" },
  { id: "a2", name: "Trần Mai", position: "Kiến trúc sư trưởng", exp: "8 năm", date: "1 ngày trước", status: "interview" },
  { id: "a3", name: "Phạm Quang", position: "Kỹ sư kết cấu", exp: "3 năm", date: "2 ngày trước", status: "new" },
  { id: "a4", name: "Nguyễn Hoa", position: "QA/QC", exp: "4 năm", date: "3 ngày trước", status: "interview" },
  { id: "a5", name: "Đỗ Tuấn", position: "Chỉ huy trưởng", exp: "10 năm", date: "5 ngày trước", status: "rejected" },
];

const statusStyle = {
  new: { bg: "hsl(var(--admin-info-soft))", color: "hsl(var(--admin-info))", label: "Mới" },
  interview: { bg: "hsl(var(--admin-warning-soft))", color: "hsl(var(--admin-warning))", label: "Phỏng vấn" },
  rejected: { bg: "hsl(var(--admin-danger-soft))", color: "hsl(var(--admin-danger))", label: "Từ chối" },
};

const AdminRecruitment = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [apps, setApps] = useState(initialApps);

  const update = (id: string, status: App["status"]) => {
    setApps((a) => a.map((x) => (x.id === id ? { ...x, status } : x)));
    toast({ title: "Cập nhật", description: `Trạng thái: ${statusStyle[status].label}` });
  };

  return (
    <AdminLayout>
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-7">
        <div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("recruit.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {positions.length} {t("recruit.positions")} · {apps.length} {t("recruit.applications")}
          </p>
        </div>
        <button
          onClick={() => toast({ title: t("recruit.postPosition"), description: "Demo." })}
          className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm"
        >
          <Plus className="w-4 h-4" /> {t("recruit.postPosition")}
        </button>
      </div>

      {/* Positions */}
      <h2 className="font-display text-xl font-extrabold mb-4">{t("recruit.positions")}</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5 mb-10">
        {positions.map((p, i) => (
          <div key={i} className="admin-card p-6">
            <div className="flex items-start justify-between mb-4">
              <div
                className="w-11 h-11 rounded-2xl flex items-center justify-center text-white"
                style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))" }}
              >
                <Briefcase className="w-5 h-5" strokeWidth={1.75} />
              </div>
              <span
                className="admin-chip"
                style={{ background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }}
              >
                {t("recruit.hiring")}
              </span>
            </div>
            <h3 className="font-display text-lg font-extrabold mb-1">{p.title}</h3>
            <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{p.dept}</p>
            <div className="flex items-center justify-between mt-5 pt-4 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
              <span className="text-xs flex items-center gap-1.5" style={{ color: "hsl(var(--admin-muted))" }}>
                <MapPin className="w-3 h-3" /> {p.location}
              </span>
              <span className="text-xs font-bold" style={{ color: "hsl(var(--admin-primary))" }}>
                {p.apps} ứng viên
              </span>
            </div>
          </div>
        ))}
      </div>

      {/* Applications */}
      <h2 className="font-display text-xl font-extrabold mb-4">{t("recruit.applications")}</h2>
      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("recruit.candidate")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("recruit.position")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("recruit.experience")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("recruit.appliedOn")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("common.status")}</th>
                <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider text-right">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {apps.map((a) => {
                const st = statusStyle[a.status];
                return (
                  <tr key={a.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div
                          className="w-9 h-9 rounded-full text-white flex items-center justify-center text-sm font-bold"
                          style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))" }}
                        >
                          {a.name[0]}
                        </div>
                        <p className="font-semibold">{a.name}</p>
                      </div>
                    </td>
                    <td className="px-6 py-4">{a.position}</td>
                    <td className="px-6 py-4" style={{ color: "hsl(var(--admin-muted))" }}>{a.exp}</td>
                    <td className="px-6 py-4" style={{ color: "hsl(var(--admin-muted))" }}>{a.date}</td>
                    <td className="px-6 py-4">
                      <span className="admin-chip" style={{ background: st.bg, color: st.color }}>
                        {st.label}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-right">
                      <div className="inline-flex gap-1">
                        <button
                          onClick={() => toast({ title: "Xem CV (demo)" })}
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center"
                          style={{ color: "hsl(var(--admin-info))" }}
                        >
                          <Eye className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => update(a.id, "interview")}
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center"
                          style={{ color: "hsl(var(--admin-success))" }}
                        >
                          <CheckCircle2 className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => update(a.id, "rejected")}
                          className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center"
                          style={{ color: "hsl(var(--admin-danger))" }}
                        >
                          <X className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </AdminLayout>
  );
};

export default AdminRecruitment;
