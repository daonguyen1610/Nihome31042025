import { useMemo, useState } from "react";
import { Search, Trash2, Pencil } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

type Priority = "Low" | "Normal" | "High";
type Email = {
  id: number;
  subject: string;
  from: string;
  to: string;
  createdOn: string;
  plannedDate: string;
  sentOn: string;
  priority: Priority;
};

const KEY = "nicon_admin_msg_queue_v1";

const buildSeed = (): Email[] => {
  const samples = [
    "andrejvikavika@gmail.com",
    "glenna.montano@yahoo.com",
    "xr.oomer1st@gmail.com",
    "markus.stilwell@outlook.com",
    "courts.jada@yahoo.com",
    "fdtitnff@bekommenmail.com",
    "turney.dorothy@msn.com",
    "roxie.humphreys@googlemail.com",
    "manuela.conklin@googlemail.com",
    "alonzo.madeline@msn.com",
    "zekisuquc419@gmail.com",
    "reklam@dzenvpn.com",
    "leonski.adam@msn.com",
  ];
  return Array.from({ length: 60 }, (_, i) => ({
    id: 59251 - i,
    subject: "Nicon. Contact us",
    from: samples[i % samples.length],
    to: "test@mail.com",
    createdOn: `2026-04-${String(24 - Math.floor(i / 5)).padStart(2, "0")} ${String(15 - (i % 12)).padStart(2, "0")}:${String((i * 7) % 60).padStart(2, "0")}:00`,
    plannedDate: "",
    sentOn: i % 5 === 0 ? `2026-04-${String(24 - Math.floor(i / 5)).padStart(2, "0")} 16:00:00` : "",
    priority: "High" as const,
  }));
};

const load = (): Email[] => {
  try { const raw = localStorage.getItem(KEY); return raw ? JSON.parse(raw) : buildSeed(); } catch { return buildSeed(); }
};
const save = (items: Email[]) => localStorage.setItem(KEY, JSON.stringify(items));

const priorityBadgeClass = (p: Priority) =>
  p === "High"
    ? "border-rose-200 bg-rose-50 text-rose-700"
    : p === "Normal"
      ? "border-amber-200 bg-amber-50 text-amber-700"
      : "border-slate-200 bg-slate-50 text-slate-700";

