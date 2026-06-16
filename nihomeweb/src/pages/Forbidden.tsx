import { Link } from "react-router-dom";
import { useI18n } from "@/lib/i18n";

const Forbidden = () => {
  const { t } = useI18n();
  return (
    <div className="flex min-h-screen items-center justify-center bg-muted px-6">
      <div className="max-w-md text-center">
        <h1 className="mb-4 text-4xl font-bold">403</h1>
        <p className="mb-2 text-xl font-semibold text-foreground">{t("forbidden.title")}</p>
        <p className="mb-6 text-sm text-muted-foreground">{t("forbidden.description")}</p>
        <Link
          to="/admin"
          className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
        >
          {t("forbidden.back")}
        </Link>
      </div>
    </div>
  );
};

export default Forbidden;
