import { useEffect, useMemo, useState, type ReactNode } from "react";
import { AlertTriangle, Check, ChevronLeft, ChevronRight, Loader2 } from "lucide-react";
import { adminApi, type ContractResponse, type LegalActivationPreview } from "@/services/adminApi";
import { extractApiError, isConcurrencyConflict } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { personOption } from "@/lib/people";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

type PermitDraft = {
  code: string;
  name: string;
  selected: boolean;
  alreadyExists: boolean;
  requiresSetup: boolean;
  issuingAgency: string;
  assigneeUserId: number;
  approverUserId: number;
  plannedStart: string;
  plannedEnd: string;
  predecessors: string[];
};

type Props = {
  contract?: ContractResponse;
  operationalProjectId?: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onActivated: () => Promise<void>;
};

const datePart = (value?: string | null) => value?.slice(0, 10) ?? "";
const today = () => new Date().toISOString().slice(0, 10);
const plusDays = (date: string, days: number) => {
  const value = new Date(`${date}T00:00:00Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
};
const isoDate = (value: string) => `${value}T00:00:00.000Z`;

export default function LegalActivationWizard({
  contract,
  operationalProjectId,
  open,
  onOpenChange,
  onActivated,
}: Props) {
  const { t, lang } = useI18n();
  const [preview, setPreview] = useState<LegalActivationPreview | null>(null);
  const [step, setStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [permitErrors, setPermitErrors] = useState<Record<string, string>>({});
  const [concurrency, setConcurrency] = useState(false);
  const [contractRowVersion, setContractRowVersion] = useState(contract?.rowVersion ?? "");
  const [leadUserId, setLeadUserId] = useState(0);
  const [packageStart, setPackageStart] = useState("");
  const [packageEnd, setPackageEnd] = useState("");
  const [permits, setPermits] = useState<PermitDraft[]>([]);
  const [templateId, setTemplateId] = useState(0);
  const template =
    preview?.appliedTemplate ?? preview?.templates?.find((item) => item.id === templateId);
  const requiredCodes = useMemo(
    () =>
      new Set(
        template?.items.filter((item) => item.isRequired).map((item) => item.permitTypeCode) ?? [],
      ),
    [template],
  );

  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    setPermitErrors({});
    setConcurrency(false);
    setStep(0);
    const request = contract
      ? adminApi.getContractLegalActivationPreview(contract.id)
      : adminApi.getProjectLegalSetupPreview(operationalProjectId!);
    void request
      .then(({ data }) => {
        if (cancelled) return;
        const start =
          datePart(data.existingPlannedStart) || datePart(contract?.startDate) || today();
        const end =
          datePart(data.existingPlannedEnd) || datePart(contract?.endDate) || plusDays(start, 180);
        const lead =
          data.existingLegalLeadUserId ??
          data.projectMembers.find((member) => member.canLeadLegal)?.userId ??
          0;
        const firstAssignee =
          data.projectMembers.find((member) => member.userId !== lead)?.userId ?? lead;
        const approverFor = (assignee: number) =>
          lead && lead !== assignee ? lead : data.projectManagerUserId;
        setPreview(data);
        setContractRowVersion(contract?.rowVersion ?? "");
        setTemplateId(data.appliedTemplate?.id ?? 0);
        setLeadUserId(lead);
        setPackageStart(start);
        setPackageEnd(end);
        setPermits(
          data.permitTypes.map((permit) => ({
            code: permit.code,
            name: permit.name,
            selected:
              permit.requiresSetup ||
              !!data.appliedTemplate?.items.some(
                (item) => item.permitTypeCode === permit.code && item.isRequired,
              ),
            alreadyExists: permit.alreadyExists,
            requiresSetup: permit.requiresSetup,
            issuingAgency: "",
            assigneeUserId: firstAssignee,
            approverUserId: approverFor(firstAssignee),
            plannedStart: start,
            plannedEnd: end,
            predecessors: [],
          })),
        );
      })
      .catch((reason) => setError(extractApiError(reason)))
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [contract, operationalProjectId, open]);

  const selected = useMemo(
    () =>
      permits.filter(
        (permit) => permit.selected && (!permit.alreadyExists || permit.requiresSetup),
      ),
    [permits],
  );
  const hasConfiguredPermit = permits.some(
    (permit) => permit.alreadyExists && !permit.requiresSetup,
  );
  const memberName = (id: number) =>
    preview?.projectMembers.find((member) => member.userId === id)?.name ?? `#${id}`;
  const updatePermit = (code: string, patch: Partial<PermitDraft>) => {
    setPermits((current) =>
      current.map((permit) => (permit.code === code ? { ...permit, ...patch } : permit)),
    );
    setPermitErrors((current) => {
      if (!current[code]) return current;
      const next = { ...current };
      delete next[code];
      return next;
    });
  };

  const validateStep = () => {
    if (!preview?.eligible) return preview?.message ?? t("contracts.legal.error.ineligible");
    if (step === 1 && !preview.existingWorkPackageId && !template)
      return t("legalTemplates.chooseRequired");
    if (step === 1 && selected.length === 0 && !hasConfiguredPermit)
      return t("contracts.legal.error.permitRequired");
    if (step === 2) {
      if (!leadUserId) return t("contracts.legal.error.leadRequired");
      if (!packageStart || !packageEnd || packageEnd < packageStart)
        return t("contracts.legal.error.packageDates");
      const nextPermitErrors: Record<string, string> = {};
      for (const permit of selected) {
        if (
          !preview.projectMembers.some((member) => member.userId === permit.assigneeUserId) ||
          !preview.projectMembers.some((member) => member.userId === permit.approverUserId)
        )
          nextPermitErrors[permit.code] = t("contracts.legal.error.assignmentRequired");
        else if (permit.assigneeUserId === permit.approverUserId)
          nextPermitErrors[permit.code] = t("contracts.legal.error.selfApproval");
        if (
          !permit.plannedStart ||
          !permit.plannedEnd ||
          permit.plannedEnd < permit.plannedStart ||
          permit.plannedStart < packageStart ||
          permit.plannedEnd > packageEnd
        )
          nextPermitErrors[permit.code] = t("contracts.legal.error.taskDates");
      }
      setPermitErrors(nextPermitErrors);
      const firstPermitError = Object.values(nextPermitErrors)[0];
      if (firstPermitError) return firstPermitError;
    }
    setPermitErrors({});
    return null;
  };

  const next = () => {
    const message = validateStep();
    if (message) {
      setError(message);
      return;
    }
    setError(null);
    setStep((current) => Math.min(3, current + 1));
  };

  const reloadPreserving = async () => {
    setLoading(true);
    try {
      const { data } = contract
        ? await adminApi.getContractLegalActivationPreview(contract.id)
        : await adminApi.getProjectLegalSetupPreview(operationalProjectId!);
      if (contract)
        setContractRowVersion((await adminApi.getContract(contract.id)).data.rowVersion);
      setPreview(data);
      if (data.appliedTemplate) setTemplateId(data.appliedTemplate.id);
      setPermits((current) =>
        data.permitTypes.map((type) => {
          const previous = current.find((item) => item.code === type.code);
          return {
            code: type.code,
            name: type.name,
            alreadyExists: type.alreadyExists,
            requiresSetup: type.requiresSetup,
            selected:
              previous?.selected ||
              type.requiresSetup ||
              !!data.appliedTemplate?.items.some(
                (item) => item.permitTypeCode === type.code && item.isRequired,
              ),
            issuingAgency: previous?.issuingAgency ?? "",
            assigneeUserId: previous?.assigneeUserId ?? 0,
            approverUserId: previous?.approverUserId ?? 0,
            plannedStart: previous?.plannedStart ?? packageStart,
            plannedEnd: previous?.plannedEnd ?? packageEnd,
            predecessors: previous?.predecessors ?? [],
          };
        }),
      );
      setConcurrency(false);
      setError(null);
      setStep(1);
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  };

  const submit = async () => {
    setError(null);
    setConcurrency(false);
    const message = validateStep();
    if (message) {
      setError(message);
      return;
    }
    setSaving(true);
    try {
      const request = {
        templateId: template?.id,
        templateRowVersion: template?.rowVersion,
        workPackageRowVersion: preview?.workPackageRowVersion ?? undefined,
        legalLeadUserId: leadUserId,
        plannedStart: isoDate(packageStart),
        plannedEnd: isoDate(packageEnd),
        permits: selected.map((permit) => ({
          permitTypeCode: permit.code,
          issuingAgency: permit.issuingAgency.trim() || null,
          assigneeUserId: permit.assigneeUserId,
          approverUserId: permit.approverUserId,
          plannedStart: isoDate(permit.plannedStart),
          plannedEnd: isoDate(permit.plannedEnd),
          predecessorPermitTypeCodes: permit.predecessors,
        })),
      };
      if (contract)
        await adminApi.activateContractWithLegal(contract.id, {
          ...request,
          rowVersion: contractRowVersion,
        });
      else await adminApi.setupProjectLegal(operationalProjectId!, request);
      await onActivated();
      onOpenChange(false);
    } catch (reason) {
      setConcurrency(isConcurrencyConflict(reason));
      setError(extractApiError(reason));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        if (!saving) onOpenChange(value);
      }}
    >
      <DialogContent className="flex max-h-[92dvh] w-[calc(100vw-1rem)] max-w-6xl flex-col overflow-hidden p-0 sm:w-full">
        <DialogHeader className="border-b px-4 py-4 sm:px-6">
          <DialogTitle>{t("contracts.legal.title")}</DialogTitle>
          <DialogDescription>{t("contracts.legal.description")}</DialogDescription>
          <ol className="grid grid-cols-4 gap-1 pt-2 text-center text-[11px] sm:text-xs">
            {["check", "permits", "assignment", "review"].map((key, index) => (
              <li
                key={key}
                className={
                  index <= step
                    ? "rounded bg-primary px-1 py-1.5 text-primary-foreground"
                    : "rounded bg-muted px-1 py-1.5 text-muted-foreground"
                }
              >
                {index + 1}. {t(`contracts.legal.step.${key}`)}
              </li>
            ))}
          </ol>
        </DialogHeader>

        <div className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-4 py-4 sm:px-6">
          {loading ? (
            <div className="flex min-h-56 items-center justify-center">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : null}
          {error ? (
            <div
              role="alert"
              className="mb-4 flex items-start gap-2 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive"
            >
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          ) : null}
          {concurrency ? (
            <div className="mb-4 space-y-2 text-sm font-medium text-amber-700">
              {t("contracts.legal.error.concurrency")}
              <Button
                type="button"
                variant="outline"
                disabled={loading || saving}
                onClick={() => void reloadPreserving()}
              >
                {t("legalTemplates.reloadPreserving")}
              </Button>
            </div>
          ) : null}

          {!loading && preview && step === 0 ? (
            <div className="grid gap-3 sm:grid-cols-2">
              {contract ? (
                <Summary label={t("contracts.legal.contract")} value={contract.contractNumber} />
              ) : null}
              <Summary
                label={t("contracts.legal.project")}
                value={`${preview.projectCode} — ${preview.projectName}`}
              />
              {contract ? (
                <Summary label={t("contracts.legal.currentStatus")} value={contract.status} />
              ) : null}
              <Summary
                label={t("contracts.legal.package")}
                value={
                  preview.existingWorkPackageId
                    ? t("contracts.legal.packageReuse")
                    : t("contracts.legal.packageCreate")
                }
              />
              {preview.alreadyActivated ? (
                <p className="sm:col-span-2 rounded-md bg-emerald-50 p-3 text-sm text-emerald-800">
                  {t("contracts.legal.alreadyActivated")}
                </p>
              ) : null}
            </div>
          ) : null}

          {!loading && preview && step === 1 ? (
            <div className="space-y-2">
              <Label htmlFor="legal-template-select">{t("legalTemplates.title")}</Label>
              <select
                id="legal-template-select"
                className="w-full rounded-md border bg-background p-2"
                value={templateId}
                disabled={!!preview.appliedTemplate}
                onChange={(event) => {
                  const id = Number(event.target.value);
                  setTemplateId(id);
                  const chosen = preview.templates.find((item) => item.id === id);
                  setPermits((current) =>
                    current.map((permit) => ({
                      ...permit,
                      selected:
                        permit.requiresSetup ||
                        !!chosen?.items.some((item) => item.permitTypeCode === permit.code),
                    })),
                  );
                }}
              >
                <option value={0}>{t("legalTemplates.notApplied")}</option>
                {(preview.appliedTemplate
                  ? [preview.appliedTemplate]
                  : (preview.templates ?? [])
                ).map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.code} · {item.version} ·{" "}
                    {lang === "vi"
                      ? item.nameVi
                      : lang === "zh"
                        ? item.nameZh
                        : lang === "ja"
                          ? item.nameJa
                          : item.name}
                  </option>
                ))}
              </select>
              {template ? (
                <p className="whitespace-pre-wrap text-sm text-muted-foreground">
                  {lang === "vi"
                    ? template.scopeVi
                    : lang === "zh"
                      ? template.scopeZh
                      : lang === "ja"
                        ? template.scopeJa
                        : template.scope}
                </p>
              ) : (
                <p className="text-sm text-amber-700">{t("legalTemplates.chooseRequired")}</p>
              )}
              {preview.templateAppliedAt ? (
                <p className="text-xs text-muted-foreground">
                  {new Date(preview.templateAppliedAt).toLocaleString()} · #
                  {preview.templateAppliedByUserId}
                </p>
              ) : null}
              {permits.map((permit) => (
                <label key={permit.code} className="flex items-start gap-3 rounded-lg border p-3">
                  <Checkbox
                    checked={permit.selected || (permit.alreadyExists && !permit.requiresSetup)}
                    disabled={
                      requiredCodes.has(permit.code) ||
                      (permit.alreadyExists && !permit.requiresSetup)
                    }
                    onCheckedChange={(checked) =>
                      updatePermit(permit.code, { selected: checked === true })
                    }
                  />
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium">{permit.name}</span>
                    <span className="text-xs">
                      {t(
                        requiredCodes.has(permit.code)
                          ? "legalTemplates.required"
                          : "legalTemplates.optional",
                      )}
                    </span>
                    <span className="text-xs text-muted-foreground">{permit.code}</span>
                  </span>
                  {permit.alreadyExists && !permit.requiresSetup ? (
                    <span className="shrink-0 text-xs text-emerald-700">
                      {t("contracts.legal.existing")}
                    </span>
                  ) : null}
                </label>
              ))}
            </div>
          ) : null}

          {!loading && preview && step === 2 ? (
            <div className="space-y-5">
              <div className="grid gap-3 rounded-lg border p-3 sm:grid-cols-3">
                <Field label={t("contracts.legal.lead")}>
                  <MemberSelect
                    ariaLabel={t("contracts.legal.lead")}
                    disabled={
                      !!preview.existingWorkPackageId && !preview.appliedTemplate && !!template
                    }
                    value={leadUserId}
                    members={preview.projectMembers.filter((member) => member.canLeadLegal)}
                    onChange={setLeadUserId}
                  />
                </Field>
                <Field label={t("contracts.legal.start")}>
                  <Input
                    type="date"
                    disabled={
                      !!preview.existingWorkPackageId && !preview.appliedTemplate && !!template
                    }
                    value={packageStart}
                    onChange={(event) => setPackageStart(event.target.value)}
                  />
                </Field>
                <Field label={t("contracts.legal.end")}>
                  <Input
                    type="date"
                    disabled={
                      !!preview.existingWorkPackageId && !preview.appliedTemplate && !!template
                    }
                    value={packageEnd}
                    onChange={(event) => setPackageEnd(event.target.value)}
                  />
                </Field>
              </div>
              <div className="hidden overflow-x-auto md:block">
                <table className="w-full min-w-[920px] text-sm">
                  <thead>
                    <tr className="border-b text-left text-xs text-muted-foreground">
                      <th className="p-2">{t("contracts.legal.permit")}</th>
                      <th className="p-2">{t("contracts.legal.agency")}</th>
                      <th className="p-2">{t("contracts.legal.assignee")}</th>
                      <th className="p-2">{t("contracts.legal.approver")}</th>
                      <th className="p-2">{t("contracts.legal.period")}</th>
                      <th className="p-2">{t("contracts.legal.predecessor")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selected.map((permit) => (
                      <PermitRow
                        key={permit.code}
                        permit={permit}
                        all={permits.filter(
                          (item) => item.selected || (item.alreadyExists && !item.requiresSetup),
                        )}
                        preview={preview}
                        update={updatePermit}
                        error={permitErrors[permit.code]}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="space-y-3 md:hidden">
                {selected.map((permit) => (
                  <PermitCard
                    key={permit.code}
                    permit={permit}
                    all={permits.filter(
                      (item) => item.selected || (item.alreadyExists && !item.requiresSetup),
                    )}
                    preview={preview}
                    update={updatePermit}
                    error={permitErrors[permit.code]}
                  />
                ))}
              </div>
            </div>
          ) : null}

          {!loading && preview && step === 3 ? (
            <div className="space-y-3">
              <Summary label={t("contracts.legal.lead")} value={memberName(leadUserId)} />
              <Summary
                label={t("contracts.legal.period")}
                value={`${packageStart} → ${packageEnd}`}
              />
              {selected.map((permit) => (
                <div key={permit.code} className="rounded-lg border p-3 text-sm">
                  <p className="font-medium">{permit.name}</p>
                  {permit.issuingAgency ? (
                    <p className="mt-1 text-muted-foreground">{permit.issuingAgency}</p>
                  ) : null}
                  <p className="mt-1 text-muted-foreground">
                    {memberName(permit.assigneeUserId)} → {memberName(permit.approverUserId)} ·{" "}
                    {permit.plannedStart} → {permit.plannedEnd}
                  </p>
                </div>
              ))}
            </div>
          ) : null}
        </div>

        <DialogFooter className="sticky bottom-0 flex-row justify-between gap-2 border-t bg-background px-4 py-3 sm:px-6">
          <Button
            variant="outline"
            disabled={loading || saving || step === 0}
            onClick={() => {
              setError(null);
              setStep((current) => current - 1);
            }}
          >
            <ChevronLeft className="mr-1 h-4 w-4" />
            {t("common.back")}
          </Button>
          {step < 3 ? (
            <Button disabled={loading || !preview || preview.alreadyActivated} onClick={next}>
              {t("common.next")}
              <ChevronRight className="ml-1 h-4 w-4" />
            </Button>
          ) : (
            <Button
              data-testid="legal-activation-confirm"
              disabled={saving}
              onClick={() => void submit()}
            >
              {saving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Check className="mr-2 h-4 w-4" />
              )}
              {t("contracts.legal.confirm")}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 font-medium">{value}</p>
    </div>
  );
}
function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
function MemberSelect({
  ariaLabel,
  disabled,
  value,
  members,
  onChange,
}: {
  ariaLabel: string;
  disabled?: boolean;
  value: number;
  members: LegalActivationPreview["projectMembers"];
  onChange: (id: number) => void;
}) {
  const { t } = useI18n();
  return (
    <SearchableSelect
      disabled={disabled}
      value={value ? String(value) : ""}
      onChange={(next) => onChange(Number(next))}
      ariaLabel={ariaLabel}
      placeholder={ariaLabel}
      options={members
        .filter(
          (member, index) => members.findIndex((item) => item.userId === member.userId) === index,
        )
        .map((member) => personOption(member, t))}
    />
  );
}
function Predecessors({
  permit,
  all,
  update,
}: {
  permit: PermitDraft;
  all: PermitDraft[];
  update: (code: string, patch: Partial<PermitDraft>) => void;
}) {
  return (
    <div className="flex max-h-24 flex-col gap-1 overflow-y-auto">
      {all
        .filter((item) => item.code !== permit.code)
        .map((item) => (
          <label key={item.code} className="flex items-center gap-1 text-xs">
            <Checkbox
              checked={permit.predecessors.includes(item.code)}
              onCheckedChange={(checked) =>
                update(permit.code, {
                  predecessors: checked
                    ? [...permit.predecessors, item.code]
                    : permit.predecessors.filter((code) => code !== item.code),
                })
              }
            />
            {item.name}
          </label>
        ))}
    </div>
  );
}
function PermitRow({
  permit,
  all,
  preview,
  update,
  error,
}: {
  permit: PermitDraft;
  all: PermitDraft[];
  preview: LegalActivationPreview;
  update: (code: string, patch: Partial<PermitDraft>) => void;
  error?: string;
}) {
  const { t } = useI18n();
  return (
    <tr className={cn("border-b align-top", error && "bg-destructive/5")}>
      <td className="p-2 font-medium">
        {permit.name}
        {error ? (
          <p role="alert" className="mt-1 text-xs font-normal text-destructive">
            {error}
          </p>
        ) : null}
      </td>
      <td className="p-2">
        <Input
          value={permit.issuingAgency}
          maxLength={200}
          onChange={(event) => update(permit.code, { issuingAgency: event.target.value })}
        />
      </td>
      <td className="p-2">
        <MemberSelect
          ariaLabel={t("contracts.legal.assignee")}
          value={permit.assigneeUserId}
          members={preview.projectMembers}
          onChange={(id) =>
            update(permit.code, {
              assigneeUserId: id,
              approverUserId:
                permit.approverUserId === id ? preview.projectManagerUserId : permit.approverUserId,
            })
          }
        />
      </td>
      <td className="p-2">
        <MemberSelect
          ariaLabel={t("contracts.legal.approver")}
          value={permit.approverUserId}
          members={preview.projectMembers.filter(
            (member) => member.userId !== permit.assigneeUserId,
          )}
          onChange={(id) => update(permit.code, { approverUserId: id })}
        />
      </td>
      <td className="p-2 space-y-1">
        <Input
          type="date"
          value={permit.plannedStart}
          onChange={(event) => update(permit.code, { plannedStart: event.target.value })}
        />
        <Input
          type="date"
          value={permit.plannedEnd}
          onChange={(event) => update(permit.code, { plannedEnd: event.target.value })}
        />
      </td>
      <td className="p-2">
        <Predecessors permit={permit} all={all} update={update} />
      </td>
    </tr>
  );
}
function PermitCard({
  permit,
  all,
  preview,
  update,
  error,
}: {
  permit: PermitDraft;
  all: PermitDraft[];
  preview: LegalActivationPreview;
  update: (code: string, patch: Partial<PermitDraft>) => void;
  error?: string;
}) {
  const { t } = useI18n();
  return (
    <div
      className={cn(
        "space-y-3 rounded-lg border p-3",
        error && "border-destructive/40 bg-destructive/5",
      )}
    >
      <p className="font-medium">{permit.name}</p>
      {error ? (
        <p role="alert" className="text-xs text-destructive">
          {error}
        </p>
      ) : null}
      <Field label={t("contracts.legal.agency")}>
        <Input
          value={permit.issuingAgency}
          maxLength={200}
          onChange={(event) => update(permit.code, { issuingAgency: event.target.value })}
        />
      </Field>
      <Field label={t("contracts.legal.assignee")}>
        <MemberSelect
          ariaLabel={t("contracts.legal.assignee")}
          value={permit.assigneeUserId}
          members={preview.projectMembers}
          onChange={(id) =>
            update(permit.code, {
              assigneeUserId: id,
              approverUserId:
                permit.approverUserId === id ? preview.projectManagerUserId : permit.approverUserId,
            })
          }
        />
      </Field>
      <Field label={t("contracts.legal.approver")}>
        <MemberSelect
          ariaLabel={t("contracts.legal.approver")}
          value={permit.approverUserId}
          members={preview.projectMembers.filter(
            (member) => member.userId !== permit.assigneeUserId,
          )}
          onChange={(id) => update(permit.code, { approverUserId: id })}
        />
      </Field>
      <div className="grid grid-cols-2 gap-2">
        <Input
          aria-label={t("contracts.legal.start")}
          type="date"
          value={permit.plannedStart}
          onChange={(event) => update(permit.code, { plannedStart: event.target.value })}
        />
        <Input
          aria-label={t("contracts.legal.end")}
          type="date"
          value={permit.plannedEnd}
          onChange={(event) => update(permit.code, { plannedEnd: event.target.value })}
        />
      </div>
      <Predecessors permit={permit} all={all} update={update} />
    </div>
  );
}
