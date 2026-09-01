import { useEffect, useRef, useState } from "react";
import { Braces, Download, Loader2, Plus, Save, Trash2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type CsvImportError,
  type SurveyConditionCategory,
  type SurveyConditionStatus,
  type SurveyResponse,
  type SurveySiteConditionRequest,
} from "@/services/adminApi";

type Props = {
  survey: SurveyResponse;
  canManage: boolean;
  onRefresh: () => Promise<void>;
};

const CATEGORIES: SurveyConditionCategory[] = ["RightOfWay", "Elevation", "Infrastructure"];
const STATUSES: SurveyConditionStatus[] = ["Unknown", "Available", "Unavailable", "NeedsInvestigation"];
const emptyCondition = (): SurveySiteConditionRequest => ({
  category: "RightOfWay",
  code: "",
  statusCode: "Unknown",
  numericValue: null,
  unitCode: null,
  referenceCode: null,
  description: null,
  note: null,
});

const saveBlob = (blob: Blob, fileName: string) => {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
};

const responseErrors = (error: unknown): CsvImportError[] => {
  const data = (error as { response?: { data?: { errors?: CsvImportError[] } } }).response?.data;
  return Array.isArray(data?.errors) ? data.errors : [];
};

export default function SurveyConditionsPanel({ survey, canManage, onRefresh }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const fileRef = useRef<HTMLInputElement>(null);
  const [rows, setRows] = useState<SurveySiteConditionRequest[]>(survey.siteConditions ?? []);
  const [json, setJson] = useState("");
  const [errors, setErrors] = useState<CsvImportError[]>([]);
  const [formError, setFormError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const next = survey.siteConditions ?? [];
    setRows(next);
    setJson(JSON.stringify(next.map(({ category, code, statusCode, numericValue, unitCode, referenceCode, description, note }) => ({ category, code, statusCode, numericValue, unitCode, referenceCode, description, note })), null, 2));
  }, [survey.siteConditions]);

  const validate = (conditions: SurveySiteConditionRequest[]) => {
    if (conditions.length === 0) return t("surveys.conditions.validation.oneRequired");
    if (conditions.some((row) => !CATEGORIES.includes(row.category) || !STATUSES.includes(row.statusCode) || !row.code.trim())) {
      return t("surveys.conditions.validation.required");
    }
    if (conditions.some((row) => row.numericValue != null && !Number.isFinite(row.numericValue))) {
      return t("surveys.conditions.validation.numeric");
    }
    return null;
  };

  const save = async (conditions: SurveySiteConditionRequest[]) => {
    const validation = validate(conditions);
    setFormError(validation);
    if (validation) return;
    setBusy(true);
    try {
      await adminApi.replaceSurveyConditions(survey.id, conditions.map((row) => ({
        ...row,
        code: row.code.trim(),
        unitCode: row.unitCode?.trim() || null,
        referenceCode: row.referenceCode?.trim() || null,
        description: row.description?.trim() || null,
        note: row.note?.trim() || null,
      })));
      toast({ title: t("surveys.conditions.saved") });
      await onRefresh();
    } catch (error) {
      setFormError(extractApiError(error));
    } finally {
      setBusy(false);
    }
  };

  const saveJson = async () => {
    try {
      const parsed = JSON.parse(json) as SurveySiteConditionRequest[];
      if (!Array.isArray(parsed)) throw new Error();
      setRows(parsed);
      await save(parsed);
    } catch {
      setFormError(t("surveys.conditions.validation.json"));
    }
  };

  const download = async () => {
    setBusy(true);
    try {
      saveBlob((await adminApi.downloadSurveyConditionsTemplate()).data, "survey-site-conditions-template.csv");
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const importCsv = async (file?: File) => {
    if (!file) return;
    setBusy(true);
    setErrors([]);
    try {
      await adminApi.importSurveyConditions(survey.id, file);
      toast({ title: t("surveys.conditions.imported") });
      await onRefresh();
    } catch (error) {
      const nextErrors = responseErrors(error);
      setErrors(nextErrors);
      if (nextErrors.length === 0) setFormError(extractApiError(error));
    } finally {
      setBusy(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  };

  const update = (index: number, patch: Partial<SurveySiteConditionRequest>) => {
    setRows((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, ...patch } : row));
  };

  return (
    <div className="space-y-4" data-testid="survey-conditions">
      <section className="rounded-lg border bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div><h2 className="font-semibold">{t("surveys.conditions.title")}</h2><p className="text-sm text-muted-foreground">{t("surveys.conditions.hint")}</p></div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" disabled={busy} onClick={() => void download()}><Download className="mr-2 h-4 w-4" />{t("surveys.conditions.downloadTemplate")}</Button>
            {canManage && <><input ref={fileRef} type="file" accept=".csv,text/csv" className="hidden" onChange={(event) => void importCsv(event.target.files?.[0])} /><Button size="sm" disabled={busy} onClick={() => fileRef.current?.click()}><Upload className="mr-2 h-4 w-4" />{t("surveys.conditions.import")}</Button></>}
          </div>
        </div>

        {errors.length > 0 && <div className="mt-3 rounded-md border border-rose-200 bg-rose-50 p-3" role="alert"><p className="font-medium text-rose-800">{t("surveys.conditions.importErrors")}</p><ul className="mt-1 space-y-1 text-sm text-rose-700">{errors.map((error, index) => <li key={`${error.row}-${error.column}-${index}`}>{t("surveys.conditions.errorLocation").replace("{row}", error.row?.toString() ?? "—").replace("{column}", error.column?.toString() ?? "—")}: {error.message}</li>)}</ul></div>}
        {formError && <p className="mt-3 text-sm text-rose-600" role="alert">{formError}</p>}

        <Tabs defaultValue="manual" className="mt-4">
          <TabsList><TabsTrigger value="manual">{t("surveys.conditions.manual")}</TabsTrigger><TabsTrigger value="json"><Braces className="mr-1 h-4 w-4" />{t("surveys.conditions.json")}</TabsTrigger></TabsList>
          <TabsContent value="manual" className="space-y-3">
            {rows.length === 0 && <p className="rounded-md border border-dashed p-5 text-center text-sm text-muted-foreground">{t("surveys.conditions.empty")}</p>}
            {rows.map((row, index) => (
              <div key={index} className="rounded-md border p-3">
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                  <div><Label>{t("surveys.conditions.field.category")}</Label><Select disabled={!canManage} value={row.category} onValueChange={(value) => update(index, { category: value as SurveyConditionCategory })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{CATEGORIES.map((category) => <SelectItem key={category} value={category}>{t(`surveys.conditions.category.${category}`)}</SelectItem>)}</SelectContent></Select></div>
                  <div><Label>{t("surveys.conditions.field.code")} *</Label><Input disabled={!canManage} maxLength={80} value={row.code} onChange={(event) => update(index, { code: event.target.value })} /></div>
                  <div><Label>{t("surveys.conditions.field.status")}</Label><Select disabled={!canManage} value={row.statusCode} onValueChange={(value) => update(index, { statusCode: value as SurveyConditionStatus })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{STATUSES.map((status) => <SelectItem key={status} value={status}>{t(`surveys.conditions.status.${status}`)}</SelectItem>)}</SelectContent></Select></div>
                  <div><Label>{t("surveys.conditions.field.numericValue")}</Label><Input disabled={!canManage} type="number" value={row.numericValue ?? ""} onChange={(event) => update(index, { numericValue: event.target.value === "" ? null : Number(event.target.value) })} /></div>
                  <div><Label>{t("surveys.conditions.field.unit")}</Label><Input disabled={!canManage} maxLength={20} value={row.unitCode ?? ""} onChange={(event) => update(index, { unitCode: event.target.value })} /></div>
                  <div><Label>{t("surveys.conditions.field.reference")}</Label><Input disabled={!canManage} maxLength={80} value={row.referenceCode ?? ""} onChange={(event) => update(index, { referenceCode: event.target.value })} /></div>
                  <div className="sm:col-span-2"><Label>{t("surveys.conditions.field.description")}</Label><Input disabled={!canManage} maxLength={1000} value={row.description ?? ""} onChange={(event) => update(index, { description: event.target.value })} /></div>
                  <div className="sm:col-span-2 lg:col-span-4"><Label>{t("surveys.conditions.field.note")}</Label><Textarea disabled={!canManage} maxLength={2000} value={row.note ?? ""} onChange={(event) => update(index, { note: event.target.value })} /></div>
                </div>
                {canManage && <div className="mt-2 flex justify-end"><Button variant="ghost" size="sm" disabled={busy} onClick={() => setRows((current) => current.filter((_, rowIndex) => rowIndex !== index))}><Trash2 className="mr-1 h-4 w-4" />{t("common.delete")}</Button></div>}
              </div>
            ))}
            {canManage && <div className="flex flex-wrap gap-2"><Button variant="outline" disabled={busy} onClick={() => setRows((current) => [...current, emptyCondition()])}><Plus className="mr-2 h-4 w-4" />{t("surveys.conditions.add")}</Button><Button disabled={busy} onClick={() => void save(rows)}>{busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}{t("common.save")}</Button></div>}
          </TabsContent>
          <TabsContent value="json" className="space-y-3"><Textarea className="min-h-80 font-mono text-xs" disabled={!canManage} value={json} onChange={(event) => setJson(event.target.value)} aria-label={t("surveys.conditions.json")} />{canManage && <Button disabled={busy} onClick={() => void saveJson()}><Save className="mr-2 h-4 w-4" />{t("surveys.conditions.saveJson")}</Button>}</TabsContent>
        </Tabs>
      </section>
    </div>
  );
}
