import { FileDown } from "lucide-react";
import { useI18n } from "@/lib/i18n";

type AdminExportButtonProps = {
  onClick: () => void;
  disabled?: boolean;
  label?: string;
  title?: string;
  className?: string;
};

const AdminExportButton = ({ onClick, disabled, label, title, className = "" }: AdminExportButtonProps) => {
  const { t } = useI18n();
  const text = label ?? t("common.exportExcel");

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={title ?? text}
      className={`inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border text-sm font-bold transition hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50 ${className}`}
      style={{ borderColor: "hsl(var(--admin-border))" }}
    >
      <FileDown className="w-4 h-4 shrink-0" />
      <span>{text}</span>
    </button>
  );
};

export default AdminExportButton;
