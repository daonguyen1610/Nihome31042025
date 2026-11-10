import { PageEmpty } from "@/components/PageState";
import { useI18n } from "@/lib/i18n";

export function NoDesignProjectNotice() {
  const { t } = useI18n();
  return (
    <div data-testid="construction-no-design-project">
      <PageEmpty message={t("construction.common.noDesignProject.generic")} />
    </div>
  );
}
