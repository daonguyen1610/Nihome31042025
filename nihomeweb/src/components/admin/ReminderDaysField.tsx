import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useI18n } from "@/lib/i18n";

export const MAX_REMINDER_DAYS = 365;
export const DEFAULT_REMINDER_DAYS = 3;

/** "Remind N days before the due date" for one record; 0 reminds on the day itself. */
export function ReminderDaysField({
  id,
  value,
  onChange,
  disabled,
  labelClassName,
}: {
  id: string;
  value: number;
  onChange: (days: number) => void;
  disabled?: boolean;
  /** Lets a form keep the label style of the fields beside it. */
  labelClassName?: string;
}) {
  const { t } = useI18n();
  return (
    <div className="space-y-1">
      <Label htmlFor={id} className={labelClassName}>
        {t("common.reminderDays.label")}
      </Label>
      <Input
        id={id}
        type="number"
        inputMode="numeric"
        min={0}
        max={MAX_REMINDER_DAYS}
        step={1}
        value={value}
        disabled={disabled}
        aria-describedby={`${id}-help`}
        onChange={(event) => {
          const days = Math.trunc(Number(event.target.value));
          onChange(Number.isFinite(days) ? Math.min(MAX_REMINDER_DAYS, Math.max(0, days)) : 0);
        }}
      />
      <p id={`${id}-help`} className="text-xs text-muted-foreground">
        {t("common.reminderDays.help")}
      </p>
    </div>
  );
}
