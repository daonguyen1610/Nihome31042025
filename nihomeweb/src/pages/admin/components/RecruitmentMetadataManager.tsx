import { useCallback, useEffect, useMemo, useState } from "react";
import { Check, Pencil, Plus, Trash2, X } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  adminApi,
  type RecruitmentMetadataItemResponse,
  type UpsertRecruitmentMetadataItemRequest,
} from "@/services/adminApi";

type MetadataGroupKey = "employment-type" | "experience-level" | "application-status";

type GroupFormState = {
  value: string;
  label: string;
  translationKey: string;
  isActive: boolean;
  sortOrder: number;
};

const GROUPS: MetadataGroupKey[] = ["employment-type", "experience-level", "application-status"];

const emptyForm: GroupFormState = {
  value: "",
  label: "",
  translationKey: "",
  isActive: true,
  sortOrder: 0,
};

function getErrorMessage(error: unknown) {
  if (
    typeof error === "object" &&
    error !== null &&
    "response" in error &&
    typeof error.response === "object" &&
    error.response !== null &&
    "data" in error.response &&
    typeof error.response.data === "object" &&
    error.response.data !== null &&
    "message" in error.response.data &&
    typeof error.response.data.message === "string"
  ) {
    return error.response.data.message;
  }

  return undefined;
}

const initialForms = (): Record<MetadataGroupKey, GroupFormState> => ({
  "employment-type": { ...emptyForm },
  "experience-level": { ...emptyForm },
  "application-status": { ...emptyForm },
});

const initialEditingIds = (): Record<MetadataGroupKey, number | null> => ({
  "employment-type": null,
  "experience-level": null,
  "application-status": null,
});

