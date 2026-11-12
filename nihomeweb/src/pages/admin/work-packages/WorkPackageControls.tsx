import { newPriceLine } from "./workPackageFormatting";
import { FieldScope } from "@/components/ui/field-scope";
import { Link } from "react-router-dom";
import { Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { MoneyInput } from "@/components/ui/money-input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  type DepartmentWorkPackageOptionsResponse,
  type UpsertInputPriceOfferRequest,
} from "@/services/adminApi";
import { formatPriceNumber, money } from "./workPackageFormatting";

export const Field = ({
  label,
  children,
  className = "",
  htmlFor,
  required = false,
  error,
  onBlur,
}: {
  label: string;
  children: React.ReactNode;
  className?: string;
  htmlFor?: string;
  required?: boolean;
  error?: string;
  onBlur?: () => void;
}) => (
  <FieldScope className={`min-w-0 space-y-2 ${className}`} onBlur={onBlur}>
    <Label htmlFor={htmlFor}>
      {label}
      {required && (
        <span aria-hidden="true" className="ml-1 text-destructive">
          *
        </span>
      )}
    </Label>
    {children}
    {error && (
      <p id={`${htmlFor}-error`} role="alert" className="text-sm text-destructive">
        {error}
      </p>
    )}
  </FieldScope>
);

export const ChoiceGrid = ({
  items,
  selected,
  onChange,
  emptyLabel,
}: {
  items: { id: number; label: string }[];
  selected: number[];
  onChange: (ids: number[]) => void;
  emptyLabel: string;
}) =>
  items.length === 0 ? (
    <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
      {emptyLabel}
    </p>
  ) : (
    <div className="grid max-h-36 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">
      {items.map((item) => (
        <label key={item.id} className="flex cursor-pointer items-start gap-2 text-sm">
          <Checkbox
            checked={selected.includes(item.id)}
            onCheckedChange={(checked) =>
              onChange(checked ? [...selected, item.id] : selected.filter((id) => id !== item.id))
            }
          />
          <span>{item.label}</span>
        </label>
      ))}
    </div>
  );

export const ReviewCheck = ({
  label,
  checked,
  disabled,
  onChange,
}: {
  label: string;
  checked: boolean;
  disabled: boolean;
  onChange: (checked: boolean) => void;
}) => (
  <label className="flex items-start gap-3 rounded-lg border p-3 text-sm">
    <Checkbox
      checked={checked}
      disabled={disabled}
      onCheckedChange={(value) => onChange(value === true)}
    />
    <span>{label}</span>
  </label>
);

export type InputPriceLine = UpsertInputPriceOfferRequest["lines"][number];

export const InputPriceNumberField = ({
  id,
  label,
  value,
  min,
  max,
  onChange,
}: {
  id: string;
  label: string;
  value: number;
  min: number;
  max: number;
  onChange: (value: number) => void;
}) => {
  const invalid = !Number.isFinite(value) || value < min || value > max;
  return (
    <Field label={label} htmlFor={id}>
      <MoneyInput
        id={id}
        value={value}
        decimalScale={4}
        formatValue={formatPriceNumber}
        preferDecimalComma
        aria-invalid={invalid || undefined}
        aria-describedby={invalid ? `${id}-error` : undefined}
        onChange={onChange}
      />
      {invalid && (
        <p id={`${id}-error`} role="alert" className="break-words text-xs text-destructive">
          {formatPriceNumber(min)} ≤ {label} ≤ {formatPriceNumber(max)}
        </p>
      )}
    </Field>
  );
};

