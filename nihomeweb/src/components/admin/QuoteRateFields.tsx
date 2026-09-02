import { useEffect, useRef, useState } from "react";
import { AlertCircle, Loader2 } from "lucide-react";
import { Link } from "react-router-dom";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import { roundQuoteMoney } from "@/lib/quoteTotals";
import {
  adminApi,
  type MaterialRateCatalogResponse,
  type MaterialRateRevisionResponse,
} from "@/services/adminApi";

interface QuoteRateFieldsProps {
  catalogId?: number | null;
  pricingDate?: string | null;
  unitPrice?: number | null;
  overrideReason?: string | null;
  rateSource?: "Catalog" | "Override" | "CatalogReference";
  canOverride: boolean;
  disabled?: boolean;
  onChange: (patch: {
    materialRateCatalogId?: number | null;
    pricingEffectiveDate?: string | null;
    unitPricePerSqm?: number | null;
    rateOverrideReason?: string | null;
  }) => void;
  onEffectiveRevisionChange?: (revision: MaterialRateRevisionResponse | null) => void;
}

const QuoteRateFields = ({
  catalogId,
  pricingDate,
  unitPrice,
  overrideReason,
  rateSource = "Catalog",
  canOverride,
  disabled = false,
  onChange,
  onEffectiveRevisionChange,
}: QuoteRateFieldsProps) => {
  const { t } = useI18n();
  const [catalogs, setCatalogs] = useState<MaterialRateCatalogResponse[]>([]);
  const [revision, setRevision] = useState<MaterialRateRevisionResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const lastResolvedKey = useRef<string | null>(null);
  const onChangeRef = useRef(onChange);
  const onEffectiveRevisionChangeRef = useRef(onEffectiveRevisionChange);

  useEffect(() => {
    onChangeRef.current = onChange;
    onEffectiveRevisionChangeRef.current = onEffectiveRevisionChange;
  }, [onChange, onEffectiveRevisionChange]);

  useEffect(() => {
    let cancelled = false;
    void adminApi.listMaterialRateCatalogs(undefined, false, "InvestmentRate")
      .then(({ data }) => { if (!cancelled) setCatalogs(data); })
      .catch((err) => { if (!cancelled) setError(extractApiError(err)); });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!catalogId || !pricingDate) {
      setRevision(null);
      setError(null);
      onEffectiveRevisionChangeRef.current?.(null);
      lastResolvedKey.current = null;
      return;
    }
    const key = `${catalogId}:${pricingDate}`;
    let cancelled = false;
    setLoading(true);
    setError(null);
    void adminApi.getEffectiveMaterialRateRevision(catalogId, pricingDate)
      .then(({ data }) => {
        if (cancelled) return;
        const quoteRevision = {
          ...data,
          totalRatePerSqm: roundQuoteMoney(data.totalRatePerSqm),
        };
        const selectionChanged = lastResolvedKey.current !== null && lastResolvedKey.current !== key;
        lastResolvedKey.current = key;
        setRevision(quoteRevision);
        onEffectiveRevisionChangeRef.current?.(quoteRevision);
        if (!canOverride || unitPrice == null || (selectionChanged && rateSource !== "Override")) {
          onChangeRef.current({ unitPricePerSqm: quoteRevision.totalRatePerSqm, rateOverrideReason: null });
        }
      })
      .catch((err) => {
        if (cancelled) return;
        setRevision(null);
        onEffectiveRevisionChangeRef.current?.(null);
        setError(t("quotes.validation.noEffectiveRate"));
        if (!canOverride) onChangeRef.current({ unitPricePerSqm: null, rateOverrideReason: null });
        if ((err as { response?: { status?: number } }).response?.status !== 404) {
          setError(extractApiError(err));
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [canOverride, catalogId, pricingDate, rateSource, t, unitPrice]);

  const isOverride = Boolean(revision && unitPrice != null && unitPrice !== revision.totalRatePerSqm);

  return (
    <div className="space-y-3 rounded-md border bg-muted/20 p-3">
      <div className="text-xs leading-relaxed text-muted-foreground">
        {t("quotes.rate.usageHint")}{" "}
        <Link className="font-medium text-primary underline underline-offset-2" to="/admin/material-rates/investment">
          {t("quotes.rate.manageCatalogs")}
        </Link>
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <Label>{t("quotes.field.materialRateCatalog")} *</Label>
          <Select
            disabled={disabled}
            value={catalogId ? String(catalogId) : undefined}
            onValueChange={(value) => {
              setRevision(null);
              onEffectiveRevisionChangeRef.current?.(null);
              onChange({
                materialRateCatalogId: Number(value),
                unitPricePerSqm: null,
                rateOverrideReason: null,
              });
            }}
          >
            <SelectTrigger data-testid="quote-rate-catalog"><SelectValue placeholder={t("quotes.rate.selectCatalog")} /></SelectTrigger>
            <SelectContent>
              {catalogs.map((catalog) => (
                <SelectItem key={catalog.id} value={String(catalog.id)}>
                  {catalog.code} · {catalog.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {catalogs.length === 0 && !error && (
            <p className="mt-1 text-xs text-amber-700">{t("quotes.rate.noCatalogs")}</p>
          )}
        </div>
        <div>
          <Label>{t("quotes.field.pricingEffectiveDate")} *</Label>
          <Input
            type="date"
            data-testid="quote-rate-date"
            disabled={disabled}
            value={pricingDate?.slice(0, 10) ?? ""}
            onChange={(event) => {
              setRevision(null);
              onEffectiveRevisionChangeRef.current?.(null);
              onChange({
                pricingEffectiveDate: event.target.value || null,
                unitPricePerSqm: null,
                rateOverrideReason: null,
              });
            }}
          />
        </div>
      </div>

      {loading && <p className="flex items-center gap-2 text-xs text-muted-foreground"><Loader2 className="h-3.5 w-3.5 animate-spin" />{t("quotes.rate.loading")}</p>}
      {error && <p className="flex items-start gap-2 rounded bg-destructive/10 p-2 text-xs text-destructive"><AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />{error}</p>}
      {revision && (
        <dl className="grid gap-2 rounded-md border bg-background p-3 text-sm sm:grid-cols-3">
          <div><dt className="text-xs text-muted-foreground">{t("quotes.field.materialRateRevision")}</dt><dd className="font-medium">V{revision.version}</dd></div>
          <div><dt className="text-xs text-muted-foreground">{t("quotes.field.catalogRate")}</dt><dd className="font-medium">{formatVnd(revision.totalRatePerSqm)} {revision.currency}/m²</dd></div>
          <div><dt className="text-xs text-muted-foreground">{t("quotes.field.rateSource")}</dt><dd className="font-medium">{t(`quotes.rateSource.${isOverride ? "Override" : "Catalog"}`)}</dd></div>
        </dl>
      )}

      <div>
        <Label>{t("quotes.field.appliedRate")} *</Label>
        <Input
          type="number"
          inputMode="decimal"
          min={0.01}
          step={0.01}
          data-testid="quote-applied-rate"
          disabled={disabled || !canOverride}
          value={unitPrice ?? ""}
          onChange={(event) => onChange({ unitPricePerSqm: event.target.value ? Number(event.target.value) : null })}
        />
        {!canOverride && <p className="mt-1 text-xs text-muted-foreground">{t("quotes.rate.catalogLocked")}</p>}
      </div>

      {canOverride && isOverride && (
        <div>
          <Label>{t("quotes.field.rateOverrideReason")} *</Label>
          <Textarea
            disabled={disabled}
            minLength={10}
            maxLength={500}
            value={overrideReason ?? ""}
            onChange={(event) => onChange({ rateOverrideReason: event.target.value })}
            placeholder={t("quotes.rate.overrideReasonHint")}
          />
        </div>
      )}
    </div>
  );
};

export default QuoteRateFields;