const RecruitmentMetadataManager = ({
  onUpdated,
}: {
  onUpdated?: () => Promise<void> | void;
}) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [items, setItems] = useState<RecruitmentMetadataItemResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [submittingGroup, setSubmittingGroup] = useState<MetadataGroupKey | null>(null);
  const [forms, setForms] = useState<Record<MetadataGroupKey, GroupFormState>>(initialForms);
  const [editingIds, setEditingIds] = useState<Record<MetadataGroupKey, number | null>>(initialEditingIds);

  const loadItems = useCallback(async () => {
    setLoading(true);
    try {
      const response = await adminApi.getRecruitmentMetadataItems(true);
      setItems(response.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    loadItems();
  }, [loadItems]);

  const itemsByGroup = useMemo(
    () =>
      GROUPS.reduce<Record<MetadataGroupKey, RecruitmentMetadataItemResponse[]>>((acc, groupKey) => {
        acc[groupKey] = items
          .filter((item) => item.groupKey === groupKey)
          .sort((left, right) => left.sortOrder - right.sortOrder || left.label.localeCompare(right.label));
        return acc;
      }, {
        "employment-type": [],
        "experience-level": [],
        "application-status": [],
      }),
    [items],
  );

  const resetGroupForm = (groupKey: MetadataGroupKey) => {
    setForms((prev) => ({
      ...prev,
      [groupKey]: {
        ...emptyForm,
        sortOrder: itemsByGroup[groupKey].length + 1,
      },
    }));
    setEditingIds((prev) => ({ ...prev, [groupKey]: null }));
  };

  const startCreate = (groupKey: MetadataGroupKey) => {
    resetGroupForm(groupKey);
  };

  const startEdit = (groupKey: MetadataGroupKey, item: RecruitmentMetadataItemResponse) => {
    setForms((prev) => ({
      ...prev,
      [groupKey]: {
        value: item.value,
        label: item.label,
        translationKey: item.translationKey ?? "",
        isActive: item.isActive,
        sortOrder: item.sortOrder,
      },
    }));
    setEditingIds((prev) => ({ ...prev, [groupKey]: item.id }));
  };

  const updateForm = <K extends keyof GroupFormState>(
    groupKey: MetadataGroupKey,
    key: K,
    value: GroupFormState[K],
  ) => {
    setForms((prev) => ({
      ...prev,
      [groupKey]: {
        ...prev[groupKey],
        [key]: value,
      },
    }));
  };

  const saveGroup = async (groupKey: MetadataGroupKey, event: React.FormEvent) => {
    event.preventDefault();
    const form = forms[groupKey];

    if (!form.value.trim() || !form.label.trim()) {
      toast({
        title: t("form.required"),
        description: t("recruit.metadata.fields.value"),
        variant: "destructive",
      });
      return;
    }

    setSubmittingGroup(groupKey);

    try {
      const payload: UpsertRecruitmentMetadataItemRequest = {
        groupKey,
        value: form.value.trim(),
        label: form.label.trim(),
        translationKey: form.translationKey.trim() || undefined,
        isActive: form.isActive,
        sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      };

      const editingId = editingIds[groupKey];
      if (editingId == null) {
        await adminApi.createRecruitmentMetadataItem(payload);
        toast({ title: t("form.created") });
      } else {
        await adminApi.updateRecruitmentMetadataItem(editingId, payload);
        toast({ title: t("form.updated") });
      }

      resetGroupForm(groupKey);
      await loadItems();
      await onUpdated?.();
    } catch (error) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(error),
        variant: "destructive",
      });
    } finally {
      setSubmittingGroup(null);
    }
  };

  const removeItem = async (item: RecruitmentMetadataItemResponse) => {
    if (!window.confirm(t("form.confirmDelete"))) return;

    try {
      await adminApi.deleteRecruitmentMetadataItem(item.id);
      toast({ title: t("form.deleted") });
      await loadItems();
      await onUpdated?.();
    } catch (error) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(error),
        variant: "destructive",
      });
    }
  };

  const groupTitleKey: Record<MetadataGroupKey, string> = {
    "employment-type": "recruit.metadata.groups.employmentType",
    "experience-level": "recruit.metadata.groups.experienceLevel",
    "application-status": "recruit.metadata.groups.applicationStatus",
  };

  return (
    <div className="admin-card p-5 mb-8">
      <div className="flex items-center justify-between gap-4 flex-wrap mb-5">
        <div>
          <h2 className="font-display text-xl font-extrabold">{t("recruit.metadata.title")}</h2>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("recruit.metadata.description")}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-5">
        {GROUPS.map((groupKey) => {
          const groupItems = itemsByGroup[groupKey];
          const form = forms[groupKey];
          const editingId = editingIds[groupKey];
          const isSubmitting = submittingGroup === groupKey;

          return (
            <section
              key={groupKey}
              className="rounded-3xl border p-4 flex flex-col gap-4"
              style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-card))" }}
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="font-display text-lg font-extrabold">{t(groupTitleKey[groupKey])}</h3>
                  <p className="text-xs mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("recruit.metadata.count", { count: groupItems.length })}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => startCreate(groupKey)}
                  className="inline-flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-bold border"
                  style={{
                    borderColor: "hsl(var(--admin-border))",
                    background: "hsl(var(--admin-bg))",
                    color: "hsl(var(--admin-primary))",
                  }}
                >
                  <Plus className="w-3.5 h-3.5" /> {t("recruit.metadata.add")}
                </button>
              </div>

              <form onSubmit={(event) => saveGroup(groupKey, event)} className="grid grid-cols-1 gap-3">
                <label className="block">
                  <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("recruit.metadata.fields.value")}
                  </span>
                  <input
                    className="admin-input"
                    value={form.value}
                    onChange={(event) => updateForm(groupKey, "value", event.target.value)}
                    placeholder={t("recruit.metadata.placeholders.value")}
                  />
                </label>

                <label className="block">
                  <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("recruit.metadata.fields.label")}
                  </span>
                  <input
                    className="admin-input"
                    value={form.label}
                    onChange={(event) => updateForm(groupKey, "label", event.target.value)}
                    placeholder={t("recruit.metadata.placeholders.label")}
                  />
                </label>

                <label className="block">
                  <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("recruit.metadata.fields.translationKey")}
                  </span>
                  <input
                    className="admin-input"
                    value={form.translationKey}
                    onChange={(event) => updateForm(groupKey, "translationKey", event.target.value)}
                    placeholder={t("recruit.metadata.placeholders.translationKey")}
                  />
                </label>

                <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-3">
                  <label className="block">
                    <span className="text-xs font-bold uppercase tracking-wider mb-1.5 block" style={{ color: "hsl(var(--admin-muted))" }}>
                      {t("recruit.metadata.fields.sortOrder")}
                    </span>
                    <input
                      type="number"
                      min="0"
                      className="admin-input"
                      value={form.sortOrder}
                      onChange={(event) => updateForm(groupKey, "sortOrder", Number(event.target.value))}
                    />
                  </label>

                  <label className="inline-flex items-center gap-2 px-3 rounded-xl border self-end h-11" style={{ borderColor: "hsl(var(--admin-border))" }}>
                    <input
                      type="checkbox"
                      checked={form.isActive}
                      onChange={(event) => updateForm(groupKey, "isActive", event.target.checked)}
                    />
                    <span className="text-sm font-semibold">{t("recruit.metadata.fields.isActive")}</span>
                  </label>
                </div>

                <div className="flex items-center gap-2">
                  <button type="submit" className="admin-btn-primary" disabled={isSubmitting}>
                    {editingId == null ? t("form.create") : t("form.update")}
                  </button>
                  {editingId != null && (
                    <button
                      type="button"
                      className="admin-btn-primary opacity-70"
                      onClick={() => resetGroupForm(groupKey)}
                    >
                      {t("common.cancel")}
                    </button>
                  )}
                </div>
              </form>

              <div className="rounded-2xl border overflow-hidden" style={{ borderColor: "hsl(var(--admin-border))" }}>
                <table className="w-full text-sm">
                  <thead style={{ background: "hsl(var(--admin-bg))" }}>
                    <tr className="text-left">
                      <th className="px-4 py-3 font-semibold">{t("recruit.metadata.table.label")}</th>
                      <th className="px-4 py-3 font-semibold">{t("recruit.metadata.table.value")}</th>
                      <th className="px-4 py-3 font-semibold">{t("common.status")}</th>
                      <th className="px-4 py-3 font-semibold text-right">{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {loading ? (
                      <tr>
                        <td colSpan={4} className="px-4 py-8 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                          {t("common.loading")}
                        </td>
                      </tr>
                    ) : groupItems.length === 0 ? (
                      <tr>
                        <td colSpan={4} className="px-4 py-8 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                          {t("recruit.metadata.empty")}
                        </td>
                      </tr>
                    ) : (
                      groupItems.map((item) => (
                        <tr key={item.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                          <td className="px-4 py-3">
                            <div className="font-semibold">{item.label}</div>
                            {item.translationKey && (
                              <div className="text-xs mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
                                {item.translationKey}
                              </div>
                            )}
                          </td>
                          <td className="px-4 py-3 font-mono text-xs">{item.value}</td>
                          <td className="px-4 py-3">
                            {item.isActive ? (
                              <Check className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />
                            ) : (
                              <X className="w-4 h-4" style={{ color: "hsl(var(--admin-danger))" }} />
                            )}
                          </td>
                          <td className="px-4 py-3">
                            <div className="flex justify-end items-center gap-1.5">
                              <button
                                type="button"
                                onClick={() => startEdit(groupKey, item)}
                                className="inline-flex items-center justify-center w-9 h-9 rounded-lg border transition-colors"
                                style={{
                                  borderColor: "hsl(var(--admin-border))",
                                  background: "hsl(var(--admin-bg))",
                                  color: "hsl(var(--admin-primary))",
                                }}
                                title={t("common.edit")}
                              >
                                <Pencil className="w-3.5 h-3.5" />
                              </button>
                              <button
                                type="button"
                                onClick={() => removeItem(item)}
                                className="inline-flex items-center justify-center w-9 h-9 rounded-lg border transition-colors"
                                style={{
                                  borderColor: "hsl(var(--admin-border))",
                                  background: "hsl(var(--admin-danger-soft))",
                                  color: "hsl(var(--admin-danger))",
                                }}
                                title={t("common.delete")}
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
};

export default RecruitmentMetadataManager;
