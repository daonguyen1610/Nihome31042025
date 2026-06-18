import { useEffect, useMemo, useState } from "react";
import Layout from "@/components/layout/Layout";
import { ArrowUpRight, MapPin, Clock, Briefcase, Sparkles, Heart, GraduationCap, Coffee, Send, Paperclip } from "lucide-react";
import recruitHero from "@/assets/recruit-hero.jpg";
import culture1 from "@/assets/recruit-culture-1.jpg";
import culture2 from "@/assets/recruit-culture-2.jpg";
import culture3 from "@/assets/recruit-culture-3.jpg";
import { useToast } from "@/hooks/use-toast";
import { useEmploymentTypes, useJobPositions } from "@/hooks/useContentApi";
import { contentApi } from "@/services/contentApi";
import { useI18n } from "@/lib/i18n";

type ApplicationForm = {
  candidateName: string;
  email: string;
  phone: string;
  experienceYears: string;
  coverLetter: string;
};

const emptyForm: ApplicationForm = {
  candidateName: "",
  email: "",
  phone: "",
  experienceYears: "",
  coverLetter: "",
};

function getExperienceLabel(value: string, t: (key: string) => string) {
  switch (value) {
    case "student": return t("rec.exp.student");
    case "junior": return t("rec.exp.junior");
    case "mid": return t("rec.exp.mid");
    case "senior": return t("rec.exp.senior");
    default: return value;
  }
}

const EMP_TYPE_KEY_MAP: Record<string, string> = {
  "full-time": "rec.empType.fullTime",
  "part-time": "rec.empType.partTime",
  "intern": "rec.empType.intern",
};

function getEmploymentTypeLabel(code: string, fallback: string, t: (key: string) => string) {
  const key = EMP_TYPE_KEY_MAP[code];
  if (key) return t(key);
  return fallback || code;
}

