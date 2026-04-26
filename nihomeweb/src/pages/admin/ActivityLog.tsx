import { useMemo, useState } from "react";
import { Search as SearchIcon, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";

type Log = {
  id: string;
  type: string;
  customer: string;
  ip: string;
  message: string;
  createdOn: string;
};

const KEY = "nicon_admin_logs_v1";
const seed: Log[] = [
  { id: "l1", type: "Edit a news", customer: "duong.tranngocthuy@nicon.vn", ip: "115.78.15.232", message: "Edited a news (ID = 3)", createdOn: "2025-03-20 11:18:51" },
  { id: "l2", type: "Edit a news", customer: "duong.tranngocthuy@nicon.vn", ip: "115.78.15.232", message: "Edited a news (ID = 5)", createdOn: "2025-03-20 11:01:03" },
  { id: "l3", type: "Edit a customer", customer: "duong.tranngocthuy@nicon.vn", ip: "115.78.15.232", message: "Edited a customer (ID = 1201848)", createdOn: "2025-03-06 10:30:22" },
  { id: "l4", type: "Add a new customer", customer: "duong.tranngocthuy@nicon.vn", ip: "115.78.15.232", message: "Added a new customer (ID = 1201848)", createdOn: "2025-03-06 10:28:33" },
  { id: "l5", type: "Edit a news", customer: "duong.tranngocthuy@nicon.vn", ip: "171.246.133.56", message: "Edited a news (ID = 7)", createdOn: "2024-12-25 15:54:11" },
  { id: "l6", type: "Add a new news", customer: "tringuyen@nicon.vn", ip: "116.100.46.41", message: "Added a new news (ID = 8)", createdOn: "2024-12-25 15:26:30" },
  { id: "l7", type: "Edit a project", customer: "tringuyen@nicon.vn", ip: "116.100.46.41", message: "Edited a project (ID = 12)", createdOn: "2024-11-15 09:14:22" },
  { id: "l8", type: "Login", customer: "tringuyen@nicon.vn", ip: "116.100.46.41", message: "User logged in", createdOn: "2024-11-10 08:01:15" },
];
const load = (): Log[] => {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? JSON.parse(raw) : seed;
  } catch {
    return seed;
  }
};
const save = (items: Log[]) => localStorage.setItem(KEY, JSON.stringify(items));

const ActivityLog = () => {
  const { t } = useI18n();
  const [items, setItems] = useState<Log[]>(load);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [ip, setIp] = useState("");
  const [type, setType] = useState("All");

  const types = useMemo(() => ["All", ...Array.from(new Set(items.map((i) => i.type)))], [items]);

  const filtered = useMemo(() => {
    return items.filter((i) => {
      if (ip && !i.ip.includes(ip)) return false;
      if (type !== "All" && i.type !== type) return false;
      const d = i.createdOn.slice(0, 10);
      if (from && d < from) return false;
      if (to && d > to) return false;
      return true;
    });
  }, [items, ip, type, from, to]);

  const remove = (id: string) => {
    const next = items.filter((i) => i.id !== id);
    setItems(next);
    save(next);
    toast.success(t("form.deleted"));
  };

  const clearAll = () => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    setItems([]);
    save([]);
    toast.success(t("form.deleted"));
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("log.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} / {items.length}
          </p>
        </div>
        <button
          onClick={clearAll}
          className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2.5 rounded-xl"
          style={{ background: "hsl(var(--admin-danger))", color: "white" }}
        >
          <Trash2 className="w-4 h-4" /> {t("log.clear")}
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("log.from")}
            </span>
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("log.to")}
            </span>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("log.ip")}
            </span>
            <input value={ip} onChange={(e) => setIp(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("log.type")}
            </span>
            <select value={type} onChange={(e) => setType(e.target.value)} className="admin-input w-full">
              {types.map((tp) => (
                <option key={tp}>{tp}</option>
              ))}
            </select>
          </label>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[800px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-5 py-3 font-semibold">{t("log.type")}</th>
                <th className="px-5 py-3 font-semibold">{t("log.customer")}</th>
                <th className="px-5 py-3 font-semibold">{t("log.ip")}</th>
                <th className="px-5 py-3 font-semibold">{t("log.message")}</th>
                <th className="px-5 py-3 font-semibold">{t("log.createdOn")}</th>
                <th className="px-5 py-3 font-semibold text-right">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((l) => (
                <tr key={l.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-5 py-3 font-semibold">{l.type}</td>
                  <td className="px-5 py-3 text-xs" style={{ color: "hsl(var(--admin-primary))" }}>{l.customer}</td>
                  <td className="px-5 py-3 font-mono text-xs">{l.ip}</td>
                  <td className="px-5 py-3 text-xs">{l.message}</td>
                  <td className="px-5 py-3 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{l.createdOn}</td>
                  <td className="px-5 py-3 text-right">
                    <button
                      onClick={() => remove(l.id)}
                      className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted"
                      style={{ color: "hsl(var(--admin-danger))" }}
                    >
                      <Trash2 className="w-3.5 h-3.5" /> {t("common.delete")}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </AdminLayout>
  );
};

export default ActivityLog;
