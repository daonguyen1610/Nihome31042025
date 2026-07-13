import { useState } from "react";
import { Plus, Pencil, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  getStores,
  saveStores,
  newId,
  type StoreItem,
} from "@/lib/settingsStore";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

const blank: StoreItem = { id: "", name: "", url: "", displayOrder: 1, hosts: "" };

const StoresPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [rows, setRows] = useState<StoreItem[]>(() => getStores());
  const [editing, setEditing] = useState<StoreItem | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<StoreItem>(blank);
  const dialogOpen = adding || editing !== null;
  const closeDialog = () => {
    setAdding(false);
    setEditing(null);
  };

  const persist = (next: StoreItem[]) => { setRows(next); saveStores(next); };
  const remove = (id: string) => {
    if (!confirm(t("form.confirmDelete"))) return;
    persist(rows.filter((r) => r.id !== id));
    toast({ title: t("form.deleted") });
  };
  const submit = () => {
    if (!draft.name.trim()) return;
    if (adding) {
      persist([{ ...draft, id: newId() }, ...rows]);
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
          <h1 className="text-2xl font-semibold">{t("set.stores")}</h1>
          <Button onClick={() => { setDraft(blank); setAdding(true); }}>
            <Plus className="mr-1.5 h-4 w-4" /> {t("set.add")}
          </Button>
        </header>

        <section className="overflow-hidden rounded-lg border bg-card">
          <div className="overflow-x-auto">
            <table className="w-full divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left font-medium">Store name</th>
                  <th className="px-4 py-3 text-left font-medium">Store URL</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.displayOrder")}</th>
                  <th className="w-40 px-4 py-3 text-left font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((r) => (
                  <tr key={r.id} className="hover:bg-muted/40 transition">
                    <td className="px-4 py-3 font-medium">{r.name}</td>
                    <td className="px-4 py-3">{r.url}</td>
                    <td className="px-4 py-3">{r.displayOrder}</td>
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
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <Dialog open={dialogOpen} onOpenChange={(open) => (!open ? closeDialog() : null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{adding ? t("set.add") : t("common.edit")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            {([
              ["name", "Store name", "text"],
              ["url", "Store URL", "text"],
              ["hosts", "Hosts (comma-separated)", "text"],
              ["displayOrder", t("set.displayOrder"), "number"],
            ] as const).map(([key, label, type]) => (
              <div key={key} className="space-y-1.5">
                <Label htmlFor={`store-${key}`} className="text-xs">{label}</Label>
                <Input
                  id={`store-${key}`}
                  type={type}
                  value={(draft as Record<string, string | number | boolean>)[key] as string | number}
                  onChange={(e) => setDraft({ ...draft, [key]: type === "number" ? +e.target.value : e.target.value } as StoreItem)}
                  className="h-9"
                />
              </div>
            ))}
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

export default StoresPage;
