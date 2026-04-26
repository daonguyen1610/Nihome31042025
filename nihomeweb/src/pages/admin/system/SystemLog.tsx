import { useMemo, useState } from "react";
import { Trash2, Search, Eye } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

type Level = "Information" | "Warning" | "Error" | "Fatal" | "Debug";
type Entry = {
  id: string;
  level: Level;
  shortMessage: string;
  fullMessage: string;
  ipAddress: string;
  customer: string;
  pageUrl: string;
  createdOn: string;
};

const KEY = "nicon_admin_system_log_v1";

const seed: Entry[] = [
  { id: "1", level: "Error", shortMessage: "Object reference not set to an instance of an object.", fullMessage: "System.NullReferenceException at Nicon.Web.Controllers.HomeController.Index()", ipAddress: "115.78.15.232", customer: "tringuyen@nicon.vn", pageUrl: "/admin", createdOn: "2026-04-24 08:12:01" },
  { id: "2", level: "Warning", shortMessage: "Slow query detected (>2s)", fullMessage: "Query: SELECT * FROM Products took 2.341s", ipAddress: "127.0.0.1", customer: "System", pageUrl: "/admin/posts", createdOn: "2026-04-24 07:45:22" },
  { id: "3", level: "Information", shortMessage: "User signed in", fullMessage: "User tringuyen@nicon.vn signed in successfully.", ipAddress: "115.78.15.232", customer: "tringuyen@nicon.vn", pageUrl: "/login", createdOn: "2026-04-24 07:30:11" },
  { id: "4", level: "Error", shortMessage: "SMTP connection failed", fullMessage: "Could not connect to smtp.nicon.vn:587 — timeout.", ipAddress: "127.0.0.1", customer: "System", pageUrl: "-", createdOn: "2026-04-23 22:14:05" },
  { id: "5", level: "Information", shortMessage: "Scheduled task completed", fullMessage: "Task SendEmails completed in 1.2s", ipAddress: "127.0.0.1", customer: "System", pageUrl: "-", createdOn: "2026-04-23 21:00:00" },
  { id: "6", level: "Fatal", shortMessage: "Application restart required", fullMessage: "Out of memory — heap exhausted.", ipAddress: "127.0.0.1", customer: "System", pageUrl: "-", createdOn: "2026-04-22 03:11:42" },
  { id: "7", level: "Debug", shortMessage: "Cache rebuilt for Categories", fullMessage: "Rebuilt 14 category records in 0.32s", ipAddress: "127.0.0.1", customer: "System", pageUrl: "-", createdOn: "2026-04-22 02:01:00" },
  { id: "8", level: "Warning", shortMessage: "Login attempt failed", fullMessage: "Invalid credentials for user admin@example.com", ipAddress: "203.205.34.12", customer: "Guest", pageUrl: "/login", createdOn: "2026-04-21 19:22:18" },
];

const load = (): Entry[] => {
  try { const raw = localStorage.getItem(KEY); return raw ? JSON.parse(raw) : seed; } catch { return seed; }
};
const save = (items: Entry[]) => localStorage.setItem(KEY, JSON.stringify(items));

const levelColor: Record<Level, string> = {
  Information: "hsl(210 80% 45%)",
  Warning: "hsl(38 92% 50%)",
  Error: "hsl(var(--admin-danger))",
  Fatal: "hsl(0 80% 35%)",
  Debug: "hsl(var(--admin-muted))",
};

