import { useEffect, useState } from "react";
import { adminApi, type MasterDataOption } from "@/services/adminApi";
import type {
  LegalChecklistTemplate,
  SaveLegalChecklistTemplate,
} from "@/services/legalChecklistTypes";
import { usePermissions } from "@/hooks/usePermissions";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { localizedName } from "@/lib/category";
import { useAppSelector } from "@/store";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";

const blank = (): SaveLegalChecklistTemplate => ({
  code: "",
  nameVi: "",
  name: "",
  nameZh: "",
  nameJa: "",
  scopeVi: "",
  scope: "",
  scopeZh: "",
  scopeJa: "",
  items: [],
});
const languages = [
  ["vi", "nameVi", "scopeVi"],
  ["en", "name", "scope"],
  ["zh", "nameZh", "scopeZh"],
  ["ja", "nameJa", "scopeJa"],
] as const;

export default function LegalChecklistTemplates() {
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const userId = useAppSelector((state) => state.auth.user?.userId);
  const [rows, setRows] = useState<LegalChecklistTemplate[]>([]);
  const [types, setTypes] = useState<MasterDataOption[]>([]);
  const [draft, setDraft] = useState<SaveLegalChecklistTemplate | null>(null);
  const [editing, setEditing] = useState<LegalChecklistTemplate | null>(null);
  const [selected, setSelected] = useState<LegalChecklistTemplate | null>(null);
  const [approval, setApproval] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const load = async () => {
    const [templates, permits] = await Promise.all([
      adminApi.listLegalChecklistTemplates(),
      adminApi.listMasterDataByCategory("permit_type"),
    ]);
    setRows(templates.data);
    setTypes(permits.data);
    setSelected((current) =>
      current ? (templates.data.find((item) => item.id === current.id) ?? current) : null,
    );
  };
  useEffect(() => {
    void load()
      .catch((err) => setError(extractApiError(err)))
      .finally(() => setLoading(false));
  }, []);
  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    setError("");
    try {
      await action();
      await load();
      setDraft(null);
      setSelected(null);
      setApproval("");
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setBusy(false);
    }
  };
  const save = () => {
    if (!draft) return;
    if (
      !/^[A-Za-z0-9][A-Za-z0-9_-]{0,59}$/.test(draft.code) ||
      languages.some(([, name, scope]) => !draft[name].trim() || !draft[scope].trim()) ||
      !draft.items.some((item) => item.isRequired)
    ) {
      setError(t("legalTemplates.validation"));
      return;
    }
    void run(() => adminApi.saveLegalChecklistTemplate(editing?.id ?? null, draft));
  };
  const refresh = async () => {
    setBusy(true);
    try {
      await load();
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setBusy(false);
    }
  };
  return (
    <details className="rounded-lg border bg-white p-3" data-testid="legal-templates">
      <summary className="cursor-pointer font-semibold">{t("legalTemplates.title")}</summary>
      <div className="mt-3 space-y-3">
        {error ? (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        ) : null}
        {loading ? <p>{t("common.loading")}</p> : null}
        {!loading && rows.length === 0 ? <p>{t("legalTemplates.empty")}</p> : null}
        <Button
          data-testid="legal-template-refresh"
          variant="outline"
          disabled={busy || loading}
          onClick={() => void refresh()}
        >
          {t("common.refresh")}
        </Button>
        {draft &&
        selected?.status === "Draft" &&
        editing?.id === selected.id &&
        draft.rowVersion !== selected.rowVersion ? (
          <Button
            disabled={busy}
            variant="outline"
            onClick={() => {
              setDraft({ ...draft, rowVersion: selected.rowVersion });
              setEditing(selected);
              setError("");
            }}
          >
            {t("legalTemplates.reviewLatest")}
          </Button>
        ) : null}
        {has("permit.templates.manage") ? (
          <Button
            disabled={busy}
            onClick={() => {
              setEditing(null);
              setDraft(blank());
              setSelected(null);
            }}
          >
            {t("legalTemplates.create")}
          </Button>
        ) : null}
        <div className="grid gap-2 md:grid-cols-2">
          {rows.map((row) => (
            <Button
              key={row.id}
              variant="outline"
              className="h-auto justify-start whitespace-normal text-left"
              disabled={busy}
              onClick={() => {
                setSelected(row);
                setDraft(null);
                setApproval("");
              }}
            >
              {row.code} · {row.version} · {localizedName({ ...row, nameEn: row.name }, lang)} ·{" "}
              {t(`legalTemplates.status.${row.status}`)}
            </Button>
          ))}
        </div>
        {selected ? (
          <div className="space-y-3 rounded border p-3">
            <h3 className="font-semibold">
              {localizedName({ ...selected, nameEn: selected.name }, lang)} · {selected.code} ·{" "}
              {selected.version}
            </h3>
            {languages.map(([lang, name, scope]) => (
              <div key={lang}>
                <p className="font-medium">
                  {t(`legalTemplates.language.${lang}`)} · {selected[name]}
                </p>
                <p className="whitespace-pre-wrap text-sm">{selected[scope]}</p>
              </div>
            ))}
            <p className="text-xs">
              {t("legalTemplates.author")} #{selected.createdByUserId} ·{" "}
              {t("legalTemplates.editor")} #{selected.updatedByUserId}
            </p>
            {selected.publishedByUserId ? (
              <p className="text-xs">
                {t("legalTemplates.reviewer")} #{selected.publishedByUserId} ·{" "}
                {selected.publishedAt ? new Date(selected.publishedAt).toLocaleString() : ""} ·{" "}
                {selected.approvalReference}
              </p>
            ) : null}
            <ul className="space-y-1">
              {selected.items.map((item) => (
                <li key={item.permitTypeCode}>
                  {types.find((type) => type.code === item.permitTypeCode)?.name ??
                    item.permitTypeCode}{" "}
                  · {t(item.isRequired ? "legalTemplates.required" : "legalTemplates.optional")}
                </li>
              ))}
            </ul>
            <div className="flex flex-wrap gap-2">
              {has("permit.templates.manage") && selected.status === "Draft" ? (
                <Button
                  disabled={busy}
                  onClick={() => {
                    setEditing(selected);
                    setDraft({ ...selected });
                  }}
                >
                  {t("common.edit")}
                </Button>
              ) : null}
              {has("permit.templates.manage") && selected.status !== "Draft" ? (
                <Button
                  disabled={busy}
                  onClick={() =>
                    void run(() =>
                      adminApi.decideLegalChecklistTemplate(selected.id, "versions", {
                        rowVersion: selected.rowVersion,
                      }),
                    )
                  }
                >
                  {t("legalTemplates.newVersion")}
                </Button>
              ) : null}
              {has("permit.templates.publish") && selected.status === "Published" ? (
                <Button
                  disabled={busy}
                  variant="outline"
                  onClick={() =>
                    void run(() =>
                      adminApi.decideLegalChecklistTemplate(selected.id, "retire", {
                        rowVersion: selected.rowVersion,
                      }),
                    )
                  }
                >
                  {t("legalTemplates.retire")}
                </Button>
              ) : null}
            </div>
            {has("permit.templates.publish") && selected.status === "Draft" ? (
              <div className="space-y-2">
                <Label htmlFor="legal-approval">{t("legalTemplates.approvalReference")}</Label>
                <Textarea
                  id="legal-approval"
                  value={approval}
                  maxLength={1000}
                  onChange={(event) => setApproval(event.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  {t("legalTemplates.independentReview")}
                </p>
                <Button
                  disabled={
                    busy ||
                    !approval.trim() ||
                    userId === selected.createdByUserId ||
                    userId === selected.updatedByUserId
                  }
                  onClick={() =>
                    void run(() =>
                      adminApi.decideLegalChecklistTemplate(selected.id, "publish", {
                        rowVersion: selected.rowVersion,
                        approvalReference: approval,
                      }),
                    )
                  }
                >
                  {t("legalTemplates.publish")}
                </Button>
              </div>
            ) : null}
          </div>
        ) : null}
        {draft ? (
          <form
            className="space-y-3 rounded border p-3"
            onSubmit={(event) => {
              event.preventDefault();
              save();
            }}
          >
            <Label htmlFor="legal-code">{t("legalTemplates.code")}</Label>
            <Input
              id="legal-code"
              value={draft.code}
              required
              maxLength={60}
              pattern={"[A-Za-z0-9][A-Za-z0-9_\\-]*"}
              disabled={!!editing || busy}
              onChange={(event) => setDraft({ ...draft, code: event.target.value })}
            />
            <div className="grid gap-3 md:grid-cols-2">
              {languages.map(([lang, name, scope]) => (
                <fieldset key={lang} className="space-y-2">
                  <legend>{t(`legalTemplates.language.${lang}`)}</legend>
                  <Label htmlFor={`legal-${name}`}>{t("legalTemplates.name")}</Label>
                  <Input
                    id={`legal-${name}`}
                    value={draft[name]}
                    required
                    maxLength={200}
                    disabled={busy}
                    onChange={(event) => setDraft({ ...draft, [name]: event.target.value })}
                  />
                  <Label htmlFor={`legal-${scope}`}>{t("legalTemplates.scope")}</Label>
                  <Textarea
                    id={`legal-${scope}`}
                    value={draft[scope]}
                    required
                    maxLength={2000}
                    disabled={busy}
                    onChange={(event) => setDraft({ ...draft, [scope]: event.target.value })}
                  />
                </fieldset>
              ))}
            </div>
            <p className="text-sm">{t("legalTemplates.validation")}</p>
            {types.map((type) => {
              const item = draft.items.find((entry) => entry.permitTypeCode === type.code);
              return (
                <div
                  key={type.code}
                  data-testid={`template-permit-${type.code}`}
                  className="flex flex-wrap items-center gap-3 rounded border p-2"
                >
                  <label className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      disabled={busy}
                      checked={!!item}
                      onChange={(event) =>
                        setDraft({
                          ...draft,
                          items: event.target.checked
                            ? [...draft.items, { permitTypeCode: type.code, isRequired: false }]
                            : draft.items.filter((entry) => entry.permitTypeCode !== type.code),
                        })
                      }
                    />
                    {type.name}
                  </label>
                  <label className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      disabled={busy || !item}
                      checked={item?.isRequired ?? false}
                      onChange={(event) =>
                        setDraft({
                          ...draft,
                          items: draft.items.map((entry) =>
                            entry.permitTypeCode === type.code
                              ? { ...entry, isRequired: event.target.checked }
                              : entry,
                          ),
                        })
                      }
                    />
                    {t("legalTemplates.required")}
                  </label>
                </div>
              );
            })}
            <div className="flex gap-2">
              <Button type="submit" disabled={busy}>
                {t("common.save")}
              </Button>
              <Button
                type="button"
                disabled={busy}
                variant="outline"
                onClick={() => setDraft(null)}
              >
                {t("common.cancel")}
              </Button>
            </div>
          </form>
        ) : null}
      </div>
    </details>
  );
}
