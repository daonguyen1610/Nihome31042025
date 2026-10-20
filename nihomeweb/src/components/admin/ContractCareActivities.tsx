import { useEffect, useRef, useState } from "react";
import { ActivityTimeline } from "@/components/admin/ActivityTimeline";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ContractCareActivityListResponse,
  type ContractCareActivityType,
} from "@/services/adminApi";

const types: ContractCareActivityType[] = ["Call", "Email", "Meeting", "Note"];

export default function ContractCareActivities({
  contractId,
  canManage,
}: {
  contractId: number;
  canManage: boolean;
}) {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const [page, setPage] = useState(1);
  const [refresh, setRefresh] = useState(0);
  const [data, setData] = useState<ContractCareActivityListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [type, setType] = useState<ContractCareActivityType>("Call");
  const [content, setContent] = useState("");
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const draftKey = useRef<{ payload: string; key: string } | null>(null);
  const submitting = useRef(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    adminApi
      .listContractCareActivities(contractId, page)
      .then(({ data: result }) => {
        if (active) setData(result);
      })
      .catch((reason) => {
        if (active) setError(extractApiError(reason));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [contractId, page, refresh]);

  const save = async () => {
    if (submitting.current) return;
    const trimmed = content.trim();
    if (!trimmed || trimmed.length > 4000) {
      setSaveError(t("contracts.care.contentRule"));
      return;
    }
    const payload = JSON.stringify({ type, content: trimmed });
    if (draftKey.current?.payload !== payload) {
      draftKey.current = { payload, key: crypto.randomUUID() };
    }
    submitting.current = true;
    setSaving(true);
    setSaveError(null);
    try {
      await adminApi.addContractCareActivity(
        contractId,
        { type, content: trimmed },
        draftKey.current.key,
      );
      draftKey.current = null;
      setContent("");
      setPage(1);
      setRefresh((value) => value + 1);
      toast({ title: t("contracts.care.saved") });
    } catch (reason) {
      setSaveError(extractApiError(reason));
    } finally {
      submitting.current = false;
      setSaving(false);
    }
  };

  return (
    <section className="space-y-4" data-testid="contract-care">
      <h2 className="text-lg font-semibold">{t("contracts.care.title")}</h2>
      <p className="text-sm text-muted-foreground">{t("contracts.care.hint")}</p>
      {loading ? (
        <p role="status">{t("common.loading")}</p>
      ) : error ? (
        <div role="alert" className="space-y-2">
          <p className="text-sm text-destructive">{error}</p>
          <Button variant="outline" onClick={() => setRefresh((value) => value + 1)}>
            {t("common.retry")}
          </Button>
        </div>
      ) : data?.items.length ? (
        <>
          <ActivityTimeline
            locale={lang}
            todayLabel={t("common.today")}
            entries={data.items.map((entry) => ({
              id: entry.id,
              typeLabel: t(`customers.activity.${entry.type}`),
              content: entry.content,
              createdByName: entry.createdByName,
              at: entry.occurredAt,
            }))}
          />
          <div className="flex items-center justify-end gap-2">
            <Button
              variant="outline"
              disabled={page <= 1}
              onClick={() => setPage((value) => value - 1)}
            >
              {t("common.prev")}
            </Button>
            <span className="text-sm tabular-nums">
              {page.toLocaleString(lang)} /{" "}
              {Math.max(1, Math.ceil(data.total / data.pageSize)).toLocaleString(lang)}
            </span>
            <Button
              variant="outline"
              disabled={page * data.pageSize >= data.total}
              onClick={() => setPage((value) => value + 1)}
            >
              {t("common.next")}
            </Button>
          </div>
        </>
      ) : (
        <p className="rounded border border-dashed p-4 text-sm text-muted-foreground">
          {t("contracts.care.empty")}
        </p>
      )}
      {canManage ? (
        <form
          className="space-y-3 rounded border p-4"
          onSubmit={(event) => {
            event.preventDefault();
            void save();
          }}
        >
          <div className="space-y-1">
            <Label htmlFor="contract-care-type">{t("contracts.care.type")}</Label>
            <Select
              value={type}
              disabled={saving}
              onValueChange={(value) => setType(value as ContractCareActivityType)}
            >
              <SelectTrigger id="contract-care-type" className="w-full sm:w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {types.map((value) => (
                  <SelectItem key={value} value={value}>
                    {t(`customers.activity.${value}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="contract-care-content">{t("contracts.care.content")}</Label>
            <Textarea
              id="contract-care-content"
              value={content}
              disabled={saving}
              required
              maxLength={4000}
              rows={3}
              aria-describedby="contract-care-rule"
              onChange={(event) => setContent(event.target.value)}
            />
            <p id="contract-care-rule" className="text-xs text-muted-foreground">
              {t("contracts.care.contentRule")}
            </p>
          </div>
          {saveError ? (
            <p role="alert" className="text-sm text-destructive">
              {saveError}
            </p>
          ) : null}
          <Button type="submit" disabled={saving || !content.trim()}>
            {saving ? t("common.loading") : t("customers.activity.add")}
          </Button>
        </form>
      ) : null}
    </section>
  );
}