export const InputPriceLineEditor = ({
  line,
  index,
  currency,
  canRemove,
  onChange,
  onRemove,
  t,
}: {
  line: InputPriceLine;
  index: number;
  currency: string;
  canRemove: boolean;
  onChange: (patch: Partial<InputPriceLine>) => void;
  onRemove: () => void;
  t: (key: string) => string;
}) => {
  const id = (field: string) => `input-price-${index}-${field}`;
  return (
    <fieldset
      className="min-w-0 rounded-xl border bg-card p-4"
      data-testid={`input-price-line-${index}`}
    >
      <legend className="px-2 text-sm font-semibold">
        {t("workPackages.inputPrice.item")} {index + 1}
      </legend>
      <div className="grid min-w-0 gap-4 sm:grid-cols-2 xl:grid-cols-[minmax(0,3fr)_minmax(5rem,0.75fr)_minmax(7rem,1fr)_minmax(9rem,1.25fr)_minmax(10rem,1.25fr)]">
        <div className="min-w-0 space-y-4 sm:col-span-2 xl:col-span-1">
          <Field label={t("workPackages.inputPrice.itemCode")} htmlFor={id("code")}>
            <Input
              id={id("code")}
              maxLength={120}
              placeholder={t("workPackages.inputPrice.itemCode")}
              value={line.itemCode}
              onChange={(event) => onChange({ itemCode: event.target.value })}
            />
          </Field>
          <Field label={t("workPackages.inputPrice.description")} htmlFor={id("description")}>
            <Textarea
              id={id("description")}
              maxLength={500}
              className="min-h-20"
              placeholder={t("workPackages.inputPrice.description")}
              value={line.description}
              onChange={(event) => onChange({ description: event.target.value })}
            />
          </Field>
        </div>
        <Field label={t("workPackages.inputPrice.unit")} htmlFor={id("unit")}>
          <Input
            id={id("unit")}
            maxLength={30}
            placeholder={t("workPackages.inputPrice.unit")}
            value={line.unit}
            onChange={(event) => onChange({ unit: event.target.value })}
          />
        </Field>
        <InputPriceNumberField
          label={t("workPackages.inputPrice.quantity")}
          id={id("quantity")}
          value={line.quantity}
          min={0.0001}
          max={999999999999}
          onChange={(quantity) => onChange({ quantity })}
        />
        <div className="min-w-0 space-y-4">
          <InputPriceNumberField
            label={t("workPackages.inputPrice.unitPrice")}
            id={id("price")}
            value={line.unitPrice}
            min={0}
            max={999999999999}
            onChange={(unitPrice) => onChange({ unitPrice })}
          />
          <InputPriceNumberField
            label={t("quotes.field.vatPercent")}
            id={id("vat")}
            value={line.vatPercent}
            min={0}
            max={100}
            onChange={(vatPercent) => onChange({ vatPercent })}
          />
        </div>
        <div className="min-w-0 space-y-4">
          <Field label={t("workPackages.inputPrice.amount")} htmlFor={id("amount")}>
            <output
              id={id("amount")}
              htmlFor={`${id("quantity")} ${id("price")}`}
              className="block min-h-10 break-words rounded-md border bg-muted/40 px-3 py-2 text-right font-semibold tabular-nums"
              data-testid={`input-price-amount-${index}`}
            >
              {Number.isFinite(line.quantity * line.unitPrice)
                ? money(line.quantity * line.unitPrice, currency)
                : "—"}
            </output>
          </Field>
          <Button
            type="button"
            variant="outline"
            className="w-full whitespace-normal"
            disabled={!canRemove}
            onClick={onRemove}
            aria-label={`${t("common.delete")} ${index + 1}`}
          >
            <Trash2 className="mr-2 h-4 w-4 shrink-0" />
            {t("common.delete")}
          </Button>
        </div>
      </div>
    </fieldset>
  );
};

