import Link from "next/link";
import { PageHeader } from "@/components/common/page-header";
import { PageContainer } from "@/components/layout/page-container";

export default function NotFound() {
  return (
    <main className="relative isolate min-h-screen overflow-hidden py-16">
      <div className="hero-mesh absolute inset-0 -z-20" />
      <div className="hero-glow absolute inset-x-0 top-0 -z-10 h-[24rem]" />
      <PageContainer className="flex min-h-[70vh] items-center">
        <div className="surface-panel w-full px-6 py-10 sm:px-10 sm:py-14">
          <PageHeader
            eyebrow="404"
            title="This space has not been built yet."
            description="The route you requested is not part of the current Nihome Phase 1 shell."
            actions={
              <>
                <Link className="button-primary" href="/">
                  Back to client home
                </Link>
                <Link className="button-secondary" href="/admin/dashboard">
                  Open admin shell
                </Link>
              </>
            }
          />
        </div>
      </PageContainer>
    </main>
  );
}
