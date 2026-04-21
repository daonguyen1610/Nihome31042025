import Link from "next/link";
import { EmptyState } from "@/components/common/empty-state";
import { PageHeader } from "@/components/common/page-header";
import { StatusBadge } from "@/components/common/status-badge";
import { PageContainer } from "@/components/layout/page-container";

export const metadata = {
  title: "Notifications",
};

export default function NotificationsPage() {
  return (
    <main className="py-12 sm:py-16">
      <PageContainer className="space-y-8">
        <div className="flex flex-wrap items-center gap-3">
          <StatusBadge label="Shell ready" tone="success" />
          <StatusBadge label="No data layer yet" tone="warning" />
        </div>

        <PageHeader
          eyebrow="Client portal"
          title="Notifications are staged for a later phase."
          description="The route exists now so future alerting, document review notices, and project updates have a stable place in the portal map."
        />

        <EmptyState
          title="Notification flows are deferred"
          description="This page is part of the Phase 1 structure only. Real alerts will follow once the team chooses auth, API patterns, and event ownership."
          action={
            <Link className="button-secondary" href="/">
              Return home
            </Link>
          }
        />
      </PageContainer>
    </main>
  );
}