export const PriceOfferDialog = ({
  open,
  onOpenChange,
  editing,
  form,
  setForm,
  vendors,
  saving,
  onSave,
  t,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing: boolean;
  form: UpsertInputPriceOfferRequest;
  setForm: React.Dispatch<React.SetStateAction<UpsertInputPriceOfferRequest>>;
  vendors: DepartmentWorkPackageOptionsResponse["vendors"];
  saving: boolean;
  onSave: () => void;
  t: (key: string) => string;
}) => (
  <Dialog open={open} onOpenChange={onOpenChange}>
    <DialogContent
      className="flex max-h-[calc(100dvh-2rem)] w-[calc(100vw-2rem)] max-w-[90rem] flex-col gap-0 overflow-hidden p-0 sm:max-w-[90rem]"
      data-testid="input-price-dialog"
    >
      <DialogHeader className="shrink-0 border-b p-4 pr-12 sm:p-6 sm:pr-12">
        <DialogTitle>
          {t(editing ? "workPackages.inputPrice.edit" : "workPackages.inputPrice.add")}
        </DialogTitle>
        <DialogDescription>{t("workPackages.inputPrice.formHelp")}</DialogDescription>
      </DialogHeader>
      <div
        className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-6"
        data-testid="input-price-dialog-body"
      >
        <div className="grid min-w-0 gap-5 sm:grid-cols-2">
          <Field label={t("workPackages.inputPrice.vendor")} htmlFor="input-price-vendor">
            <Select
              value={form.vendorId ? String(form.vendorId) : ""}
              onValueChange={(value) =>
                setForm((current) => ({ ...current, vendorId: Number(value) }))
              }
            >
              <SelectTrigger id="input-price-vendor" className="min-w-0 [&>span]:truncate">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {vendors.length === 0 && (
                  <div className="px-2 py-1.5 text-sm text-muted-foreground">
                    {t("common.noData")}
                  </div>
                )}
                {vendors.map((vendor) => (
                  <SelectItem key={vendor.id} value={String(vendor.id)}>
                    {vendor.code} · {vendor.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field label={t("workPackages.inputPrice.reference")} htmlFor="input-price-reference">
            <Input
              id="input-price-reference"
              maxLength={120}
              value={form.supplierReference}
              onChange={(event) =>
                setForm((current) => ({ ...current, supplierReference: event.target.value }))
              }
            />
          </Field>
          <Field label={t("workPackages.inputPrice.currency")} htmlFor="input-price-currency">
            <p id="input-price-currency" className="pt-2 text-sm font-medium">
              {form.currency}
            </p>
          </Field>
          <Field label={t("workPackages.inputPrice.validUntil")} htmlFor="input-price-valid-until">
            <Input
              id="input-price-valid-until"
              type="date"
              value={form.validUntil}
              onChange={(event) =>
                setForm((current) => ({ ...current, validUntil: event.target.value }))
              }
            />
          </Field>
          <div className="space-y-3 sm:col-span-2">
            <div className="flex items-center justify-between">
              <span>{t("workPackages.inputPrice.lines")}</span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() =>
                  setForm((current) => ({ ...current, lines: [...current.lines, newPriceLine()] }))
                }
              >
                <Plus className="mr-1 h-4 w-4" />
                {t("workPackages.inputPrice.addLine")}
              </Button>
            </div>
            {form.lines.map((line, index) => (
              <InputPriceLineEditor
                key={index}
                line={line}
                index={index}
                currency={form.currency}
                canRemove={form.lines.length > 1}
                t={t}
                onChange={(patch) =>
                  setForm((current) => ({
                    ...current,
                    lines: current.lines.map((item, i) =>
                      i === index ? { ...item, ...patch } : item,
                    ),
                  }))
                }
                onRemove={() =>
                  setForm((current) => ({
                    ...current,
                    lines: current.lines.filter((_, i) => i !== index),
                  }))
                }
              />
            ))}
          </div>
          <Field
            label={t("workPackages.field.description")}
            className="sm:col-span-2"
            htmlFor="input-price-note"
          >
            <Textarea
              id="input-price-note"
              maxLength={2000}
              value={form.note ?? ""}
              onChange={(event) => setForm((current) => ({ ...current, note: event.target.value }))}
            />
          </Field>
        </div>
      </div>
      <DialogFooter className="shrink-0 border-t bg-background p-4 sm:p-6">
        <Button variant="outline" onClick={() => onOpenChange(false)}>
          {t("common.cancel")}
        </Button>
        <Button disabled={saving} onClick={onSave}>
          {t("common.save")}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
);

export const Summary = ({
  icon,
  label,
  value,
  href,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  href?: string;
}) => (
  <div className="rounded-xl border bg-card p-4">
    <div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
      <span className="[&>svg]:h-4 [&>svg]:w-4">{icon}</span>
      {label}
    </div>
    {href ? (
      <Link className="font-medium text-primary hover:underline" to={href}>
        {value}
      </Link>
    ) : (
      <p className="font-medium">{value}</p>
    )}
  </div>
);
