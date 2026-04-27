import { ReactNode, useMemo, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  FileText,
  Building2,
  Inbox,
  Briefcase,
  Settings as SettingsIcon,
  LogOut,
  ExternalLink,
  Search,
  Bell,
  Menu,
  X,
  PanelLeftClose,
  PanelLeftOpen,
  ChevronDown,
  FolderTree,
  Users,
  History,
  Award,
  Handshake,
  Building,
  Image as ImageIcon,
  Map as MapIcon,
  Info,
  Cog,
  HardDrive,
  HelpCircle,
  Truck,
  Workflow,
  Mail,
  Languages,
  SlidersHorizontal,
  ScrollText,
  AlertTriangle,
  Wrench,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { getCurrentUser, logout } from "@/lib/auth";
import { useI18n } from "@/lib/i18n";
import LanguageToggle from "@/components/LanguageToggle";
import logoNicon from "@/assets/logo-nicon.png";
import type { LucideIcon } from "lucide-react";

type NavItem = { to: string; label: string; icon: LucideIcon; end?: boolean };
type NavGroup = { id: string; label: string; icon: LucideIcon; items: NavItem[] };

const AdminLayout = ({ children }: { children: ReactNode }) => {
  const location = useLocation();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(false);
  const user = getCurrentUser();

  const dashboardItem: NavItem = {
    to: "/admin",
    label: t("nav.dashboard"),
    icon: LayoutDashboard,
    end: true,
  };

  const groups: NavGroup[] = useMemo(
    () => [
      {
        id: "content",
        label: t("nav.content"),
        icon: FileText,
        items: [
          { to: "/admin/posts", label: t("nav.posts"), icon: FileText },
          { to: "/admin/projects", label: t("nav.projects"), icon: Building2 },
          { to: "/admin/recruitment", label: t("nav.recruitment"), icon: Briefcase },
          { to: "/admin/contacts", label: t("nav.contacts"), icon: Inbox },
          { to: "/admin/categories", label: t("nav.categories"), icon: FolderTree },
        ],
      },
      {
        id: "branding",
        label: t("nav.branding"),
        icon: ImageIcon,
        items: [
          { to: "/admin/clients", label: t("nav.clients"), icon: Handshake },
          { to: "/admin/partners", label: t("nav.partners"), icon: Building },
          { to: "/admin/suppliers", label: t("nav.suppliers"), icon: Truck },
          { to: "/admin/awards", label: t("nav.awards"), icon: Award },
          { to: "/admin/about", label: t("nav.about"), icon: Info },
        ],
      },
      {
        id: "processes",
        label: t("nav.processes"),
        icon: Workflow,
        items: [
          { to: "/admin/processes/general", label: t("proc.general"), icon: FileText },
          { to: "/admin/processes/ptcskh", label: t("proc.ptcskh"), icon: FileText },
          { to: "/admin/processes/dt", label: t("proc.dt"), icon: FileText },
          { to: "/admin/processes/tk", label: t("proc.tk"), icon: FileText },
          { to: "/admin/processes/tc", label: t("proc.tc"), icon: FileText },
          { to: "/admin/processes/ttqtct", label: t("proc.ttqtct"), icon: FileText },
          { to: "/admin/processes/qlns", label: t("proc.qlns"), icon: FileText },
          { to: "/admin/processes/mhdgncu", label: t("proc.mhdgncu"), icon: FileText },
        ],
      },
      {
        id: "users",
        label: t("nav.users"),
        icon: Users,
        items: [
          { to: "/admin/activity-log", label: t("nav.activityLog"), icon: History },
        ],
      },
      {
        id: "config",
        label: t("nav.config"),
        icon: Cog,
        items: [
          { to: "/admin/settings", label: t("settings.title"), icon: SlidersHorizontal },
          { to: "/admin/languages", label: t("set.languages"), icon: Languages },
          { to: "/admin/translations", label: t("nav.translations"), icon: Languages },
          { to: "/admin/email-templates", label: t("set.emailTemplates"), icon: Mail },
        ],
      },
      {
        id: "system",
        label: t("nav.system"),
        icon: HardDrive,
        items: [
          { to: "/admin/system/log", label: t("sys.log.title"), icon: ScrollText },
          { to: "/admin/system/warnings", label: t("sys.warn.title"), icon: AlertTriangle },
          { to: "/admin/system/maintenance", label: t("sys.maint.title"), icon: Wrench },
          { to: "/admin/map", label: t("nav.map"), icon: MapIcon },
          { to: "/admin/help", label: t("nav.help"), icon: HelpCircle },
        ],
      },
    ],
    [t],
  );

  // Group expand/collapse: keep group containing active route open
  const initialOpen = useMemo(() => {
    const map: Record<string, boolean> = {};
    groups.forEach((g) => {
      map[g.id] = g.items.some((it) => location.pathname.startsWith(it.to));
    });
    // open content group by default
    if (!Object.values(map).some(Boolean)) map["content"] = true;
    return map;
  }, [groups, location.pathname]);

  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>(initialOpen);
  const toggleGroup = (id: string) =>
    setOpenGroups((s) => ({ ...s, [id]: !s[id] }));

  const handleLogout = () => {
    logout();
    navigate("/login");
  };

  const renderItem = (item: NavItem) => {
    const active = item.end
      ? location.pathname === item.to
      : location.pathname.startsWith(item.to);
    return (
      <Link
        key={item.to}
        to={item.to}
        onClick={() => setOpen(false)}
        title={collapsed ? item.label : undefined}
        className={cn(
          "flex items-center gap-3 rounded-xl text-sm font-semibold transition-all",
          collapsed ? "px-3 py-3 justify-center" : "px-4 py-2.5",
        )}
        style={
          active
            ? {
                background:
                  "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))",
                color: "white",
                boxShadow: "0 8px 18px -6px hsl(var(--admin-primary) / 0.45)",
              }
            : { color: "hsl(var(--admin-sidebar-text))" }
        }
      >
        <item.icon className="w-4 h-4 shrink-0" strokeWidth={1.75} />
        {!collapsed && <span className="truncate">{item.label}</span>}
      </Link>
    );
  };

  return (
    <div className="admin-scope min-h-screen flex">
      {/* Sidebar */}
      <aside
        className={cn(
          "fixed lg:sticky top-0 left-0 z-40 h-screen transition-all duration-300 bg-white border-r flex flex-col",
          collapsed ? "lg:w-20" : "lg:w-72",
          "w-72",
          open ? "translate-x-0" : "-translate-x-full lg:translate-x-0",
        )}
        style={{ borderColor: "hsl(var(--admin-border))" }}
      >
        <div className={cn("py-7 flex items-center justify-between", collapsed ? "px-4" : "px-7")}>
          <Link to="/admin" className="flex items-center gap-2 min-w-0">
            <img
              src={logoNicon}
              alt="NICON"
              className={cn("w-auto object-contain transition-all", collapsed ? "h-9" : "h-11")}
            />
          </Link>
          <button
            onClick={() => setOpen(false)}
            className="lg:hidden w-9 h-9 rounded-full bg-muted flex items-center justify-center"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        <nav className="flex-1 overflow-y-auto px-4 pb-6 space-y-1">
          {/* Dashboard */}
          {renderItem(dashboardItem)}

          {/* Groups */}
          {groups.map((g) => {
            const groupActive = g.items.some((it) =>
              location.pathname.startsWith(it.to),
            );
            const isOpen = openGroups[g.id] ?? false;
            if (collapsed) {
              // In collapsed mode: render items directly as icons
              return (
                <div key={g.id} className="pt-1">
                  {g.items.map(renderItem)}
                </div>
              );
            }
            return (
              <div key={g.id} className="pt-2">
                <button
                  onClick={() => toggleGroup(g.id)}
                  className={cn(
                    "w-full flex items-center gap-3 rounded-xl px-4 py-2.5 text-[11px] uppercase tracking-[0.18em] font-bold transition",
                    groupActive ? "" : "hover:bg-muted/60",
                  )}
                  style={{ color: "hsl(var(--admin-muted))" }}
                >
                  <g.icon className="w-3.5 h-3.5" />
                  <span className="flex-1 text-left">{g.label}</span>
                  <ChevronDown
                    className={cn("w-3.5 h-3.5 transition-transform", isOpen ? "rotate-180" : "")}
                  />
                </button>
                {isOpen && <div className="mt-1 space-y-1">{g.items.map(renderItem)}</div>}
              </div>
            );
          })}
        </nav>

        <div
          className="px-4 py-5 border-t space-y-1"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <Link
            to="/"
            title={collapsed ? t("nav.viewSite") : undefined}
            className={cn(
              "flex items-center gap-3 rounded-xl text-sm font-semibold hover:bg-muted transition",
              collapsed ? "px-3 py-2.5 justify-center" : "px-4 py-2.5",
            )}
            style={{ color: "hsl(var(--admin-sidebar-text))" }}
          >
            <ExternalLink className="w-4 h-4" /> {!collapsed && t("nav.viewSite")}
          </Link>
          <button
            onClick={handleLogout}
            title={collapsed ? t("nav.logout") : undefined}
            className={cn(
              "w-full flex items-center gap-3 rounded-xl text-sm font-semibold hover:bg-muted transition",
              collapsed ? "px-3 py-2.5 justify-center" : "px-4 py-2.5",
            )}
            style={{ color: "hsl(var(--admin-danger))" }}
          >
            <LogOut className="w-4 h-4" /> {!collapsed && t("nav.logout")}
          </button>
        </div>
      </aside>

      {open && (
        <div
          onClick={() => setOpen(false)}
          className="lg:hidden fixed inset-0 z-30 bg-black/40"
        />
      )}

      {/* Main */}
      <div className="flex-1 min-w-0 flex flex-col">
        {/* Topbar */}
        <header
          className="sticky top-0 z-20 h-16 bg-white/85 backdrop-blur-xl border-b flex items-center px-5 lg:px-8 gap-3"
          style={{ borderColor: "hsl(var(--admin-border))" }}
        >
          <button
            onClick={() => setOpen(true)}
            className="lg:hidden w-9 h-9 rounded-full bg-muted flex items-center justify-center"
          >
            <Menu className="w-4 h-4" />
          </button>
          <button
            onClick={() => setCollapsed((c) => !c)}
            className="hidden lg:flex w-9 h-9 rounded-full bg-muted hover:bg-muted/70 items-center justify-center transition"
            aria-label="Toggle sidebar"
            title={collapsed ? "Mở rộng" : "Thu nhỏ"}
          >
            {collapsed ? <PanelLeftOpen className="w-4 h-4" /> : <PanelLeftClose className="w-4 h-4" />}
          </button>
          <div className="hidden md:block max-w-xs w-full">
            <div
              className="flex items-center gap-2 rounded-full px-4 py-2 border"
              style={{
                background: "hsl(var(--admin-bg))",
                borderColor: "hsl(var(--admin-border))",
              }}
            >
              <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
              <input
                placeholder={t("common.search")}
                className="bg-transparent outline-none text-sm flex-1 placeholder:opacity-60 min-w-0"
              />
            </div>
          </div>
          <div className="ml-auto flex items-center gap-3">
            <LanguageToggle />
            <button
              className="w-9 h-9 rounded-full flex items-center justify-center hover:bg-muted transition"
              style={{ color: "hsl(var(--admin-sidebar-text))" }}
              aria-label="Notifications"
            >
              <Bell className="w-4 h-4" />
            </button>
            <div
              className="hidden md:flex items-center gap-3 pl-3 border-l"
              style={{ borderColor: "hsl(var(--admin-border))" }}
            >
              <div
                className="w-9 h-9 rounded-full text-white flex items-center justify-center font-bold text-xs"
                style={{
                  background:
                    "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))",
                }}
              >
                {user?.name?.[0]?.toUpperCase() ?? "A"}
              </div>
              <div className="text-xs">
                <p className="font-bold leading-tight">{user?.name ?? "Admin"}</p>
                <p style={{ color: "hsl(var(--admin-muted))" }}>
                  {user?.email ?? "admin@nicon.vn"}
                </p>
              </div>
            </div>
          </div>
        </header>

        <main className="flex-1 p-5 lg:p-8">{children}</main>
      </div>
    </div>
  );
};

export default AdminLayout;
