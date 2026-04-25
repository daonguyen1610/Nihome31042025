import { useMemo, useState } from "react";
import { Search, Trash2, Pencil } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

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

  const toggle = (id: number) => {
    const n = new Set(selected); if (n.has(id)) { n.delete(id); } else { n.add(id); } setSelected(n);
  };
  const toggleAll = () => {
    if (pageRows.every((r) => selected.has(r.id))) {
      const n = new Set(selected); pageRows.forEach((r) => n.delete(r.id)); setSelected(n);
    } else {
      const n = new Set(selected); pageRows.forEach((r) => n.add(r.id)); setSelected(n);
    }
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
      <div className="flex items-center justify-between mb-6 gap-3 flex-wrap">
        <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("sys.queue.title")}</h1>
        <div className="flex gap-2">
          <button onClick={removeSelected} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2 rounded-lg text-white" style={{ background: "hsl(var(--admin-danger))" }}>
            <Trash2 className="w-4 h-4" /> {t("sys.queue.deleteSelected")}
          </button>
          <button onClick={removeAll} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2 rounded-lg text-white" style={{ background: "hsl(var(--admin-danger))" }}>
            <Trash2 className="w-4 h-4" /> {t("sys.queue.deleteAll")}
          </button>
        </div>
      </div>

      <div className="admin-card p-5 mb-5">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("log.from")}</span>
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("log.to")}</span>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("sys.queue.fromAddr")}</span>
            <input value={fromAddr} onChange={(e) => setFromAddr(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("sys.queue.toAddr")}</span>
            <input value={toAddr} onChange={(e) => setToAddr(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="flex items-center gap-2 mt-2">
            <input type="checkbox" checked={notSent} onChange={(e) => setNotSent(e.target.checked)} />
            <span className="text-sm font-semibold">{t("sys.queue.onlyNotSent")}</span>
          </label>
        </div>
        <div className="mt-4">
          <button className="admin-btn-primary inline-flex items-center gap-2 px-4 py-2 text-sm">
            <Search className="w-4 h-4" /> {t("set.search")}
          </button>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[1100px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-4 py-3 w-10"><input type="checkbox" checked={pageRows.length > 0 && pageRows.every((r) => selected.has(r.id))} onChange={toggleAll} /></th>
                <th className="px-4 py-3 font-bold">ID</th>
                <th className="px-4 py-3 font-bold">{t("sys.queue.subject")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.queue.from")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.queue.to")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.queue.createdOn")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.queue.sentOn")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.queue.priority")}</th>
                <th className="px-4 py-3 font-bold">{t("common.edit")}</th>
              </tr>
            </thead>
            <tbody>
              {pageRows.map((m) => (
                <tr key={m.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-4 py-3"><input type="checkbox" checked={selected.has(m.id)} onChange={() => toggle(m.id)} /></td>
                  <td className="px-4 py-3 text-xs font-mono">{m.id}</td>
                  <td className="px-4 py-3 text-xs">{m.subject}</td>
                  <td className="px-4 py-3 text-xs">{m.from}</td>
                  <td className="px-4 py-3 text-xs">{m.to}</td>
                  <td className="px-4 py-3 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{m.createdOn}</td>
                  <td className="px-4 py-3 text-xs" style={{ color: m.sentOn ? "hsl(142 71% 35%)" : "hsl(var(--admin-muted))" }}>{m.sentOn || "—"}</td>
                  <td className="px-4 py-3 text-xs"><span className="px-2 py-0.5 rounded-md text-white text-xs font-bold" style={{ background: "hsl(0 80% 55%)" }}>{m.priority}</span></td>
                  <td className="px-4 py-3">
                    <button className="px-2 py-1 rounded-md text-xs font-bold bg-muted"><Pencil className="w-3.5 h-3.5" /></button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="flex items-center justify-between gap-4 px-6 py-3 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
          <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
            {(page - 1) * perPage + 1} - {Math.min(page * perPage, filtered.length)} of {filtered.length} items
          </p>
          <div className="flex items-center gap-1">
            {Array.from({ length: Math.min(totalPages, 8) }, (_, i) => i + 1).map((p) => (
              <button key={p} onClick={() => setPage(p)} className="w-8 h-8 rounded-md text-xs font-bold"
                style={p === page ? { background: "hsl(var(--admin-primary))", color: "white" } : { background: "hsl(var(--admin-bg))" }}>
                {p}
              </button>
            ))}
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default MessageQueue;
