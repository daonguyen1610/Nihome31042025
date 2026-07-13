import { useState } from "react";
import { Pencil, Play, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

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
      <div className="space-y-4 p-4 sm:p-6">
        <header>
          <h1 className="text-2xl font-semibold">{t("sys.tasks.title")}</h1>
        </header>

        <div className="rounded-lg border bg-card p-4 text-sm">
          <p className="font-medium">{t("sys.tasks.note1")}</p>
          <p className="mt-1 font-semibold">{t("sys.tasks.note2")}</p>
        </div>

        <div className="overflow-x-auto rounded-lg border">
          <table className="min-w-[1000px] w-full divide-y text-sm">
            <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-3 py-3 text-left font-medium">{t("sys.tasks.name")}</th>
                <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("sys.tasks.seconds")}</th>
                <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("sys.tasks.enabled")}</th>
                <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("sys.tasks.stopOnError")}</th>
                <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("sys.tasks.lastStart")}</th>
                <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("sys.tasks.lastEnd")}</th>
                <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("sys.tasks.lastSuccess")}</th>
                <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {tasks.map((tk) => (
                <tr key={tk.id} className="hover:bg-muted/40 transition">
                  <td className="px-3 py-3 font-medium">{tk.name}</td>
                  <td className="whitespace-nowrap px-3 py-3">{tk.seconds}</td>
                  <td className="px-3 py-3">
                    {tk.enabled
                      ? <Check className="h-4 w-4 text-emerald-600" />
                      : <X className="h-4 w-4 text-muted-foreground" />}
                  </td>
                  <td className="px-3 py-3">
                    {tk.stopOnError
                      ? <Check className="h-4 w-4 text-amber-600" />
                      : <X className="h-4 w-4 text-muted-foreground" />}
                  </td>
                  <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">{tk.lastStart || "—"}</td>
                  <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">{tk.lastEnd || "—"}</td>
                  <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">{tk.lastSuccess || "—"}</td>
                  <td className="whitespace-nowrap px-3 py-3 text-right">
                    <div className="inline-flex items-center gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => runNow(tk.id)}
                        title={t("sys.tasks.runNow")}
                        aria-label={t("sys.tasks.runNow")}
                      >
                        <Play className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => { setEditing(tk); setDraft(tk); }}
                        title={t("common.edit")}
                        aria-label={t("common.edit")}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <Dialog
          open={!!editing}
          onOpenChange={(open) => {
            if (!open) {
              setEditing(null);
              setDraft(null);
            }
          }}
        >
          <DialogContent className="sm:max-w-md">
            {editing && draft && (
              <>
                <DialogHeader>
                  <DialogTitle>{t("common.edit")}: {editing.name}</DialogTitle>
                </DialogHeader>
                <div className="space-y-3">
                  <div className="space-y-1.5">
                    <Label className="text-xs" htmlFor="task-name">{t("sys.tasks.name")}</Label>
                    <Input
                      id="task-name"
                      value={draft.name}
                      onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <Label className="text-xs" htmlFor="task-seconds">{t("sys.tasks.seconds")}</Label>
                    <Input
                      id="task-seconds"
                      type="number"
                      value={draft.seconds}
                      onChange={(e) => setDraft({ ...draft, seconds: +e.target.value })}
                    />
                  </div>
                  <label className="flex items-center gap-2">
                    <Checkbox
                      checked={draft.enabled}
                      onCheckedChange={(v) => setDraft({ ...draft, enabled: v === true })}
                    />
                    <span className="text-sm font-medium">{t("sys.tasks.enabled")}</span>
                  </label>
                  <label className="flex items-center gap-2">
                    <Checkbox
                      checked={draft.stopOnError}
                      onCheckedChange={(v) => setDraft({ ...draft, stopOnError: v === true })}
                    />
                    <span className="text-sm font-medium">{t("sys.tasks.stopOnError")}</span>
                  </label>
                </div>
                <DialogFooter>
                  <Button
                    variant="outline"
                    onClick={() => { setEditing(null); setDraft(null); }}
                  >
                    {t("common.cancel")}
                  </Button>
                  <Button onClick={submit}>{t("proc.save")}</Button>
                </DialogFooter>
              </>
            )}
          </DialogContent>
        </Dialog>
      </div>
    </AdminLayout>
  );
};

export default ScheduleTasks;
