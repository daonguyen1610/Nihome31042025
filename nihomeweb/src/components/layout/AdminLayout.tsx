import { ReactNode, useEffect, useMemo, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  FileText,
  Newspaper,
  Building2,
  Inbox,
  Briefcase,
  Settings as SettingsIcon,
  LogOut,
  ExternalLink,
  Menu,
  X,
  PanelLeftClose,
  PanelLeftOpen,
  ChevronDown,
  ChevronRight,
  FolderTree,
  Users,
  UserCog,
  ShieldCheck,
  History,
  Award,
  Handshake,
  Building,
  Image as ImageIcon,
  Info,
  Cog,
  Truck,
  Workflow,
  Mail,
  Languages,
  SlidersHorizontal,
  Database,
  Bell,
  ConciergeBell,
  UserPlus,
  UserRound,
  Target,
  FileSpreadsheet,
  KeyRound,
  ClipboardList,
  PenTool,
  Scale,
  HardHat,
  ShieldAlert,
  User as UserIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { logout } from "@/lib/auth";
import { useI18n } from "@/lib/i18n";
import { useAppSelector } from "@/store";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import LanguageToggle from "@/components/LanguageToggle";
import { NotificationBell } from "@/components/layout/NotificationBell";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import logoNicon from "@/assets/logo-nicon.png";
import type { LucideIcon } from "lucide-react";

const SIDEBAR_STORAGE_KEY = "nicon.admin.sidebar.collapsed";

type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  end?: boolean;
  /** Permission code required to see this entry. Omitted = always visible. */
  permission?: string;
};
type NavGroup = { id: string; label: string; icon: LucideIcon; items: NavItem[] };

