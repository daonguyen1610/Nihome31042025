import { useState } from "react";
import { Plus, Pencil, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";

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
    <Check className="h-4 w-4 text-emerald-600" />
  ) : (
    <X className="h-4 w-4 text-muted-foreground" />
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
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h1 className="text-2xl font-semibold">{t("role.title")}</h1>
          <Button onClick={add}>
            <Plus className="mr-1.5 h-4 w-4" /> {t("common.new")}
          </Button>
        </header>

        <div className="overflow-x-auto rounded-lg border">
          <table className="min-w-[700px] w-full divide-y text-sm">
            <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left font-medium">{t("role.name")}</th>
                <th className="px-4 py-3 text-left font-medium">{t("role.freeShipping")}</th>
                <th className="px-4 py-3 text-left font-medium">{t("role.taxExempt")}</th>
                <th className="px-4 py-3 text-left font-medium">{t("cust.active")}</th>
                <th className="px-4 py-3 text-left font-medium">{t("role.systemRole")}</th>
                <th className="px-4 py-3 text-right font-medium">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {items.map((r) => (
                <tr key={r.id} className="hover:bg-muted/40 transition">
                  <td className="px-4 py-3 font-medium">{r.name}</td>
                  <td className="px-4 py-3"><Cell ok={r.freeShipping} /></td>
                  <td className="px-4 py-3"><Cell ok={r.taxExempt} /></td>
                  <td className="px-4 py-3"><Cell ok={r.active} /></td>
                  <td className="px-4 py-3"><Cell ok={r.systemRole} /></td>
                  <td className="px-4 py-3 text-right">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => edit(r)}
                    >
                      <Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                    </Button>
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
