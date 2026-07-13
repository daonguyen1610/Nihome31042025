import { useEffect, useState } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { ArrowLeft, Save, Plus, Trash2, Loader2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi } from "@/services/adminApi";
import type { UpsertJobPositionRequest, JobPositionResponse, EmploymentTypeResponse, RecruitmentDropdownOptionResponse } from "@/services/adminApi";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";

interface FormData {
  id: number;
  title: string;
  department: string;
  location: string;
  employmentType: string;
  experienceLevel: string;
  description: string;
  requirements: string[];
  benefits: string[];
  isActive: boolean;
  sortOrder: number;
}

const empty: FormData = {
  id: 0,
  title: "",
  department: "",
  location: "",
  employmentType: "",
  experienceLevel: "",
  description: "",
  requirements: [],
  benefits: [],
  isActive: true,
  sortOrder: 0,
};

const selectClasses =
  "flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50";

const JobPositionForm = ({ mode }: { mode: "create" | "edit" }) => {
  const { id: idParam } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const [data, setData] = useState<FormData>(empty);
  const [loading, setLoading] = useState(mode === "edit");
  const [submitting, setSubmitting] = useState(false);
  const [employmentTypes, setEmploymentTypes] = useState<EmploymentTypeResponse[]>([]);
  const [employmentTypesLoading, setEmploymentTypesLoading] = useState(true);
  const [experienceLevels, setExperienceLevels] = useState<RecruitmentDropdownOptionResponse[]>([]);
  const [benefitOptions, setBenefitOptions] = useState<RecruitmentDropdownOptionResponse[]>([]);

  useEffect(() => {
    setEmploymentTypesLoading(true);
    adminApi.getEmploymentTypes(true)
      .then((res) => setEmploymentTypes(res.data))
      .catch(() => setEmploymentTypes([]))
      .finally(() => setEmploymentTypesLoading(false));

    adminApi.getRecruitmentDropdownOptions("experience-level", true)
      .then((res) => setExperienceLevels(res.data))
      .catch(() => setExperienceLevels([]));

    adminApi.getRecruitmentDropdownOptions("benefit", true)
      .then((res) => setBenefitOptions(res.data))
      .catch(() => setBenefitOptions([]));
  }, []);

  useEffect(() => {
    if (mode !== "create") return;
    if (data.employmentType) return;
    const firstType = employmentTypes.find((x) => x.isActive) ?? employmentTypes[0];
    if (!firstType) return;
    update("employmentType", firstType.code);
  }, [mode, employmentTypes, data.employmentType]);

  useEffect(() => {
    if (mode !== "create") return;
    if (data.experienceLevel) return;
    const firstLevel = experienceLevels.find((x) => x.isActive) ?? experienceLevels[0];
    if (!firstLevel) return;
    update("experienceLevel", firstLevel.code);
  }, [mode, experienceLevels, data.experienceLevel]);

  useEffect(() => {
    if (mode !== "edit" || !idParam) return;
    adminApi.getJobPositions(true).then((res) => {
      const pos = (res.data as JobPositionResponse[]).find((p) => p.id === Number(idParam));
      if (pos) {
        setData({
          id: pos.id,
          title: pos.title,
          department: pos.department,
          location: pos.location,
          employmentType: pos.employmentType,
          experienceLevel: pos.experienceLevel,
          description: pos.description ?? "",
          requirements: pos.requirements ?? [],
          benefits: pos.benefits ?? [],
          isActive: pos.isActive,
          sortOrder: pos.sortOrder,
        });
      }
      setLoading(false);
    }).catch(() => setLoading(false));
  }, [mode, idParam]);

  const update = <K extends keyof FormData>(key: K, value: FormData[K]) =>
    setData((d) => ({ ...d, [key]: value }));

  const addRequirement = () => update("requirements", [...data.requirements, ""]);
  const updateRequirement = (i: number, val: string) => {
    const next = [...data.requirements];
    next[i] = val;
    update("requirements", next);
  };
  const removeRequirement = (i: number) =>
    update("requirements", data.requirements.filter((_, idx) => idx !== i));

  const toggleBenefit = (code: string) => {
    const next = data.benefits.includes(code)
      ? data.benefits.filter((b) => b !== code)
      : [...data.benefits, code];
    update("benefits", next);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!data.title.trim() || !data.department.trim() || !data.location.trim()) {
      toast({ title: t("form.required"), variant: "destructive" });
      return;
    }
    if (employmentTypesLoading) {
      toast({ title: t("recruit.jobForm.loadingTypes"), description: t("recruit.jobForm.loadingTypesDesc"), variant: "destructive" });
      return;
    }
    if (employmentTypes.length === 0) {
      toast({ title: t("recruit.jobForm.missingTypes"), description: t("recruit.jobForm.missingTypesDesc"), variant: "destructive" });
      return;
    }
    if (!data.employmentType.trim()) {
      toast({ title: t("form.required"), description: t("recruit.jobForm.selectType"), variant: "destructive" });
      return;
    }
    const payload: UpsertJobPositionRequest = {
      title: data.title,
      department: data.department,
      location: data.location,
      employmentType: data.employmentType,
      experienceLevel: data.experienceLevel,
      description: data.description || undefined,
      requirements: data.requirements.filter((r) => r.trim()),
      benefits: data.benefits,
      isActive: data.isActive,
      sortOrder: data.sortOrder,
    };
    setSubmitting(true);
    try {
      if (mode === "create") {
        await adminApi.createJobPosition(payload);
      } else {
        await adminApi.updateJobPosition(data.id, payload);
      }
      toast({ title: mode === "create" ? t("form.created") : t("form.updated"), description: data.title });
      navigate("/admin/recruitment");
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <AdminLayout>
        <div className="flex h-64 items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-primary" />
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex items-center gap-3">
          <Button asChild size="icon" variant="outline" className="h-10 w-10 rounded-full">
            <Link to="/admin/recruitment" aria-label={t("common.back")}>
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <h1 className="text-2xl font-semibold">
              {mode === "create" ? t("recruit.jobForm.titleCreate") : t("recruit.jobForm.titleEdit")}
            </h1>
            {mode === "edit" && data.title && (
              <p className="text-sm text-muted-foreground">{data.title}</p>
            )}
          </div>
        </header>

        <form onSubmit={handleSubmit} className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <div className="space-y-4 lg:col-span-2">
            {/* Basic info */}
            <section className="rounded-lg border bg-card p-6">
              <h2 className="mb-4 text-base font-semibold">{t("form.basicInfo")}</h2>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <Field label={t("recruit.jobForm.positionTitle")} className="md:col-span-2">
                  <Input
                    className="h-9"
                    value={data.title}
                    onChange={(e) => update("title", e.target.value)}
                    placeholder={t("recruit.jobForm.positionTitlePlaceholder")}
                    required
                  />
                </Field>
                <Field label={t("recruit.jobForm.department")}>
                  <Input
                    className="h-9"
                    value={data.department}
                    onChange={(e) => update("department", e.target.value)}
                    placeholder={t("recruit.jobForm.departmentPlaceholder")}
                    required
                  />
                </Field>
                <Field label={t("recruit.jobForm.location")}>
                  <Input
                    className="h-9"
                    value={data.location}
                    onChange={(e) => update("location", e.target.value)}
                    placeholder={t("recruit.jobForm.locationPlaceholder")}
                    required
                  />
                </Field>
                <Field label={t("recruit.jobForm.employmentType")}>
                  <select
                    className={selectClasses}
                    value={data.employmentType}
                    onChange={(e) => update("employmentType", e.target.value)}
                    required
                    disabled={employmentTypes.length === 0}
                  >
                    {employmentTypes.length === 0 && <option value="">{t("recruit.jobForm.noOptions")}</option>}
                    {employmentTypes.map((type) => (
                      <option key={type.id} value={type.code}>
                        {type.name}
                      </option>
                    ))}
                  </select>
                  <p className="mt-1.5 text-xs text-muted-foreground">
                    {t("recruit.jobForm.manageAt")} <Link to="/admin/recruitment/employment-types" className="underline">{t("empTypes.employmentTypes")}</Link>.
                  </p>
                </Field>
                <Field label={t("recruit.jobForm.experienceLevel")}>
                  <select
                    className={selectClasses}
                    value={data.experienceLevel}
                    onChange={(e) => update("experienceLevel", e.target.value)}
                    disabled={experienceLevels.length === 0}
                  >
                    {experienceLevels.length === 0 && <option value="">{t("recruit.jobForm.noOptions")}</option>}
                    {experienceLevels.map((lvl) => (
                      <option key={lvl.id} value={lvl.code}>
                        {lvl.name}
                      </option>
                    ))}
                  </select>
                </Field>
              </div>
            </section>

            {/* Description */}
            <section className="rounded-lg border bg-card p-6">
              <h2 className="mb-4 text-base font-semibold">{t("recruit.jobForm.description")}</h2>
              <Textarea
                className="min-h-28"
                value={data.description}
                onChange={(e) => update("description", e.target.value)}
                placeholder={t("recruit.jobForm.descPlaceholder")}
              />
            </section>

            {/* Requirements */}
            <section className="rounded-lg border bg-card p-6">
              <h2 className="mb-4 text-base font-semibold">{t("recruit.jobForm.requirements")}</h2>
              <div className="space-y-2">
                {data.requirements.map((req, i) => (
                  <div key={i} className="flex gap-2">
                    <Input
                      className="h-9 flex-1"
                      value={req}
                      onChange={(e) => updateRequirement(i, e.target.value)}
                      placeholder={t("recruit.jobForm.reqPlaceholder")}
                    />
                    <Button
                      type="button"
                      size="icon"
                      variant="outline"
                      className="h-9 w-9 text-destructive hover:bg-destructive/10 hover:text-destructive"
                      onClick={() => removeRequirement(i)}
                      aria-label={t("common.delete")}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                ))}
                <button
                  type="button"
                  onClick={addRequirement}
                  className="flex w-full items-center justify-center gap-1.5 rounded-md border border-dashed py-2 text-sm text-muted-foreground transition hover:bg-muted"
                >
                  <Plus className="h-3.5 w-3.5" /> {t("recruit.jobForm.addReq")}
                </button>
              </div>
            </section>

            {/* Benefits */}
            {benefitOptions.length > 0 && (
              <section className="rounded-lg border bg-card p-6">
                <h2 className="mb-4 text-base font-semibold">{t("recruit.jobForm.benefits")}</h2>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  {benefitOptions.map((opt) => (
                    <label key={opt.id} className="flex cursor-pointer items-center gap-3">
                      <Checkbox
                        checked={data.benefits.includes(opt.code)}
                        onCheckedChange={() => toggleBenefit(opt.code)}
                      />
                      <span className="text-sm">{opt.name}</span>
                    </label>
                  ))}
                </div>
              </section>
            )}
          </div>

          {/* Sidebar */}
          <div className="space-y-4">
            <section className="rounded-lg border bg-card p-6">
              <h2 className="mb-4 text-base font-semibold">{t("recruit.jobForm.settings")}</h2>
              <div className="space-y-4">
                <label className="flex cursor-pointer items-center gap-3">
                  <Checkbox
                    checked={data.isActive}
                    onCheckedChange={(v) => update("isActive", v === true)}
                  />
                  <span className="text-sm font-medium">{t("recruit.jobForm.isHiring")}</span>
                </label>
                <Field label={t("recruit.jobForm.sortOrder")}>
                  <Input
                    type="number"
                    className="h-9"
                    value={data.sortOrder}
                    onChange={(e) => update("sortOrder", Number(e.target.value))}
                    min="0"
                  />
                </Field>
              </div>
            </section>

            <Button
              type="submit"
              disabled={submitting || employmentTypesLoading || employmentTypes.length === 0 || !data.employmentType.trim()}
              className="w-full"
            >
              <Save className="mr-1.5 h-4 w-4" />
              {mode === "create" ? t("form.create") : t("form.update")}
            </Button>
          </div>
        </form>
      </div>
    </AdminLayout>
  );
};

const Field = ({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) => (
  <div className={cn("space-y-1.5", className)}>
    <Label className="text-xs">{label}</Label>
    {children}
  </div>
);

export default JobPositionForm;
