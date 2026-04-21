import type { StatusTone } from "@/types/common";

type StatusBadgeProps = {
  label: string;
  tone?: StatusTone;
};

const toneStyles: Record<StatusTone, string> = {
  neutral: "bg-white/80 text-[#334e68] border-[#1f2933]/10",
  info: "bg-[#f3e7d9] text-[#8b5e3c] border-[#9c6b46]/15",
  success: "bg-[#e9f4ee] text-[#1d6b42] border-[#1d6b42]/12",
  warning: "bg-[#fff4dd] text-[#9c6b46] border-[#9c6b46]/16",
};

export function StatusBadge({
  label,
  tone = "neutral",
}: StatusBadgeProps) {
  return (
    <span
      className={`inline-flex items-center rounded-full border px-3 py-1 text-sm font-semibold ${toneStyles[tone]}`}
    >
      {label}
    </span>
  );
}
