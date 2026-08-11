import { useCallback, useEffect, useMemo, useState } from "react";
import { ArrowLeft, Save } from "lucide-react";
import { Link, Navigate, useNavigate, useParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { useAppSelector } from "@/store";
import { adminApi, type MasterDataOption } from "@/services/adminApi";
import { vendorApi, type UpsertVendorRequest, type VendorOwnerOptionResponse, type VendorResponse, type VendorType } from "@/services/vendorApi";

const VENDOR_TYPES: VendorType[] = ["Supplier", "SubContractor", "Both"];

type FormErrors = Partial<Record<keyof UpsertVendorRequest, string>>;

const emptyForm = (ownerUserId: number): UpsertVendorRequest => ({
  vendorCode: "",
  companyName: "",
  vendorType: "Supplier",
  taxCode: "",
  phone: "",
  email: "",
  address: "",
  contactPerson: "",
  licenseNo: "",
  serviceGroupCode: "",
  ownerUserId,
  isActive: true,
});

const VendorFormPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const navigate = useNavigate();
  const { id: idParam } = useParams<{ id: string }>();
  const currentUser = useAppSelector((state) => state.auth.user);
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.vendorsManage);
  const canSeeAll = has(ADMIN_PERMS.vendorsViewAll);
  const isEdit = idParam != null;
  const vendorId = Number(idParam);
  const validId = !isEdit || (Number.isInteger(vendorId) && vendorId > 0);

  const [form, setForm] = useState<UpsertVendorRequest>(() => emptyForm(currentUser?.userId ?? 0));
  const [vendor, setVendor] = useState<VendorResponse | null>(null);
  const [serviceGroups, setServiceGroups] = useState<MasterDataOption[]>([]);
  const [owners, setOwners] = useState<VendorOwnerOptionResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [errors, setErrors] = useState<FormErrors>({});

  const loadData = useCallback(async () => {
    if (!validId || !currentUser) return;
    setLoading(true);
    setLoadError(null);
    try {
      const [groupsResponse, vendorResponse, usersResponse] = await Promise.all([
        adminApi.getMasterDataOptions("vendor_service_group"),
        isEdit ? vendorApi.get(vendorId) : Promise.resolve(null),
        canSeeAll ? vendorApi.ownerOptions() : Promise.resolve(null),
      ]);
      setServiceGroups(groupsResponse.data.filter((item) => item.isActive));
      setOwners(usersResponse?.data ?? []);
      if (vendorResponse) {
        const data = vendorResponse.data;
        setVendor(data);
        setForm({
          vendorCode: data.vendorCode,
          companyName: data.companyName,
          vendorType: data.vendorType,
          taxCode: data.taxCode ?? "",
          phone: data.phone ?? "",
          email: data.email ?? "",
          address: data.address ?? "",
          contactPerson: data.contactPerson ?? "",
          licenseNo: data.licenseNo ?? "",
          serviceGroupCode: data.serviceGroupCode,
          ownerUserId: canSeeAll ? data.ownerUserId : currentUser.userId,
          isActive: data.isActive,
        });
      } else {
        setForm(emptyForm(currentUser.userId));
      }
    } catch (err) {
      const status = (err as { response?: { status?: number } }).response?.status;
      setLoadError(status === 404 ? t("vendors.notFound") : extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [canSeeAll, currentUser, isEdit, t, validId, vendorId]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const serviceGroupOptions = useMemo(() => serviceGroups.map((item) => {
    const translated = item.labelKey ? t(item.labelKey) : "";
    return { value: item.code, label: translated && translated !== item.labelKey ? translated : item.name };
  }), [serviceGroups, t]);

  const ownerOptions = useMemo(() => {
    if (!currentUser) return [];
    if (!canSeeAll) return [{ value: String(currentUser.userId), label: currentUser.fullName }];
    const options = owners.map((owner) => ({
      value: String(owner.id),
      label: owner.fullName || owner.email || owner.phoneNumber,
      hint: owner.email || owner.phoneNumber,
    }));
    if (vendor && !options.some((option) => option.value === String(vendor.ownerUserId))) {
      options.push({ value: String(vendor.ownerUserId), label: vendor.ownerName, hint: "" });
    }
    return options;
  }, [canSeeAll, currentUser, owners, vendor]);

  const updateField = <K extends keyof UpsertVendorRequest>(field: K, value: UpsertVendorRequest[K]) => {
    setForm((current) => ({ ...current, [field]: value }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  };

  const validate = () => {
    const next: FormErrors = {};
    if (!form.vendorCode.trim()) next.vendorCode = t("vendors.validation.vendorCodeRequired");
    if (!form.companyName.trim()) next.companyName = t("vendors.validation.companyNameRequired");
    if (!form.serviceGroupCode) next.serviceGroupCode = t("vendors.validation.serviceGroupRequired");
    if (!form.ownerUserId) next.ownerUserId = t("vendors.validation.ownerRequired");
    if (form.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) next.email = t("vendors.validation.emailInvalid");
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setSubmitError(null);
    if (!validate()) return;
    setSaving(true);
    const payload: UpsertVendorRequest = {
      ...form,
      vendorCode: form.vendorCode.trim(),
      companyName: form.companyName.trim(),
      taxCode: form.taxCode?.trim() || null,
      phone: form.phone?.trim() || null,
      email: form.email?.trim() || null,
      address: form.address?.trim() || null,
      contactPerson: form.contactPerson?.trim() || null,
      licenseNo: form.licenseNo?.trim() || null,
      ownerUserId: canSeeAll ? form.ownerUserId : currentUser?.userId ?? form.ownerUserId,
    };
    try {
      const { data } = isEdit ? await vendorApi.update(vendorId, payload) : await vendorApi.create(payload);
      toast({ title: t(isEdit ? "vendors.toast.updated" : "vendors.toast.created") });
      navigate(`/admin/procurement/vendors/${data.id}`);
    } catch (err) {
      setSubmitError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  if (!canManage) return <Navigate to="/forbidden" replace />;
  if (!validId) return <AdminLayout><div className="p-4"><PageError message={t("vendors.notFound")} /></div></AdminLayout>;
  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (loadError) return <AdminLayout><div className="p-4"><PageError message={loadError} onRetry={() => void loadData()} /></div></AdminLayout>;

  const fieldError = (field: keyof UpsertVendorRequest) => errors[field] ? <p className="text-xs text-destructive">{errors[field]}</p> : null;

  return (
    <AdminLayout>
      <div className="mx-auto max-w-5xl space-y-4 p-3 md:p-4" data-testid="vendor-form-page">
        <Link to={isEdit ? `/admin/procurement/vendors/${vendorId}` : "/admin/procurement/vendors"} className="inline-flex items-center gap-1 text-sm text-slate-600 hover:text-slate-900"><ArrowLeft className="h-4 w-4" />{t("common.back")}</Link>
        <header>
          <h1 className="text-xl font-bold text-slate-900 md:text-2xl">{t(isEdit ? "vendors.form.editTitle" : "vendors.form.createTitle")}</h1>
          <p className="mt-1 text-sm text-slate-600">{t("vendors.form.subtitle")}</p>
        </header>

        <form onSubmit={submit} className="space-y-4">
          <section className="rounded-lg border bg-white p-4 shadow-sm">
            <h2 className="mb-4 font-semibold">{t("vendors.form.section.identity")}</h2>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1"><Label htmlFor="vendor-code">{t("vendors.field.vendorCode")} *</Label><Input id="vendor-code" maxLength={50} value={form.vendorCode} onChange={(event) => updateField("vendorCode", event.target.value)} aria-invalid={Boolean(errors.vendorCode)} />{fieldError("vendorCode")}</div>
              <div className="space-y-1"><Label htmlFor="company-name">{t("vendors.field.companyName")} *</Label><Input id="company-name" maxLength={300} value={form.companyName} onChange={(event) => updateField("companyName", event.target.value)} aria-invalid={Boolean(errors.companyName)} />{fieldError("companyName")}</div>
              <div className="space-y-1"><Label>{t("vendors.field.vendorType")} *</Label><Select value={form.vendorType} onValueChange={(value) => updateField("vendorType", value as VendorType)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{VENDOR_TYPES.map((value) => <SelectItem key={value} value={value}>{t(`vendors.type.${value}`)}</SelectItem>)}</SelectContent></Select></div>
              <div className="space-y-1"><Label>{t("vendors.field.serviceGroup")} *</Label><SearchableSelect value={form.serviceGroupCode || null} onChange={(value) => updateField("serviceGroupCode", value)} options={serviceGroupOptions} placeholder={t("vendors.form.selectServiceGroup")} searchPlaceholder={t("vendors.form.searchServiceGroup")} emptyText={t("vendors.form.noServiceGroup")} />{fieldError("serviceGroupCode")}</div>
              <div className="space-y-1"><Label>{t("vendors.field.owner")} *</Label><SearchableSelect value={form.ownerUserId ? String(form.ownerUserId) : null} onChange={(value) => updateField("ownerUserId", Number(value))} options={ownerOptions} disabled={!canSeeAll} placeholder={t("vendors.form.selectOwner")} searchPlaceholder={t("vendors.form.searchOwner")} emptyText={t("vendors.form.noOwner")} />{!canSeeAll ? <p className="text-xs text-slate-500">{t("vendors.form.scopedOwnerHelp")}</p> : null}{fieldError("ownerUserId")}</div>
              <div className="flex items-center justify-between rounded-md border p-3"><div><Label htmlFor="vendor-active">{t("vendors.field.status")}</Label><p className="text-xs text-slate-500">{t("vendors.form.activeHelp")}</p></div><Switch id="vendor-active" checked={form.isActive} onCheckedChange={(value) => updateField("isActive", value)} aria-label={t("vendors.form.activeToggleLabel")} /></div>
            </div>
          </section>

          <section className="rounded-lg border bg-white p-4 shadow-sm">
            <h2 className="mb-4 font-semibold">{t("vendors.form.section.contact")}</h2>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1"><Label htmlFor="contact-person">{t("vendors.field.contactPerson")}</Label><Input id="contact-person" maxLength={150} value={form.contactPerson ?? ""} onChange={(event) => updateField("contactPerson", event.target.value)} /></div>
              <div className="space-y-1"><Label htmlFor="phone">{t("vendors.field.phone")}</Label><Input id="phone" type="tel" maxLength={30} value={form.phone ?? ""} onChange={(event) => updateField("phone", event.target.value)} /></div>
              <div className="space-y-1"><Label htmlFor="email">{t("vendors.field.email")}</Label><Input id="email" type="email" maxLength={200} value={form.email ?? ""} onChange={(event) => updateField("email", event.target.value)} aria-invalid={Boolean(errors.email)} />{fieldError("email")}</div>
              <div className="space-y-1 md:col-span-2"><Label htmlFor="address">{t("vendors.field.address")}</Label><Textarea id="address" maxLength={500} value={form.address ?? ""} onChange={(event) => updateField("address", event.target.value)} /></div>
            </div>
          </section>

          <section className="rounded-lg border bg-white p-4 shadow-sm">
            <h2 className="mb-4 font-semibold">{t("vendors.form.section.compliance")}</h2>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1"><Label htmlFor="tax-code">{t("vendors.field.taxCode")}</Label><Input id="tax-code" maxLength={20} value={form.taxCode ?? ""} onChange={(event) => updateField("taxCode", event.target.value)} /></div>
              <div className="space-y-1"><Label htmlFor="license-no">{t("vendors.field.licenseNo")}</Label><Input id="license-no" maxLength={100} value={form.licenseNo ?? ""} onChange={(event) => updateField("licenseNo", event.target.value)} /></div>
            </div>
          </section>

          {submitError ? <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{submitError}</div> : null}
          <div className="flex flex-wrap justify-end gap-2"><Button type="button" variant="outline" onClick={() => navigate(isEdit ? `/admin/procurement/vendors/${vendorId}` : "/admin/procurement/vendors")}>{t("common.cancel")}</Button><Button type="submit" disabled={saving}><Save className="mr-2 h-4 w-4" />{t(saving ? "vendors.form.saving" : "common.save")}</Button></div>
        </form>
      </div>
    </AdminLayout>
  );
};

export default VendorFormPage;
