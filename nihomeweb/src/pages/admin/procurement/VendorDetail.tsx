import { useCallback, useEffect, useState } from "react";
import { ArrowLeft, ExternalLink, Pencil, Trash2 } from "lucide-react";
import { Link, useNavigate, useParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { isManagedDocumentPath, resolveSafeLinkUrl } from "@/lib/url";
import { adminApi, type DeletionImpactResponse, type UpdateVendorRequest, type VendorResponse } from "@/services/adminApi";
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
  const [deleteImpact, setDeleteImpact] = useState<DeletionImpactResponse | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [loadingImpact, setLoadingImpact] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    const vendorId = Number(id);
    if (!Number.isInteger(vendorId)) { setError(t("proc.vendors.notFound")); setLoading(false); return; }
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getVendor(vendorId);
      setVendor(data);
    } catch (loadError) {
      setError(extractApiError(loadError) || t("common.error"));
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

  const openDelete = async () => {
    if (!vendor) return;
    setConfirmDelete(true);
    setDeleteImpact(null);
    setDeleteError(null);
    setLoadingImpact(true);
    try {
      setDeleteImpact((await adminApi.getVendorDeletionImpact(vendor.id)).data);
    } catch (impactError) {
      setDeleteError(extractApiError(impactError) || t("common.error"));
    } finally {
      setLoadingImpact(false);
    }
  };

  const deleteVendor = async (confirmation: string) => {
    if (!vendor) return;
    setDeleting(true);
    setDeleteError(null);
    try {
      return (await adminApi.deleteVendor(vendor.id, {
        planToken: deleteImpact!.planToken,
        confirmation,
        rowVersion: vendor.rowVersion,
      })).data;
    } catch (deleteFailure) {
      setDeleteError(extractApiError(deleteFailure) || t("common.error"));
      throw deleteFailure;
    } finally {
      setDeleting(false);
    }
  };

  const deleteCompleted = () => {
    toast({ title: t("proc.vendors.deleted") });
    navigate("/admin/vendors");
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
            <div className="flex flex-col gap-3 border-b pb-5 sm:flex-row sm:items-start sm:justify-between"><div><div className="flex flex-wrap items-center gap-2"><h1 className="text-2xl font-semibold">{vendor.companyName}</h1><Badge variant={vendor.isActive ? "default" : "secondary"}>{t(vendor.isActive ? "proc.vendors.status.active" : "proc.vendors.status.inactive")}</Badge></div><p className="mt-1 text-sm text-muted-foreground">{vendor.vendorCode} · {t(`proc.vendors.type.${vendor.vendorType}`)}</p></div>{canManage && <div className="flex gap-2"><Button onClick={() => setEditing(true)} className="gap-2"><Pencil className="h-4 w-4" />{t("common.edit")}</Button><Button variant="outline" onClick={() => void openDelete()} className="gap-2 text-destructive hover:text-destructive"><Trash2 className="h-4 w-4" />{t("common.delete")}</Button></div>}</div>
            {!vendor.isActive && <div className="rounded-md border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">{t("proc.vendors.inactiveWarning")}</div>}
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.company")}</h2><dl className="grid gap-5 rounded-md border bg-card p-5 sm:grid-cols-2 lg:grid-cols-3">{value(t("proc.vendors.field.taxCode"), vendor.taxCode)}{value(t("proc.vendors.field.tradeCategory"), vendor.tradeCategory)}{value(t("proc.vendors.field.licenseNo"), vendor.licenseNo)}{value(t("proc.vendors.field.address"), vendor.address)}</dl></section>
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.contact")}</h2><dl className="grid gap-5 rounded-md border bg-card p-5 sm:grid-cols-2 lg:grid-cols-3">{value(t("proc.vendors.field.contactPerson"), vendor.contactPerson)}{value(t("proc.vendors.field.phone"), vendor.phone)}{value(t("proc.vendors.field.email"), vendor.email)}</dl></section>
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.documents")}</h2><div className="flex flex-wrap gap-2 rounded-md border bg-card p-5">{capabilityUrl ? <AdminFilePreview url={vendor.capabilityFileUrl} showLabel label={t("proc.vendors.openCapability")} fetchFile={isManagedDocumentPath(vendor.capabilityFileUrl ?? "", "/files/business-documents/vendors") ? async () => (await adminApi.getVendorDocumentContent(vendor.id)).data : undefined} /> : <p className="text-sm text-muted-foreground">{t("proc.vendors.noDocuments")}</p>}{folderUrl && <Button asChild variant="outline"><Link to={folderUrl} target="_blank" rel="noopener noreferrer"><ExternalLink className="mr-2 h-4 w-4" />{t("proc.vendors.openFolder")}</Link></Button>}</div></section>
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.related")}</h2><div className="overflow-hidden rounded-md border"><table className="w-full text-sm"><thead className="bg-muted/60 text-left"><tr><th className="px-4 py-3">{t("proc.vendors.related.contract")}</th><th className="px-4 py-3">{t("proc.vendors.related.project")}</th><th className="px-4 py-3">{t("proc.vendors.field.status")}</th></tr></thead><tbody className="divide-y">{vendor.contracts.map((contract) => <tr key={contract.id}><td className="px-4 py-3"><Link className="font-medium text-primary hover:underline" to={`/admin/contracts/${contract.id}`}>{contract.contractNumber}</Link></td><td className="px-4 py-3">{contract.operationalProjectId ? <Link className="text-primary hover:underline" to={`/admin/operational-projects/${contract.operationalProjectId}`}>{contract.operationalProjectCode} · {contract.operationalProjectName}</Link> : t("common.noData")}</td><td className="px-4 py-3">{t(`contracts.status.${contract.status}`)}</td></tr>)}{vendor.contracts.length === 0 && <tr><td colSpan={3} className="px-4 py-6 text-center text-muted-foreground">{t("proc.vendors.related.empty")}</td></tr>}</tbody></table></div><div className="space-y-2">{vendor.ratings.map((rating) => <div key={rating.id} className="flex flex-wrap items-center justify-between gap-2 border-b py-2 text-sm"><span>{rating.contractNumber} · {rating.operationalProjectCode}</span><span>{t(`procurement.status.${rating.status}`)} · {rating.overallScore.toFixed(1)}</span></div>)}</div></section>
            <section className="space-y-3"><h2 className="text-lg font-semibold">{t("proc.vendors.section.history")}</h2><dl className="grid gap-5 rounded-md border bg-card p-5 sm:grid-cols-2 lg:grid-cols-4">{value(t("proc.vendors.field.createdBy"), vendor.createdByName)}{value(t("proc.vendors.field.createdAt"), new Date(vendor.createdAt).toLocaleString())}{value(t("proc.vendors.field.updatedBy"), vendor.updatedByName)}{value(t("proc.vendors.field.updatedAt"), new Date(vendor.updatedAt).toLocaleString())}</dl><div className="divide-y rounded-md border">{vendor.history.map((item) => <div key={item.id} className="p-3 text-sm"><div className="flex flex-wrap justify-between gap-2"><span className="font-medium">{item.actorName || t("common.noData")}</span><time className="text-muted-foreground">{new Date(item.createdAt).toLocaleString()}</time></div><p className="mt-1 text-muted-foreground">{t(`proc.vendors.history.action.${item.action}`)}</p></div>)}{vendor.history.length === 0 && <p className="p-4 text-sm text-muted-foreground">{t("proc.vendors.history.empty")}</p>}</div></section>
          </>
        )}
      </div>
      <Dialog open={editing} onOpenChange={setEditing}><DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto"><DialogHeader><DialogTitle>{t("proc.vendors.editTitle")}</DialogTitle><DialogDescription>{t("proc.vendors.formDescription")}</DialogDescription></DialogHeader>{vendor && <VendorForm vendor={vendor} onSubmit={(request) => update(request as UpdateVendorRequest)} onCancel={() => setEditing(false)} />}</DialogContent></Dialog>
      <DeletionImpactDialog open={confirmDelete} impact={deleteImpact} loading={loadingImpact} deleting={deleting} error={deleteError} onOpenChange={(open) => { if (!deleting) setConfirmDelete(open); }} onConfirm={deleteVendor} onCompleted={deleteCompleted} />
    </AdminLayout>
  );
}