const MessageQueue = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [items, setItems] = useState<Email[]>(load);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [fromAddr, setFromAddr] = useState("");
  const [toAddr, setToAddr] = useState("");
  const [notSent, setNotSent] = useState(false);
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [page, setPage] = useState(1);
  const perPage = 15;

  const filtered = useMemo(() => items.filter((m) => {
    if (notSent && m.sentOn) return false;
    const d = m.createdOn.slice(0, 10);
    if (from && d < from) return false;
    if (to && d > to) return false;
    if (fromAddr && !m.from.toLowerCase().includes(fromAddr.toLowerCase())) return false;
    if (toAddr && !m.to.toLowerCase().includes(toAddr.toLowerCase())) return false;
    return true;
  }), [items, from, to, fromAddr, toAddr, notSent]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / perPage));
  const pageRows = filtered.slice((page - 1) * perPage, page * perPage);
  const allSelected = pageRows.length > 0 && pageRows.every((r) => selected.has(r.id));
  const someSelected = pageRows.some((r) => selected.has(r.id)) && !allSelected;

  const toggle = (id: number) => {
    const n = new Set(selected); if (n.has(id)) { n.delete(id); } else { n.add(id); } setSelected(n);
  };
  const toggleAll = (checked: boolean) => {
    const n = new Set(selected);
    pageRows.forEach((r) => (checked ? n.add(r.id) : n.delete(r.id)));
    setSelected(n);
  };
  const removeSelected = () => {
    if (selected.size === 0) return;
    if (!confirm(t("form.confirmDelete"))) return;
    const next = items.filter((i) => !selected.has(i.id));
    setItems(next); save(next); setSelected(new Set());
    toast({ title: t("form.deleted") });
  };
  const removeAll = () => {
    if (!confirm(t("form.confirmDelete"))) return;
    setItems([]); save([]); setSelected(new Set());
    toast({ title: t("form.deleted") });
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold">{t("sys.queue.title")}</h1>
          <div className="flex flex-wrap gap-2">
            <Button variant="destructive" onClick={removeSelected}>
              <Trash2 className="mr-1.5 h-4 w-4" /> {t("sys.queue.deleteSelected")}
            </Button>
            <Button variant="destructive" onClick={removeAll}>
              <Trash2 className="mr-1.5 h-4 w-4" /> {t("sys.queue.deleteAll")}
            </Button>
          </div>
        </header>

        <section className="rounded-lg border bg-card p-4">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="mq-from" className="text-xs">{t("log.from")}</Label>
              <Input id="mq-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="h-9" />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="mq-to" className="text-xs">{t("log.to")}</Label>
              <Input id="mq-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} className="h-9" />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="mq-fromaddr" className="text-xs">{t("sys.queue.fromAddr")}</Label>
              <Input id="mq-fromaddr" value={fromAddr} onChange={(e) => setFromAddr(e.target.value)} className="h-9" />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="mq-toaddr" className="text-xs">{t("sys.queue.toAddr")}</Label>
              <Input id="mq-toaddr" value={toAddr} onChange={(e) => setToAddr(e.target.value)} className="h-9" />
            </div>
            <label className="mt-2 flex items-center gap-2">
              <Checkbox checked={notSent} onCheckedChange={(v) => setNotSent(v === true)} />
              <span className="text-sm font-medium">{t("sys.queue.onlyNotSent")}</span>
            </label>
          </div>
          <div className="mt-4">
            <Button size="sm">
              <Search className="mr-1.5 h-4 w-4" /> {t("set.search")}
            </Button>
          </div>
        </section>

        <section className="overflow-hidden rounded-lg border bg-card">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[1100px] divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="w-10 px-4 py-3 text-left">
                    <Checkbox
                      checked={allSelected ? true : someSelected ? "indeterminate" : false}
                      onCheckedChange={(v) => toggleAll(v === true)}
                      aria-label={t("common.selectAll")}
                    />
                  </th>
                  <th className="px-4 py-3 text-left font-medium">ID</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.queue.subject")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.queue.from")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.queue.to")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.queue.createdOn")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.queue.sentOn")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.queue.priority")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("common.edit")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {pageRows.map((m) => (
                  <tr key={m.id} className="hover:bg-muted/40 transition">
                    <td className="px-4 py-3">
                      <Checkbox
                        checked={selected.has(m.id)}
                        onCheckedChange={() => toggle(m.id)}
                        aria-label={`${t("common.selectAll")} · ${m.id}`}
                      />
                    </td>
                    <td className="px-4 py-3 font-mono text-xs">{m.id}</td>
                    <td className="px-4 py-3 text-xs">{m.subject}</td>
                    <td className="px-4 py-3 text-xs">{m.from}</td>
                    <td className="px-4 py-3 text-xs">{m.to}</td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">{m.createdOn}</td>
                    <td className={`px-4 py-3 text-xs ${m.sentOn ? "text-emerald-700" : "text-muted-foreground"}`}>{m.sentOn || "—"}</td>
                    <td className="px-4 py-3 text-xs">
                      <Badge variant="outline" className={`font-medium ${priorityBadgeClass(m.priority)}`}>
                        {m.priority}
                      </Badge>
                    </td>
                    <td className="px-4 py-3">
                      <Button size="icon" variant="ghost" className="h-8 w-8" aria-label={t("common.edit")}>
                        <Pencil className="h-3.5 w-3.5" />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between gap-4 border-t px-4 py-3">
            <p className="text-xs text-muted-foreground">
              {(page - 1) * perPage + 1} - {Math.min(page * perPage, filtered.length)} of {filtered.length} items
            </p>
            <div className="flex items-center gap-1">
              {Array.from({ length: Math.min(totalPages, 8) }, (_, i) => i + 1).map((p) => (
                <Button
                  key={p}
                  size="sm"
                  variant={p === page ? "default" : "ghost"}
                  className="h-8 w-8 p-0"
                  onClick={() => setPage(p)}
                >
                  {p}
                </Button>
              ))}
            </div>
          </div>
        </section>
      </div>
    </AdminLayout>
  );
};

export default MessageQueue;
