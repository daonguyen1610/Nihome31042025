import { LoadingState } from "@/components/common/loading-state";
import { PageHeader } from "@/components/common/page-header";
import { StatusBadge } from "@/components/common/status-badge";

const dashboardCards = [
  {
    title: "Admin shell is live",
    detail: "Sidebar, header, and dashboard route are ready for later modules.",
    tone: "success" as const,
  },
  {
    title: "Module depth is deferred",
    detail: "CRM, design, construction, procurement, and finance wait for later phases.",
    tone: "warning" as const,
  },
  {
    title: "No data assumptions yet",
    detail: "Phase 1 avoids auth, API, and server-state commitments on purpose.",
    tone: "info" as const,
  },
];

export const metadata = {
  title: "Admin dashboard",
};

export default function AdminDashboardPage() {
  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="Admin portal"
        title="The Nihome admin shell is ready for deeper modules."
        description="This dashboard is intentionally light. It establishes the runtime-ready shell, shared layout behavior, and reusable presentation patterns without introducing business logic too early."
      />

      <div className="grid gap-4 xl:grid-cols-3">
        {dashboardCards.map((card) => (
          <article className="surface-card p-6" key={card.title}>
            <StatusBadge label={card.title} tone={card.tone} />
            <p className="mt-4 text-sm leading-7 text-[#52606d]">{card.detail}</p>
          </article>
        ))}
      </div>

      <LoadingState label="Future admin modules will plug into this shell after auth, API, and state decisions are documented." />
    </section>
  );
}