const SystemLog = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [items, setItems] = useState<Entry[]>(load);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [level, setLevel] = useState<"All" | Level>("All");
  const [q, setQ] = useState("");
  const [view, setView] = useState<Entry | null>(null);

  const filtered = useMemo(() => items.filter((i) => {
    if (level !== "All" && i.level !== level) return false;
    const d = i.createdOn.slice(0, 10);
    if (from && d < from) return false;
    if (to && d > to) return false;
    if (q && !i.shortMessage.toLowerCase().includes(q.toLowerCase())) return false;
    return true;
  }), [items, level, from, to, q]);

  const remove = (id: string) => {
    const next = items.filter((i) => i.id !== id);
    setItems(next); save(next);
    toast({ title: t("form.deleted") });
  };
  const clearAll = () => {
    if (!confirm(t("form.confirmDelete"))) return;
    setItems([]); save([]);
    toast({ title: t("form.deleted") });
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-3 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("sys.log.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} / {items.length}
          </p>
        </div>
        <button onClick={clearAll} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2.5 rounded-xl text-white" style={{ background: "hsl(var(--admin-danger))" }}>
          <Trash2 className="w-4 h-4" /> {t("sys.log.clear")}
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("log.from")}</span>
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("log.to")}</span>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("sys.log.level")}</span>
            <select value={level} onChange={(e) => setLevel(e.target.value as "All" | Level)} className="admin-input w-full">
              {(["All", "Information", "Warning", "Error", "Fatal", "Debug"] as const).map((l) => <option key={l}>{l}</option>)}
            </select>
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("sys.log.message")}</span>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
              <input value={q} onChange={(e) => setQ(e.target.value)} className="admin-input w-full pl-9" />
            </div>
          </label>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[900px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-5 py-3 font-semibold">{t("sys.log.level")}</th>
                <th className="px-5 py-3 font-semibold">{t("sys.log.shortMessage")}</th>
                <th className="px-5 py-3 font-semibold">{t("log.ip")}</th>
                <th className="px-5 py-3 font-semibold">{t("log.customer")}</th>
                <th className="px-5 py-3 font-semibold">{t("log.createdOn")}</th>
                <th className="px-5 py-3 font-semibold text-right">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((l) => (
                <tr key={l.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-5 py-3">
                    <span className="inline-flex items-center text-xs font-bold px-2 py-1 rounded-md text-white" style={{ background: levelColor[l.level] }}>
                      {l.level}
                    </span>
                  </td>
                  <td className="px-5 py-3 text-xs">{l.shortMessage}</td>
                  <td className="px-5 py-3 font-mono text-xs">{l.ipAddress}</td>
                  <td className="px-5 py-3 text-xs" style={{ color: "hsl(var(--admin-primary))" }}>{l.customer}</td>
                  <td className="px-5 py-3 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{l.createdOn}</td>
                  <td className="px-5 py-3 text-right">
                    <div className="inline-flex gap-1">
                      <button onClick={() => setView(l)} className="px-2 py-1 rounded-md text-xs font-bold bg-muted">
                        <Eye className="w-3.5 h-3.5" />
                      </button>
                      <button onClick={() => remove(l.id)} className="px-2 py-1 rounded-md text-xs font-bold text-white" style={{ background: "hsl(var(--admin-danger))" }}>
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {filtered.length === 0 && (
                <tr><td colSpan={6} className="px-5 py-10 text-center text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{t("proc.empty")}</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {view && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" onClick={() => setView(null)}>
          <div className="bg-white rounded-2xl w-full max-w-2xl p-6 max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-display text-xl font-extrabold mb-4">{view.shortMessage}</h3>
            <div className="space-y-3 text-sm">
              <p><b>{t("sys.log.level")}:</b> <span style={{ color: levelColor[view.level] }}>{view.level}</span></p>
              <p><b>{t("log.ip")}:</b> <span className="font-mono">{view.ipAddress}</span></p>
              <p><b>{t("log.customer")}:</b> {view.customer}</p>
              <p><b>{t("sys.log.pageUrl")}:</b> <span className="font-mono text-xs">{view.pageUrl}</span></p>
              <p><b>{t("log.createdOn")}:</b> {view.createdOn}</p>
              <div>
                <b>{t("sys.log.fullMessage")}:</b>
                <pre className="mt-2 p-3 rounded-lg text-xs whitespace-pre-wrap" style={{ background: "hsl(var(--admin-bg))" }}>{view.fullMessage}</pre>
              </div>
            </div>
            <div className="flex justify-end mt-5">
              <button onClick={() => setView(null)} className="px-4 py-2 rounded-lg text-sm font-bold bg-muted">{t("common.cancel")}</button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default SystemLog;
