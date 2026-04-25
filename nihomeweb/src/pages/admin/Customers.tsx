import { useMemo, useState } from "react";
import { Search as SearchIcon, Plus, Pencil, Check } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";

type Customer = {
  id: string;
  email: string;
  name: string;
  roles: string[];
  company?: string;
  active: boolean;
  createdOn: string;
  lastActivity: string;
};

const KEY = "nicon_admin_customers_v1";
const seed: Customer[] = [
  { id: "u1", email: "tringuyen@nicon.vn", name: "Trí Nguyễn", roles: ["Administrators", "Registered"], company: "NICON", active: true, createdOn: "2018-06-26", lastActivity: "2026-04-24" },
  { id: "u2", email: "doandao120594@gmail.com", name: "Doan Dao", roles: ["Administrators", "Registered"], active: true, createdOn: "2025-03-06", lastActivity: "2025-03-20" },
  { id: "u3", email: "duong.tranngocthuy@nicon.vn", name: "Duong Thuy", roles: ["Administrators", "Registered"], active: true, createdOn: "2024-07-17", lastActivity: "2025-03-06" },
  { id: "u4", email: "phongthietke@nicon.com", name: "phongthietke", roles: ["Registered"], company: "phongthietke@nicon.com", active: true, createdOn: "2021-04-15", lastActivity: "2021-07-08" },
  { id: "u5", email: "phongcungung@nicon.com", name: "phongcungung", roles: ["Registered"], company: "phongcungung", active: true, createdOn: "2021-07-08", lastActivity: "2021-08-01" },
  { id: "u6", email: "khoa@nicon.vn", name: "khoa quach", roles: ["Registered"], active: true, createdOn: "2024-10-08", lastActivity: "2024-10-29" },
  { id: "u7", email: "rin@nicon.com", name: "Rin", roles: ["Administrators", "Registered"], active: true, createdOn: "2024-07-30", lastActivity: "2024-09-24" },
  { id: "u8", email: "testing@example.com", name: "Test User", roles: ["Registered"], company: "Testing", active: true, createdOn: "2025-02-20", lastActivity: "2025-02-20" },
];
const load = (): Customer[] => {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? JSON.parse(raw) : seed;
  } catch {
    return seed;
  }
};
const save = (items: Customer[]) => localStorage.setItem(KEY, JSON.stringify(items));

const Customers = () => {
  const { t } = useI18n();
  const [items, setItems] = useState<Customer[]>(load);
  const [email, setEmail] = useState("");
  const [name, setName] = useState("");

  const filtered = useMemo(
    () =>
      items.filter(
        (i) =>
          i.email.toLowerCase().includes(email.toLowerCase()) &&
          i.name.toLowerCase().includes(name.toLowerCase()),
      ),
    [items, email, name],
  );

  const add = () => {
    const newEmail = window.prompt(t("cust.email"));
    if (!newEmail?.trim()) return;
    const newName = window.prompt(t("cust.firstName")) ?? "User";
    const today = new Date().toISOString().slice(0, 10);
    const next = [
      { id: `u${Date.now()}`, email: newEmail.trim(), name: newName.trim(), roles: ["Registered"], active: true, createdOn: today, lastActivity: today },
      ...items,
    ];
    setItems(next);
    save(next);
    toast.success(t("form.created"));
  };

  const edit = (c: Customer) => {
    const newName = window.prompt(t("cust.firstName"), c.name);
    if (!newName?.trim()) return;
    const next = items.map((i) => (i.id === c.id ? { ...i, name: newName.trim() } : i));
    setItems(next);
    save(next);
    toast.success(t("form.updated"));
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("cust.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} / {items.length}
          </p>
        </div>
        <button onClick={add} className="admin-btn-primary inline-flex items-center gap-2">
          <Plus className="w-4 h-4" /> {t("common.new")}
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("cust.email")}
            </span>
            <input value={email} onChange={(e) => setEmail(e.target.value)} className="admin-input w-full" />
          </label>
          <label className="block">
            <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("cust.firstName")}
            </span>
            <input value={name} onChange={(e) => setName(e.target.value)} className="admin-input w-full" />
          </label>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[800px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-5 py-3 font-semibold">{t("cust.email")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.firstName")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.role")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.company")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.active")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.createdOn")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.lastActivity")}</th>
                <th className="px-5 py-3 font-semibold text-right">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((c) => (
                <tr key={c.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-5 py-3 font-semibold">{c.email}</td>
                  <td className="px-5 py-3">{c.name}</td>
                  <td className="px-5 py-3 text-xs">{c.roles.join(", ")}</td>
                  <td className="px-5 py-3 text-xs">{c.company ?? "—"}</td>
                  <td className="px-5 py-3">
                    {c.active && <Check className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />}
                  </td>
                  <td className="px-5 py-3 text-xs">{c.createdOn}</td>
                  <td className="px-5 py-3 text-xs">{c.lastActivity}</td>
                  <td className="px-5 py-3 text-right">
                    <button
                      onClick={() => edit(c)}
                      className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted"
                    >
                      <Pencil className="w-3.5 h-3.5" /> {t("common.edit")}
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

export default Customers;
