import { useI18n, type Lang } from "@/lib/i18n";
import { Globe, Check } from "lucide-react";
import { cn } from "@/lib/utils";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

type Props = { variant?: "light" | "dark" };

const langs: { code: Lang; label: string; native: string }[] = [
  { code: "vi", label: "VI", native: "Tiếng Việt" },
  { code: "en", label: "EN", native: "English" },
  { code: "zh", label: "中文", native: "中文" },
  { code: "ja", label: "日本", native: "日本語" },
];

const LanguageToggle = ({ variant = "light" }: Props) => {
  const { lang, setLang } = useI18n();
  const current = langs.find((l) => l.code === lang) ?? langs[0];

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          aria-label="Change language"
          className={cn(
            "inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold uppercase tracking-wider transition-all select-none",
            variant === "dark"
              ? "bg-white/10 border border-white/15 text-white hover:bg-white/15"
              : "bg-secondary border border-border text-foreground/80 hover:text-foreground"
          )}
        >
          <Globe className={cn("w-3.5 h-3.5", variant === "dark" ? "text-white/70" : "text-muted-foreground")} />
          <span className="leading-none">{current.label}</span>
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44 rounded-2xl p-1.5">
        {langs.map((l) => (
          <DropdownMenuItem
            key={l.code}
            onClick={() => setLang(l.code)}
            className={cn(
              "flex items-center justify-between gap-2 rounded-xl px-3 py-2 text-sm font-semibold cursor-pointer",
              lang === l.code && "bg-secondary"
            )}
          >
            <span>{l.native}</span>
            {lang === l.code && <Check className="w-3.5 h-3.5 text-primary" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
};

export default LanguageToggle;
