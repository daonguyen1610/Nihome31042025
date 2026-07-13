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
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

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

  const dialogOpen = adding || editing !== null;
  const closeDialog = () => {
    setAdding(false);
    setEditing(null);
  };

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
    closeDialog();
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold">{t("set.email")}</h1>
          <Button onClick={() => { setDraft(blank); setAdding(true); }}>
            <Plus className="mr-1.5 h-4 w-4" /> {t("set.add")}
          </Button>
        </header>

        <section className="overflow-hidden rounded-lg border bg-card">
          <div className="overflow-x-auto">
            <table className="w-full divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left font-medium">Email address</th>
                  <th className="px-4 py-3 text-left font-medium">Display name</th>
                  <th className="px-4 py-3 text-left font-medium">Default</th>
                  <th className="w-60 px-4 py-3 text-left font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((r) => (
                  <tr key={r.id} className="hover:bg-muted/40 transition">
                    <td className="px-4 py-3 font-medium">{r.email}</td>
                    <td className="px-4 py-3">{r.displayName}</td>
                    <td className="px-4 py-3">
                      {r.isDefault ? (
                        <Check className="h-5 w-5 text-emerald-600" />
                      ) : (
                        <Button size="sm" variant="outline" onClick={() => setDefault(r.id)}>
                          Mark as default
                        </Button>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex gap-2">
                        <Button size="sm" variant="outline" onClick={() => { setDraft(r); setEditing(r); }}>
                          <Pencil className="mr-1 h-3 w-3" /> {t("common.edit")}
                        </Button>
                        <Button size="sm" variant="destructive" onClick={() => remove(r.id)}>
                          <Trash2 className="mr-1 h-3 w-3" /> {t("common.delete")}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-4 py-10 text-center text-sm text-muted-foreground">
                      {t("posts.empty")}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <Dialog open={dialogOpen} onOpenChange={(open) => (!open ? closeDialog() : null)}>
        <DialogContent className="max-w-xl">
          <DialogHeader>
            <DialogTitle>{adding ? t("set.add") : t("common.edit")}</DialogTitle>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {([
              ["email", "Email address", "text"],
              ["displayName", "Display name", "text"],
              ["host", "Host", "text"],
              ["port", "Port", "number"],
              ["username", "Username", "text"],
            ] as const).map(([key, label, type]) => (
              <div key={key} className="space-y-1.5">
                <Label htmlFor={`email-${key}`} className="text-xs">{label}</Label>
                <Input
                  id={`email-${key}`}
                  type={type}
                  value={(draft as Record<string, string | number | boolean>)[key] as string | number}
                  onChange={(e) => setDraft({ ...draft, [key]: type === "number" ? +e.target.value : e.target.value } as EmailAccount)}
                  className="h-9"
                />
              </div>
            ))}
            <label className="col-span-full mt-2 flex items-center gap-2">
              <Checkbox
                checked={draft.enableSsl}
                onCheckedChange={(v) => setDraft({ ...draft, enableSsl: v === true })}
              />
              <span className="text-sm font-medium">Enable SSL</span>
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={closeDialog}>{t("common.cancel")}</Button>
            <Button onClick={submit}>{t("proc.save")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default EmailAccountsPage;
