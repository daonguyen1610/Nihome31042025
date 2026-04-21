import Link from "next/link";
import { PageHeader } from "@/components/common/page-header";
import { StatusBadge } from "@/components/common/status-badge";
import { PageContainer } from "@/components/layout/page-container";

const highlights = [
  "Client portal shell with branded home experience",
  "Shared layout primitives ready for future screens",
  "Runtime-ready Next.js foundation for later auth and API work",
];

export default function ClientHomePage() {
  return (
    <main className="relative isolate overflow-hidden py-12 sm:py-16">
      <div className="hero-mesh absolute inset-0 -z-20" />
      <div className="hero-glow absolute inset-x-0 top-0 -z-10 h-[32rem]" />

      <PageContainer className="space-y-12">
        <section className="grid gap-10 lg:grid-cols-[1.2fr_0.8fr] lg:items-end">
          <div className="space-y-8">
            <PageHeader
              eyebrow="Client portal"
              title="Property operations that feel calm, premium, and connected."
              description="The Nihome client experience now has a Phase 1 shell: branded, structured, and ready for projects, notifications, and deeper portal flows without pretending those features already exist."
              actions={
                <>
                  <Link className="button-primary" href="/projects">
                    View projects
                  </Link>
                  <Link className="button-secondary" href="/notifications">
                    Open notifications
                  </Link>
                </>
              }
            />

            <div className="grid gap-3 sm:grid-cols-3">
              {highlights.map((item) => (
                <article className="feature-card" key={item}>
                  <span className="feature-dot" />
                  <p>{item}</p>
                </article>
              ))}
            </div>
          </div>

          <aside className="status-panel">
            <div className="space-y-4">
              <StatusBadge label="Phase 1 shell" tone="info" />
              <h2 className="font-display text-3xl text-[#1f2933]">
                The client home now anchors the portal instead of acting as a
                disconnected landing experiment.
              </h2>
              <p className="text-base leading-8 text-[#52606d]">
                This keeps the current Nihome visual tone while introducing the
                route structure, layout system, and reusable pieces that later
                features can plug into.
              </p>
            </div>

            <div className="status-card space-y-4">
              <p className="text-sm font-semibold uppercase tracking-[0.2em] text-[#9c6b46]">
                What this shell includes
              </p>
              <ul className="space-y-3 text-sm leading-7 text-[#52606d]">
                <li>Client navigation and portal-aware layout</li>
                <li>Placeholder routes for projects and notifications</li>
                <li>Admin shell and auth shell for future phases</li>
              </ul>
            </div>

            <div className="rounded-[28px] bg-[#1f2933] px-6 py-5 text-[#f8f1e7] shadow-xl">
              <p className="text-sm uppercase tracking-[0.2em] text-[#f3c892]">
                Next step
              </p>
              <p className="mt-3 text-base leading-7">
                Phase 2 can now decide auth, API patterns, and deeper modules
                on top of a runtime-ready shell.
              </p>
            </div>
          </aside>
        </section>
      </PageContainer>
    </main>
  );
}
