import { useEffect, useState } from "react";
import { AlertTriangle, Link2Off, Loader2, Trash2 } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import type { DeletionImpactAction, DeletionImpactResponse } from "@/services/adminApi";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

interface DeletionImpactDialogProps {
  open: boolean;
  impact: DeletionImpactResponse | null;
  loading: boolean;
  deleting: boolean;
  error?: string | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (confirmation: string) => Promise<void>;
}

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
}: DeletionImpactDialogProps) => {
  const { t } = useI18n();
  const [confirmation, setConfirmation] = useState("");

  useEffect(() => {
    if (!open) setConfirmation("");
  }, [open]);

  const confirmed = impact != null && confirmation.trim() === impact.requiredConfirmation;
  const canSubmit = impact?.canDelete === true && confirmed && !loading && !deleting;

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

        {impact ? (
          <div className="space-y-4">
            {impact.items.length === 0 ? (
              <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                {t("deletionImpact.noDependencies")}
              </p>
            ) : (
              <div className="space-y-2">
                {impact.items.map((item) => {
                  const Icon = actionIcon[item.action];
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
                      {item.examples.length > 0 ? (
                        <p className="mt-2 break-words text-xs text-muted-foreground">
                          {t("deletionImpact.examples", { examples: item.examples.join(", ") })}
                        </p>
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
          <AlertDialogCancel disabled={deleting}>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            disabled={!canSubmit}
            onClick={(event) => {
              event.preventDefault();
              void onConfirm(confirmation.trim());
            }}
          >
            {deleting ? t("deletionImpact.deleting") : t("deletionImpact.confirm")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
};
