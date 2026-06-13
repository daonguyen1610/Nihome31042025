import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, Briefcase, MapPin, Eye, CheckCircle2, X, Pencil, Trash2, ChevronDown, Users, FileDown, Search } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi } from "@/services/adminApi";
import type { JobPositionResponse, JobApplicationResponse, EmploymentTypeResponse } from "@/services/adminApi";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { matchesSearch } from "@/lib/utils";

const EMP_TYPE_KEY_MAP: Record<string, string> = {
  "full-time": "rec.empType.fullTime",
  "part-time": "rec.empType.partTime",
  "intern": "rec.empType.intern",
};

function getEmploymentTypeLabel(code: string, fallback: string, t: (key: string) => string) {
  const key = EMP_TYPE_KEY_MAP[code];
  if (key) return t(key);
  return fallback || code;
}

function getExperienceLabel(code: string, t: (key: string) => string) {
  switch (code) {
    case "student": return t("rec.exp.student");
    case "junior": return t("rec.exp.junior");
    case "mid": return t("rec.exp.mid");
    case "senior": return t("rec.exp.senior");
    default: return code;
  }
}

const APP_STATUS_STYLE: Record<string, { bg: string; color: string }> = {
  new: { bg: "hsl(var(--admin-info-soft))", color: "hsl(var(--admin-info))" },
  interview: { bg: "hsl(var(--admin-warning-soft))", color: "hsl(var(--admin-warning))" },
  hired: { bg: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" },
  rejected: { bg: "hsl(var(--admin-danger-soft))", color: "hsl(var(--admin-danger))" },
};

function getErrorMessage(error: unknown) {
  if (error && typeof error === "object" && "response" in error) {
    const r = (error as { response?: { data?: { message?: string } } }).response;
    if (r?.data?.message) return r.data.message;
  }
  return undefined;
}

const AdminRecruitment = () => {
  const { t } = useI18n();
  const { toast } = useToast();

  const [positions, setPositions] = useState<JobPositionResponse[]>([]);
  const [applications, setApplications] = useState<JobApplicationResponse[]>([]);
  const [loadingPositions, setLoadingPositions] = useState(true);
  const [loadingApps, setLoadingApps] = useState(true);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [employmentTypes, setEmploymentTypes] = useState<EmploymentTypeResponse[]>([]);
  const [filterPosition, setFilterPosition] = useState<number | "">("");
  const [filterStatus, setFilterStatus] = useState<string>("");
  const [positionQuery, setPositionQuery] = useState("");
  const [appQuery, setAppQuery] = useState("");

  const loadPositions = useCallback(async () => {
    setLoadingPositions(true);
    try {
      const res = await adminApi.getJobPositions(true);
      setPositions(res.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setLoadingPositions(false);
    }
  }, [t, toast]);

  const loadApplications = useCallback(async () => {
    setLoadingApps(true);
    try {
      const res = await adminApi.getJobApplications(
        filterPosition !== "" ? filterPosition : undefined,
        filterStatus || undefined,
      );
      setApplications(res.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setLoadingApps(false);
    }
  }, [t, toast, filterPosition, filterStatus]);

  useEffect(() => { loadPositions(); }, [loadPositions]);
  useEffect(() => { loadApplications(); }, [loadApplications]);
  useEffect(() => {
    adminApi.getEmploymentTypes(true)
      .then((res) => setEmploymentTypes(res.data))
      .catch(() => setEmploymentTypes([]));
  }, []);

  const employmentTypeMap = useMemo(() => {
    return new Map(employmentTypes.map((item) => [item.code, item.name]));
  }, [employmentTypes]);

  const filteredPositions = useMemo(() => {
    if (!positionQuery.trim()) return positions;
    return positions.filter((p) =>
      matchesSearch(p.title, positionQuery) ||
      matchesSearch(p.department, positionQuery) ||
      matchesSearch(p.location, positionQuery),
    );
  }, [positions, positionQuery]);

  const filteredApplications = useMemo(() => {
    if (!appQuery.trim()) return applications;
    return applications.filter((a) =>
      matchesSearch(a.candidateName, appQuery) ||
      matchesSearch(a.email, appQuery) ||
      matchesSearch(a.phone, appQuery) ||
      matchesSearch(a.positionTitle, appQuery),
    );
  }, [applications, appQuery]);

  const deletePosition = async (id: number, title: string) => {
    if (!confirm(t("recruit.deletePositionConfirm").replace("{title}", title))) return;
    setDeletingId(id);
    try {
      await adminApi.deleteJobPosition(id);
      toast({ title: t("recruit.deleted"), description: title });
      loadPositions();
      loadApplications();
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    } finally {
      setDeletingId(null);
    }
  };

  const updateAppStatus = async (id: number, status: string) => {
    try {
      await adminApi.updateApplicationStatus(id, status);
      setApplications((prev) => prev.map((a) => (a.id === id ? { ...a, status } : a)));
      toast({ title: t("recruit.updated"), description: t(`recruit.status.${status}` as Parameters<typeof t>[0]) });
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    }
  };

  const deleteApp = async (id: number) => {
    if (!confirm(t("recruit.deleteAppConfirm"))) return;
    try {
      await adminApi.deleteApplication(id);
      setApplications((prev) => prev.filter((a) => a.id !== id));
      toast({ title: t("recruit.deleted") });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  const activeCount = useMemo(() => positions.filter((p) => p.isActive).length, [positions]);

  const handleExportApplications = () => {
    downloadCsv({
      filename: createCsvFilename("admin-recruitment-applications"),
      columns: [
        { header: "ID", value: "id" },
        { header: t("recruit.candidate"), value: "candidateName" },
        { header: "Email", value: "email" },
        { header: "Phone", value: (row) => row.phone ?? "" },
        { header: t("recruit.position"), value: "positionTitle" },
        { header: t("common.status"), value: (row) => t(`recruit.status.${row.status}` as Parameters<typeof t>[0]) },
        {
          header: t("recruit.experience"),
          value: (row) => (row.experienceYears != null ? String(row.experienceYears) : ""),
        },
        { header: t("recruit.appliedOn"), value: (row) => new Date(row.appliedAt).toLocaleString("vi-VN") },
        { header: "CV URL", value: (row) => row.cvUrl ?? "" },
        { header: "Cover letter", value: (row) => row.coverLetter ?? "" },
      ],
      rows: applications,
    });
  };

  return (
    <AdminLayout>
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-7">
        <div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("recruit.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {activeCount} {t("recruit.positions")} {t("recruit.positionsOpen")} · {applications.length} {t("recruit.applications")}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Link
            to="/admin/recruitment/employment-types"
            className="inline-flex items-center gap-2 px-4 py-2.5 text-sm rounded-xl border font-semibold hover:bg-muted transition"
            style={{ borderColor: "hsl(var(--admin-border))" }}
          >
            {t("nav.employmentTypes")}
          </Link>
          <Link
            to="/admin/recruitment/new"
            className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm"
          >
            <Plus className="w-4 h-4" /> {t("recruit.postPosition")}
          </Link>
        </div>
      </div>

      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-3 mb-4">
        <h2 className="font-display text-xl font-extrabold">{t("recruit.positions")}</h2>
        <div
          className="flex items-center gap-2 rounded-full px-4 py-2 border w-full lg:w-80"
          style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
        >
          <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
          <input
            value={positionQuery}
            onChange={(e) => setPositionQuery(e.target.value)}
            placeholder={t("recruit.searchPositionPlaceholder")}
            className="bg-transparent outline-none text-sm flex-1 placeholder:opacity-60"
          />
        </div>
      </div>
      {loadingPositions ? (
        <div className="flex justify-center py-10">
          <div className="w-7 h-7 border-4 rounded-full animate-spin" style={{ borderColor: "hsl(var(--admin-primary))", borderTopColor: "transparent" }} />
        </div>
      ) : filteredPositions.length === 0 ? (
        <div className="admin-card p-10 text-center mb-10" style={{ color: "hsl(var(--admin-muted))" }}>
          {positions.length === 0 ? t("recruit.noPositions") : t("common.noData")}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5 mb-10">
          {filteredPositions.map((p) => (
            <div key={p.id} className="admin-card p-6 flex flex-col">
              <div className="flex items-start justify-between mb-4">
                <div
                  className="w-11 h-11 rounded-2xl flex items-center justify-center text-white"
                  style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))" }}
                >
                  <Briefcase className="w-5 h-5" strokeWidth={1.75} />
                </div>
                <span
                  className="admin-chip"
                  style={
                    p.isActive
                      ? { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }
                      : { background: "hsl(var(--admin-danger-soft))", color: "hsl(var(--admin-danger))" }
                  }
                >
                  {p.isActive ? t("recruit.hiring") : t("recruit.closed")}
                </span>
              </div>
              <h3 className="font-display text-lg font-extrabold mb-0.5">{p.title}</h3>
              <p className="text-xs mb-2" style={{ color: "hsl(var(--admin-muted))" }}>{p.department}</p>
              <div className="flex flex-wrap gap-2 mb-4">
                <span className="admin-chip text-xs">
                  {getEmploymentTypeLabel(p.employmentType, employmentTypeMap.get(p.employmentType) ?? p.employmentType, t)}
                </span>
                <span className="admin-chip text-xs">{getExperienceLabel(p.experienceLevel, t)}</span>
              </div>
              <div className="flex items-center justify-between mt-auto pt-4 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                <span className="text-xs flex items-center gap-1.5" style={{ color: "hsl(var(--admin-muted))" }}>
                  <MapPin className="w-3 h-3" /> {p.location}
                </span>
                <span className="text-xs font-bold flex items-center gap-1" style={{ color: "hsl(var(--admin-primary))" }}>
                  <Users className="w-3 h-3" /> {p.applicationCount} {t("recruit.candidateCount")}
                </span>
              </div>
              <div className="flex gap-2 mt-3">
                <Link
                  to={`/admin/recruitment/${p.id}/edit`}
                  className="flex-1 inline-flex items-center justify-center gap-1.5 py-1.5 text-xs rounded-lg border hover:bg-muted transition"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <Pencil className="w-3 h-3" /> {t("recruit.edit")}
                </Link>
                <button
                  onClick={() => deletePosition(p.id, p.title)}
                  disabled={deletingId === p.id}
                  className="flex-1 inline-flex items-center justify-center gap-1.5 py-1.5 text-xs rounded-lg border text-destructive hover:bg-destructive/10 transition disabled:opacity-50"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <Trash2 className="w-3 h-3" /> {t("recruit.delete")}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-4">
        <h2 className="font-display text-xl font-extrabold">{t("recruit.applications")}</h2>
        <div className="flex flex-wrap items-center gap-2">
          <div
            className="flex items-center gap-2 rounded-full px-3 py-1.5 border"
            style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
          >
            <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
            <input
              value={appQuery}
              onChange={(e) => setAppQuery(e.target.value)}
              placeholder={t("recruit.searchApplicationPlaceholder")}
              className="bg-transparent outline-none text-sm w-44 placeholder:opacity-60"
            />
          </div>
          <AdminExportButton onClick={handleExportApplications} disabled={loadingApps || applications.length === 0} />
          <div className="relative">
            <select
              className="admin-input pr-8 text-sm appearance-none"
              value={filterPosition}
              onChange={(e) => setFilterPosition(e.target.value === "" ? "" : Number(e.target.value))}
            >
              <option value="">{t("recruit.allPositions")}</option>
              {positions.map((p) => (
                <option key={p.id} value={p.id}>{p.title}</option>
              ))}
            </select>
            <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 pointer-events-none" style={{ color: "hsl(var(--admin-muted))" }} />
          </div>
          <div className="relative">
            <select
              className="admin-input pr-8 text-sm appearance-none"
              value={filterStatus}
              onChange={(e) => setFilterStatus(e.target.value)}
            >
              <option value="">{t("recruit.allStatuses")}</option>
              <option value="new">{t("recruit.status.new")}</option>
              <option value="interview">{t("recruit.status.interview")}</option>
              <option value="hired">{t("recruit.status.hired")}</option>
              <option value="rejected">{t("recruit.status.rejected")}</option>
            </select>
            <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 pointer-events-none" style={{ color: "hsl(var(--admin-muted))" }} />
          </div>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        {loadingApps ? (
          <div className="flex justify-center py-10">
            <div className="w-7 h-7 border-4 rounded-full animate-spin" style={{ borderColor: "hsl(var(--admin-primary))", borderTopColor: "transparent" }} />
          </div>
        ) : filteredApplications.length === 0 ? (
          <div className="p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
            {applications.length === 0 ? t("recruit.noApplications") : t("common.noData")}
          </div>
        ) : (
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
                {filteredApplications.map((a) => {
                  const st = APP_STATUS_STYLE[a.status] ?? APP_STATUS_STYLE.new;
                  return (
                    <tr key={a.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div
                            className="w-9 h-9 rounded-full text-white flex items-center justify-center text-sm font-bold shrink-0"
                            style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))" }}
                          >
                            {a.candidateName[0]?.toUpperCase()}
                          </div>
                          <div>
                            <p className="font-semibold">{a.candidateName}</p>
                            <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{a.email}</p>
                            {a.phone && <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{a.phone}</p>}
                          </div>
                        </div>
                      </td>
                      <td className="px-6 py-4 font-medium">{a.positionTitle}</td>
                      <td className="px-6 py-4" style={{ color: "hsl(var(--admin-muted))" }}>
                        {a.experienceYears != null ? `${a.experienceYears} ${t("recruit.expYears")}` : "—"}
                      </td>
                      <td className="px-6 py-4" style={{ color: "hsl(var(--admin-muted))" }}>
                        {new Date(a.appliedAt).toLocaleDateString("vi-VN")}
                      </td>
                      <td className="px-6 py-4">
                        <div className="relative inline-block">
                          <select
                            className="admin-chip pr-6 appearance-none cursor-pointer text-xs"
                            value={a.status}
                            onChange={(e) => updateAppStatus(a.id, e.target.value)}
                            style={{ background: st.bg, color: st.color }}
                          >
                            <option value="new">{t("recruit.status.new")}</option>
                            <option value="interview">{t("recruit.status.interview")}</option>
                            <option value="hired">{t("recruit.status.hired")}</option>
                            <option value="rejected">{t("recruit.status.rejected")}</option>
                          </select>
                          <ChevronDown className="absolute right-1 top-1/2 -translate-y-1/2 w-3 h-3 pointer-events-none" style={{ color: st.color }} />
                        </div>
                      </td>
                      <td className="px-6 py-4 text-right">
                        <div className="inline-flex gap-1">
                          {a.coverLetter && (
                            <button
                              onClick={() => alert(a.coverLetter)}
                              title={t("recruit.candidateCount")}
                              className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center"
                              style={{ color: "hsl(var(--admin-info))" }}
                            >
                              <Eye className="w-4 h-4" />
                            </button>
                          )}
                          {a.cvUrl && (
                            <a
                              href={`${import.meta.env.VITE_API_URL ?? ""}${a.cvUrl}`}
                              target="_blank"
                              rel="noopener noreferrer"
                              title="CV"
                              className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center"
                              style={{ color: "hsl(var(--admin-primary))" }}
                            >
                              <FileDown className="w-4 h-4" />
                            </a>
                          )}
                          <button
                            onClick={() => updateAppStatus(a.id, "interview")}
                            title={t("recruit.status.interview")}
                            className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center"
                            style={{ color: "hsl(var(--admin-success))" }}
                          >
                            <CheckCircle2 className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => updateAppStatus(a.id, "rejected")}
                            title={t("recruit.status.rejected")}
                            className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center"
                            style={{ color: "hsl(var(--admin-warning))" }}
                          >
                            <X className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => deleteApp(a.id)}
                            title={t("recruit.delete")}
                            className="w-8 h-8 rounded-lg hover:bg-muted flex items-center justify-center"
                            style={{ color: "hsl(var(--admin-danger))" }}
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </AdminLayout>
  );
};

export default AdminRecruitment;
