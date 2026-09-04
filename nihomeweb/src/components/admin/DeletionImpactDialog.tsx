import { useEffect, useRef, useState } from "react";
import { AlertTriangle, ExternalLink, Link2Off, Loader2, RotateCcw, Trash2 } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { adminApi, type DeletionImpactAction, type DeletionImpactResponse, type HardDeleteOperationResult } from "@/services/adminApi";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

interface DeletionImpactDialogProps {
  open: boolean;
  impact: DeletionImpactResponse | null;
  loading: boolean;
  deleting: boolean;
  error?: string | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (confirmation: string) => Promise<HardDeleteOperationResult | null>;
  onCompleted: () => Promise<void> | void;
}

const POLL_INTERVAL_MS = 2_000;

const actionIcon: Record<DeletionImpactAction, typeof Trash2> = {
  Delete: Trash2,
  Unlink: Link2Off,
  Block: AlertTriangle,
};

const actionClass: Record<DeletionImpactAction, string> = {
  Delete: "border-rose-200 bg-rose-50 text-rose-700",
  Unlink: "border-amber-200 bg-amber-50 text-amber-700",
  Block: "border-red-300 bg-red-100 text-red-800",
};

export const DeletionImpactDialog = ({
  open,
  impact,
  loading,
  deleting,
  error,
  onOpenChange,
  onConfirm,
  onCompleted,
}: DeletionImpactDialogProps) => {
  const { t } = useI18n();
  const [confirmation, setConfirmation] = useState("");
  const [operation, setOperation] = useState<HardDeleteOperationResult | null>(null);
  const [operationError, setOperationError] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const completedRef = useRef(false);

  useEffect(() => {
    if (!open) {
      setConfirmation("");
      setOperation(null);
      setOperationError(false);
      completedRef.current = false;
    }
  }, [open]);

  useEffect(() => {
    if (!open || !operation || operation.isComplete || operation.status === "Failed" || operation.requiresManualAction) return;

    let active = true;
    const poll = async () => {
      try {
        const response = await adminApi.getHardDeleteOperation(operation.operationId);
        if (!active) return;
        setOperationError(false);
        setOperation(response.data);
      } catch {
        if (active) setOperationError(true);
      }
    };
    const timer = window.setInterval(() => void poll(), POLL_INTERVAL_MS);
    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [open, operation]);

  useEffect(() => {
    if (!operation?.isComplete || completedRef.current) return;
    completedRef.current = true;
    void onCompleted();
  }, [onCompleted, operation]);

  const confirmed = impact != null && confirmation === impact.requiredConfirmation;
  const operationActive = operation != null && !operation.isComplete && operation.status !== "Failed" && !operation.requiresManualAction;
  const canSubmit = impact?.canDelete === true && confirmed && !loading && !deleting && operation == null;

  const submit = async () => {
    try {
      const result = await onConfirm(confirmation);
      if (result == null || result.isComplete) {
        await onCompleted();
        return;
      }
      setOperation(result);
    } catch {
      return;
    }
  };

  const retry = async () => {
    if (!operation) return;
    setRetrying(true);
    setOperationError(false);
    try {
      const response = await adminApi.retryHardDeleteOperation(operation.operationId);
      setOperation(response.data);
    } catch {
      setOperationError(true);
    } finally {
      setRetrying(false);
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <AlertDialogHeader>
          <AlertDialogTitle>{t("deletionImpact.title")}</AlertDialogTitle>
          <AlertDialogDescription>
            {impact
              ? t("deletionImpact.description", { name: impact.resourceLabel, count: impact.totalAffected })
              : t("deletionImpact.loading")}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {loading ? (
          <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            {t("deletionImpact.loading")}
          </div>
        ) : null}

        {error ? (
          <div role="alert" className="flex gap-2 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
            <AlertTriangle className="h-4 w-4" />
            <p>{error}</p>
          </div>
        ) : null}

        {operation ? (
          <div className="space-y-3 rounded-md border border-amber-200 bg-amber-50 p-4" role="status">
            <div className="flex items-start gap-2 text-sm text-amber-900">
              {operationActive ? <Loader2 className="mt-0.5 h-4 w-4 animate-spin" /> : <AlertTriangle className="mt-0.5 h-4 w-4" />}
              <div className="space-y-1">
                <p className="font-medium">
                  {operation.requiresManualAction
                    ? t("deletionImpact.operation.manualAction")
                    : operation.status === "Failed"
                      ? t("deletionImpact.operation.failed")
                      : t("deletionImpact.operation.processing")}
                </p>
                <p className="text-xs">{t("deletionImpact.operation.reference", { id: operation.operationId })}</p>
              </div>
            </div>
            {operationError ? <p role="alert" className="text-sm text-destructive">{t("deletionImpact.operation.statusError")}</p> : null}
            {(operation.status === "Failed" || operation.requiresManualAction) ? (
              <Button type="button" variant="outline" size="sm" disabled={retrying} onClick={() => void retry()}>
                {retrying ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <RotateCcw className="mr-2 h-4 w-4" />}
                {t("deletionImpact.operation.retry")}
              </Button>
            ) : null}
          </div>
        ) : null}

        {impact && !operation ? (
          <div className="space-y-4">
            {impact.items.length === 0 ? (
              <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                {t("deletionImpact.noDependencies")}
              </p>
            ) : (
              <div className="space-y-2">
                {impact.items.map((item) => {
                  const Icon = actionIcon[item.action];
                  const canResolve = item.action === "Block" && !item.key.endsWith(".fileBlockers");
                  const resolutionLinks = canResolve
                    ? (item.resolutionLinks ?? []).filter((link) => link.url.startsWith("/admin/"))
                    : [];
                  const resolutionUrl = canResolve &&
                    item.count > resolutionLinks.length &&
                    item.resolutionUrl?.startsWith("/admin/")
                    ? item.resolutionUrl
                    : null;
                  return (
                    <section key={item.key} className="rounded-md border p-3">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="flex items-center gap-2">
                          <Icon className="h-4 w-4 text-muted-foreground" />
                          <span className="text-sm font-medium">{t(`deletionImpact.item.${item.key}`)}</span>
                        </div>
                        <Badge variant="outline" className={actionClass[item.action]}>
                          {t(`deletionImpact.action.${item.action}`)} · {item.count}
                        </Badge>
                      </div>
                      {resolutionLinks.length > 0 ? (
                        <div className="mt-2 space-y-1 text-xs">
                          <p className="text-muted-foreground">{t("deletionImpact.relatedRecords")}</p>
                          {resolutionLinks.map((link) => (
                            <a
                              key={link.url}
                              href={link.url}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="flex w-fit items-center gap-1.5 font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                            >
                              {link.label}
                              <ExternalLink className="h-3.5 w-3.5" />
                            </a>
                          ))}
                        </div>
                      ) : item.examples.length > 0 ? (
                        <p className="mt-2 break-words text-xs text-muted-foreground">
                          {t("deletionImpact.examples", { examples: item.examples.join(", ") })}
                        </p>
                      ) : null}
                      {resolutionUrl ? (
                        <a
                          href={resolutionUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="mt-3 inline-flex items-center gap-1.5 text-xs font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        >
                          {t("deletionImpact.viewAll")}
                          <ExternalLink className="h-3.5 w-3.5" />
                        </a>
                      ) : null}
                    </section>
                  );
                })}
              </div>
            )}

            {!impact.canDelete ? (
              <div role="alert" className="flex gap-2 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
                <AlertTriangle className="h-4 w-4" />
                <p>{t("deletionImpact.blocked")}</p>
              </div>
            ) : (
              <div className="space-y-2">
                <Label htmlFor="deletion-confirmation">
                  {t("deletionImpact.confirmationLabel", { value: impact.requiredConfirmation })}
                </Label>
                <Input
                  id="deletion-confirmation"
                  value={confirmation}
                  onChange={(event) => setConfirmation(event.target.value)}
                  autoComplete="off"
                  placeholder={impact.requiredConfirmation}
                  disabled={deleting}
                />
                <p className="text-xs text-muted-foreground">{t("deletionImpact.irreversible")}</p>
              </div>
            )}
          </div>
        ) : null}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={deleting || operationActive}>{t("common.cancel")}</AlertDialogCancel>
          {!operation ? (
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              disabled={!canSubmit}
              onClick={(event) => {
                event.preventDefault();
                void submit();
              }}
            >
              {deleting ? t("deletionImpact.deleting") : t("deletionImpact.confirm")}
            </AlertDialogAction>
          ) : null}
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
};
