import Link from "next/link";
import { EmptyState } from "@/components/common/empty-state";
import { PageHeader } from "@/components/common/page-header";
import { PageContainer } from "@/components/layout/page-container";

export const metadata = {
  title: "Projects",
};

export default function ProjectsPage() {
  return (
    <main className="py-12 sm:py-16">
      <PageContainer className="space-y-8">
        <PageHeader
          eyebrow="Client portal"
          title="Projects will appear here as the portal gains real data."
          description="Phase 1 keeps this screen lightweight on purpose. It proves the client route group, layout, and empty-state handling without introducing API assumptions too early."
        />

        <EmptyState
          title="No live project feed yet"
          description="Project data, progress views, and document coordination will be added after the team decides the auth and API integration layers."
          action={
            <Link className="button-secondary" href="/">
              Back to client home
            </Link>
          }
        />
      </PageContainer>
    </main>
  );
}
