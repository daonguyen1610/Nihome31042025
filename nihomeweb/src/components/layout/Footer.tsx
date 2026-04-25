import { Link } from "react-router-dom";
import { Facebook, Youtube, Instagram, Linkedin, Mail, Phone, MapPin, ArrowUpRight, Send } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import logoNicon from "@/assets/logo-nicon.png";

const Footer = () => {
  const { t } = useI18n();
  const links = [
    { to: "/profile", label: t("site.nav.profile") },
    { to: "/services", label: t("site.nav.services") },
    { to: "/projects", label: t("site.nav.projects") },
    { to: "/news", label: t("site.nav.news") },
    { to: "/activities", label: t("site.nav.activities") },
    { to: "/clients", label: t("site.nav.clients") },
    { to: "/recruitment", label: t("site.nav.recruitment") },
  ];

  return (
    <footer className="bg-surface-dark text-surface-dark-foreground relative overflow-hidden">
      <div className="absolute -top-32 -right-32 w-96 h-96 rounded-full bg-primary/20 blur-3xl pointer-events-none" />
      <div className="absolute -bottom-32 -left-32 w-96 h-96 rounded-full bg-accent-orange/15 blur-3xl pointer-events-none" />

      <div className="container-custom relative py-20 lg:py-24">
        <div className="mb-16 lg:mb-20 grid grid-cols-1 lg:grid-cols-12 gap-6 items-center pb-12 border-b border-white/10">
          <div className="lg:col-span-7">
            <h3 className="font-display text-3xl md:text-4xl lg:text-5xl font-bold leading-[1.1] text-balance">
              {t("footer.newsletter.title")}
            </h3>
          </div>
          <form className="lg:col-span-5 flex gap-2 p-2 bg-white/10 backdrop-blur-md rounded-full border border-white/15">
            <input
              type="email"
              placeholder={t("footer.newsletter.placeholder")}
              className="flex-1 bg-transparent px-5 py-2 text-sm placeholder:text-white/50 focus:outline-none"
            />
            <button
              type="submit"
              className="btn-gradient text-white px-5 py-2.5 rounded-full text-xs uppercase tracking-wider font-bold inline-flex items-center gap-2"
            >
              {t("footer.newsletter.subscribe")} <Send className="w-3.5 h-3.5" />
            </button>
          </form>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 lg:gap-16">
          <div className="lg:col-span-5">
            <div className="mb-6">
              <img src={logoNicon} alt="NICON - Partner of Development" className="h-16 w-auto object-contain brightness-0 invert" />
            </div>
            <p className="text-white/70 text-base leading-relaxed max-w-md mb-8">
              {t("footer.brand.desc")}
            </p>
            <div className="flex items-center gap-3">
              {[
                { Icon: Facebook, label: "Facebook" },
                { Icon: Youtube, label: "Youtube" },
                { Icon: Instagram, label: "Instagram" },
                { Icon: Linkedin, label: "LinkedIn" },
              ].map(({ Icon, label }) => (
                <a
                  key={label}
                  href="#"
                  aria-label={label}
                  className="w-14 h-14 rounded-2xl border-2 border-white/20 bg-white/10 flex items-center justify-center hover:bg-gradient-primary hover:border-transparent hover:scale-110 hover:shadow-glow transition-all"
                >
                  <Icon className="w-6 h-6" strokeWidth={2} />
                </a>
              ))}
            </div>
          </div>

          <div className="lg:col-span-3">
            <h4 className="text-xs uppercase tracking-[0.2em] text-white/50 mb-6 font-bold">
              {t("footer.explore")}
            </h4>
            <ul className="space-y-3">
              {links.map((l) => (
                <li key={l.to}>
                  <Link to={l.to} className="text-white/80 hover:text-primary transition-colors inline-flex items-center gap-1.5 group">
                    {l.label}
                    <ArrowUpRight className="w-3.5 h-3.5 opacity-0 -translate-x-1 group-hover:opacity-100 group-hover:translate-x-0 transition-all" />
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="lg:col-span-4">
            <h4 className="text-xs uppercase tracking-[0.2em] text-white/50 mb-6 font-bold">
              {t("footer.contact")}
            </h4>
            <ul className="space-y-4 text-white/80">
              <li className="flex items-start gap-3">
                <span className="w-9 h-9 rounded-full bg-primary/15 text-primary flex items-center justify-center shrink-0">
                  <MapPin className="w-4 h-4" />
                </span>
                <span className="pt-1.5">{t("footer.address")}</span>
              </li>
              <li className="flex items-center gap-3">
                <span className="w-9 h-9 rounded-full bg-primary/15 text-primary flex items-center justify-center shrink-0">
                  <Phone className="w-4 h-4" />
                </span>
                <a href="tel:0909450266" className="hover:text-primary transition-colors">0909 450 266</a>
              </li>
              <li className="flex items-center gap-3">
                <span className="w-9 h-9 rounded-full bg-primary/15 text-primary flex items-center justify-center shrink-0">
                  <Mail className="w-4 h-4" />
                </span>
                <a href="mailto:sale@nicon.vn" className="hover:text-primary transition-colors">sale@nicon.vn</a>
              </li>
            </ul>
          </div>
        </div>

        <div className="mt-16 pt-8 border-t border-white/10 flex flex-col md:flex-row items-start md:items-center justify-between gap-4 text-xs text-white/50">
          <p>© {new Date().getFullYear()} {t("footer.copyright")}</p>
          <div className="flex items-center gap-6">
            <span>{t("footer.cert")}</span>
            <span>•</span>
            <span>{t("footer.partner")}</span>
          </div>
        </div>
      </div>
    </footer>
  );
};

export default Footer;
