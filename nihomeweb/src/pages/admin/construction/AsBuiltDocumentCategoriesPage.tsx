import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Pencil, Trash2, Search as SearchIcon } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { localizedName } from "@/lib/category";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import {
  adminApi,
  type AsBuiltDocumentCategoryResponse,
  type UpsertAsBuiltDocumentCategoryRequest,
} from "@/services/adminApi";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";

interface FormData {
  code: string;
  nameVi: string;
  nameEn: string;
  nameZh: string;
  nameJa: string;
  isRequired: boolean;
  isActive: boolean;
  sortOrder: number;
}

const emptyForm: FormData = {
  code: "",
  nameVi: "",
  nameEn: "",
  nameZh: "",
  nameJa: "",
  isRequired: false,
  isActive: true,
  sortOrder: 0,
};

const extractError = (error: unknown): string | undefined => {
  if (
    typeof error === "object" &&
    error !== null &&
    "response" in error &&
    typeof error.response === "object" &&
    error.response !== null &&
    "data" in error.response &&
    typeof error.response.data === "object" &&
    error.response.data !== null
  ) {
    const data = error.response.data as { detail?: unknown; message?: unknown };
    if (typeof data.detail === "string") return data.detail;
    if (typeof data.message === "string") return data.message;
  }
  return undefined;
};

