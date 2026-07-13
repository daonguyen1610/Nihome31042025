import { useMemo, useState } from "react";
import { Plus, Pencil, Check, X, Trash2, Download, Upload } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  getCountries,
  saveCountries,
  newId,
  type Country,
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

const blank: Country = {
  id: "", name: "", allowsBilling: true, allowsShipping: true,
  twoLetterCode: "", threeLetterCode: "", numericCode: 0,
  subjectToVat: false, numberOfStates: 0, displayOrder: 100, published: true,
};

const BoolCell = ({ on }: { on: boolean }) =>
  on ? (
    <Check className="h-5 w-5 text-emerald-600" />
  ) : (
    <X className="h-5 w-5 text-destructive" />
  );

const CountriesPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [rows, setRows] = useState<Country[]>(() => getCountries());
  const [editing, setEditing] = useState<Country | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<Country>(blank);
  const [page, setPage] = useState(1);
  const perPage = 15;

  const totalPages = Math.max(1, Math.ceil(rows.length / perPage));
  const pageRows = useMemo(() => rows.slice((page - 1) * perPage, page * perPage), [rows, page]);
  const dialogOpen = adding || editing !== null;
  const closeDialog = () => {
    setAdding(false);
    setEditing(null);
  };

  const persist = (next: Country[]) => { setRows(next); saveCountries(next); };
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
          <h1 className="text-2xl font-semibold">{t("set.countries")}</h1>
          <div className="flex flex-wrap gap-2">
            <Button onClick={() => { setDraft(blank); setAdding(true); }}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("set.add")}
            </Button>
            <Button variant="outline">
              <Upload className="mr-1.5 h-4 w-4" /> Export CSV
            </Button>
            <Button variant="outline">
              <Download className="mr-1.5 h-4 w-4" /> Import CSV
            </Button>
          </div>
        </header>

        <section className="overflow-hidden rounded-lg border bg-card">
          <div className="overflow-x-auto">
            <table className="w-full divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left font-medium">Name</th>
                  <th className="px-4 py-3 text-left font-medium">Billing</th>
                  <th className="px-4 py-3 text-left font-medium">Shipping</th>
                  <th className="px-4 py-3 text-left font-medium">2-letter</th>
                  <th className="px-4 py-3 text-left font-medium">3-letter</th>
                  <th className="px-4 py-3 text-left font-medium">Numeric</th>
                  <th className="px-4 py-3 text-left font-medium">VAT</th>
                  <th className="px-4 py-3 text-left font-medium">States</th>
                  <th className="px-4 py-3 text-left font-medium">Order</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.published")}</th>
                  <th className="w-32 px-4 py-3 text-left font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {pageRows.map((r) => (
                  <tr key={r.id} className="hover:bg-muted/40 transition">
                    <td className="px-4 py-3 font-medium">{r.name}</td>
                    <td className="px-4 py-3"><BoolCell on={r.allowsBilling} /></td>
                    <td className="px-4 py-3"><BoolCell on={r.allowsShipping} /></td>
                    <td className="px-4 py-3 font-mono text-xs">{r.twoLetterCode}</td>
                    <td className="px-4 py-3 font-mono text-xs">{r.threeLetterCode}</td>
                    <td className="px-4 py-3">{r.numericCode}</td>
                    <td className="px-4 py-3"><BoolCell on={r.subjectToVat} /></td>
                    <td className="px-4 py-3">{r.numberOfStates}</td>
                    <td className="px-4 py-3">{r.displayOrder}</td>
                    <td className="px-4 py-3"><BoolCell on={r.published} /></td>
                    <td className="px-4 py-3">
                      <div className="flex gap-1">
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8"
                          onClick={() => { setDraft(r); setEditing(r); }}
                          aria-label={t("common.edit")}
                        >
                          <Pencil className="h-3.5 w-3.5" />
                        </Button>
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8 text-destructive hover:text-destructive"
                          onClick={() => remove(r.id)}
                          aria-label={t("common.delete")}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between gap-4 border-t px-4 py-3">
            <p className="text-xs text-muted-foreground">
              {(page - 1) * perPage + 1} - {Math.min(page * perPage, rows.length)} of {rows.length} items
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

      <Dialog open={dialogOpen} onOpenChange={(open) => (!open ? closeDialog() : null)}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{adding ? t("set.add") : t("common.edit")}</DialogTitle>
          </DialogHeader>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {([
              ["name", "Name", "text"],
              ["twoLetterCode", "Two letter ISO code", "text"],
              ["threeLetterCode", "Three letter ISO code", "text"],
              ["numericCode", "Numeric ISO code", "number"],
              ["numberOfStates", "Number of states", "number"],
              ["displayOrder", "Display order", "number"],
            ] as const).map(([key, label, type]) => (
              <div key={key} className="space-y-1.5">
                <Label htmlFor={`country-${key}`} className="text-xs">{label}</Label>
                <Input
                  id={`country-${key}`}
                  type={type}
                  value={(draft as Record<string, string | number | boolean>)[key] as string | number}
                  onChange={(e) => setDraft({ ...draft, [key]: type === "number" ? +e.target.value : e.target.value } as Country)}
                  className="h-9"
                />
              </div>
            ))}
            {([
              ["allowsBilling", "Allows billing"],
              ["allowsShipping", "Allows shipping"],
              ["subjectToVat", "Subject to VAT"],
              ["published", "Published"],
            ] as const).map(([key, label]) => (
              <label key={key} className="mt-2 flex items-center gap-2">
                <Checkbox
                  checked={draft[key]}
                  onCheckedChange={(v) => setDraft({ ...draft, [key]: v === true })}
                />
                <span className="text-sm font-medium">{label}</span>
              </label>
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

export default CountriesPage;
