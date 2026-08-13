import { useCallback, useEffect, useState } from "react";
import { ArrowLeft, ExternalLink, Pencil, Trash2 } from "lucide-react";
import { Link, useNavigate, useParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { resolveSafeLinkUrl } from "@/lib/url";
import { adminApi, type UpdateVendorRequest, type VendorResponse } from "@/services/adminApi";
import VendorForm from "./VendorForm";

export default function VendorDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const canManage = has(ADMIN_PERMS.vendorsManage);
  const [vendor, setVendor] = useState<VendorResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    const vendorId = Number(id);
    if (!Number.isInteger(vendorId)) { setError(t("proc.vendors.notFound")); setLoading(false); return; }
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getVendor(vendorId);
      setVendor(data);
    } catch {
      setError(t("proc.vendors.notFound"));
    } finally {
      setLoading(false);
    }
  }, [id, t]);

  useEffect(() => { void load(); }, [load]);

  const update = async (request: UpdateVendorRequest) => {
    if (!vendor) return;
    const { data } = await adminApi.updateVendor(vendor.id, request);
    setVendor(data);
    setEditing(false);
    toast({ title: t("proc.vendors.updated") });
  };

  const deleteVendor = async () => {
    if (!vendor) return;
    setDeleting(true);
    try {
      await adminApi.deleteVendor(vendor.id);
      toast({ title: t("proc.vendors.deleted") });
      navigate("/admin/vendors");
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
      setDeleting(false);
    }
  };

  const value = (label: string, content?: string | null) => <div><dt className="text-xs font-medium uppercase text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-sm">{content || t("common.noData")}</dd></div>;
  const capabilityUrl = vendor?.capabilityFileUrl ? resolveSafeLinkUrl(vendor.capabilityFileUrl) : undefined;
  const folderUrl = vendor?.driveFolder ? resolveSafeLinkUrl(vendor.driveFolder) : undefined;

  return (
    <AdminLayout>
      <div className="space-y-5 p-4 sm:p-6">
        <Button variant="ghost" className="gap-2 px-0" onClick={() => navigate("/admin/vendors")}><ArrowLeft className="h-4 w-4" />{t("proc.vendors.backToList")}</Button>
        {loading ? <PageLoading /> : error || !vendor ? <PageError message={error || t("proc.vendors.notFound")} onRetry={() => void load()} /> : (
          <>
            <div className="flex flex-col gap-3 border-b pb-5 sm:flex-row sm:items-start sm:justify-between"><div><div className="flex flex-wrap items-center gap-2"><h1 className="text-2xl font-semibold">{vendor.companyName}</h1><Badge variant={vendor.isActive ? "default" : "secondary"}>{t(vendor.isActive ? "proc.vendors.status.active" : "proc.vendors.status.inactive")}</Badge></div><p className="mt-1 text-sm text-muted-foreground">{vendor.vendorCode} · {t(`proc.vendors.type.${vendor.vendorType}`)}</p></div>{canManage && <div className="flex gap-2"><Button onClick={() => setEditing(true)} className="gap-2"><Pencil className="h-4 w-4" />{t("common.edit")}</Button><Button variant="outline" onClick={() => setConfirmDelete(true)} className="gap-2 text-destructive hover:text-destructive"><Trash2 className="h-4 w-4" />{t("common.delete")}</Button></div>}</div>
            {!vendor.isActive && <div className="rounded-md border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">{t("proc.vendors.inactiveWarning")}</div>}
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.company")}</h2><dl className="grid gap-5 rounded-md border bg-card p-5 sm:grid-cols-2 lg:grid-cols-3">{value(t("proc.vendors.field.taxCode"), vendor.taxCode)}{value(t("proc.vendors.field.tradeCategory"), vendor.tradeCategory)}{value(t("proc.vendors.field.licenseNo"), vendor.licenseNo)}{value(t("proc.vendors.field.address"), vendor.address)}</dl></section>
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.contact")}</h2><dl className="grid gap-5 rounded-md border bg-card p-5 sm:grid-cols-2 lg:grid-cols-3">{value(t("proc.vendors.field.contactPerson"), vendor.contactPerson)}{value(t("proc.vendors.field.phone"), vendor.phone)}{value(t("proc.vendors.field.email"), vendor.email)}</dl></section>
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.documents")}</h2><div className="flex flex-wrap gap-2 rounded-md border bg-card p-5">{capabilityUrl ? <AdminFilePreview url={vendor.capabilityFileUrl} showLabel label={t("proc.vendors.openCapability")} /> : <p className="text-sm text-muted-foreground">{t("proc.vendors.noDocuments")}</p>}{folderUrl && <Button asChild variant="outline"><Link to={folderUrl} target="_blank" rel="noopener noreferrer"><ExternalLink className="mr-2 h-4 w-4" />{t("proc.vendors.openFolder")}</Link></Button>}</div></section>
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.history")}</h2><dl className="grid gap-5 rounded-md border bg-card p-5 sm:grid-cols-2 lg:grid-cols-3">{value(t("proc.vendors.field.createdBy"), vendor.createdByName)}{value(t("proc.vendors.field.createdAt"), new Date(vendor.createdAt).toLocaleString())}{value(t("proc.vendors.field.updatedAt"), new Date(vendor.updatedAt).toLocaleString())}</dl></section>
          </>
        )}
      </div>
      <Dialog open={editing} onOpenChange={setEditing}><DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto"><DialogHeader><DialogTitle>{t("proc.vendors.editTitle")}</DialogTitle><DialogDescription>{t("proc.vendors.formDescription")}</DialogDescription></DialogHeader>{vendor && <VendorForm vendor={vendor} onSubmit={(request) => update(request as UpdateVendorRequest)} onCancel={() => setEditing(false)} />}</DialogContent></Dialog>
      <AlertDialog open={confirmDelete} onOpenChange={(open) => { if (!deleting) setConfirmDelete(open); }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{t("proc.vendors.deleteTitle")}</AlertDialogTitle><AlertDialogDescription>{t("proc.vendors.deleteDescription").replace("{name}", vendor?.companyName ?? "")}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel disabled={deleting}>{t("common.cancel")}</AlertDialogCancel><AlertDialogAction disabled={deleting} onClick={(event) => { event.preventDefault(); void deleteVendor(); }} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">{deleting ? t("proc.vendors.deleting") : t("common.delete")}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </AdminLayout>
  );
}