export default function AsBuiltDocumentCategoriesPage() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionAsBuiltCategoriesManage);

  const [items, setItems] = useState<AsBuiltDocumentCategoryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [q, setQ] = useState("");

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<FormData>(emptyForm);

  const [pendingDelete, setPendingDelete] = useState<AsBuiltDocumentCategoryResponse | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.getAsBuiltDocumentCategories(true);
      setItems(res.data ?? []);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const filtered = useMemo(() => {
    const search = q.trim().toLowerCase();
    if (!search) return items;
    return items.filter(
      (i) =>
        i.code.toLowerCase().includes(search) ||
        localizedName(i, lang).toLowerCase().includes(search) ||
        (i.nameVi || i.name || "").toLowerCase().includes(search)
    );
  }, [items, q, lang]);

  const openCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm, sortOrder: items.length + 1 });
    setDialogOpen(true);
  };

  const openEdit = (item: AsBuiltDocumentCategoryResponse) => {
    setEditingId(item.id);
    setForm({
      code: item.code,
      nameVi: item.nameVi || item.name,
      nameEn: item.nameEn || "",
      nameZh: item.nameZh || "",
      nameJa: item.nameJa || "",
      isRequired: item.isRequired,
      isActive: item.isActive,
      sortOrder: item.sortOrder,
    });
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditingId(null);
    setForm(emptyForm);
  };

  const submitForm = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.code.trim()) {
      toast({ title: t("asbuiltCat.error.codeRequired"), variant: "destructive" });
      return;
    }
    if (!form.nameVi.trim()) {
      toast({ title: t("asbuiltCat.error.nameRequired"), variant: "destructive" });
      return;
    }

    setSubmitting(true);
    try {
      const payload: UpsertAsBuiltDocumentCategoryRequest = {
        code: form.code.trim(),
        name: form.nameVi.trim(),
        nameVi: form.nameVi.trim(),
        nameEn: form.nameEn.trim() || form.nameVi.trim(),
        nameZh: form.nameZh.trim() || form.nameVi.trim(),
        nameJa: form.nameJa.trim() || form.nameVi.trim(),
        isRequired: form.isRequired,
        isActive: form.isActive,
        sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      };

      if (editingId == null) {
        await adminApi.createAsBuiltDocumentCategory(payload);
        toast({ title: t("form.created") });
      } else {
        await adminApi.updateAsBuiltDocumentCategory(editingId, payload);
        toast({ title: t("form.saved") });
      }
      closeDialog();
      await loadData();
    } catch (err) {
      toast({
        title: extractError(err) || t("common.error"),
        variant: "destructive",
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async () => {
    if (!pendingDelete) return;
    const id = pendingDelete.id;
    setPendingDelete(null);
    try {
      await adminApi.deleteAsBuiltDocumentCategory(id);
      toast({ title: t("form.deleted") });
      await loadData();
    } catch (err) {
      toast({
        title: extractError(err) || t("common.error"),
        variant: "destructive",
      });
    }
  };

  return (
    <AdminLayout>
      <div className="flex flex-1 flex-col gap-4 p-4 md:p-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-xl font-semibold md:text-2xl">
            {t("asbuiltCat.title")}
          </h1>
          <div className="flex items-center gap-2">
            <div className="relative">
              <SearchIcon className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder={t("common.search")}
                value={q}
                onChange={(e) => setQ(e.target.value)}
                className="w-48 pl-8"
              />
            </div>
            {canManage && (
              <Button size="sm" onClick={openCreate}>
                <Plus className="mr-2 h-4 w-4" />
                {t("asbuiltCat.action.new")}
              </Button>
            )}
          </div>
        </header>

        {loading ? (
          <div className="flex flex-1 items-center justify-center">
            <span className="text-muted-foreground">{t("common.loading")}</span>
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex flex-1 items-center justify-center">
            <span className="text-muted-foreground">{t("common.noData")}</span>
          </div>
        ) : (
          <div className="rounded-lg border bg-card">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-24">{t("asbuiltCat.field.code")}</TableHead>
                  <TableHead>{t("asbuiltCat.field.name")}</TableHead>
                  <TableHead className="w-24 text-center">{t("asbuiltCat.field.isRequired")}</TableHead>
                  <TableHead className="w-24 text-center">{t("asbuiltCat.field.isActive")}</TableHead>
                  <TableHead className="w-20 text-center">{t("asbuiltCat.field.sortOrder")}</TableHead>
                  {canManage && <TableHead className="w-24" />}
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((item) => (
                  <TableRow key={item.id} className={!item.isActive ? "opacity-50" : ""}>
                    <TableCell className="font-mono text-sm">{item.code}</TableCell>
                    <TableCell>{localizedName(item, lang)}</TableCell>
                    <TableCell className="text-center">
                      {item.isRequired ? (
                        <Badge variant="default">{t("common.yes")}</Badge>
                      ) : (
                        <Badge variant="secondary">{t("common.no")}</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-center">
                      {item.isActive ? (
                        <Badge variant="default">{t("common.active")}</Badge>
                      ) : (
                        <Badge variant="outline">{t("common.inactive")}</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-center">{item.sortOrder}</TableCell>
                    {canManage && (
                      <TableCell>
                        <div className="flex items-center justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => openEdit(item)}
                            title={t("common.edit")}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => setPendingDelete(item)}
                            title={t("common.delete")}
                          >
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </div>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>

      {/* Create/Edit Dialog */}
      <Dialog open={dialogOpen} onOpenChange={(open) => !open && closeDialog()}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>
              {editingId ? t("asbuiltCat.form.titleEdit") : t("asbuiltCat.form.titleCreate")}
            </DialogTitle>
          </DialogHeader>
          <form onSubmit={submitForm} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="code">{t("asbuiltCat.field.code")} *</Label>
              <Input
                id="code"
                value={form.code}
                onChange={(e) => setForm({ ...form, code: e.target.value })}
                placeholder="Drawing"
                disabled={editingId != null}
              />
              {editingId != null && (
                <p className="text-xs text-muted-foreground">{t("asbuiltCat.hint.codeReadonly")}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="nameVi">{t("asbuiltCat.field.nameVi")} *</Label>
              <Input
                id="nameVi"
                value={form.nameVi}
                onChange={(e) => setForm({ ...form, nameVi: e.target.value })}
                placeholder="Bản vẽ hoàn công"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="nameEn">{t("asbuiltCat.field.nameEn")}</Label>
              <Input
                id="nameEn"
                value={form.nameEn}
                onChange={(e) => setForm({ ...form, nameEn: e.target.value })}
                placeholder="As-built drawings"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="nameZh">{t("asbuiltCat.field.nameZh")}</Label>
              <Input
                id="nameZh"
                value={form.nameZh}
                onChange={(e) => setForm({ ...form, nameZh: e.target.value })}
                placeholder="竣工图纸"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="nameJa">{t("asbuiltCat.field.nameJa")}</Label>
              <Input
                id="nameJa"
                value={form.nameJa}
                onChange={(e) => setForm({ ...form, nameJa: e.target.value })}
                placeholder="竣工図面"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="sortOrder">{t("asbuiltCat.field.sortOrder")}</Label>
              <Input
                id="sortOrder"
                type="number"
                value={form.sortOrder}
                onChange={(e) => setForm({ ...form, sortOrder: parseInt(e.target.value) || 0 })}
              />
            </div>
            <div className="flex items-center gap-6">
              <div className="flex items-center gap-2">
                <Switch
                  id="isRequired"
                  checked={form.isRequired}
                  onCheckedChange={(v) => setForm({ ...form, isRequired: v })}
                />
                <Label htmlFor="isRequired">{t("asbuiltCat.field.isRequired")}</Label>
              </div>
              <div className="flex items-center gap-2">
                <Switch
                  id="isActive"
                  checked={form.isActive}
                  onCheckedChange={(v) => setForm({ ...form, isActive: v })}
                />
                <Label htmlFor="isActive">{t("asbuiltCat.field.isActive")}</Label>
              </div>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={closeDialog} disabled={submitting}>
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={submitting}>
                {submitting ? t("common.saving") : t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation */}
      <AlertDialog open={!!pendingDelete} onOpenChange={(open) => !open && setPendingDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("asbuiltCat.delete.title")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("asbuiltCat.delete.confirm").replace("{name}", pendingDelete?.nameVi || pendingDelete?.name || "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">
              {t("common.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </AdminLayout>
  );
}
