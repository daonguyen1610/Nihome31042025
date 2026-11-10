import { useState } from "react";
import { Trash2 } from "lucide-react";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import { Button } from "@/components/ui/button";
import { useToast } from "@/hooks/use-toast";
import { useUiError } from "@/hooks/useUiError";
import { useI18n } from "@/lib/i18n";
import type { ConfirmDeletionRequest, DeletionImpactResponse } from "@/services/adminApi";

interface ConstructionDeletionControlProps {
  rowVersion: string;
  getImpact: () => Promise<{ data: DeletionImpactResponse }>;
  deleteRecord: (request: ConfirmDeletionRequest) => Promise<unknown>;
  onDeleted: () => void;
}

export function ConstructionDeletionControl({
  rowVersion,
  getImpact,
  deleteRecord,
  onDeleted,
}: ConstructionDeletionControlProps) {
  const { t } = useI18n();
  const { toast } = useToast();
  const uiError = useUiError();
  const [open, setOpen] = useState(false);
  const [impact, setImpact] = useState<DeletionImpactResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const showImpact = async () => {
    setOpen(true);
    setLoading(true);
    setError(null);
    try {
      const response = await getImpact();
      setImpact(response.data);
    } catch (cause) {
      setError(uiError(cause, t("construction.common.actionError")));
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <Button
        size="sm"
        variant="ghost"
        className="text-destructive hover:text-destructive"
        onClick={() => void showImpact()}
      >
        <Trash2 className="mr-2 h-4 w-4" />
        {t("common.delete")}
      </Button>
      <DeletionImpactDialog
        open={open}
        impact={impact}
        loading={loading}
        deleting={deleting}
        error={error}
        onOpenChange={setOpen}
        onConfirm={async (confirmation) => {
          if (!impact) return null;
          setDeleting(true);
          setError(null);
          try {
            await deleteRecord({
              confirmation,
              planToken: impact.planToken,
              rowVersion,
            });
            return null;
          } catch (cause) {
            setError(uiError(cause, t("construction.common.actionError")));
            throw cause;
          } finally {
            setDeleting(false);
          }
        }}
        onCompleted={() => {
          setOpen(false);
          setImpact(null);
          toast({ title: t("construction.actions.deleted") });
          onDeleted();
        }}
      />
    </>
  );
}
