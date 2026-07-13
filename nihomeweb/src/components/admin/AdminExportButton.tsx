import { FileDown } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type AdminExportButtonProps = {
  onClick: () => void;
  disabled?: boolean;
  label?: string;
  title?: string;
  className?: string;
};

const AdminExportButton = ({ onClick, disabled, label, title, className }: AdminExportButtonProps) => {
  const { t } = useI18n();
  const text = label ?? t("common.exportExcel");

  return (
    <Button
      type="button"
      variant="outline"
      onClick={onClick}
      disabled={disabled}
      title={title ?? text}
      className={cn("gap-2", className)}
    >
      <FileDown className="h-4 w-4 shrink-0" />
      <span>{text}</span>
    </Button>
  );
};

export default AdminExportButton;
