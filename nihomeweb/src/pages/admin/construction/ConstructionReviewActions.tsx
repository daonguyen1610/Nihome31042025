import { useState } from "react";
import { Check, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useAppSelector } from "@/store";
import { useI18n } from "@/lib/i18n";

interface ConstructionReviewActionsProps {
  allowed: boolean;
  status: string;
  createdByUserId?: number | null;
  lastEditedByUserId?: number | null;
  onDecide: (approved: boolean, reason: string) => Promise<boolean>;
}

export function ConstructionReviewActions({
  allowed,
  status,
  createdByUserId,
  lastEditedByUserId,
  onDecide,
}: ConstructionReviewActionsProps) {
  const { t } = useI18n();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId);
  const [open, setOpen] = useState(false);
  const [approved, setApproved] = useState(true);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  if (!allowed || status !== "PendingCmReview") return null;
  if (
    (createdByUserId != null && createdByUserId === currentUserId) ||
    (lastEditedByUserId != null && lastEditedByUserId === currentUserId)
  ) {
    return (
      <p role="status" className="text-sm text-muted-foreground">
        {t("cmAreas.independentReviewer")}
      </p>
    );
  }

  const openDialog = (nextApproved: boolean) => {
    setApproved(nextApproved);
    setReason("");
    setOpen(true);
  };

  const confirm = async () => {
    if (!reason.trim()) return;
    setBusy(true);
    try {
      const saved = await onDecide(approved, reason.trim());
      if (!saved) return;
      setOpen(false);
      setReason("");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="flex flex-wrap gap-2">
        <Button size="sm" onClick={() => openDialog(true)}>
          <Check className="mr-2 h-4 w-4" />
          {t("construction.common.approve")}
        </Button>
        <Button
          size="sm"
          variant="outline"
          className="text-destructive hover:text-destructive"
          onClick={() => openDialog(false)}
        >
          <X className="mr-2 h-4 w-4" />
          {t("construction.common.reject")}
        </Button>
      </div>
      <Dialog open={open} onOpenChange={(nextOpen) => !busy && setOpen(nextOpen)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {t(approved ? "construction.cm.approveTitle" : "construction.cm.rejectTitle")}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="review-reason">{t("construction.cm.decisionReason")}</Label>
            <Textarea
              id="review-reason"
              required
              maxLength={2000}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" disabled={busy} onClick={() => setOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant={approved ? "default" : "destructive"}
              disabled={busy || !reason.trim()}
              onClick={() => void confirm()}
            >
              {t("common.confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
