import { Link } from "react-router-dom";
import {
  FileText,
  Building2,
  Briefcase,
  ArrowUpRight,
  Plus,
  PieChart as PieChartIcon,
  Users,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { getCurrentUser } from "@/lib/auth";
import { useActivities, useProjects, useJobPositions } from "@/hooks/useContentApi";

const Dashboard = () => {
  const { t } = useI18n();
  const user = getCurrentUser();
  const { data: activities } = useActivities();
  const { data: projects } = useProjects();
  const { data: jobPositions } = useJobPositions();

  const totalPosts = (activities ?? []).length;
  const ongoingProjects = (projects ?? []).filter((p) => p.status === "ongoing").length;
  const totalProjects = (projects ?? []).length;
  const openPositions = (jobPositions ?? []).filter((j) => j.isActive).length;
  const totalApplications = (jobPositions ?? []).reduce((sum, j) => sum + (j.applicationCount ?? 0), 0);

  const recentPosts = (activities ?? []).slice(0, 5);

  const stats = [
    {
      label: t("dash.totalPosts"),
      value: totalPosts,
      icon: FileText,
      bg: "linear-gradient(135deg, hsl(244 75% 64%), hsl(255 80% 72%))",
      to: "/admin/posts",
    },
    {
      label: t("dash.activeProjects"),
      value: `${ongoingProjects}/${totalProjects}`,
      icon: Building2,
      bg: "linear-gradient(135deg, hsl(354 84% 57%), hsl(22 95% 58%))",
      to: "/admin/projects",
    },
    {
      label: t("dash.openPositions"),
      value: openPositions,
      icon: Briefcase,
      bg: "linear-gradient(135deg, hsl(150 65% 45%), hsl(160 65% 50%))",
      to: "/admin/recruitment",
    },
    {
      label: t("dash.totalApplications"),
      value: totalApplications,
      icon: Users,
      bg: "linear-gradient(135deg, hsl(195 85% 50%), hsl(210 85% 60%))",
      to: "/admin/recruitment",
    },
  ];

  return (
    <AdminLayout>
      {/* Welcome */}
      <div className="mb-8">
        <p className="text-sm font-semibold" style={{ color: "hsl(var(--admin-primary))" }}>
          {t("dash.welcome")}, {user?.name ?? "Admin"} 👋
        </p>
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold mt-1 tracking-tight">{t("dash.overview")}</h1>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-5 mb-8">
        {stats.map((s, i) => (
          <Link key={i} to={s.to} className="admin-stat-card" style={{ background: s.bg }}>
            <div className="relative z-10 flex items-start justify-between mb-6">
              <div className="w-11 h-11 rounded-2xl bg-white/15 backdrop-blur flex items-center justify-center">
                <s.icon className="w-5 h-5" strokeWidth={1.75} />
              </div>
              <ArrowUpRight className="w-4 h-4 opacity-60" />
            </div>
            <p className="font-display text-4xl font-extrabold leading-none mb-2 relative z-10">{s.value}</p>
            <p className="text-sm text-white/85 font-medium relative z-10">{s.label}</p>
          </Link>
        ))}
      </div>

      {/* Two column */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-5">
        {/* Recent posts */}
        <div className="admin-card xl:col-span-2 p-6">
          <div className="flex items-center justify-between mb-5">
            <h2 className="font-display text-lg font-extrabold">{t("dash.recentPosts")}</h2>
            <Link
              to="/admin/posts"
              className="text-xs font-bold inline-flex items-center gap-1"
              style={{ color: "hsl(var(--admin-primary))" }}
            >
              {t("dash.viewAll")} <ArrowUpRight className="w-3.5 h-3.5" />
            </Link>
          </div>
          {recentPosts.length === 0 ? (
            <p className="text-sm py-8 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("common.noData")}
            </p>
          ) : (
            <div className="divide-y" style={{ borderColor: "hsl(var(--admin-border))" }}>
              {recentPosts.map((p) => (
                <div key={p.id} className="py-3 flex items-center gap-4 first:pt-0">
                  <img src={p.imageUrl} alt="" className="w-14 h-14 rounded-xl object-cover" />
                  <div className="flex-1 min-w-0">
                    <p className="font-semibold text-sm line-clamp-1">{p.title}</p>
                    <p className="text-xs mt-0.5" style={{ color: "hsl(var(--admin-muted))" }}>
                      {p.date} · {p.category}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Quick action */}
        <div
          className="admin-card p-6 relative overflow-hidden"
          style={{
            background: "linear-gradient(135deg, hsl(244 75% 64%), hsl(255 80% 72%))",
            color: "white",
            border: "none",
          }}
        >
          <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full" />
          <p className="text-xs uppercase tracking-wider font-bold opacity-80">{t("dash.quickAction")}</p>
          <h3 className="font-display text-xl font-extrabold mt-2 relative z-10">{t("dash.createPost")}</h3>
          <p className="text-sm opacity-85 mt-2 mb-5 relative z-10">{t("dash.createPostDesc")}</p>
          <Link
            to="/admin/posts/new"
            className="inline-flex items-center gap-2 bg-white text-sm font-bold px-5 py-2.5 rounded-full relative z-10"
            style={{ color: "hsl(var(--admin-primary))" }}
          >
            <Plus className="w-4 h-4" /> {t("posts.create")}
          </Link>
        </div>
      </div>
    </AdminLayout>
  );
};

export default Dashboard;