const Recruitment = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { data, loading, error, refetch } = useJobPositions();
  const { data: employmentTypeData } = useEmploymentTypes();
  const positions = useMemo(() => data ?? [], [data]);
  const employmentTypeMap = useMemo(
    () => new Map((employmentTypeData ?? []).map((item) => [item.code, item.name])),
    [employmentTypeData],
  );
  const [selectedPositionId, setSelectedPositionId] = useState<number | null>(null);
  const [form, setForm] = useState<ApplicationForm>(emptyForm);
  const [cvFile, setCvFile] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!selectedPositionId && positions.length > 0) {
      setSelectedPositionId(positions[0].id);
    }
  }, [positions, selectedPositionId]);

  const selectedPosition = positions.find((item) => item.id === selectedPositionId) ?? null;

  const benefits = [
    { icon: Heart, title: t("rec.b1.title"), desc: t("rec.b1.desc") },
    { icon: GraduationCap, title: t("rec.b2.title"), desc: t("rec.b2.desc") },
    { icon: Coffee, title: t("rec.b3.title"), desc: t("rec.b3.desc") },
    { icon: Sparkles, title: t("rec.b4.title"), desc: t("rec.b4.desc") },
  ];

  const updateForm = <K extends keyof ApplicationForm>(key: K, value: ApplicationForm[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const submitApplication = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedPosition) {
      toast({ title: t("common.error"), description: t("rec.toast.selectPosition"), variant: "destructive" });
      return;
    }

    if (!form.candidateName.trim() || !form.email.trim()) {
      toast({ title: t("common.error"), description: t("rec.toast.fillRequired"), variant: "destructive" });
      return;
    }

    setSubmitting(true);
    try {
      let cvUrl: string | undefined;
      if (cvFile) {
        const uploadRes = await contentApi.uploadCv(cvFile);
        cvUrl = uploadRes.data.cvUrl;
      }

      await contentApi.submitJobApplication({
        jobPositionId: selectedPosition.id,
        candidateName: form.candidateName.trim(),
        email: form.email.trim(),
        phone: form.phone.trim() || undefined,
        experienceYears: form.experienceYears ? Number(form.experienceYears) : undefined,
        coverLetter: form.coverLetter.trim() || undefined,
        cvUrl,
      });
      toast({ title: t("rec.toast.successTitle"), description: `${t("rec.toast.successDesc")} ${selectedPosition.title}` });
      setForm(emptyForm);
      setCvFile(null);
    } catch (submitError) {
      const message = submitError && typeof submitError === "object" && "response" in submitError
        ? (submitError as { response?: { data?: { detail?: string; message?: string } } }).response?.data?.detail
          ?? (submitError as { response?: { data?: { detail?: string; message?: string } } }).response?.data?.message
        : undefined;
      toast({ title: t("common.error"), description: message ?? t("rec.toast.errorDesc"), variant: "destructive" });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Layout>
      {/* Hero */}
      <section className="relative pt-32 pb-20 lg:pt-40 lg:pb-28 overflow-hidden">
        <img src={recruitHero} alt="" className="absolute inset-0 w-full h-full object-cover" />
        <div className="absolute inset-0 bg-gradient-to-r from-background via-background/95 to-background/40" />
        <div className="relative container-custom max-w-3xl">
          <p className="eyebrow text-primary mb-6">{t("rec.eyebrow")}</p>
          <h1 className="font-display text-5xl md:text-6xl lg:text-7xl font-extrabold leading-[1.05] tracking-tight text-balance">
            {t("rec.titleA")} <span className="text-gradient-primary">{t("rec.titleB")}</span>.
          </h1>
          <p className="mt-8 text-lg lg:text-xl text-muted-foreground leading-relaxed max-w-2xl">
            {t("rec.intro")}
          </p>
          <div className="mt-10 flex flex-wrap gap-4">
            <a href="#positions" className="btn-pill btn-gradient text-white px-8 py-4 text-sm uppercase tracking-wider">
              {t("rec.cta1")} <ArrowUpRight className="w-4 h-4" />
            </a>
            <a href="#apply-form" className="btn-pill bg-secondary border border-border px-8 py-4 text-sm uppercase tracking-wider hover:bg-foreground hover:text-background">
              {t("rec.cta2")}
            </a>
          </div>
        </div>
      </section>

      {/* Culture gallery */}
      <section className="py-16 bg-surface">
        <div className="container-custom">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
            {[culture1, culture2, culture3].map((img, i) => (
              <div key={i} className="image-zoom aspect-[4/3] rounded-3xl overflow-hidden">
                <img src={img} alt="" loading="lazy" className="w-full h-full object-cover" />
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Benefits */}
      <section className="py-20 lg:py-28 bg-background">
        <div className="container-custom">
          <div className="text-center max-w-2xl mx-auto mb-14">
            <p className="eyebrow text-primary mb-6 justify-center">{t("rec.benefits.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
              {t("rec.benefits.titleA")} <span className="text-gradient-primary">{t("rec.benefits.titleB")}</span>.
            </h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {benefits.map((b, i) => (
              <div key={i} className="bg-card border border-border rounded-3xl p-7 hover-lift">
                <div className="w-14 h-14 rounded-2xl bg-gradient-primary text-white flex items-center justify-center mb-5 shadow-glow">
                  <b.icon className="w-6 h-6" strokeWidth={1.75} />
                </div>
                <h3 className="font-display text-xl font-extrabold mb-2">{b.title}</h3>
                <p className="text-muted-foreground text-sm leading-relaxed">{b.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Open positions */}
      <section id="positions" className="py-20 lg:py-28 bg-surface">
        <div className="container-custom">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 mb-10">
            <div className="max-w-xl">
              <p className="eyebrow text-primary mb-6">{t("rec.positions.eyebrow")}</p>
              <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
                {positions.length} {t("rec.positions.countA")} <span className="text-gradient-primary">{t("rec.positions.countB")}</span>.
              </h2>
            </div>
          </div>
          {loading ? (
            <div className="py-12 text-center text-muted-foreground">{t("rec.loading")}</div>
          ) : error ? (
            <div className="bg-card border border-border rounded-2xl p-6 text-center space-y-3">
              <p className="text-muted-foreground">{error}</p>
              <button onClick={refetch} className="btn-pill btn-gradient text-white px-6 py-3 text-xs uppercase tracking-wider">
                {t("rec.retry")}
              </button>
            </div>
          ) : positions.length === 0 ? (
            <div className="bg-card border border-border rounded-2xl p-8 text-center text-muted-foreground">
              {t("rec.noPositions")}
            </div>
          ) : (
            <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,1.2fr)_minmax(340px,0.8fr)] gap-6 items-start">
              <div className="space-y-3">
                {positions.map((position) => (
                  <div
                    key={position.id}
                    className={[
                      "group bg-card border rounded-2xl p-5 lg:p-6 flex flex-col gap-5 transition",
                      selectedPositionId === position.id ? "border-primary shadow-glow" : "border-border hover-lift",
                    ].join(" ")}
                  >
                    <div className="flex flex-col lg:flex-row lg:items-start gap-5">
                      <div className="flex-1 min-w-0">
                        <p className="text-[10px] uppercase tracking-wider font-bold text-primary mb-1">{position.department}</p>
                        <h3 className="font-display text-xl lg:text-2xl font-extrabold group-hover:text-primary transition-colors">
                          {position.title}
                        </h3>
                        {position.description && (
                          <p className="mt-3 text-sm text-muted-foreground leading-relaxed">{position.description}</p>
                        )}
                      </div>
                      <a
                        href="#apply-form"
                        onClick={() => setSelectedPositionId(position.id)}
                        className="btn-pill btn-gradient text-white px-6 py-3 text-xs uppercase tracking-wider shrink-0"
                      >
                        {t("rec.apply")} <ArrowUpRight className="w-4 h-4" />
                      </a>
                    </div>
                    <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
                      <span className="flex items-center gap-1.5"><MapPin className="w-3.5 h-3.5" /> {position.location}</span>
                      <span className="flex items-center gap-1.5"><Clock className="w-3.5 h-3.5" /> {getEmploymentTypeLabel(position.employmentType, employmentTypeMap.get(position.employmentType) ?? position.employmentType, t)}</span>
                      <span className="flex items-center gap-1.5"><Briefcase className="w-3.5 h-3.5" /> {getExperienceLabel(position.experienceLevel, t)}</span>
                    </div>
                    {position.requirements.length > 0 && (
                      <ul className="grid gap-2 text-sm text-muted-foreground">
                        {position.requirements.map((requirement, index) => (
                          <li key={`${position.id}-${index}`} className="flex gap-2">
                            <span className="text-primary">•</span>
                            <span>{requirement}</span>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                ))}
              </div>

              <div id="apply-form" className="bg-card border border-border rounded-3xl p-6 lg:p-7 sticky top-24">
                <p className="eyebrow text-primary mb-4">{t("rec.form.eyebrow")}</p>
                <h3 className="font-display text-2xl font-extrabold mb-2">
                  {selectedPosition?.title ?? t("rec.form.noPosition")}
                </h3>
                <p className="text-sm text-muted-foreground mb-6">
                  {selectedPosition
                    ? `${selectedPosition.department} • ${selectedPosition.location}`
                    : t("rec.form.noPositionHint")}
                </p>

                <form className="space-y-4" onSubmit={submitApplication}>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-1.5 block">{t("rec.form.nameLabel")}</label>
                    <input className="admin-input w-full" value={form.candidateName} onChange={(e) => updateForm("candidateName", e.target.value)} placeholder={t("rec.form.namePlaceholder")} />
                  </div>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-1.5 block">{t("rec.form.emailLabel")}</label>
                    <input type="email" className="admin-input w-full" value={form.email} onChange={(e) => updateForm("email", e.target.value)} placeholder="email@company.com" />
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-1.5 block">{t("rec.form.phoneLabel")}</label>
                      <input className="admin-input w-full" value={form.phone} onChange={(e) => updateForm("phone", e.target.value)} placeholder={t("rec.form.phonePlaceholder")} />
                    </div>
                    <div>
                      <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-1.5 block">{t("rec.form.expLabel")}</label>
                      <input type="number" min="0" className="admin-input w-full" value={form.experienceYears} onChange={(e) => updateForm("experienceYears", e.target.value)} placeholder="3" />
                    </div>
                  </div>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-1.5 block">{t("rec.form.coverLabel")}</label>
                    <textarea className="admin-input w-full min-h-28" value={form.coverLetter} onChange={(e) => updateForm("coverLetter", e.target.value)} placeholder={t("rec.form.coverPlaceholder")} />
                  </div>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-1.5 block">{t("rec.form.cvLabel")}</label>
                    <label className="flex items-center gap-2 cursor-pointer admin-input w-full text-sm">
                      <Paperclip className="w-4 h-4 text-muted-foreground shrink-0" />
                      <span className={cvFile ? "text-foreground" : "text-muted-foreground"}>
                        {cvFile ? cvFile.name : t("rec.form.cvPlaceholder")}
                      </span>
                      <input
                        type="file"
                        accept=".pdf,.doc,.docx"
                        className="hidden"
                        onChange={(e) => setCvFile(e.target.files?.[0] ?? null)}
                      />
                    </label>
                  </div>
                  <button disabled={submitting || !selectedPosition} className="btn-pill btn-gradient text-white px-6 py-3 text-xs uppercase tracking-wider w-full disabled:opacity-50 disabled:cursor-not-allowed">
                    {submitting ? t("rec.form.submitting") : t("rec.form.submit")} <Send className="w-4 h-4" />
                  </button>
                </form>
              </div>
            </div>
          )}
        </div>
      </section>
    </Layout>
  );
};

export default Recruitment;
