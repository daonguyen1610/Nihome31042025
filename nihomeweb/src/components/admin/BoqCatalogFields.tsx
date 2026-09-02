import { useEffect, useState } from "react";
import { AlertCircle, Loader2 } from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import {
  adminApi,
  type MaterialRateCatalogResponse,
  type MaterialRateRevisionResponse,
} from "@/services/adminApi";

interface BoqCatalogFieldsProps {
  catalogId?: number | null;
  pricingDate?: string | null;
  disabled?: boolean;
  onApply: (revision: MaterialRateRevisionResponse, pricingDate: string) => void;
}

const BoqCatalogFields = ({ catalogId, pricingDate, disabled = false, onApply }: BoqCatalogFieldsProps) => {
  const { t } = useI18n();
  const [catalogs, setCatalogs] = useState<MaterialRateCatalogResponse[]>([]);
  const [selectedCatalogId, setSelectedCatalogId] = useState<number | null>(catalogId ?? null);
  const [selectedDate, setSelectedDate] = useState(pricingDate?.slice(0, 10) ?? new Date().toISOString().slice(0, 10));
  const [revision, setRevision] = useState<MaterialRateRevisionResponse | null>(null);
  const [resolvedSelectionKey, setResolvedSelectionKey] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const selectionKey = selectedCatalogId && selectedDate ? `${selectedCatalogId}:${selectedDate}` : null;

  useEffect(() => {
    let cancelled = false;
    void adminApi.listMaterialRateCatalogs(undefined, false, "Boq")
      .then(({ data }) => { if (!cancelled) setCatalogs(data); })
      .catch((err) => { if (!cancelled) setError(extractApiError(err)); });
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!selectedCatalogId || !selectedDate) {
      setRevision(null);
      setResolvedSelectionKey(null);
      setLoading(false);
      setError(null);
      return;
    }
    let cancelled = false;
    setRevision(null);
    setResolvedSelectionKey(null);
    setLoading(true);
    setError(null);
    void adminApi.getEffectiveMaterialRateRevision(selectedCatalogId, selectedDate)
      .then(({ data }) => {
        if (cancelled) return;
        setRevision(data);
        setResolvedSelectionKey(`${selectedCatalogId}:${selectedDate}`);
      })
      .catch((err) => {
        if (cancelled) return;
        setRevision(null);
        setError((err as { response?: { status?: number } }).response?.status === 404
          ? t("quotes.boqCatalog.noEffectiveRevision")
          : extractApiError(err));
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [selectedCatalogId, selectedDate, t]);

  return (
    <section className="space-y-3 rounded-md border bg-muted/20 p-3" data-testid="quote-boq-catalog-fields">
      <p className="text-xs leading-relaxed text-muted-foreground">
        {t("quotes.boqCatalog.usageHint")}{" "}
        <Link className="font-medium text-primary underline underline-offset-2" to="/admin/material-rates/boq">
          {t("quotes.rate.manageCatalogs")}
        </Link>
      </p>
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <Label>{t("quotes.field.materialRateCatalog")}</Label>
          <Select disabled={disabled} value={selectedCatalogId ? String(selectedCatalogId) : undefined} onValueChange={(value) => {
            setRevision(null);
            setResolvedSelectionKey(null);
            setSelectedCatalogId(Number(value));
          }}>
            <SelectTrigger data-testid="quote-boq-catalog"><SelectValue placeholder={t("quotes.boqCatalog.selectCatalog")} /></SelectTrigger>
            <SelectContent>{catalogs.map((catalog) => <SelectItem key={catalog.id} value={String(catalog.id)}>{catalog.code} · {catalog.name}</SelectItem>)}</SelectContent>
          </Select>
          {catalogs.length === 0 && !error && <p className="mt-1 text-xs text-amber-700">{t("quotes.boqCatalog.noCatalogs")}</p>}
        </div>
        <div>
          <Label>{t("quotes.field.pricingEffectiveDate")}</Label>
          <Input type="date" data-testid="quote-boq-catalog-date" disabled={disabled} value={selectedDate} onChange={(event) => {
            setRevision(null);
            setResolvedSelectionKey(null);
            setSelectedDate(event.target.value);
          }} />
        </div>
      </div>
      {loading && <p className="flex items-center gap-2 text-xs text-muted-foreground"><Loader2 className="h-3.5 w-3.5 animate-spin" />{t("quotes.rate.loading")}</p>}
      {error && <p className="flex items-start gap-2 rounded bg-destructive/10 p-2 text-xs text-destructive"><AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />{error}</p>}
      {revision && resolvedSelectionKey === selectionKey && (
        <div className="flex flex-col gap-3 rounded-md border bg-background p-3 sm:flex-row sm:items-center sm:justify-between">
          <dl className="grid flex-1 gap-2 text-sm sm:grid-cols-3">
            <div><dt className="text-xs text-muted-foreground">{t("quotes.field.materialRateRevision")}</dt><dd className="font-medium">V{revision.version}</dd></div>
            <div><dt className="text-xs text-muted-foreground">{t("quotes.boqCatalog.lineCount")}</dt><dd className="font-medium">{revision.lines.length}</dd></div>
            <div><dt className="text-xs text-muted-foreground">{t("quotes.boqCatalog.total")}</dt><dd className="font-medium">{formatVnd(revision.totalAmount)} {revision.currency}</dd></div>
          </dl>
          <Button type="button" size="sm" data-testid="quote-boq-catalog-apply" disabled={disabled || loading || revision.lines.length === 0} onClick={() => onApply(revision, selectedDate)}>
            {t(catalogId ? "quotes.boqCatalog.replace" : "quotes.boqCatalog.apply")}
          </Button>
        </div>
      )}
    </section>
  );
};

export default BoqCatalogFields;
