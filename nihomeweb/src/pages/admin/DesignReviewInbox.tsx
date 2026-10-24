import { useParams, useSearchParams } from "react-router-dom";
import { useI18n } from "@/lib/i18n";
import AdminLayout from "@/components/layout/AdminLayout";
import { DesignReviewsTab } from "./design/DesignReviewsTab";

export default function DesignReviewInbox() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const { t } = useI18n();
  const projectId = Number(id);
  const reviewId = Number(searchParams.get("reviewId"));
  if (!Number.isSafeInteger(projectId) || projectId <= 0)
    return (
      <AdminLayout>
        <div role="alert">{t("common.notFound")}</div>
      </AdminLayout>
    );
  return (
    <AdminLayout>
      <div className="mx-auto w-full max-w-7xl space-y-4">
        <DesignReviewsTab
          recipientProjectId={projectId}
          focusedReviewId={Number.isSafeInteger(reviewId) && reviewId > 0 ? reviewId : undefined}
        />
      </div>
    </AdminLayout>
  );
}
