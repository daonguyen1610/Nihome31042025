import { useI18n } from "@/lib/i18n";
import { InlineWarning } from "@/components/PageState";
import { FieldScope } from "@/components/ui/field-scope";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { useDesignDisciplines } from "./useDesignDisciplines";

/**
 * Required discipline picker for drawing forms. Says why it is empty when the
 * discipline list failed to load or has not been set up, instead of leaving an
 * empty dropdown as the only clue.
 */
export function DisciplineSelectField({
  label,
  placeholder,
  value,
  onChange,
  disabled,
  error,
  errorId,
  disciplines,
  triggerTestId,
}: {
  label: string;
  placeholder: string;
  value: string;
  onChange: (code: string) => void;
  disabled?: boolean;
  /** Validation message shown under the field. */
  error?: string;
  errorId: string;
  disciplines: ReturnType<typeof useDesignDisciplines>;
  triggerTestId?: string;
}) {
  const { t } = useI18n();
  return (
    <FieldScope data-form-field="discipline">
      <Label>{label} *</Label>
      <Select value={value} onValueChange={onChange} disabled={disabled}>
        <SelectTrigger
          className="mt-1 h-9"
          data-testid={triggerTestId}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
        >
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {disciplines.options.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {error && (
        <p id={errorId} className="mt-1 text-xs text-destructive">
          {error}
        </p>
      )}
      {disciplines.error ? (
        <div className="mt-2">
          <InlineWarning
            message={t("designProjects.discipline.loadFailed", { reason: disciplines.error })}
            onRetry={() => void disciplines.reload()}
          />
        </div>
      ) : (
        !disciplines.loading &&
        disciplines.options.length === 0 && (
          <p className="mt-1 text-xs text-muted-foreground">
            {t("designProjects.discipline.empty")}
          </p>
        )
      )}
    </FieldScope>
  );
}