const AdminLayout = ({ children }: { children: ReactNode }) => {
  const location = useLocation();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    if (typeof window === "undefined") return false;
    try {
      return window.localStorage.getItem(SIDEBAR_STORAGE_KEY) === "1";
    } catch {
      return false;
    }
  });
  useEffect(() => {
    try {
      window.localStorage.setItem(SIDEBAR_STORAGE_KEY, collapsed ? "1" : "0");
    } catch {
      /* ignore quota / privacy-mode errors */
    }
  }, [collapsed]);
  // The `collapsed` preference is desktop-only: the mobile drawer must always
  // render the full labelled nav even when the desktop layout was last left
  // collapsed. Track viewport via matchMedia so `effectiveCollapsed` flips
  // back to false whenever we drop below the `lg` breakpoint (1024px).
  const [isDesktop, setIsDesktop] = useState<boolean>(() => {
    if (typeof window === "undefined" || !window.matchMedia) return true;
    return window.matchMedia("(min-width: 1024px)").matches;
  });
  useEffect(() => {
    if (typeof window === "undefined" || !window.matchMedia) return;
    const mql = window.matchMedia("(min-width: 1024px)");
    const handler = (e: MediaQueryListEvent) => setIsDesktop(e.matches);
    mql.addEventListener("change", handler);
    return () => mql.removeEventListener("change", handler);
  }, []);
  const effectiveCollapsed = collapsed && isDesktop;
  const user = useAppSelector((state) => state.auth.user);
  const { permissions } = usePermissions();

  const dashboardItem: NavItem = useMemo(
    () => ({
      to: "/admin",
      label: t("nav.dashboard"),
      icon: LayoutDashboard,
      end: true,
      permission: ADMIN_PERMS.dashboard,
    }),
    [t],
  );
  const notificationsItem: NavItem = useMemo(
    () => ({
      to: "/admin/notifications",
      label: t("notify.title"),
      icon: Bell,
      permission: ADMIN_PERMS.notifications,
    }),
    [t],
  );

  const rawGroups: NavGroup[] = useMemo(
    () => [
      {
        id: "crm",
        label: t("nav.crm"),
        icon: UserPlus,
        items: [
          { to: "/admin/leads", label: t("nav.leads"), icon: UserPlus, permission: ADMIN_PERMS.leads },
          { to: "/admin/customers", label: t("nav.customers"), icon: UserRound, permission: ADMIN_PERMS.customers },
          { to: "/admin/opportunities", label: t("nav.opportunities"), icon: Target, permission: ADMIN_PERMS.opportunities },
          { to: "/admin/quotes", label: t("nav.quotes"), icon: FileSpreadsheet, permission: ADMIN_PERMS.quotes },
          { to: "/admin/contracts", label: t("nav.contracts"), icon: FileText, permission: ADMIN_PERMS.contracts },
          { to: "/admin/tenders", label: t("nav.tenders"), icon: Briefcase, permission: ADMIN_PERMS.tenders },
          { to: "/admin/surveys", label: t("nav.surveys"), icon: ClipboardList, permission: ADMIN_PERMS.surveys },
          { to: "/admin/capability-documents", label: t("nav.capabilityDocs"), icon: FolderTree, permission: ADMIN_PERMS.capabilityDocs },
        ],
      },
      {
        id: "design",
        label: t("nav.design"),
        icon: PenTool,
        items: [
          { to: "/admin/design-projects", label: t("nav.designProjects"), icon: PenTool, permission: ADMIN_PERMS.designProjects },
        ],
      },
      {
        id: "permits",
        label: t("nav.permits"),
        icon: Scale,
        items: [
          { to: "/admin/permits", label: t("nav.permits"), icon: Scale, permission: ADMIN_PERMS.permits },
        ],
      },
      {
        id: "construction",
        label: t("nav.construction"),
        icon: HardHat,
        items: [
          { to: "/admin/construction/tasks", label: t("nav.constructionTasks"), icon: HardHat, permission: ADMIN_PERMS.constructionTasks },
          { to: "/admin/construction/diary", label: t("nav.constructionDiary"), icon: ClipboardList, permission: ADMIN_PERMS.constructionDiary },
          { to: "/admin/construction/punchlist", label: t("nav.constructionPunch"), icon: ShieldAlert, permission: ADMIN_PERMS.constructionPunch },
        ],
      },
      {
        id: "content",
        label: t("nav.content"),
        icon: FileText,
        items: [
          { to: "/admin/activities", label: t("nav.activities"), icon: FileText, permission: ADMIN_PERMS.activities },
          { to: "/admin/news", label: t("nav.news"), icon: Newspaper, permission: ADMIN_PERMS.news },
          { to: "/admin/projects", label: t("nav.projects"), icon: Building2, permission: ADMIN_PERMS.projects },
          { to: "/admin/services", label: t("nav.services"), icon: ConciergeBell, permission: ADMIN_PERMS.services },
          { to: "/admin/recruitment", label: t("nav.recruitment"), icon: Briefcase, permission: ADMIN_PERMS.recruitment },
          { to: "/admin/contacts", label: t("nav.contacts"), icon: Inbox, permission: ADMIN_PERMS.contacts },
          { to: "/admin/categories", label: t("nav.categories"), icon: FolderTree, permission: ADMIN_PERMS.categories },
        ],
      },
      {
        id: "branding",
        label: t("nav.branding"),
        icon: ImageIcon,
        items: [
          { to: "/admin/clients", label: t("nav.clients"), icon: Handshake, permission: ADMIN_PERMS.logos },
          { to: "/admin/partners", label: t("nav.partners"), icon: Building, permission: ADMIN_PERMS.logos },
          { to: "/admin/suppliers", label: t("nav.suppliers"), icon: Truck, permission: ADMIN_PERMS.logos },
          { to: "/admin/awards", label: t("nav.awards"), icon: Award, permission: ADMIN_PERMS.logos },
          { to: "/admin/about", label: t("nav.about"), icon: Info, permission: ADMIN_PERMS.about },
        ],
      },
      {
        id: "processes",
        label: t("nav.processes"),
        icon: Workflow,
        items: [
          { to: "/admin/processes/general", label: t("proc.general"), icon: FileText, permission: ADMIN_PERMS.processes },
          { to: "/admin/processes/ptcskh", label: t("proc.ptcskh"), icon: FileText, permission: ADMIN_PERMS.processes },
          { to: "/admin/processes/dt", label: t("proc.dt"), icon: FileText, permission: ADMIN_PERMS.processes },
          { to: "/admin/processes/tk", label: t("proc.tk"), icon: FileText, permission: ADMIN_PERMS.processes },
          { to: "/admin/processes/tc", label: t("proc.tc"), icon: FileText, permission: ADMIN_PERMS.processes },
          { to: "/admin/processes/ttqtct", label: t("proc.ttqtct"), icon: FileText, permission: ADMIN_PERMS.processes },
          { to: "/admin/processes/qlns", label: t("proc.qlns"), icon: FileText, permission: ADMIN_PERMS.processes },
          { to: "/admin/processes/mhdgncu", label: t("proc.mhdgncu"), icon: FileText, permission: ADMIN_PERMS.processes },
        ],
      },
      {
        id: "users",
        label: t("nav.users"),
        icon: Users,
        items: [
          { to: "/admin/users", label: t("nav.userManagement"), icon: UserCog, permission: ADMIN_PERMS.users },
          { to: "/admin/roles", label: t("nav.roleManagement"), icon: ShieldCheck, permission: ADMIN_PERMS.rbacRoles },
        ],
      },
      {
        id: "config",
        label: t("nav.config"),
        icon: Cog,
        items: [
          { to: "/admin/settings", label: t("settings.title"), icon: SlidersHorizontal, permission: ADMIN_PERMS.settings },
          { to: "/admin/languages", label: t("set.languages"), icon: Languages, permission: ADMIN_PERMS.translations },
          { to: "/admin/translations", label: t("nav.translations"), icon: Languages, permission: ADMIN_PERMS.translations },
          { to: "/admin/master-data", label: t("nav.masterData"), icon: Database, permission: ADMIN_PERMS.masterData },
          { to: "/admin/workflows", label: t("nav.workflows"), icon: Workflow, permission: ADMIN_PERMS.workflow },
          { to: "/admin/email-templates", label: t("set.emailTemplates"), icon: Mail, permission: ADMIN_PERMS.emailTemplates },
          { to: "/admin/activity-log", label: t("nav.activityLog"), icon: History, permission: ADMIN_PERMS.activityLog },
        ],
      },
    ],
    [t],
  );

  const groups: NavGroup[] = useMemo(
    () =>
      rawGroups
        .map((g) => ({
          ...g,
          items: g.items.filter((it) => !it.permission || permissions.has(it.permission)),
        }))
        .filter((g) => g.items.length > 0),
    [rawGroups, permissions],
  );

  const showDashboard =
    !dashboardItem.permission || permissions.has(dashboardItem.permission);
  const showNotifications =
    !notificationsItem.permission || permissions.has(notificationsItem.permission);

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

  // Breadcrumb: derive from current path against the flat item list.
  const flatItems = useMemo(() => {
    const all: { to: string; label: string; groupLabel?: string }[] = [
      { to: dashboardItem.to, label: dashboardItem.label },
      { to: notificationsItem.to, label: notificationsItem.label },
    ];
    for (const g of rawGroups) {
      for (const it of g.items) {
        all.push({ to: it.to, label: it.label, groupLabel: g.label });
      }
    }
    return all;
  }, [dashboardItem, notificationsItem, rawGroups]);

  const breadcrumb = useMemo(() => {
    const path = location.pathname;
    // find longest matching route so nested routes still resolve correctly
    const matches = flatItems
      .filter((it) => path === it.to || path.startsWith(`${it.to}/`))
      .sort((a, b) => b.to.length - a.to.length);
    const current = matches[0];
    const crumbs: { label: string; to?: string }[] = [{ label: t("nav.dashboard"), to: "/admin" }];
    if (!current) return crumbs;
    if (current.to === "/admin") return crumbs;
    if (current.groupLabel) crumbs.push({ label: current.groupLabel });
    crumbs.push({ label: current.label });
    return crumbs;
  }, [flatItems, location.pathname, t]);

  const renderItem = (item: NavItem) => {
    const active = item.end
      ? location.pathname === item.to
      : location.pathname.startsWith(item.to);
    return (
      <Link
        key={item.to}
        to={item.to}
        onClick={() => setOpen(false)}
        title={effectiveCollapsed ? item.label : undefined}
        aria-current={active ? "page" : undefined}
        className={cn(
          "flex items-center gap-3 rounded-xl text-sm font-semibold transition-all",
          effectiveCollapsed ? "px-3 py-3 justify-center" : "px-4 py-2.5",
          active
            ? "bg-gradient-to-br from-primary to-orange-500 text-primary-foreground shadow-md shadow-primary/40"
            : "text-foreground/70 hover:bg-muted",
        )}
      >
        <item.icon className="w-4 h-4 shrink-0" strokeWidth={1.75} />
        {!effectiveCollapsed && <span className="truncate">{item.label}</span>}
      </Link>
    );
  };

  return (
    <div className="admin-scope min-h-screen flex">
      {/* Sidebar */}
      <aside
        className={cn(
          "fixed lg:sticky top-0 left-0 z-40 h-screen transition-all duration-300 bg-background border-r flex flex-col",
          effectiveCollapsed ? "lg:w-20" : "lg:w-72",
          "w-72",
          open ? "translate-x-0" : "-translate-x-full lg:translate-x-0",
        )}
      >
        <div className={cn("py-7 flex items-center justify-between", effectiveCollapsed ? "px-4" : "px-7")}>
          <Link to="/admin" className="flex items-center gap-2 min-w-0">
            <img
              src={logoNicon}
              alt="NICON"
              className={cn("w-auto object-contain transition-all", effectiveCollapsed ? "h-9" : "h-11")}
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
          {showDashboard && renderItem(dashboardItem)}
          {showNotifications && renderItem(notificationsItem)}

          {/* Groups */}
          {groups.map((g) => {
            const groupActive = g.items.some((it) =>
              location.pathname.startsWith(it.to),
            );
            const isOpen = openGroups[g.id] ?? false;
            if (effectiveCollapsed) {
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
                  aria-expanded={isOpen}
                  aria-controls={`admin-nav-group-${g.id}`}
                  className={cn(
                    "w-full flex items-center gap-3 rounded-xl px-4 py-2.5 text-[11px] uppercase tracking-[0.18em] font-bold transition text-muted-foreground",
                    groupActive ? "" : "hover:bg-muted/60",
                  )}
                >
                  <g.icon className="w-3.5 h-3.5" />
                  <span className="flex-1 text-left">{g.label}</span>
                  <ChevronDown
                    className={cn("w-3.5 h-3.5 transition-transform", isOpen ? "rotate-180" : "")}
                  />
                </button>
                {isOpen && (
                  <div id={`admin-nav-group-${g.id}`} className="mt-1 space-y-1">
                    {g.items.map(renderItem)}
                  </div>
                )}
              </div>
            );
          })}
        </nav>

        <div className="px-4 py-5 border-t space-y-1">
          <Link
            to="/"
            title={effectiveCollapsed ? t("nav.viewSite") : undefined}
            className={cn(
              "flex items-center gap-3 rounded-xl text-sm font-semibold hover:bg-muted transition text-foreground/70",
              effectiveCollapsed ? "px-3 py-2.5 justify-center" : "px-4 py-2.5",
            )}
          >
            <ExternalLink className="w-4 h-4" /> {!effectiveCollapsed && t("nav.viewSite")}
          </Link>
          <button
            onClick={handleLogout}
            title={effectiveCollapsed ? t("nav.logout") : undefined}
            className={cn(
              "w-full flex items-center gap-3 rounded-xl text-sm font-semibold hover:bg-destructive/10 transition text-destructive",
              effectiveCollapsed ? "px-3 py-2.5 justify-center" : "px-4 py-2.5",
            )}
          >
            <LogOut className="w-4 h-4" /> {!effectiveCollapsed && t("nav.logout")}
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
        <header className="sticky top-0 z-20 h-16 bg-background/85 backdrop-blur-xl border-b flex items-center px-5 lg:px-8 gap-3">
          <button
            onClick={() => setOpen(true)}
            className="lg:hidden w-9 h-9 rounded-full bg-muted flex items-center justify-center"
            aria-label={t("nav.openSidebar")}
          >
            <Menu className="w-4 h-4" />
          </button>
          <button
            onClick={() => setCollapsed((c) => !c)}
            className="hidden lg:flex w-9 h-9 rounded-full bg-muted hover:bg-muted/70 items-center justify-center transition"
            aria-label={t("nav.toggleSidebar")}
            title={collapsed ? t("nav.expand") : t("nav.collapse")}
          >
            {collapsed ? <PanelLeftOpen className="w-4 h-4" /> : <PanelLeftClose className="w-4 h-4" />}
          </button>

          {/* Breadcrumb */}
          <nav aria-label="breadcrumb" className="hidden md:flex min-w-0 items-center gap-1.5 text-sm text-muted-foreground">
            {breadcrumb.map((c, i) => {
              const isLast = i === breadcrumb.length - 1;
              return (
                <span key={`${c.label}-${i}`} className="flex items-center gap-1.5 min-w-0">
                  {i > 0 && <ChevronRight className="w-3.5 h-3.5 shrink-0 opacity-60" />}
                  {c.to && !isLast ? (
                    <Link to={c.to} className="truncate hover:text-foreground transition">
                      {c.label}
                    </Link>
                  ) : (
                    <span className={cn("truncate", isLast && "text-foreground font-medium")}>
                      {c.label}
                    </span>
                  )}
                </span>
              );
            })}
          </nav>

          <div className="ml-auto flex items-center gap-3">
            <LanguageToggle />
            <NotificationBell />
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button
                  type="button"
                  className="flex items-center gap-3 pl-3 md:border-l md:border-border rounded-full md:rounded-md p-1 hover:bg-muted transition"
                  aria-label={t("nav.userMenu")}
                >
                  <div className="w-9 h-9 rounded-full text-primary-foreground flex items-center justify-center font-bold text-xs bg-gradient-to-br from-primary to-orange-500">
                    {user?.fullName?.[0]?.toUpperCase() ?? "A"}
                  </div>
                  <div className="hidden md:block text-xs text-left">
                    <p className="font-bold leading-tight">{user?.fullName ?? "Admin"}</p>
                    <p className="text-muted-foreground">{user?.email ?? "admin@nicon.vn"}</p>
                  </div>
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-56">
                <DropdownMenuLabel className="font-normal">
                  <p className="text-sm font-medium">{user?.fullName ?? "Admin"}</p>
                  <p className="text-xs text-muted-foreground">{user?.email ?? "admin@nicon.vn"}</p>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link to="/my-profile">
                    <UserIcon className="mr-2 h-4 w-4" />
                    {t("nav.myProfile")}
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuItem asChild>
                  <Link to="/my-profile?tab=security">
                    <KeyRound className="mr-2 h-4 w-4" />
                    {t("nav.changePassword")}
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={handleLogout} className="text-destructive focus:text-destructive">
                  <LogOut className="mr-2 h-4 w-4" />
                  {t("nav.logout")}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </header>

        <main className="flex-1 p-5 lg:p-8">{children}</main>
      </div>
    </div>
  );
};

export default AdminLayout;
