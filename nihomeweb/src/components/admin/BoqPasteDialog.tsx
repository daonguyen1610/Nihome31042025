import { useMemo, useState } from "react";
import { ClipboardPaste } from "lucide-react";
import { parseBoqPaste } from "@/lib/boqPaste";
import { useI18n } from "@/lib/i18n";
import type { QuoteItemInput } from "@/services/adminApi";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";

interface BoqPasteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (items: QuoteItemInput[]) => void;
}

const BoqPasteDialog = ({ open, onOpenChange, onConfirm }: BoqPasteDialogProps) => {
  const { t } = useI18n();
  const [value, setValue] = useState("");
  const result = useMemo(() => parseBoqPaste(value), [value]);

  const close = () => {
    setValue("");
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={(next) => { if (!next) close(); }}>
      <DialogContent className="sm:max-w-2xl" data-testid="boq-paste-dialog">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <ClipboardPaste className="h-5 w-5" />
            {t("quotes.paste.title")}
          </DialogTitle>
          <DialogDescription>{t("quotes.paste.instructions")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="rounded-md border bg-muted/30 p-3 text-xs leading-relaxed text-muted-foreground">
            <div className="font-medium text-foreground">{t("quotes.paste.columns")}</div>
            <div>{t("quotes.paste.example")}</div>
            <div>{t("quotes.paste.numberHint")}</div>
          </div>
          <Textarea
            autoFocus
            rows={9}
            value={value}
            data-testid="boq-paste-input"
            placeholder={t("quotes.paste.placeholder")}
            onChange={(event) => setValue(event.target.value)}
          />
          {value && (
            <div className="space-y-1 text-sm" aria-live="polite">
              <p className={result.items.length > 0 ? "text-emerald-700" : "text-muted-foreground"}>
                {t("quotes.paste.validCount", { count: result.items.length })}
              </p>
              {result.invalidRows.length > 0 && (
                <p className="text-destructive" data-testid="boq-paste-errors">
                  {t("quotes.paste.invalidRows", { rows: result.invalidRows.join(", ") })}
                </p>
              )}
            </div>
          )}
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={close}>{t("common.cancel")}</Button>
          <Button
            type="button"
            data-testid="boq-paste-confirm"
            disabled={result.items.length === 0 || result.invalidRows.length > 0}
            onClick={() => {
              onConfirm(result.items);
              close();
            }}
          >
            {t("quotes.paste.confirm", { count: result.items.length })}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default BoqPasteDialog;
