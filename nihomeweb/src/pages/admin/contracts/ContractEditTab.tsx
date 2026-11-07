import { useI18n } from "@/lib/i18n";
import { formatVndWithSymbol } from "@/lib/numberFormat";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  type ContractClassificationOptions,
  type ContractDirection,
  type ContractType,
  type CustomerResponse,
  type OperationalProjectListItemResponse,
} from "@/services/adminApi";
import { ContractEditForm } from "./contractDetailShared";

export interface EditInfoTabProps {
  form: ContractEditForm;
  customers: CustomerResponse[];
  projects: OperationalProjectListItemResponse[];
  classification: ContractClassificationOptions | null;
  lockDirection: boolean;
  error: string | null;
  onChange: (next: ContractEditForm) => void;
}

export const EditInfoTab = ({
  form,
  customers,
  projects,
  classification,
  lockDirection,
  error,
  onChange,
}: EditInfoTabProps) => {
  const { t } = useI18n();

  return (
    <div className="space-y-4 rounded-lg border border-border bg-card p-4 shadow-sm">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-number">{t("contracts.field.number")}</Label>
          <Input
            id="contract-detail-number"
            value={form.contractNumber}
            onChange={(event) => onChange({ ...form, contractNumber: event.target.value })}
            className="font-mono"
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-customer">{t("contracts.field.customer")} *</Label>
          <Select
            value={String(form.customerId)}
            onValueChange={(value) => {
              const customerId = Number(value);
              const customer = customers.find((item) => item.id === customerId);
              onChange({
                ...form,
                customerId,
                operationalProjectId: projects.some(
                  (project) =>
                    project.id === form.operationalProjectId && project.customerId === customerId,
                )
                  ? form.operationalProjectId
                  : null,
                ownerName: customer?.ownerName ?? "",
              });
            }}
          >
            <SelectTrigger id="contract-detail-customer">
              <SelectValue />
            </SelectTrigger>
            <SelectContent className="max-h-72">
              {customers.map((customer) => (
                <SelectItem key={customer.id} value={String(customer.id)}>
                  {customer.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="contract-detail-project">{t("contracts.field.operationalProject")} *</Label>
        <Select
          value={form.operationalProjectId == null ? "" : String(form.operationalProjectId)}
          onValueChange={(value) => onChange({ ...form, operationalProjectId: Number(value) })}
        >
          <SelectTrigger id="contract-detail-project">
            <SelectValue placeholder={t("contracts.selectOperationalProject")} />
          </SelectTrigger>
          <SelectContent className="max-h-72">
            {projects
              .filter((project) => project.customerId === form.customerId)
              .map((project) => (
                <SelectItem key={project.id} value={String(project.id)}>
                  {project.code} · {project.name}
                </SelectItem>
              ))}
          </SelectContent>
        </Select>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-direction">{t("contracts.field.direction")} *</Label>
          <Select
            value={form.direction}
            disabled={lockDirection}
            onValueChange={(value) => {
              const direction = value as ContractDirection;
              const type = classification?.allowedTypes[direction]?.[0];
              if (type) onChange({ ...form, direction, type, vendorId: null });
            }}
          >
            <SelectTrigger id="contract-detail-direction">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(classification?.directions ?? []).map((direction) => (
                <SelectItem key={direction} value={direction}>
                  {t(`contracts.direction.${direction}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-type">{t("contracts.field.type")} *</Label>
          <Select
            value={form.type}
            onValueChange={(value) =>
              onChange({ ...form, type: value as ContractType, vendorId: null })
            }
          >
            <SelectTrigger id="contract-detail-type">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(classification?.allowedTypes[form.direction] ?? []).map((type) => (
                <SelectItem key={type} value={type}>
                  {t(`contracts.type.${type}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {form.direction === "Downstream" ? (
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-vendor">{t("contracts.field.vendor")} *</Label>
          <Select
            value={form.vendorId == null ? "" : String(form.vendorId)}
            onValueChange={(value) => onChange({ ...form, vendorId: Number(value) })}
          >
            <SelectTrigger id="contract-detail-vendor">
              <SelectValue placeholder={t("contracts.selectVendor")} />
            </SelectTrigger>
            <SelectContent className="max-h-72">
              {(classification?.vendors ?? [])
                .filter((vendor) =>
                  form.type === "Supply"
                    ? vendor.vendorType !== "SubContractor"
                    : vendor.vendorType !== "Supplier",
                )
                .map((vendor) => (
                  <SelectItem key={vendor.id} value={String(vendor.id)}>
                    {vendor.vendorCode} · {vendor.companyName}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>
      ) : null}

      <div className="space-y-1.5 rounded-md border bg-muted/30 px-3 py-2.5">
        <span>{t("contracts.field.owner")}</span>
        <p className="text-sm font-medium">
          {form.ownerName || t("contracts.ownerCurrentUserFallback")}
        </p>
        <p className="text-xs text-muted-foreground">{t("contracts.ownerInheritedHint")}</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-signed">{t("contracts.field.signedDate")}</Label>
          <Input
            id="contract-detail-signed"
            type="date"
            value={form.signedDate}
            onChange={(event) => onChange({ ...form, signedDate: event.target.value })}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-start">{t("contracts.field.startDate")}</Label>
          <Input
            id="contract-detail-start"
            type="date"
            value={form.startDate}
            onChange={(event) => onChange({ ...form, startDate: event.target.value })}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-end">{t("contracts.field.endDate")}</Label>
          <Input
            id="contract-detail-end"
            type="date"
            value={form.endDate}
            onChange={(event) => onChange({ ...form, endDate: event.target.value })}
          />
        </div>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="contract-detail-value">{t("contracts.field.value")} *</Label>
        <Input
          id="contract-detail-value"
          type="number"
          min={0}
          value={form.value}
          onChange={(event) => onChange({ ...form, value: Number(event.target.value) || 0 })}
        />
        <p className="text-xs text-muted-foreground">{formatVndWithSymbol(form.value)}</p>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="contract-detail-scope">{t("contracts.field.scope")}</Label>
        <Textarea
          id="contract-detail-scope"
          rows={4}
          value={form.scopeOfWork}
          onChange={(event) => onChange({ ...form, scopeOfWork: event.target.value })}
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="contract-detail-note">{t("contracts.field.note")}</Label>
        <Textarea
          id="contract-detail-note"
          rows={3}
          value={form.note}
          onChange={(event) => onChange({ ...form, note: event.target.value })}
        />
      </div>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
};
