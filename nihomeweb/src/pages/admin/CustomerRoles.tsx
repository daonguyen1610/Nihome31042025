import { useState } from "react";
import { Plus, Pencil, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";

type Role = {
  id: string;
  name: string;
  freeShipping: boolean;
  taxExempt: boolean;
  active: boolean;
  systemRole: boolean;
};

const KEY = "nicon_admin_roles_v1";
const seed: Role[] = [
  { id: "r1", name: "Administrators", freeShipping: false, taxExempt: false, active: true, systemRole: true },
  { id: "r2", name: "Forum Moderators", freeShipping: false, taxExempt: false, active: true, systemRole: true },
  { id: "r3", name: "Guests", freeShipping: false, taxExempt: false, active: true, systemRole: true },
  { id: "r4", name: "Registered", freeShipping: false, taxExempt: false, active: true, systemRole: true },
  { id: "r5", name: "Vendors", freeShipping: false, taxExempt: false, active: true, systemRole: true },
];
const load = (): Role[] => {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? JSON.parse(raw) : seed;
  } catch {
    return seed;
  }
};
const save = (items: Role[]) => localStorage.setItem(KEY, JSON.stringify(items));

const Cell = ({ ok }: { ok: boolean }) =>
  ok ? (
    <Check className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />
  ) : (
    <X className="w-4 h-4" style={{ color: "hsl(var(--admin-danger))" }} />
  );

const CustomerRoles = () => {
  const { t } = useI18n();
  const [items, setItems] = useState<Role[]>(load);

  const add = () => {
    const name = window.prompt(t("role.name"));
    if (!name?.trim()) return;
    const next = [
      ...items,
      { id: `r${Date.now()}`, name: name.trim(), freeShipping: false, taxExempt: false, active: true, systemRole: false },
    ];
    setItems(next);
    save(next);
    toast.success(t("form.created"));
  };

  const edit = (r: Role) => {
    const name = window.prompt(t("role.name"), r.name);
    if (!name?.trim()) return;
    const next = items.map((i) => (i.id === r.id ? { ...i, name: name.trim() } : i));
    setItems(next);
    save(next);
    toast.success(t("form.updated"));
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("role.title")}</h1>
        <button onClick={add} className="admin-btn-primary inline-flex items-center gap-2">
          <Plus className="w-4 h-4" /> {t("common.new")}
        </button>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[700px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-5 py-3 font-semibold">{t("role.name")}</th>
                <th className="px-5 py-3 font-semibold">{t("role.freeShipping")}</th>
                <th className="px-5 py-3 font-semibold">{t("role.taxExempt")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.active")}</th>
                <th className="px-5 py-3 font-semibold">{t("role.systemRole")}</th>
                <th className="px-5 py-3 font-semibold text-right">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-5 py-3 font-semibold">{r.name}</td>
                  <td className="px-5 py-3"><Cell ok={r.freeShipping} /></td>
                  <td className="px-5 py-3"><Cell ok={r.taxExempt} /></td>
                  <td className="px-5 py-3"><Cell ok={r.active} /></td>
                  <td className="px-5 py-3"><Cell ok={r.systemRole} /></td>
                  <td className="px-5 py-3 text-right">
                    <button
                      onClick={() => edit(r)}
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

export default CustomerRoles;
