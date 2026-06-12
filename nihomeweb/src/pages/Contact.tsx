import { useEffect, useState } from "react";
import Layout from "@/components/layout/Layout";
import PageHeader from "@/components/PageHeader";
import { MapPin, Phone, Mail, Clock, Send, Facebook, Linkedin, Youtube } from "lucide-react";
import { useToast } from "@/hooks/use-toast";
import { useI18n } from "@/lib/i18n";
import { contentApi } from "@/services/contentApi";

const FALLBACK_MAP_URL =
  "https://www.openstreetmap.org/export/embed.html?bbox=106.7%2C10.78%2C106.82%2C10.85&layer=mapnik";

const Contact = () => {
  const { toast } = useToast();
  const { t } = useI18n();
  const [form, setForm] = useState({ name: "", email: "", phone: "", subject: "", message: "" });
  const [loading, setLoading] = useState(false);
  const [mapUrl, setMapUrl] = useState<string>(FALLBACK_MAP_URL);

  useEffect(() => {
    let cancelled = false;
    contentApi
      .getMapEmbed()
      .then(({ data }) => {
        if (cancelled) return;
        const v = data.mapEmbedUrl?.trim();
        if (v) setMapUrl(v);
      })
      .catch(() => {
        /* keep fallback */
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const offices = [
    { city: t("contact.office.hq"), address: t("contact.office.hqAddr"), phone: "+84 28 7300 1234", email: "info@nicon.vn" },
    { city: t("contact.office.bd"), address: t("contact.office.bdAddr"), phone: "+84 274 365 4321", email: "binhduong@nicon.vn" },
  ];

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await contentApi.submitContact({
        name: form.name,
        email: form.email,
        phone: form.phone || undefined,
        subject: form.subject,
        message: form.message,
      });
      toast({ title: t("contact.toast.title"), description: t("contact.toast.desc") });
      setForm({ name: "", email: "", phone: "", subject: "", message: "" });
    } catch {
      toast({ title: t("common.error"), description: t("common.tryAgain"), variant: "destructive" });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Layout>
      <PageHeader
        eyebrow={t("contact.eyebrow")}
        title={t("contact.title")}
        description={t("contact.desc")}
      />

      <section className="py-16 lg:py-20 bg-background">
        <div className="container-custom grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-16">
          {/* Form */}
          <div className="lg:col-span-7">
            <div className="bg-card border border-border rounded-3xl p-8 lg:p-10 shadow-card">
              <p className="eyebrow text-primary mb-6">{t("contact.form.eyebrow")}</p>
              <h2 className="font-display text-3xl md:text-4xl font-extrabold mb-8 tracking-tight">
                {t("contact.form.title")}
              </h2>
              <form onSubmit={submit} className="space-y-5">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                  <div>
                    <label className="text-xs uppercase tracking-wider font-bold text-foreground/70 mb-2 block">{t("contact.form.name")}</label>
                    <input
                      required
                      value={form.name}
                      onChange={(e) => setForm({ ...form, name: e.target.value })}
                      className="w-full bg-secondary rounded-2xl px-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                      placeholder={t("contact.form.namePh")}
                    />
                  </div>
                  <div>
                    <label className="text-xs uppercase tracking-wider font-bold text-foreground/70 mb-2 block">{t("contact.form.phone")}</label>
                    <input
                      value={form.phone}
                      onChange={(e) => setForm({ ...form, phone: e.target.value })}
                      className="w-full bg-secondary rounded-2xl px-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                      placeholder="0123 456 789"
                    />
                  </div>
                </div>
                <div>
                  <label className="text-xs uppercase tracking-wider font-bold text-foreground/70 mb-2 block">{t("contact.form.email")}</label>
                  <input
                    required
                    type="email"
                    value={form.email}
                    onChange={(e) => setForm({ ...form, email: e.target.value })}
                    className="w-full bg-secondary rounded-2xl px-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                    placeholder={t("contact.form.emailPh")}
                  />
                </div>
                <div>
                  <label className="text-xs uppercase tracking-wider font-bold text-foreground/70 mb-2 block">{t("contact.form.subject")}</label>
                  <select
                    value={form.subject}
                    onChange={(e) => setForm({ ...form, subject: e.target.value })}
                    className="w-full bg-secondary rounded-2xl px-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition"
                  >
                    <option value="">{t("contact.form.subjectPh")}</option>
                    <option>{t("contact.form.subj1")}</option>
                    <option>{t("contact.form.subj2")}</option>
                    <option>{t("contact.form.subj3")}</option>
                    <option>{t("contact.form.subj4")}</option>
                    <option>{t("contact.form.subj5")}</option>
                  </select>
                </div>
                <div>
                  <label className="text-xs uppercase tracking-wider font-bold text-foreground/70 mb-2 block">{t("contact.form.message")}</label>
                  <textarea
                    required
                    rows={5}
                    value={form.message}
                    onChange={(e) => setForm({ ...form, message: e.target.value })}
                    className="w-full bg-secondary rounded-2xl px-5 py-3.5 text-sm border border-transparent focus:border-primary focus:bg-background outline-none transition resize-none"
                    placeholder={t("contact.form.messagePh")}
                  />
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="btn-pill btn-gradient text-white px-8 py-4 text-sm uppercase tracking-wider w-full md:w-auto disabled:opacity-60"
                >
                  {loading ? t("contact.form.sending") : <>{t("contact.form.send")} <Send className="w-4 h-4" /></>}
                </button>
              </form>
            </div>
          </div>

          {/* Info */}
          <aside className="lg:col-span-5 space-y-6">
            {offices.map((o, i) => (
              <div key={i} className="bg-card border border-border rounded-3xl p-7 hover-lift">
                <h3 className="font-display text-xl font-extrabold mb-5">{o.city}</h3>
                <div className="space-y-3 text-sm">
                  <p className="flex gap-3 text-foreground/80"><MapPin className="w-4 h-4 text-primary mt-0.5 shrink-0" /> {o.address}</p>
                  <p className="flex gap-3 text-foreground/80"><Phone className="w-4 h-4 text-primary mt-0.5 shrink-0" /> {o.phone}</p>
                  <p className="flex gap-3 text-foreground/80"><Mail className="w-4 h-4 text-primary mt-0.5 shrink-0" /> {o.email}</p>
                </div>
              </div>
            ))}

            <div className="bg-gradient-primary text-white rounded-3xl p-7 relative overflow-hidden">
              <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full" />
              <Clock className="w-7 h-7 mb-4" strokeWidth={1.5} />
              <h3 className="font-display text-xl font-extrabold mb-2">{t("contact.hours.title")}</h3>
              <p className="text-white/90 text-sm">{t("contact.hours.weekday")}</p>
              <p className="text-white/90 text-sm">{t("contact.hours.sat")}</p>
              <div className="flex gap-3 mt-5 relative z-10">
                {[Facebook, Linkedin, Youtube].map((Icon, i) => (
                  <a key={i} href="#" className="w-10 h-10 rounded-full bg-white/15 backdrop-blur hover:bg-white hover:text-primary flex items-center justify-center transition">
                    <Icon className="w-4 h-4" />
                  </a>
                ))}
              </div>
            </div>
          </aside>
        </div>
      </section>

      {/* Map */}
      <section className="pb-20 bg-background">
        <div className="container-custom">
          <div className="aspect-[21/9] rounded-3xl overflow-hidden border border-border">
            <iframe
              title="NICON map"
              src={mapUrl}
              className="w-full h-full"
              loading="lazy"
              referrerPolicy="no-referrer-when-downgrade"
              allowFullScreen
            />
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default Contact;
