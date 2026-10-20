import { useEffect, useRef, useState } from "react";
import { adminApi, type ContractReminder, type ContractReminderPreview } from "@/services/adminApi";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export default function ContractReminders({
  contractId,
  canManage,
}: {
  contractId: number;
  canManage: boolean;
}) {
  const { t } = useI18n();
  const [rows, setRows] = useState<ContractReminder[]>([]);
  const [kind, setKind] = useState<"Renewal" | "End">("Renewal");
  const [date, setDate] = useState("");
  const [contactId, setContactId] = useState<number>();
  const [preview, setPreview] = useState<ContractReminderPreview | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reload, setReload] = useState(0);
  const previewVersion = useRef(0);
  const invalidatePreview = () => {
    previewVersion.current += 1;
    setPreview(null);
  };
  const today = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(
    new Date(),
  );
  useEffect(() => {
    let active = true;
    setLoading(true);
    adminApi
      .listContractReminders(contractId)
      .then(({ data }) => {
        if (active) {
          setRows(data);
          setError("");
        }
      })
      .catch((reason) => {
        if (active) setError(extractApiError(reason));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
      previewVersion.current += 1;
    };
  }, [contractId, reload]);
  const run = async (action: () => Promise<unknown>) => {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setBusy(false);
    }
  };
  return (
    <section className="space-y-4 rounded-lg border p-4" aria-label={t("contractReminder.title")}>
      <h2 className="font-semibold">{t("contractReminder.title")}</h2>
      <p className="text-sm text-muted-foreground">{t("contractReminder.help")}</p>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {canManage && (
        <div className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1">
              <Label htmlFor="reminder-kind">{t("contractReminder.kind")}</Label>
              <select
                id="reminder-kind"
                className="h-10 w-full rounded-md border bg-background px-3"
                value={kind}
                disabled={busy}
                onChange={(e) => {
                  setKind(e.target.value as "Renewal" | "End");
                  invalidatePreview();
                }}
              >
                <option value="Renewal">{t("contractReminder.Renewal")}</option>
                <option value="End">{t("contractReminder.End")}</option>
              </select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="reminder-date">{t("contractReminder.date")}</Label>
              <Input
                id="reminder-date"
                type="date"
                min={today}
                value={date}
                disabled={busy}
                onChange={(e) => {
                  setDate(e.target.value);
                  invalidatePreview();
                }}
              />
            </div>
          </div>
          <Button
            variant="outline"
            disabled={busy || !date}
            onClick={() =>
              run(async () => {
                if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || date < today) {
                  setError(t("contractReminder.dateRule"));
                  return;
                }
                const version = ++previewVersion.current;
                setPreview(null);
                const { data } = await adminApi.previewContractReminder(contractId, {
                  kind,
                  reminderDate: date,
                  contactId,
                });
                if (version !== previewVersion.current) return;
                setContactId(data.contactId);
                setPreview(data);
              })
            }
          >
            {t("contractReminder.preview")}
          </Button>
          {preview && (
            <div className="space-y-3">
              <p className="text-sm font-medium" data-testid="reminder-preview-schedule">
                {t(`contractReminder.${preview.kind}`)} · {t("contractReminder.date")}:{" "}
                {preview.reminderDate.slice(0, 10)}
              </p>
              <Label htmlFor="reminder-contact">{t("contractReminder.recipient")}</Label>
              <select
                id="reminder-contact"
                className="h-10 w-full rounded-md border bg-background px-3"
                value={contactId}
                disabled={busy}
                onChange={(e) => {
                  setContactId(Number(e.target.value));
                  invalidatePreview();
                }}
              >
                {preview.contacts.map((contact) => (
                  <option key={contact.id} value={contact.id}>
                    {contact.name} — {contact.email}
                  </option>
                ))}
              </select>
              <p className="break-words text-sm">{preview.subject}</p>
              <iframe
                title={t("contractReminder.preview")}
                srcDoc={preview.body}
                sandbox=""
                className="h-64 w-full rounded border bg-white"
              />
              <Button
                disabled={busy}
                onClick={() =>
                  run(async () => {
                    await adminApi.scheduleContractReminder(contractId, preview.previewToken);
                    setPreview(null);
                    setReload((value) => value + 1);
                  })
                }
              >
                {t("contractReminder.confirm")}
              </Button>
            </div>
          )}
        </div>
      )}
      {loading ? (
        <p role="status">{t("common.loading")}</p>
      ) : rows.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("contractReminder.empty")}</p>
      ) : (
        <ul className="space-y-3">
          {rows.map((row) => (
            <li key={row.id} className="space-y-2 rounded border p-3 text-sm">
              <p>
                {t(`contractReminder.${row.kind}`)} · {row.reminderDate.slice(0, 10)} ·{" "}
                {t(`contractReminder.${row.status}`)}
              </p>
              <p className="break-words">{row.toEmail}</p>
              <p className="break-words">{row.subject}</p>
              {(row.errorCode || row.status === "Sending") && (
                <p className="text-amber-700">
                  {t(`contractReminder.${row.errorCode ?? "deliveryWarning"}`)}
                </p>
              )}
              {canManage && row.status === "Scheduled" && (
                <Button
                  size="sm"
                  variant="outline"
                  disabled={busy}
                  onClick={() =>
                    run(async () => {
                      await adminApi.cancelContractReminder(contractId, row.id);
                      setReload((value) => value + 1);
                    })
                  }
                >
                  {t("common.cancel")}
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
      <Button
        variant="ghost"
        disabled={busy || loading}
        onClick={() => setReload((value) => value + 1)}
      >
        {t("common.refresh")}
      </Button>
    </section>
  );
}
