import { useEffect, useState } from "react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import { adminApi, type ConstructionTaskDrawingsResponse } from "@/services/adminApi";

export default function ConstructionTaskDrawings({
  taskId,
  canManage,
}: {
  taskId: number;
  canManage: boolean;
}) {
  const { t } = useI18n();
  const [data, setData] = useState<ConstructionTaskDrawingsResponse | null>(null);
  const [selected, setSelected] = useState<number[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [reload, setReload] = useState(0);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    setData(null);
    setSaved(false);
    adminApi
      .getConstructionTaskDrawings(taskId)
      .then(({ data: response }) => {
        if (!active) return;
        setData(response);
        setSelected(response.linked.map((x) => x.ifcReleaseItemId));
      })
      .catch((err) => {
        if (active) setError(extractApiError(err));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [taskId, reload]);

  const save = async () => {
    if (!data || selected.length > 200) return;
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      const { data: response } = await adminApi.setConstructionTaskDrawings(taskId, {
        version: data.version,
        ifcReleaseItemIds: selected,
      });
      setData(response);
      setSelected(response.linked.map((x) => x.ifcReleaseItemId));
      setSaved(true);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="space-y-3 rounded-lg border p-3" data-testid="construction-task-drawings">
      <h3 className="font-medium">{t("constructionTasks.drawings.title")}</h3>
      <p className="text-sm text-muted-foreground">{t("constructionTasks.drawings.help")}</p>
      {loading && <p role="status">{t("common.loading")}</p>}
      {error && (
        <div role="alert" className="space-y-2 text-sm text-destructive">
          <p>{error}</p>
          <Button variant="outline" disabled={saving} onClick={() => setReload((x) => x + 1)}>
            {t("common.refresh")}
          </Button>
        </div>
      )}
      {data && (
        <>
          {data.linked.length === 0 && (
            <p className="text-sm text-muted-foreground">{t("constructionTasks.drawings.empty")}</p>
          )}
          <ul className="space-y-2">
            {data.linked.map((drawing) => (
              <li key={drawing.ifcReleaseItemId} className="space-y-1 break-words text-sm">
                <p>
                  {drawing.drawingCode} — {drawing.title}
                </p>
                <p className="text-xs text-muted-foreground">
                  {drawing.releaseNumber} · R{drawing.revisionNumber}
                </p>
                {drawing.hasFile && (
                  <AdminFilePreview
                    fileName={drawing.fileName}
                    contentType={drawing.contentType}
                    fetchFile={async () =>
                      (
                        await adminApi.getConstructionTaskDrawingContent(
                          taskId,
                          drawing.ifcReleaseItemId,
                        )
                      ).data
                    }
                  />
                )}
              </li>
            ))}
          </ul>
          {canManage && (
            <>
              {data.available.length === 0 && (
                <p className="text-sm text-muted-foreground">
                  {t("constructionTasks.drawings.noneAvailable")}
                </p>
              )}
              <div className="max-h-64 space-y-2 overflow-y-auto">
                {data.available.map((drawing) => (
                  <label
                    key={drawing.ifcReleaseItemId}
                    className="flex items-start gap-2 rounded border p-2 text-sm"
                  >
                    <Checkbox
                      disabled={
                        saving ||
                        (!selected.includes(drawing.ifcReleaseItemId) && selected.length >= 200)
                      }
                      checked={selected.includes(drawing.ifcReleaseItemId)}
                      onCheckedChange={(checked) => {
                        setSaved(false);
                        setSelected((current) =>
                          checked === true
                            ? [...current, drawing.ifcReleaseItemId]
                            : current.filter((x) => x !== drawing.ifcReleaseItemId),
                        );
                      }}
                    />
                    <span className="min-w-0 break-words">
                      {drawing.drawingCode} — {drawing.title}
                      <span className="block text-xs text-muted-foreground">
                        {drawing.constructionItem} · {drawing.releaseNumber} · R
                        {drawing.revisionNumber}
                      </span>
                    </span>
                  </label>
                ))}
              </div>
              <Button
                type="button"
                disabled={saving}
                onClick={() => void save()}
                data-testid="construction-drawings-save"
              >
                {saving ? t("common.saving") : t("constructionTasks.drawings.save")}
              </Button>
              {saved && (
                <p role="status" className="text-sm">
                  {t("constructionTasks.drawings.saved")}
                </p>
              )}
            </>
          )}
        </>
      )}
    </section>
  );
}
