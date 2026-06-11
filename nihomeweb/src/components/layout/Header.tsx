import { useState, useEffect, useMemo } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import { Menu, X, Search, User, LogOut, LayoutDashboard } from "lucide-react";
import { cn } from "@/lib/utils";
import { isAdminRole } from "@/lib/auth";
import { useI18n } from "@/lib/i18n";
import { useAppSelector, useAppDispatch } from "@/store";
import { logoutThunk } from "@/store/authSlice";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import LanguageToggle from "@/components/LanguageToggle";
import logoNicon from "@/assets/logo-nicon.png";

const Header = () => {
  const { t } = useI18n();
  const dispatch = useAppDispatch();
  const authUser = useAppSelector((s) => s.auth.user);
  const isAdmin = authUser ? isAdminRole(authUser.role) : false;
  const nav = useMemo(
    () => [
      { to: "/", label: t("site.nav.home") },
      { to: "/profile", label: t("site.nav.profile") },
      { to: "/services", label: t("site.nav.services") },
      { to: "/projects", label: t("site.nav.projects") },
      { to: "/news", label: t("site.nav.news") },
      { to: "/activities", label: t("site.nav.activities") },
      { to: "/clients", label: t("site.nav.clients") },
      { to: "/recruitment", label: t("site.nav.recruitment") },
      { to: "/contact", label: t("site.nav.contact") },
    ],
    [t],
  );
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);
  const location = useLocation();

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 20);
    onScroll();
    window.addEventListener("scroll", onScroll);
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => setOpen(false), [location.pathname]);

  const isHome = location.pathname === "/";
  const transparent = isHome && !scrolled && !open;

  return (
    <header
      className={cn(
        "fixed top-0 left-0 right-0 z-50 transition-all duration-500",
        transparent ? "bg-transparent" : "bg-background/85 backdrop-blur-xl border-b border-border/60 shadow-soft"
      )}
    >
      <div className="container-custom">
        <div className="flex items-center justify-between gap-3 h-20 lg:h-[88px]">
          {/* Logo */}
          <Link to="/" className="flex items-center group shrink-0">
            <img
              src={logoNicon}
              alt="NICON - Partner of Development"
              className={cn(
                "h-10 2xl:h-14 w-auto object-contain transition-all duration-500",
                transparent && "brightness-0 invert"
              )}
            />
          </Link>

          {/* Desktop nav — pill style */}
          <nav
            className={cn(
              "hidden 2xl:flex items-center gap-0.5 rounded-full transition-all duration-500 px-1.5 min-w-0 flex-shrink",
              transparent
                ? "bg-white/10 backdrop-blur-md border border-white/15"
                : "bg-secondary border border-border"
            )}
          >
            {nav.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === "/"}
                className={({ isActive }) =>
                  cn(
                    "px-2.5 2xl:px-4 py-2 text-[13px] 2xl:text-sm font-semibold rounded-full transition-all whitespace-nowrap",
                    isActive
                      ? "bg-primary text-primary-foreground shadow-glow"
                      : transparent
                      ? "text-white/85 hover:text-white hover:bg-white/10"
                      : "text-foreground/75 hover:text-foreground hover:bg-background"
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          {/* Right utilities */}
          <div className="hidden 2xl:flex items-center gap-2 shrink-0">
            <button
              aria-label={t("site.nav.search")}
              className={cn(
                "hidden 2xl:flex w-9 h-9 rounded-full items-center justify-center transition-colors",
                transparent
                  ? "text-white/85 hover:text-white hover:bg-white/10"
                  : "text-foreground/70 hover:text-foreground hover:bg-secondary"
              )}
            >
              <Search className="w-4 h-4" />
            </button>
            <LanguageToggle variant={transparent ? "dark" : "light"} />
            {authUser ? (
              <DropdownMenu>
                <DropdownMenuTrigger
                  className={cn(
                    "flex items-center gap-1.5 pl-2.5 pr-3 2xl:pl-3 2xl:pr-4 py-2 rounded-full text-[11px] 2xl:text-xs uppercase tracking-wider font-bold transition-all whitespace-nowrap outline-none focus-visible:ring-2 focus-visible:ring-primary",
                    transparent
                      ? "bg-white text-foreground hover:shadow-glow"
                      : "bg-foreground text-background hover:bg-primary"
                  )}
                >
                  <User className="w-3.5 h-3.5" />
                  {isAdmin ? "Admin" : authUser.fullName}
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="w-56">
                  <DropdownMenuLabel className="font-normal">
                    <div className="flex flex-col gap-0.5">
                      <span className="text-sm font-semibold truncate">{authUser.fullName}</span>
                      {authUser.email && (
                        <span className="text-xs text-muted-foreground truncate">{authUser.email}</span>
                      )}
                    </div>
                  </DropdownMenuLabel>
                  <DropdownMenuSeparator />
                  {isAdmin ? (
                    <DropdownMenuItem asChild>
                      <Link to="/admin" className="cursor-pointer">
                        <LayoutDashboard className="w-4 h-4 mr-2" />
                        {t("profile.adminDashboard") || "Trang quản trị"}
                      </Link>
                    </DropdownMenuItem>
                  ) : (
                    <DropdownMenuItem asChild>
                      <Link to="/my-profile" className="cursor-pointer">
                        <User className="w-4 h-4 mr-2" />
                        {t("profile.myProfile") || "Hồ sơ của tôi"}
                      </Link>
                    </DropdownMenuItem>
                  )}
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    onSelect={() => {
                      void dispatch(logoutThunk());
                    }}
                    className="cursor-pointer text-destructive focus:text-destructive"
                  >
                    <LogOut className="w-4 h-4 mr-2" />
                    {t("profile.logout") || "Đăng xuất"}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            ) : (
              <Link
                to="/login"
                className={cn(
                  "px-3 2xl:px-5 py-2 rounded-full text-[11px] 2xl:text-xs uppercase tracking-wider font-bold transition-all whitespace-nowrap",
                  transparent
                    ? "bg-white text-foreground hover:shadow-glow"
                    : "btn-gradient text-white"
                )}
              >
                {t("site.nav.login")}
              </Link>
            )}
          </div>

          {/* Mobile toggle */}
          <button
            onClick={() => setOpen(!open)}
            className={cn(
              "2xl:hidden w-10 h-10 rounded-full flex items-center justify-center transition-colors",
              transparent ? "bg-white/10 text-white" : "bg-secondary text-foreground"
            )}
            aria-label="Menu"
          >
            {open ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
          </button>
        </div>
      </div>

      {/* Mobile menu */}
      <div
        className={cn(
          "2xl:hidden overflow-hidden transition-all duration-500 bg-background border-t border-border",
          open ? "max-h-screen" : "max-h-0"
        )}
      >
        <nav className="container-custom py-6 flex flex-col gap-1">
          {nav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) =>
                cn(
                  "px-4 py-3 rounded-xl text-base font-semibold transition-colors",
                  isActive ? "bg-primary text-primary-foreground" : "text-foreground/80 hover:bg-secondary"
                )
              }
            >
              {item.label}
            </NavLink>
          ))}
          <div className="pt-3 mt-2 border-t border-border flex items-center justify-between gap-3">
            <LanguageToggle />
            {authUser ? (
              <div className="flex items-center gap-2">
                <Link
                  to={isAdmin ? "/admin" : "/my-profile"}
                  className="flex items-center gap-1.5 px-4 py-2 rounded-full text-xs uppercase tracking-wider font-bold whitespace-nowrap bg-foreground text-background hover:bg-primary transition-all"
                >
                  <User className="w-3.5 h-3.5" />
                  {isAdmin ? "Admin" : authUser.fullName}
                </Link>
                <button
                  type="button"
                  onClick={() => {
                    setOpen(false);
                    void dispatch(logoutThunk());
                  }}
                  aria-label={t("profile.logout") || "Đăng xuất"}
                  className="w-10 h-10 rounded-full flex items-center justify-center bg-secondary text-foreground hover:bg-destructive hover:text-destructive-foreground transition-colors"
                >
                  <LogOut className="w-4 h-4" />
                </button>
              </div>
            ) : (
              <Link
                to="/login"
                className="px-5 py-2 rounded-full text-xs uppercase tracking-wider font-bold whitespace-nowrap btn-gradient text-white"
              >
                {t("site.nav.login")}
              </Link>
            )}
          </div>
        </nav>
      </div>
    </header>
  );
};

export default Header;
