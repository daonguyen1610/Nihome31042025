import { useEffect, useState } from "react";
import { Link2, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { isContractReadyQuote } from "@/lib/contractQuotes";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import { adminApi, type ContractResponse } from "@/services/adminApi";

interface QuoteSide {
  id: number;
  code: string;
  customerId: number;
  opportunityId: number;
}

type QuoteContractLinkDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onLinked: (contract: ContractResponse) => void;
} & (
  | { mode: "fromQuote"; quote: QuoteSide }
  | { mode: "fromContract"; contract: ContractResponse }
);

interface Candidate {
  id: number;
  label: string;
  detail: string;
}

/**
 * Ties an approved quote to an existing customer contract, from either side.
 * Only candidates the server would accept are offered: same customer, same
 * opportunity when one is set, no source quote yet, and an approved quote.
 */
const QuoteContractLinkDialog = (props: QuoteContractLinkDialogProps) => {
  const { open, onOpenChange, onLinked } = props;
  const { t } = useI18n();
  const { toast } = useToast();
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const customerId = props.mode === "fromQuote" ? props.quote.customerId : props.contract.customerId;
  const opportunityId = props.mode === "fromQuote" ? props.quote.opportunityId : props.contract.opportunityId ?? null;
  const mode = props.mode;

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setSelectedId(null);
    setError(null);
    setLoading(true);
    (async () => {
      try {
        if (mode === "fromQuote") {
          const { data } = await adminApi.listContracts({ customerId, direction: "Upstream", pageSize: 100 });
          const eligible = data.items.filter((contract) =>
            contract.quoteId == null &&
            contract.status !== "Cancelled" &&
            contract.status !== "Completed" &&
            (contract.opportunityId == null || contract.opportunityId === opportunityId));
          if (cancelled) return;
          setContracts(eligible);
          setCandidates(eligible.map((contract) => ({
            id: contract.id,
            label: contract.contractNumber,
            detail: `${t(`contracts.status.${contract.status}`)} · ${formatVnd(contract.currentValue)} ₫`,
          })));
        } else {
          const { data } = await adminApi.listQuotes({ customerId, pageSize: 100 });
          const eligible = data.items.filter((quote) =>
            isContractReadyQuote(quote.status) &&
            (opportunityId == null || quote.opportunityId === opportunityId));
          if (cancelled) return;
          setCandidates(eligible.map((quote) => ({
            id: quote.id,
            label: quote.code,
            detail: `${t(`quotes.status.${quote.status}`)} · ${formatVnd(quote.grandTotal)} ₫`,
          })));
        }
      } catch (err) {
        if (!cancelled) setError(extractApiError(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [open, mode, customerId, opportunityId, t]);

  const confirm = async () => {
    if (selectedId == null) {
      setError(t(mode === "fromQuote" ? "quoteLink.contractRequired" : "quoteLink.quoteRequired"));
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const { data } = props.mode === "fromQuote"
        ? await adminApi.linkContractQuote(
          selectedId,
          props.quote.id,
          contracts.find((contract) => contract.id === selectedId)!.rowVersion,
        )
        : await adminApi.linkContractQuote(props.contract.id, selectedId, props.contract.rowVersion);
      toast({ title: t("quoteLink.linked", { quote: data.quoteCode ?? "", contract: data.contractNumber }) });
      onLinked(data);
      onOpenChange(false);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] w-[95vw] max-w-md overflow-y-auto sm:w-full">
        <DialogHeader>
          <DialogTitle>{t(mode === "fromQuote" ? "quoteLink.fromQuote.title" : "quoteLink.fromContract.title")}</DialogTitle>
          <DialogDescription>
            {t(mode === "fromQuote" ? "quoteLink.fromQuote.description" : "quoteLink.fromContract.description")}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2">
          <Label>{t(mode === "fromQuote" ? "quoteLink.contract" : "quoteLink.quote")}</Label>
          {loading ? (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t("common.loading")}
            </p>
          ) : candidates.length === 0 ? (
            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground" data-testid="quote-link-empty">
              {t(mode === "fromQuote" ? "quoteLink.fromQuote.empty" : "quoteLink.fromContract.empty")}
            </p>
          ) : (
            <Select value={selectedId != null ? String(selectedId) : undefined} onValueChange={(value) => setSelectedId(Number(value))}>
              <SelectTrigger data-testid="quote-link-select">
                <SelectValue placeholder={t(mode === "fromQuote" ? "quoteLink.contractPlaceholder" : "quoteLink.quotePlaceholder")} />
              </SelectTrigger>
              <SelectContent>
                {candidates.map((candidate) => (
                  <SelectItem key={candidate.id} value={String(candidate.id)}>
                    {candidate.label} · {candidate.detail}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
          {error && <p className="text-sm text-destructive" role="alert">{error}</p>}
        </div>
        <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button
            data-testid="quote-link-confirm"
            onClick={() => void confirm()}
            disabled={saving || loading || candidates.length === 0}
          >
            {saving ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <Link2 className="mr-1.5 h-4 w-4" />}
            {t("quoteLink.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default QuoteContractLinkDialog;
