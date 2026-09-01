import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { CheckCircle2, Download, FileSpreadsheet, Loader2, Send, Upload, XCircle } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type CsvImportError,
  type TenderEstimateRevisionResponse,
  type TenderResponse,
  type TenderStatus,
} from "@/services/adminApi";

type Props = {
  tender: TenderResponse;
  canManage: boolean;
  canApprove: boolean;
  onTenderChanged: (tender: TenderResponse) => void;
};

const saveBlob = (blob: Blob, fileName: string) => {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
};

const importErrorsFrom = (error: unknown): CsvImportError[] => {
  const response = (error as { response?: { data?: { errors?: CsvImportError[] } } }).response;
  return Array.isArray(response?.data?.errors) ? response.data.errors : [];
};

export default function TenderEstimatePanel({ tender, canManage, canApprove, onTenderChanged }: Props) {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const fileRef = useRef<HTMLInputElement>(null);
  const [revisions, setRevisions] = useState<TenderEstimateRevisionResponse[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [decisionNote, setDecisionNote] = useState("");
  const [importErrors, setImportErrors] = useState<CsvImportError[]>([]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await adminApi.listTenderEstimates(tender.id);
      setRevisions(data);
      setSelectedId((current) => current && data.some((revision) => revision.id === current)
        ? current
        : data[0]?.id ?? null);
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [tender.id, t, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const selected = useMemo(
    () => revisions.find((revision) => revision.id === selectedId) ?? null,
    [revisions, selectedId],
  );
  const approved = revisions.some((revision) => revision.status === "Approved");
  const checklistReady = tender.checklistItems.length > 0 && tender.checklistCompletionPercent === 100;
  const canSubmitTender = tender.status === "Preparing" && checklistReady && approved;
  const terminal = ["Won", "Lost", "Cancelled"].includes(tender.status);

  const money = (value: number, currency: string) => new Intl.NumberFormat(lang, {
    style: "currency",
    currency,
    maximumFractionDigits: 0,
  }).format(value);

  const downloadTemplate = async () => {
    setBusy(true);
    try {
      saveBlob((await adminApi.downloadTenderEstimateTemplate(tender.id)).data, "tender-estimate-template.csv");
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const importFile = async (file?: File) => {
    if (!file) return;
    setBusy(true);
    setImportErrors([]);
    try {
      await adminApi.importTenderEstimate(tender.id, file);
      toast({ title: t("tenders.estimate.imported") });
      await load();
    } catch (error) {
      const errors = importErrorsFrom(error);
      setImportErrors(errors);
      if (errors.length === 0) {
        toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
      }
    } finally {
      setBusy(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  };

  const decide = async (action: "submit" | "approve" | "reject") => {
    if (!selected) return;
    setBusy(true);
    try {
      if (action === "submit") await adminApi.submitTenderEstimate(tender.id, selected.id, decisionNote.trim() || null);
      if (action === "approve") await adminApi.approveTenderEstimate(tender.id, selected.id, decisionNote.trim() || null);
      if (action === "reject") await adminApi.rejectTenderEstimate(tender.id, selected.id, decisionNote.trim() || null);
      setDecisionNote("");
      toast({ title: t(`tenders.estimate.${action}Success`) });
      await load();
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  const transition = async (status: TenderStatus) => {
    setBusy(true);
    try {
      const { data } = await adminApi.transitionTender(tender.id, { status, note: decisionNote.trim() || null });
      onTenderChanged(data);
      setDecisionNote("");
      toast({ title: t("tenders.lifecycle.updated") });
    } catch (error) {
      toast({ title: t("common.error"), description: extractApiError(error), variant: "destructive" });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-4">
      <section className="rounded-lg border bg-white p-4 shadow-sm" data-testid="tender-lifecycle">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="font-semibold">{t("tenders.lifecycle.title")}</h2>
            <p className="mt-1 text-sm text-muted-foreground">{t(`tenders.lifecycle.help.${tender.status}`)}</p>
          </div>
          <Badge variant="outline">{t(`tenders.status.${tender.status}`)}</Badge>
        </div>
        {tender.status === "Preparing" && (
          <div className="mt-3 grid gap-2 sm:grid-cols-2">
            <div className={`rounded-md border p-3 text-sm ${checklistReady ? "border-emerald-200 bg-emerald-50" : "border-amber-200 bg-amber-50"}`}>
              {checklistReady ? <CheckCircle2 className="mr-2 inline h-4 w-4" /> : <XCircle className="mr-2 inline h-4 w-4" />}
              {t("tenders.lifecycle.checklistGate").replace("{percent}", String(tender.checklistCompletionPercent))}
            </div>
            <div className={`rounded-md border p-3 text-sm ${approved ? "border-emerald-200 bg-emerald-50" : "border-amber-200 bg-amber-50"}`}>
              {approved ? <CheckCircle2 className="mr-2 inline h-4 w-4" /> : <XCircle className="mr-2 inline h-4 w-4" />}
              {approved ? t("tenders.lifecycle.estimateApproved") : t("tenders.lifecycle.estimateGate")}
            </div>
          </div>
        )}
        {!terminal && canManage && (
          <div className="mt-3 flex flex-wrap gap-2">
            {tender.status === "Preparing" && (
              <Button disabled={busy || !canSubmitTender} onClick={() => void transition("Submitted")}>
                <Send className="mr-2 h-4 w-4" />{t("tenders.lifecycle.submit")}
              </Button>
            )}
            <Button variant="outline" disabled={busy} onClick={() => void transition("Cancelled")}>
              <XCircle className="mr-2 h-4 w-4" />{t("tenders.lifecycle.cancel")}
            </Button>
          </div>
        )}
      </section>

      <section className="rounded-lg border bg-white p-4 shadow-sm" data-testid="tender-estimates">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="font-semibold">{t("tenders.estimate.title")}</h2>
            <p className="text-sm text-muted-foreground">{t("tenders.estimate.hint")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" disabled={busy} onClick={() => void downloadTemplate()}>
              <Download className="mr-2 h-4 w-4" />{t("tenders.estimate.downloadTemplate")}
            </Button>
            {canManage && tender.status === "Preparing" && (
              <>
                <input ref={fileRef} className="hidden" type="file" accept=".csv,text/csv" onChange={(event) => void importFile(event.target.files?.[0])} />
                <Button size="sm" disabled={busy} onClick={() => fileRef.current?.click()}>
                  <Upload className="mr-2 h-4 w-4" />{t("tenders.estimate.import")}
                </Button>
              </>
            )}
          </div>
        </div>

        {importErrors.length > 0 && (
          <div className="mt-3 rounded-md border border-rose-200 bg-rose-50 p-3" role="alert">
            <p className="font-medium text-rose-800">{t("tenders.estimate.importErrors")}</p>
            <ul className="mt-1 space-y-1 text-sm text-rose-700">
              {importErrors.map((error, index) => (
                <li key={`${error.row}-${error.column}-${index}`}>
                  {t("tenders.estimate.errorLocation")
                    .replace("{row}", error.row?.toString() ?? "—")
                    .replace("{column}", error.column?.toString() ?? "—")}: {error.message}
                </li>
              ))}
            </ul>
          </div>
        )}

        {loading ? (
          <div className="mt-4 flex items-center gap-2 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />{t("common.loading")}</div>
        ) : revisions.length === 0 ? (
          <div className="mt-4 rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
            <FileSpreadsheet className="mx-auto mb-2 h-6 w-6" />{t("tenders.estimate.empty")}
          </div>
        ) : (
          <div className="mt-4 grid gap-4 lg:grid-cols-[15rem_minmax(0,1fr)]">
            <div className="flex gap-2 overflow-x-auto lg:block lg:space-y-2 lg:overflow-visible">
              {revisions.map((revision) => (
                <button key={revision.id} type="button" onClick={() => setSelectedId(revision.id)} className={`min-w-52 rounded-md border p-3 text-left lg:w-full ${revision.id === selectedId ? "border-primary bg-primary/5" : "hover:bg-muted/40"}`}>
                  <div className="flex items-center justify-between gap-2"><span className="font-medium">{t("tenders.estimate.revision").replace("{version}", String(revision.versionNumber))}</span><Badge variant="outline">{t(`tenders.estimate.status.${revision.status}`)}</Badge></div>
                  <p className="mt-1 truncate text-xs text-muted-foreground">{revision.sourceFileName}</p>
                </button>
              ))}
            </div>
            {selected && (
              <div className="min-w-0 space-y-3">
                <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
                  {(["costSubtotal", "bidSubtotal", "vatAmount", "grandBidTotal"] as const).map((field) => (
                    <div key={field} className="rounded-md bg-muted/40 p-3"><div className="text-xs text-muted-foreground">{t(`tenders.estimate.${field}`)}</div><div className="mt-1 font-semibold tabular-nums">{money(selected[field], selected.currency)}</div></div>
                  ))}
                </div>
                <div className="overflow-x-auto rounded-md border">
                  <table className="min-w-[760px] w-full text-sm"><thead className="bg-muted/50"><tr>{["code", "description", "unit", "quantity", "unitCost", "bidUnitPrice", "bidAmount"].map((key) => <th key={key} className="px-2 py-2 text-left text-xs">{t(`tenders.estimate.line.${key}`)}</th>)}</tr></thead><tbody className="divide-y">{selected.lines.map((line) => <tr key={line.id}><td className="px-2 py-2 font-mono text-xs">{line.itemCode}</td><td className="px-2 py-2">{line.description}</td><td className="px-2 py-2">{line.unit}</td><td className="px-2 py-2 text-right tabular-nums">{line.quantity}</td><td className="px-2 py-2 text-right tabular-nums">{money(line.unitCost, selected.currency)}</td><td className="px-2 py-2 text-right tabular-nums">{money(line.bidUnitPrice, selected.currency)}</td><td className="px-2 py-2 text-right tabular-nums font-medium">{money(line.bidAmount, selected.currency)}</td></tr>)}</tbody></table>
                </div>
                {((canManage && selected.status === "Draft") || (canApprove && selected.status === "Submitted")) && (
                  <div className="rounded-md border p-3">
                    <Textarea value={decisionNote} onChange={(event) => setDecisionNote(event.target.value)} maxLength={2000} placeholder={t("tenders.estimate.notePlaceholder")} />
                    <div className="mt-2 flex flex-wrap gap-2">
                      {canManage && selected.status === "Draft" && <Button size="sm" disabled={busy} onClick={() => void decide("submit")}><Send className="mr-2 h-4 w-4" />{t("tenders.estimate.submit")}</Button>}
                      {canApprove && selected.status === "Submitted" && <><Button size="sm" disabled={busy} onClick={() => void decide("approve")}><CheckCircle2 className="mr-2 h-4 w-4" />{t("tenders.estimate.approve")}</Button><Button size="sm" variant="destructive" disabled={busy} onClick={() => void decide("reject")}><XCircle className="mr-2 h-4 w-4" />{t("tenders.estimate.reject")}</Button></>}
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </section>
    </div>
  );
}
