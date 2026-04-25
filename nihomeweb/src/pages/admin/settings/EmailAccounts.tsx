import { useState } from "react";
import { Plus, Pencil, Check, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  getEmailAccounts,
  saveEmailAccounts,
  newId,
  type EmailAccount,
} from "@/lib/settingsStore";

const blank: EmailAccount = {
  id: "",
  email: "",
  displayName: "",
  host: "smtp.gmail.com",
  port: 587,
  username: "",
  enableSsl: true,
  isDefault: false,
};

const EmailAccountsPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [rows, setRows] = useState<EmailAccount[]>(() => getEmailAccounts());
  const [editing, setEditing] = useState<EmailAccount | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<EmailAccount>(blank);

  const persist = (next: EmailAccount[]) => {
    setRows(next);
    saveEmailAccounts(next);
  };

  const setDefault = (id: string) => {
    persist(rows.map((r) => ({ ...r, isDefault: r.id === id })));
    toast({ title: t("form.updated") });
  };

  const remove = (id: string) => {
    if (!confirm(t("form.confirmDelete"))) return;
    persist(rows.filter((r) => r.id !== id));
    toast({ title: t("form.deleted") });
  };

  const submit = () => {
    if (!draft.email.trim()) return;
    if (adding) {
      const item = { ...draft, id: newId() };
      persist([item, ...rows]);
      toast({ title: t("form.created") });
    } else if (editing) {
      persist(rows.map((r) => (r.id === editing.id ? draft : r)));
      toast({ title: t("form.updated") });
    }
    setAdding(false);
    setEditing(null);
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">
          {t("set.email")}
        </h1>
        <button
          onClick={() => { setDraft(blank); setAdding(true); }}
          className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm"
        >
          <Plus className="w-4 h-4" /> {t("set.add")}
        </button>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-6 py-3 font-bold">Email address</th>
                <th className="px-6 py-3 font-bold">Display name</th>
                <th className="px-6 py-3 font-bold">Default</th>
                <th className="px-6 py-3 font-bold w-60">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-6 py-4 font-semibold">{r.email}</td>
                  <td className="px-6 py-4">{r.displayName}</td>
                  <td className="px-6 py-4">
                    {r.isDefault ? (
                      <Check className="w-5 h-5" style={{ color: "hsl(var(--admin-primary))" }} />
                    ) : (
                      <button
                        onClick={() => setDefault(r.id)}
                        className="px-3 py-1.5 rounded-md text-xs font-bold text-white"
                        style={{ background: "hsl(142 71% 40%)" }}
                      >
                        Mark as default
                      </button>
                    )}
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex gap-2">
                      <button onClick={() => { setDraft(r); setEditing(r); }} className="px-2.5 py-1.5 rounded-md text-xs font-bold bg-muted inline-flex items-center gap-1">
                        <Pencil className="w-3 h-3" /> {t("common.edit")}
                      </button>
                      <button onClick={() => remove(r.id)} className="px-2.5 py-1.5 rounded-md text-xs font-bold inline-flex items-center gap-1 text-white" style={{ background: "hsl(var(--admin-danger))" }}>
                        <Trash2 className="w-3 h-3" /> {t("common.delete")}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-6 py-10 text-center text-sm" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("posts.empty")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {(adding || editing) && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" onClick={() => { setAdding(false); setEditing(null); }}>
          <div className="bg-white rounded-2xl w-full max-w-xl p-6" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-display text-xl font-extrabold mb-4">
              {adding ? t("set.add") : t("common.edit")}
            </h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {[
                ["email", "Email address", "text"],
                ["displayName", "Display name", "text"],
                ["host", "Host", "text"],
                ["port", "Port", "number"],
                ["username", "Username", "text"],
              ].map(([key, label, type]) => (
                <div key={key as string}>
                  <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>{label}</label>
                  <input
                    type={type as string}
                    value={(draft as Record<string, string | number | boolean>)[key as string]}
                    onChange={(e) => setDraft({ ...draft, [key as string]: type === "number" ? +e.target.value : e.target.value } as EmailAccount)}
                    className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  />
                </div>
              ))}
              <label className="flex items-center gap-2 mt-2 col-span-full">
                <input type="checkbox" checked={draft.enableSsl} onChange={(e) => setDraft({ ...draft, enableSsl: e.target.checked })} />
                <span className="text-sm font-semibold">Enable SSL</span>
              </label>
            </div>
            <div className="flex justify-end gap-2 mt-5">
              <button onClick={() => { setAdding(false); setEditing(null); }} className="px-4 py-2 rounded-lg text-sm font-bold bg-muted">
                {t("common.cancel")}
              </button>
              <button onClick={submit} className="admin-btn-primary px-4 py-2 text-sm">
                {t("proc.save")}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default EmailAccountsPage;
