import { useState } from "react";
import { Loader2, Save } from "lucide-react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { CreateVendorRequest, UpdateVendorRequest, VendorResponse, VendorType } from "@/services/adminApi";

type VendorFormValue = CreateVendorRequest & { isActive: boolean };

type VendorFormProps = {
  vendor?: VendorResponse;
  onSubmit: (value: CreateVendorRequest | UpdateVendorRequest) => Promise<void>;
  onCancel: () => void;
};

const TYPES: VendorType[] = ["Supplier", "SubContractor", "Both"];

const initialValue = (vendor?: VendorResponse): VendorFormValue => ({
  vendorCode: vendor?.vendorCode ?? "",
  companyName: vendor?.companyName ?? "",
  vendorType: vendor?.vendorType ?? "Supplier",
  taxCode: vendor?.taxCode ?? "",
  phone: vendor?.phone ?? "",
  email: vendor?.email ?? "",
  address: vendor?.address ?? "",
  contactPerson: vendor?.contactPerson ?? "",
  licenseNo: vendor?.licenseNo ?? "",
  tradeCategory: vendor?.tradeCategory ?? "",
  capabilityFileUrl: vendor?.capabilityFileUrl ?? "",
  driveFolder: vendor?.driveFolder ?? "",
  isActive: vendor?.isActive ?? true,
});

const optional = (value?: string) => value?.trim() || undefined;

export default function VendorForm({ vendor, onSubmit, onCancel }: VendorFormProps) {
  const { t } = useI18n();
  const [form, setForm] = useState<VendorFormValue>(() => initialValue(vendor));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const update = <K extends keyof VendorFormValue>(key: K, value: VendorFormValue[K]) =>
    setForm((current) => ({ ...current, [key]: value }));

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    if (!form.vendorCode.trim() || !form.companyName.trim()) {
      setError(t("proc.vendors.validation.required"));
      return;
    }

    setSaving(true);
    try {
      const request = {
        vendorCode: form.vendorCode.trim(),
        companyName: form.companyName.trim(),
        vendorType: form.vendorType,
        taxCode: optional(form.taxCode),
        phone: optional(form.phone),
        email: optional(form.email),
        address: optional(form.address),
        contactPerson: optional(form.contactPerson),
        licenseNo: optional(form.licenseNo),
        tradeCategory: optional(form.tradeCategory),
        capabilityFileUrl: optional(form.capabilityFileUrl),
        driveFolder: optional(form.driveFolder),
        ...(vendor ? { isActive: form.isActive } : {}),
      };
      await onSubmit(request);
    } catch (submitError) {
      setError(extractApiError(submitError));
    } finally {
      setSaving(false);
    }
  };

  const field = (key: keyof VendorFormValue, labelKey: string, props: React.ComponentProps<typeof Input> = {}) => (
    <div className="space-y-2">
      <Label htmlFor={`vendor-${key}`}>{t(labelKey)}</Label>
      <Input
        id={`vendor-${key}`}
        value={String(form[key] ?? "")}
        onChange={(event) => update(key, event.target.value as never)}
        disabled={saving}
        {...props}
      />
    </div>
  );

  return (
    <form onSubmit={submit} className="space-y-5">
      {error && <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">{error}</p>}
      <div className="grid gap-4 sm:grid-cols-2">
        {field("vendorCode", "proc.vendors.field.code", { required: true, maxLength: 50 })}
        {field("companyName", "proc.vendors.field.companyName", { required: true, maxLength: 300 })}
        <div className="space-y-2">
          <Label>{t("proc.vendors.field.type")}</Label>
          <Select value={form.vendorType} onValueChange={(value: VendorType) => update("vendorType", value)} disabled={saving}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {TYPES.map((type) => <SelectItem key={type} value={type}>{t(`proc.vendors.type.${type}`)}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        {field("taxCode", "proc.vendors.field.taxCode", { maxLength: 20 })}
        {field("contactPerson", "proc.vendors.field.contactPerson", { maxLength: 150 })}
        {field("phone", "proc.vendors.field.phone", { maxLength: 20 })}
        {field("email", "proc.vendors.field.email", { type: "email", maxLength: 200 })}
        {field("licenseNo", "proc.vendors.field.licenseNo", { maxLength: 100 })}
        {field("tradeCategory", "proc.vendors.field.tradeCategory", { maxLength: 300 })}
        {field("address", "proc.vendors.field.address", { maxLength: 500 })}
        <div className="space-y-2">
          <Label htmlFor="vendor-capabilityFileUrl">{t("proc.vendors.field.capabilityFileUrl")}</Label>
          <div className="flex items-center gap-2">
            <Input id="vendor-capabilityFileUrl" value={form.capabilityFileUrl ?? ""} onChange={(event) => update("capabilityFileUrl", event.target.value)} disabled={saving} maxLength={1000} />
            {form.capabilityFileUrl?.trim() && <AdminFilePreview url={form.capabilityFileUrl} />}
          </div>
        </div>
        {field("driveFolder", "proc.vendors.field.driveFolder", { maxLength: 1000 })}
      </div>
      {vendor && (
        <div className="flex items-center justify-between rounded-md border px-3 py-3">
          <div>
            <Label htmlFor="vendor-active">{t("proc.vendors.field.active")}</Label>
            <p className="text-xs text-muted-foreground">{t("proc.vendors.activeHelp")}</p>
          </div>
          <Switch id="vendor-active" checked={form.isActive} onCheckedChange={(checked) => update("isActive", checked)} disabled={saving} />
        </div>
      )}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={saving}>{t("common.cancel")}</Button>
        <Button type="submit" disabled={saving} className="gap-2">
          {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          {t("common.save")}
        </Button>
      </div>
    </form>
  );
}