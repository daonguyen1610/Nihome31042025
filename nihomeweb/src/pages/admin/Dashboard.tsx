import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  FileText,
  Building2,
  Inbox,
  Eye,
  ArrowUpRight,
  Plus,
  TrendingUp,
  Activity,
  RotateCcw,
  PieChart as PieChartIcon,
  Users,
} from "lucide-react";
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
} from "recharts";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { getCurrentUser } from "@/lib/auth";
import { activities } from "@/data/activities";
import { projects } from "@/data/projects";

type Range = "year" | "month" | "week";

const generateSeries = (range: Range) => {
  const now = new Date();
  const points = range === "year" ? 12 : range === "month" ? 30 : 7;
  return Array.from({ length: points }).map((_, i) => {
    const d = new Date(now);
    if (range === "year") d.setMonth(now.getMonth() - (points - 1 - i));
    else d.setDate(now.getDate() - (points - 1 - i));
    const label =
      range === "year"
        ? d.toLocaleDateString("vi-VN", { month: "short" })
        : d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
    const seed = (d.getDate() + d.getMonth() * 13 + (range === "year" ? 7 : 3)) % 11;
    return { label, customers: seed };
  });
};

const Dashboard = () => {
  const { t } = useI18n();
  const user = getCurrentUser();
  const [range, setRange] = useState<Range>("week");

  const stats = [
    {
      label: t("dash.totalPosts"),
      value: activities.length,
      delta: "+12%",
      icon: FileText,
      bg: "linear-gradient(135deg, hsl(244 75% 64%), hsl(255 80% 72%))",
    },
    {
      label: t("dash.activeProjects"),
      value: projects.filter((p) => p.status === "ongoing").length,
      delta: "+3",
      icon: Building2,
      bg: "linear-gradient(135deg, hsl(354 84% 57%), hsl(22 95% 58%))",
    },
    {
      label: t("dash.newContacts"),
      value: 24,
      delta: "+18%",
      icon: Inbox,
      bg: "linear-gradient(135deg, hsl(150 65% 45%), hsl(160 65% 50%))",
    },
    {
      label: t("dash.monthlyViews"),
      value: "12.8K",
      delta: "+34%",
      icon: Eye,
      bg: "linear-gradient(135deg, hsl(195 85% 50%), hsl(210 85% 60%))",
    },
  ];

  const recentPosts = activities.slice(0, 5);

  const data = useMemo(() => generateSeries(range), [range]);
  const totalNew = data.reduce((s, d) => s + d.customers, 0);

  const timeline = [
    { label: "Bài viết mới: Khởi công BMA Tây Ninh", time: "2 giờ trước", color: "hsl(var(--admin-primary))" },
    { label: "Liên hệ mới từ Trang Group", time: "5 giờ trước", color: "hsl(var(--admin-success))" },
    { label: "Cập nhật tiến độ TriMas 75%", time: "1 ngày trước", color: "hsl(var(--admin-warning))" },
    { label: "Ứng viên mới: Kỹ sư M&E", time: "2 ngày trước", color: "hsl(var(--admin-info))" },
  ];

  const RangeBtn = ({ value, label }: { value: Range; label: string }) => (
    <button
      onClick={() => setRange(value)}
      className="px-3 py-1.5 rounded-lg text-xs font-bold transition"
      style={
        range === value
          ? { background: "hsl(var(--admin-primary))", color: "white" }
          : { background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-sidebar-text))" }
      }
    >
      {label}
    </button>
  );

  return (
    <AdminLayout>
      {/* Welcome */}
      <div className="mb-8">
        <p className="text-sm font-semibold" style={{ color: "hsl(var(--admin-primary))" }}>
          {t("dash.welcome")}, {user?.name ?? "Admin"} 👋
        </p>
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold mt-1 tracking-tight">{t("dash.overview")}</h1>
      </div>

      {/* Common statistics */}
      <div className="admin-card mb-6 overflow-hidden">
        <div
          className="px-5 py-3 border-b flex items-center gap-2"
          style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-bg))" }}
        >
          <PieChartIcon className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />
          <h2 className="font-bold text-sm">{t("dash.commonStats")}</h2>
        </div>
        <div className="p-5 grid grid-cols-1 md:grid-cols-2 gap-4">
          <div
            className="rounded-2xl p-5 text-white relative overflow-hidden"
            style={{ background: "linear-gradient(135deg, hsl(38 92% 55%), hsl(28 92% 58%))" }}
          >
            <div className="flex items-start justify-between">
              <div>
                <p className="text-4xl font-extrabold leading-none">0</p>
                <p className="text-sm font-semibold mt-2 opacity-90">{t("dash.pendingReturns")}</p>
              </div>
              <RotateCcw className="w-10 h-10 opacity-30" />
            </div>
            <Link
              to="/admin/contacts"
              className="inline-flex items-center gap-1 text-xs font-bold mt-4 bg-white/15 backdrop-blur px-3 py-1.5 rounded-full"
            >
              {t("dash.moreInfo")} <ArrowUpRight className="w-3 h-3" />
            </Link>
          </div>
          <div
            className="rounded-2xl p-5 text-white relative overflow-hidden"
            style={{ background: "linear-gradient(135deg, hsl(354 84% 57%), hsl(0 80% 60%))" }}
          >
            <div className="flex items-start justify-between">
              <div>
                <p className="text-4xl font-extrabold leading-none">0</p>
                <p className="text-sm font-semibold mt-2 opacity-90">{t("dash.lowStock")}</p>
              </div>
              <PieChartIcon className="w-10 h-10 opacity-30" />
            </div>
            <Link
              to="/admin/categories"
              className="inline-flex items-center gap-1 text-xs font-bold mt-4 bg-white/15 backdrop-blur px-3 py-1.5 rounded-full"
            >
              {t("dash.moreInfo")} <ArrowUpRight className="w-3 h-3" />
            </Link>
          </div>
        </div>
      </div>

      {/* New customers chart */}
      <div className="admin-card mb-6">
        <div
          className="px-5 py-3 border-b flex items-center justify-between gap-2 flex-wrap"
          style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-bg))" }}
        >
          <div className="flex items-center gap-2">
            <Users className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />
            <h2 className="font-bold text-sm">{t("dash.newCustomers")}</h2>
            <span className="text-xs font-bold" style={{ color: "hsl(var(--admin-muted))" }}>
              · {totalNew}
            </span>
          </div>
          <div className="flex items-center gap-1.5">
            <RangeBtn value="year" label={t("dash.year")} />
            <RangeBtn value="month" label={t("dash.month")} />
            <RangeBtn value="week" label={t("dash.week")} />
          </div>
        </div>
        <div className="p-5 h-72">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={data} margin={{ top: 10, right: 20, left: -10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--admin-border))" />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} stroke="hsl(var(--admin-muted))" />
              <YAxis allowDecimals={false} tick={{ fontSize: 11 }} stroke="hsl(var(--admin-muted))" />
              <Tooltip
                contentStyle={{
                  borderRadius: 12,
                  border: "1px solid hsl(var(--admin-border))",
                  fontSize: 12,
                }}
              />
              <Line
                type="monotone"
                dataKey="customers"
                stroke="hsl(var(--admin-primary))"
                strokeWidth={2.5}
                dot={{ r: 3, fill: "hsl(var(--admin-primary))" }}
                activeDot={{ r: 5 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-5 mb-8">
        {stats.map((s, i) => (
          <div key={i} className="admin-stat-card" style={{ background: s.bg }}>
            <div className="relative z-10 flex items-start justify-between mb-6">
              <div className="w-11 h-11 rounded-2xl bg-white/15 backdrop-blur flex items-center justify-center">
                <s.icon className="w-5 h-5" strokeWidth={1.75} />
              </div>
              <span className="inline-flex items-center gap-1 text-xs font-bold bg-white/15 backdrop-blur rounded-full px-2.5 py-1">
                <TrendingUp className="w-3 h-3" /> {s.delta}
              </span>
            </div>
            <p className="font-display text-4xl font-extrabold leading-none mb-2 relative z-10">{s.value}</p>
            <p className="text-sm text-white/85 font-medium relative z-10">{s.label}</p>
          </div>
        ))}
      </div>

      {/* Two column */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-5">
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
          <div className="divide-y" style={{ borderColor: "hsl(var(--admin-border))" }}>
            {recentPosts.map((p) => (
              <div key={p.id} className="py-3 flex items-center gap-4 first:pt-0">
                <img src={p.img} alt="" className="w-14 h-14 rounded-xl object-cover" />
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-sm line-clamp-1">{p.title}</p>
                  <p className="text-xs mt-0.5" style={{ color: "hsl(var(--admin-muted))" }}>
                    {p.date} · {p.category}
                  </p>
                </div>
                <span
                  className="admin-chip"
                  style={{
                    background: "hsl(var(--admin-success-soft))",
                    color: "hsl(var(--admin-success))",
                  }}
                >
                  Live
                </span>
              </div>
            ))}
          </div>
        </div>

        <div className="space-y-5">
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
              to="/admin/posts"
              className="inline-flex items-center gap-2 bg-white text-sm font-bold px-5 py-2.5 rounded-full relative z-10"
              style={{ color: "hsl(var(--admin-primary))" }}
            >
              <Plus className="w-4 h-4" /> {t("posts.create")}
            </Link>
          </div>

          <div className="admin-card p-6">
            <h3 className="font-display text-lg font-extrabold mb-5 flex items-center gap-2">
              <Activity className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} /> {t("dash.activity")}
            </h3>
            <div className="space-y-4">
              {timeline.map((it, i) => (
                <div key={i} className="flex gap-3">
                  <span className="w-2 h-2 rounded-full mt-1.5 shrink-0" style={{ background: it.color }} />
                  <div className="flex-1">
                    <p className="text-sm font-semibold">{it.label}</p>
                    <p className="text-xs mt-0.5" style={{ color: "hsl(var(--admin-muted))" }}>
                      {it.time}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default Dashboard;
