import { useMemo, useState } from "react";
import { Plus, Pencil, Trash2, Search } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  getAllSettings,
  saveAllSettings,
  newId,
  type SettingRow as SRow,
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

const AllSettingsPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [rows, setRows] = useState<SRow[]>(() => getAllSettings());
  const [qName, setQName] = useState("");
  const [qValue, setQValue] = useState("");
  const [page, setPage] = useState(1);
  const perPage = 15;

  const [editing, setEditing] = useState<SRow | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<SRow>({ id: "", name: "", value: "", store: "All stores" });

  const filtered = useMemo(
    () =>
      rows.filter(
        (r) =>
          r.name.toLowerCase().includes(qName.toLowerCase()) &&
          r.value.toLowerCase().includes(qValue.toLowerCase()),
      ),
    [rows, qName, qValue],
  );

  const totalPages = Math.max(1, Math.ceil(filtered.length / perPage));
  const pageRows = filtered.slice((page - 1) * perPage, page * perPage);
  const dialogOpen = adding || editing !== null;
  const closeDialog = () => {
    setAdding(false);
    setEditing(null);
  };

  const persist = (next: SRow[]) => {
    setRows(next);
    saveAllSettings(next);
  };

  const startAdd = () => {
    setDraft({ id: newId(), name: "", value: "", store: "All stores" });
    setAdding(true);
  };
  const startEdit = (r: SRow) => {
    setDraft({ ...r });
    setEditing(r);
  };
  const remove = (id: string) => {
    if (!confirm(t("form.confirmDelete"))) return;
    persist(rows.filter((r) => r.id !== id));
    toast({ title: t("form.deleted") });
  };
  const submit = () => {
    if (!draft.name.trim()) return;
    if (adding) {
      persist([{ ...draft }, ...rows]);
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
        <header>
          <h1 className="text-2xl font-semibold">{t("set.all")}</h1>
        </header>

        {/* Search */}
        <section className="rounded-lg border bg-card p-4">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="all-settings-name" className="text-xs">{t("set.name")}</Label>
              <Input
                id="all-settings-name"
                value={qName}
                onChange={(e) => setQName(e.target.value)}
                className="h-9"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="all-settings-value" className="text-xs">{t("set.value")}</Label>
              <Input
                id="all-settings-value"
                value={qValue}
                onChange={(e) => setQValue(e.target.value)}
                className="h-9"
              />
            </div>
          </div>
          <div className="mt-4">
            <Button onClick={() => setPage(1)}>
              <Search className="mr-1.5 h-4 w-4" /> {t("set.search")}
            </Button>
          </div>
        </section>

        {/* Table */}
        <section className="overflow-hidden rounded-lg border bg-card">
          <div className="flex items-center justify-between border-b px-4 py-3">
            <Button size="sm" onClick={startAdd}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("set.addRow")}
            </Button>
            <p className="text-xs text-muted-foreground">
              {filtered.length} {t("common.showing")}
            </p>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left font-medium">{t("set.name")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.value")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.store")}</th>
                  <th className="w-40 px-4 py-3 text-left font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {pageRows.map((r) => (
                  <tr key={r.id} className="hover:bg-muted/40 transition">
                    <td className="px-4 py-3 font-mono text-xs">{r.name}</td>
                    <td className="px-4 py-3">{r.value}</td>
                    <td className="px-4 py-3">{r.store}</td>
                    <td className="px-4 py-3">
                      <div className="flex gap-2">
                        <Button size="sm" variant="outline" onClick={() => startEdit(r)}>
                          <Pencil className="mr-1 h-3 w-3" /> {t("common.edit")}
                        </Button>
                        <Button size="sm" variant="destructive" onClick={() => remove(r.id)}>
                          <Trash2 className="mr-1 h-3 w-3" /> {t("common.delete")}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
                {pageRows.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-4 py-10 text-center text-sm text-muted-foreground">
                      {t("posts.empty")}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {/* pagination */}
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

      {/* Editor dialog */}
      <Dialog open={dialogOpen} onOpenChange={(open) => (!open ? closeDialog() : null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{adding ? t("set.addRow") : t("common.edit")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="draft-name" className="text-xs">{t("set.name")}</Label>
              <Input
                id="draft-name"
                value={draft.name}
                onChange={(e) => setDraft({ ...draft, name: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="draft-value" className="text-xs">{t("set.value")}</Label>
              <Input
                id="draft-value"
                value={draft.value}
                onChange={(e) => setDraft({ ...draft, value: e.target.value })}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="draft-store" className="text-xs">{t("set.store")}</Label>
              <Input
                id="draft-store"
                value={draft.store}
                onChange={(e) => setDraft({ ...draft, store: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={closeDialog}>
              {t("common.cancel")}
            </Button>
            <Button onClick={submit}>{t("proc.save")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default AllSettingsPage;
