import { useEffect, useState } from "react";
import { Input } from "@/components/ui/input";
import { formatVnd, parseVnd } from "@/lib/numberFormat";
import { cn } from "@/lib/utils";

type MoneyInputProps = {
  value: number;
  onChange: (next: number) => void;
  id?: string;
  disabled?: boolean;
  className?: string;
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
}: MoneyInputProps) => {
  // formatVnd renders an em dash for null/NaN, which suits a display but not a
  // field — nobody can backspace a dash away. Empty stays empty here.
  const display = (amount: number) => (amount ? formatVnd(amount) : "");

  const [text, setText] = useState(() => display(value));
  const [focused, setFocused] = useState(false);

  useEffect(() => {
    if (focused) return;
    setText(display(value));
  }, [value, focused]);

  // parseVnd returns NaN for anything unreadable; a numeric field has to settle
  // on 0, exactly as numberFormat.ts tells callers to.
  const read = (raw: string): number => {
    const parsed = parseVnd(raw);
    return Number.isFinite(parsed) ? parsed : 0;
  };

  return (
    <Input
      id={id}
      disabled={disabled}
      inputMode="numeric"
      className={cn("text-right tabular-nums", className)}
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
