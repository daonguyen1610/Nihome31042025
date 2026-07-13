import { useState } from "react";
import { Plus, Pencil, Check, X, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  getLanguages,
  saveLanguages,
  newId,
  type Language,
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

const blank: Language = { id: "", name: "", flag: "🌐", culture: "", displayOrder: 0, published: true };

const LanguagesPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [rows, setRows] = useState<Language[]>(() => getLanguages());
  const [editing, setEditing] = useState<Language | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<Language>(blank);
  const dialogOpen = adding || editing !== null;
  const closeDialog = () => {
    setAdding(false);
    setEditing(null);
  };

  const persist = (next: Language[]) => { setRows(next); saveLanguages(next); };
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
          <h1 className="text-2xl font-semibold">{t("set.languages")}</h1>
          <Button onClick={() => { setDraft(blank); setAdding(true); }}>
            <Plus className="mr-1.5 h-4 w-4" /> {t("set.add")}
          </Button>
        </header>

        <section className="overflow-hidden rounded-lg border bg-card">
          <div className="overflow-x-auto">
            <table className="w-full divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left font-medium">Name</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.flag")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.culture")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.displayOrder")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.published")}</th>
                  <th className="w-40 px-4 py-3 text-left font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((r) => (
                  <tr key={r.id} className="hover:bg-muted/40 transition">
                    <td className="px-4 py-3 font-medium">{r.name}</td>
                    <td className="px-4 py-3 text-2xl">{r.flag}</td>
                    <td className="px-4 py-3 font-mono text-xs">{r.culture}</td>
                    <td className="px-4 py-3">{r.displayOrder}</td>
                    <td className="px-4 py-3">
                      {r.published ? (
                        <Check className="h-5 w-5 text-emerald-600" />
                      ) : (
                        <X className="h-5 w-5 text-destructive" />
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
              ["name", "Name", "text"],
              ["flag", "Flag (emoji)", "text"],
              ["culture", "Language culture", "text"],
              ["displayOrder", t("set.displayOrder"), "number"],
            ] as const).map(([key, label, type]) => (
              <div key={key} className="space-y-1.5">
                <Label htmlFor={`lang-${key}`} className="text-xs">{label}</Label>
                <Input
                  id={`lang-${key}`}
                  type={type}
                  value={(draft as Record<string, string | number | boolean>)[key] as string | number}
                  onChange={(e) => setDraft({ ...draft, [key]: type === "number" ? +e.target.value : e.target.value } as Language)}
                  className="h-9"
                />
              </div>
            ))}
            <label className="mt-2 flex items-center gap-2">
              <Checkbox
                checked={draft.published}
                onCheckedChange={(v) => setDraft({ ...draft, published: v === true })}
              />
              <span className="text-sm font-medium">Published</span>
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

export default LanguagesPage;
