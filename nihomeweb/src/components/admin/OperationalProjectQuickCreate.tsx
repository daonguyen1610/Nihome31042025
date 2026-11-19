import { useId, useState, type KeyboardEvent } from "react";
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
 * needs it. The code is prefilled with the server's next PJ code, as on the
 * Projects page, and may be edited; the caller selects the result.
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
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const start = async () => {
    setCode("");
    setName(suggestedName.trim());
    setError(null);
    setOpen(true);
    try {
      const { data } = await adminApi.suggestOperationalProjectCode();
      // Keep anything the user typed while the suggestion loaded.
      setCode((current) => current || data.code);
    } catch (cause) {
      // Without a suggestion the server still allocates a code on create.
      setError(uiError(cause));
    }
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
      const { data } = await adminApi.createOperationalProject({
        code: code.trim().toUpperCase() || null,
        name: trimmed,
        customerId,
      });
      setOpen(false);
      onCreated(data);
    } catch (cause) {
      // Keep the typed name so the user can correct and retry.
      setError(uiError(cause));
    } finally {
      setSaving(false);
    }
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      void create();
    }
    if (event.key === "Escape") {
      event.stopPropagation();
      setOpen(false);
    }
  };

  if (!open)
    return (
      <Button
        type="button"
        variant="link"
        size="sm"
        className="h-auto px-0 text-sm"
        onClick={() => void start()}
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
      <div className="grid gap-2 sm:grid-cols-[11rem_1fr]">
        <div className="space-y-1">
          <Label htmlFor={`${inputId}-code`}>{t("operationalProjects.field.code")}</Label>
          <Input
            id={`${inputId}-code`}
            value={code}
            maxLength={40}
            autoComplete="off"
            disabled={saving}
            aria-describedby={`${inputId}-help`}
            onChange={(event) => {
              setCode(event.target.value);
              setError(null);
            }}
            onKeyDown={onKeyDown}
          />
        </div>
        <div className="space-y-1">
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
            onKeyDown={onKeyDown}
          />
        </div>
      </div>
      <p id={`${inputId}-help`} className="text-xs text-muted-foreground">
        {t("opportunities.project.quickCustomerHelp")} {t("operationalProjects.codeImmutableHint")}
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
