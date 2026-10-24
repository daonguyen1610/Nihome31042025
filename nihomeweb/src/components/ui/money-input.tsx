import { useEffect, useState } from "react";
import { Input } from "@/components/ui/input";
import { formatVnd, parseVnd, parseVndDecimal } from "@/lib/numberFormat";
import { cn } from "@/lib/utils";

type MoneyInputProps = {
  value: number;
  onChange: (next: number) => void;
  id?: string;
  disabled?: boolean;
  className?: string;
  decimalScale?: number;
  formatValue?: (value: number) => string;
  preferDecimalComma?: boolean;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
  "data-testid"?: string;
};

/**
 * VND amount field with thousands separators, built on formatVnd/parseVnd so it
 * reads the same as every other amount in admin.
 *
 * The in-progress string lives in its own state rather than being reformatted on
 * each keystroke: reformatting mid-typing pushes the caret to the end every time
 * the length changes. It reformats on blur, and when an outside value arrives
 * while the field is not focused — a form prefilled from a quote, say.
 */
export const MoneyInput = ({
  value,
  onChange,
  id,
  disabled,
  className,
  decimalScale = 0,
  formatValue = formatVnd,
  preferDecimalComma = false,
  "aria-invalid": ariaInvalid,
  "aria-describedby": ariaDescribedBy,
  "data-testid": testId,
}: MoneyInputProps) => {
  // formatVnd renders an em dash for null/NaN, which suits a display but not a
  // field — nobody can backspace a dash away. Empty stays empty here.
  const display = (amount: number) => (amount ? formatValue(amount) : "");

  const [text, setText] = useState(() => display(value));
  const [focused, setFocused] = useState(false);

  useEffect(() => {
    if (focused) return;
    setText(display(value));
  }, [value, focused, formatValue]);

  // Existing integer-currency fields retain their established 0 fallback.
  // Decimal-aware fields preserve NaN so the quote validator can reject an
  // unreadable BOQ price instead of silently changing it to zero.
  const read = (raw: string): number => {
    const parsed =
      decimalScale > 0 ? parseVndDecimal(raw, decimalScale, preferDecimalComma) : parseVnd(raw);
    return Number.isFinite(parsed) ? parsed : decimalScale > 0 ? Number.NaN : 0;
  };

  return (
    <Input
      id={id}
      aria-invalid={ariaInvalid}
      aria-describedby={ariaDescribedBy}
      disabled={disabled}
      inputMode={decimalScale > 0 ? "decimal" : "numeric"}
      className={cn("text-right tabular-nums", className)}
      data-testid={testId}
      value={text}
      onFocus={() => setFocused(true)}
      onChange={(e) => {
        setText(e.target.value);
        onChange(read(e.target.value));
      }}
      onBlur={() => {
        setFocused(false);
        const parsed = read(text);
        setText(display(parsed));
        onChange(parsed);
      }}
    />
  );
};
