import { useState } from "react";
import { Pencil, Play, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

type Task = {
  id: string;
  name: string;
  seconds: number;
  enabled: boolean;
  stopOnError: boolean;
  lastStart: string;
  lastEnd: string;
  lastSuccess: string;
};

const KEY = "nicon_admin_schedule_tasks_v1";

const seed: Task[] = [
  { id: "1", name: "Send emails", seconds: 60, enabled: true, stopOnError: false, lastStart: "2026-04-24 17:30:00", lastEnd: "2026-04-24 17:30:01", lastSuccess: "2026-04-24 17:30:01" },
  { id: "2", name: "Keep alive", seconds: 300, enabled: true, stopOnError: false, lastStart: "2026-04-24 17:25:00", lastEnd: "2026-04-24 17:25:00", lastSuccess: "2026-04-24 17:25:00" },
  { id: "3", name: "Delete guests", seconds: 600, enabled: false, stopOnError: false, lastStart: "", lastEnd: "", lastSuccess: "" },
  { id: "4", name: "Clear cache", seconds: 3600, enabled: true, stopOnError: true, lastStart: "2026-04-24 17:00:00", lastEnd: "2026-04-24 17:00:02", lastSuccess: "2026-04-24 17:00:02" },
  { id: "5", name: "Update currency exchange rates", seconds: 14400, enabled: true, stopOnError: false, lastStart: "2026-04-24 12:00:00", lastEnd: "2026-04-24 12:00:01", lastSuccess: "2026-04-24 12:00:01" },
];

const load = (): Task[] => {
  try { const raw = localStorage.getItem(KEY); return raw ? JSON.parse(raw) : seed; } catch { return seed; }
};
const save = (items: Task[]) => localStorage.setItem(KEY, JSON.stringify(items));

const ScheduleTasks = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [tasks, setTasks] = useState<Task[]>(load);
  const [editing, setEditing] = useState<Task | null>(null);
  const [draft, setDraft] = useState<Task | null>(null);

  const persist = (next: Task[]) => { setTasks(next); save(next); };
  const submit = () => {
    if (!draft) return;
    persist(tasks.map((tk) => (tk.id === draft.id ? draft : tk)));
    toast({ title: t("form.updated") });
    setEditing(null); setDraft(null);
  };
  const runNow = (id: string) => {
    const now = new Date().toISOString().slice(0, 19).replace("T", " ");
    persist(tasks.map((tk) => tk.id === id ? { ...tk, lastStart: now, lastEnd: now, lastSuccess: now } : tk));
    toast({ title: t("sys.tasks.runOk") });
  };

  return (
    <AdminLayout>
      <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight mb-3">{t("sys.tasks.title")}</h1>
      <div className="admin-card p-5 mb-5">
        <p className="text-sm font-semibold mb-1">{t("sys.tasks.note1")}</p>
        <p className="text-sm font-bold">{t("sys.tasks.note2")}</p>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[1000px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-4 py-3 font-bold">{t("sys.tasks.name")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.tasks.seconds")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.tasks.enabled")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.tasks.stopOnError")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.tasks.lastStart")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.tasks.lastEnd")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.tasks.lastSuccess")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.tasks.runNow")}</th>
                <th className="px-4 py-3 font-bold">{t("common.edit")}</th>
              </tr>
            </thead>
            <tbody>
              {tasks.map((tk) => (
                <tr key={tk.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-4 py-3 font-semibold">{tk.name}</td>
                  <td className="px-4 py-3">{tk.seconds}</td>
                  <td className="px-4 py-3">{tk.enabled ? <Check className="w-4 h-4" style={{ color: "hsl(142 71% 40%)" }} /> : <X className="w-4 h-4" style={{ color: "hsl(var(--admin-danger))" }} />}</td>
                  <td className="px-4 py-3">{tk.stopOnError ? <Check className="w-4 h-4" style={{ color: "hsl(142 71% 40%)" }} /> : <X className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />}</td>
                  <td className="px-4 py-3 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{tk.lastStart || "—"}</td>
                  <td className="px-4 py-3 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{tk.lastEnd || "—"}</td>
                  <td className="px-4 py-3 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{tk.lastSuccess || "—"}</td>
                  <td className="px-4 py-3">
                    <button onClick={() => runNow(tk.id)} className="px-2 py-1 rounded-md text-xs font-bold text-white" style={{ background: "hsl(var(--admin-primary))" }}>
                      <Play className="w-3.5 h-3.5" />
                    </button>
                  </td>
                  <td className="px-4 py-3">
                    <button onClick={() => { setEditing(tk); setDraft(tk); }} className="px-2 py-1 rounded-md text-xs font-bold bg-muted">
                      <Pencil className="w-3.5 h-3.5" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {editing && draft && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" onClick={() => { setEditing(null); setDraft(null); }}>
          <div className="bg-white rounded-2xl w-full max-w-md p-6" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-display text-xl font-extrabold mb-4">{t("common.edit")}: {editing.name}</h3>
            <div className="space-y-3">
              <label className="block">
                <span className="text-xs font-bold block mb-1" style={{ color: "hsl(var(--admin-muted))" }}>{t("sys.tasks.name")}</span>
                <input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} className="admin-input w-full" />
              </label>
              <label className="block">
                <span className="text-xs font-bold block mb-1" style={{ color: "hsl(var(--admin-muted))" }}>{t("sys.tasks.seconds")}</span>
                <input type="number" value={draft.seconds} onChange={(e) => setDraft({ ...draft, seconds: +e.target.value })} className="admin-input w-full" />
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={draft.enabled} onChange={(e) => setDraft({ ...draft, enabled: e.target.checked })} />
                <span className="text-sm font-semibold">{t("sys.tasks.enabled")}</span>
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" checked={draft.stopOnError} onChange={(e) => setDraft({ ...draft, stopOnError: e.target.checked })} />
                <span className="text-sm font-semibold">{t("sys.tasks.stopOnError")}</span>
              </label>
            </div>
            <div className="flex justify-end gap-2 mt-5">
              <button onClick={() => { setEditing(null); setDraft(null); }} className="px-4 py-2 rounded-lg text-sm font-bold bg-muted">{t("common.cancel")}</button>
              <button onClick={submit} className="admin-btn-primary px-4 py-2 text-sm">{t("proc.save")}</button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default ScheduleTasks;
