import { useMemo } from "react";
import { Link } from "react-router-dom";
import { FileText, Building2, Briefcase, ArrowUpRight, Plus, Users } from "lucide-react";
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, XAxis, YAxis } from "recharts";
import AdminLayout from "@/components/layout/AdminLayout";
import {
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
} from "@/components/ui/chart";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { getCurrentUser } from "@/lib/auth";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { PageLoading } from "@/components/PageState";
import { SalesFunnelPanel } from "@/components/admin/SalesFunnelPanel";
import { useActivities, useProjects, useJobPositions } from "@/hooks/useContentApi";

const Dashboard = () => {
  const { t } = useI18n();
  const user = getCurrentUser();
  const { has } = usePermissions();
  const { data: activities } = useActivities();
  const { data: projects } = useProjects();
  const { data: jobPositions } = useJobPositions();

  const totalPosts = (activities ?? []).length;
  const ongoingProjects = (projects ?? []).filter((p) => p.status === "ongoing").length;
  const totalProjects = (projects ?? []).length;
  const openPositions = (jobPositions ?? []).filter((j) => j.isActive).length;
  const totalApplications = (jobPositions ?? []).reduce(
    (sum, j) => sum + (j.applicationCount ?? 0),
    0,
  );

  const recentPosts = (activities ?? []).slice(0, 5);

  const postsOverTime = useMemo(() => {
    const buckets = new Map<string, number>();

    (activities ?? []).forEach((activity) => {
      if (!activity.date) return;
      const parts = activity.date.split(".");
      if (parts.length < 3) return;

      const month = parts[1];
      const year = parts[2];
      if (!month || !year) return;

      const key = `${month}/${year}`;
      buckets.set(key, (buckets.get(key) ?? 0) + 1);
    });

    const entries = Array.from(buckets.entries()).map(([month, count]) => ({ month, count }));
    entries.sort((a, b) => {
      const [aMonth, aYear] = a.month.split("/");
      const [bMonth, bYear] = b.month.split("/");
      const aDate = new Date(Number(aYear), Number(aMonth) - 1, 1).getTime();
      const bDate = new Date(Number(bYear), Number(bMonth) - 1, 1).getTime();
      return aDate - bDate;
    });

    return entries.slice(-12);
  }, [activities]);

  const projectStatusData = useMemo(() => {
    const counts = new Map<string, number>();

    (projects ?? []).forEach((project) => {
      const status = project.status ?? "other";
      counts.set(status, (counts.get(status) ?? 0) + 1);
    });

    return Array.from(counts.entries()).map(([status, value]) => ({
      status,
      value,
      fill:
        status === "ongoing"
          ? "hsl(244 75% 64%)"
          : status === "completed"
            ? "hsl(150 65% 45%)"
            : "hsl(210 16% 82%)",
    }));
  }, [projects]);

  const applicationsPerPosition = useMemo(() => {
    return (jobPositions ?? [])
      .filter((position) => (position.applicationCount ?? 0) > 0)
      .map((position) => ({
        title: position.title,
        applications: position.applicationCount ?? 0,
      }))
      .sort((a, b) => b.applications - a.applications)
      .slice(0, 8);
  }, [jobPositions]);

  const postsOverTimeConfig = useMemo(
    () => ({
      count: {
        label: t("dash.totalPosts"),
        color: "hsl(var(--primary))",
      },
    }),
    [t],
  );

  const projectStatusConfig = useMemo(() => {
    const getLabel = (status: string) => {
      if (status === "ongoing") return t("proj.ongoing");
      if (status === "completed") return t("proj.completed");
      return status;
    };

    return projectStatusData.reduce<Record<string, { label: string; color: string }>>(
      (acc, item) => {
        acc[item.status] = { label: getLabel(item.status), color: item.fill };
        return acc;
      },
      {},
    );
  }, [projectStatusData, t]);

  const applicationsConfig = useMemo(
    () => ({
      applications: {
        label: t("dash.totalApplications"),
        color: "hsl(195 85% 50%)",
      },
    }),
    [t],
  );

  const truncateLabel = (value: string) => (value.length > 25 ? `${value.slice(0, 25)}...` : value);

  const stats = [
    {
      label: t("dash.totalPosts"),
      value: activities ? totalPosts : undefined,
      icon: FileText,
      iconClass: "bg-muted text-foreground",
      to: "/admin/activities",
    },
    {
      label: t("dash.activeProjects"),
      value: projects ? `${ongoingProjects}/${totalProjects}` : undefined,
      icon: Building2,
      iconClass: "bg-primary/10 text-primary",
      to: "/admin/projects",
    },
    {
      label: t("dash.openPositions"),
      value: jobPositions ? openPositions : undefined,
      icon: Briefcase,
      iconClass: "bg-success-soft text-success-strong",
      to: "/admin/recruitment",
    },
    {
      label: t("dash.totalApplications"),
      value: jobPositions ? totalApplications : undefined,
      icon: Users,
      iconClass: "bg-info-soft text-info-strong",
      to: "/admin/recruitment",
    },
  ];

  return (
    <AdminLayout>
      <div className="space-y-5">
        {/* Welcome */}
        <header>
          <p className="text-sm font-medium text-primary">
            {t("dash.welcome")}, {user?.name ?? "Admin"} 👋
          </p>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">{t("dash.overview")}</h1>
        </header>

        {/* Stat cards */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
          {stats.map((s, i) => (
            <Link
              key={i}
              to={s.to}
              className="rounded-lg border bg-card p-4 transition-colors hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <div className="mb-4 flex items-start justify-between">
                <div
                  className={`flex h-10 w-10 items-center justify-center rounded-md ${s.iconClass}`}
                >
                  <s.icon className="h-5 w-5" strokeWidth={1.75} />
                </div>
                <ArrowUpRight className="h-4 w-4 text-muted-foreground" />
              </div>
              <p className="mb-1 text-2xl font-semibold leading-8 tabular-nums">{s.value ?? "—"}</p>
              <p className="text-sm text-muted-foreground">{s.label}</p>
            </Link>
          ))}
        </div>

        {has(ADMIN_PERMS.opportunities) && <SalesFunnelPanel />}

        {/* Charts row */}
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
          <section className="rounded-lg border bg-card p-6 xl:col-span-2">
            <h2 className="mb-5 text-lg font-semibold">{t("dash.postsOverTime")}</h2>
            {!activities ? (
              <PageLoading />
            ) : postsOverTime.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted-foreground">{t("common.noData")}</p>
            ) : (
              <ChartContainer config={postsOverTimeConfig} className="aspect-auto h-64 w-full">
                <BarChart data={postsOverTime} margin={{ left: 8, right: 8, top: 8 }}>
                  <CartesianGrid vertical={false} strokeDasharray="3 3" />
                  <XAxis dataKey="month" tickLine={false} axisLine={false} />
                  <YAxis tickLine={false} axisLine={false} allowDecimals={false} width={32} />
                  <ChartTooltip content={<ChartTooltipContent />} />
                  <Bar dataKey="count" fill="var(--color-count)" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ChartContainer>
            )}
          </section>

          <section className="rounded-lg border bg-card p-6">
            <h2 className="mb-5 text-lg font-semibold">{t("dash.projectsByStatus")}</h2>
            {!projects ? (
              <PageLoading />
            ) : projectStatusData.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted-foreground">{t("common.noData")}</p>
            ) : (
              <ChartContainer config={projectStatusConfig} className="aspect-auto h-64 w-full">
                <PieChart>
                  <ChartTooltip content={<ChartTooltipContent nameKey="status" />} />
                  <Pie
                    data={projectStatusData}
                    dataKey="value"
                    nameKey="status"
                    innerRadius={60}
                    strokeWidth={0}
                  >
                    {projectStatusData.map((entry) => (
                      <Cell key={entry.status} fill={entry.fill} />
                    ))}
                  </Pie>
                  <ChartLegend content={<ChartLegendContent nameKey="status" />} />
                </PieChart>
              </ChartContainer>
            )}
          </section>
        </div>

        {/* Two column */}
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
          {/* Recent posts */}
          <section className="rounded-lg border bg-card p-6 xl:col-span-2">
            <div className="mb-5 flex items-center justify-between">
              <h2 className="text-lg font-semibold">{t("dash.recentPosts")}</h2>
              <Link
                to="/admin/activities"
                className="inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
              >
                {t("dash.viewAll")} <ArrowUpRight className="h-3.5 w-3.5" />
              </Link>
            </div>
            {!activities ? (
              <PageLoading />
            ) : recentPosts.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted-foreground">{t("common.noData")}</p>
            ) : (
              <div className="divide-y">
                {recentPosts.map((p) => (
                  <div key={p.id} className="flex items-center gap-4 py-3 first:pt-0">
                    <img src={p.imageUrl} alt="" className="h-14 w-14 rounded-lg object-cover" />
                    <div className="min-w-0 flex-1">
                      <p className="line-clamp-1 text-sm font-medium">{p.title}</p>
                      <p className="mt-0.5 text-xs text-muted-foreground">
                        {p.date} · {p.category}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Quick action */}
          <section className="rounded-lg border bg-card p-6">
            <h3 className="text-lg font-semibold">{t("dash.createPost")}</h3>
            <p className="mb-5 mt-2 text-sm text-muted-foreground">{t("dash.createPostDesc")}</p>
            <Button asChild size="sm">
              <Link to="/admin/activities/new">
                <Plus /> {t("activities.create")}
              </Link>
            </Button>
          </section>
        </div>

        {applicationsPerPosition.length > 0 ? (
          <section className="rounded-lg border bg-card p-6">
            <h2 className="mb-5 text-lg font-semibold">{t("dash.applicationsByPos")}</h2>
            <ChartContainer config={applicationsConfig} className="aspect-auto h-72 w-full">
              <BarChart
                data={applicationsPerPosition}
                layout="vertical"
                margin={{ left: 40, right: 16 }}
              >
                <CartesianGrid horizontal={false} strokeDasharray="3 3" />
                <XAxis type="number" tickLine={false} axisLine={false} allowDecimals={false} />
                <YAxis
                  dataKey="title"
                  type="category"
                  tickLine={false}
                  axisLine={false}
                  width={140}
                  tickFormatter={truncateLabel}
                />
                <ChartTooltip content={<ChartTooltipContent />} />
                <Bar
                  dataKey="applications"
                  fill="var(--color-applications)"
                  radius={[0, 6, 6, 0]}
                />
              </BarChart>
            </ChartContainer>
          </section>
        ) : null}
      </div>
    </AdminLayout>
  );
};

export default Dashboard;
