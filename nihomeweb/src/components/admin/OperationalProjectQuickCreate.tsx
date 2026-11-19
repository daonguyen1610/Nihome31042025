import { useId, useState } from "react";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useI18n } from "@/lib/i18n";
import { useUiError } from "@/hooks/useUiError";
import { adminApi } from "@/services/adminApi";
import type { OperationalProjectResponse } from "@/services/adminApi";

/**
 * Creates the customer's operational project without leaving the form that
 * needs it. The server allocates the PJ code; the caller selects the result.
 */
export function OperationalProjectQuickCreate({
  customerId,
  suggestedName,
  onCreated,
}: {
  customerId: number;
  /** Prefills the name, e.g. with the opportunity's name. */
  suggestedName: string;
  onCreated: (project: OperationalProjectResponse) => void;
}) {
  const { t } = useI18n();
  const uiError = useUiError();
  const inputId = useId();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const start = () => {
    setName(suggestedName.trim());
    setError(null);
    setOpen(true);
  };

  const create = async () => {
    if (saving) return;
    const trimmed = name.trim();
    if (!trimmed) {
      setError(t("opportunities.project.quickNameRequired"));
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const { data } = await adminApi.createOperationalProject({ name: trimmed, customerId });
      setOpen(false);
      onCreated(data);
    } catch (cause) {
      // Keep the typed name so the user can correct and retry.
      setError(uiError(cause));
    } finally {
      setSaving(false);
    }
  };

  if (!open)
    return (
      <Button
        type="button"
        variant="link"
        size="sm"
        className="h-auto px-0 text-sm"
        onClick={start}
        data-testid="opportunity-project-quick-create"
      >
        <Plus className="mr-1 h-4 w-4" aria-hidden />
        {t("opportunities.project.quickCreate")}
      </Button>
    );

  return (
    <div
      className="space-y-2 rounded-md border bg-muted/30 p-3"
      data-testid="opportunity-project-quick-form"
    >
      <Label htmlFor={inputId}>{t("opportunities.project.quickName")}</Label>
      <Input
        id={inputId}
        value={name}
        maxLength={300}
        autoFocus
        disabled={saving}
        aria-invalid={error ? true : undefined}
        aria-describedby={`${inputId}-help`}
        onChange={(event) => {
          setName(event.target.value);
          setError(null);
        }}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.preventDefault();
            void create();
          }
          if (event.key === "Escape") {
            event.stopPropagation();
            setOpen(false);
          }
        }}
      />
      <p id={`${inputId}-help`} className="text-xs text-muted-foreground">
        {t("opportunities.project.quickHelp")}
      </p>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <div className="flex justify-end gap-2">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={saving}
          onClick={() => setOpen(false)}
        >
          {t("common.cancel")}
        </Button>
        <Button
          type="button"
          variant="secondary"
          size="sm"
          disabled={saving}
          onClick={() => void create()}
        >
          {saving ? t("common.saving") : t("opportunities.project.quickSubmit")}
        </Button>
      </div>
    </div>
  );
}
