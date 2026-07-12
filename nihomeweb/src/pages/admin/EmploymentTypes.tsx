import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Search as SearchIcon, Pencil, Trash2, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, type EmploymentTypeResponse, type RecruitmentDropdownOptionResponse } from "@/services/adminApi";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Checkbox } from "@/components/ui/checkbox";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";

type Tab = "employment" | "experience" | "benefit";

type EmpTypeFormData = { code: string; name: string; isActive: boolean; sortOrder: number };
type DropdownFormData = { code: string; name: string; isActive: boolean; sortOrder: number };

const emptyEmp: EmpTypeFormData = { code: "", name: "", isActive: true, sortOrder: 0 };
const emptyDrop: DropdownFormData = { code: "", name: "", isActive: true, sortOrder: 0 };

function getErrorMessage(error: unknown) {
  if (
    typeof error === "object" && error !== null &&
    "response" in error &&
    typeof (error as { response?: { data?: { detail?: string; message?: string } } }).response === "object"
  ) {
    const data = (error as { response: { data?: { detail?: string; message?: string } } }).response?.data;
    return data?.detail ?? data?.message;
  }
  return undefined;
}

const EmploymentTypes = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [activeTab, setActiveTab] = useState<Tab>("employment");

  // --- Employment types state ---
  const [empItems, setEmpItems] = useState<EmploymentTypeResponse[]>([]);
  const [empQ, setEmpQ] = useState("");
  const [empLoading, setEmpLoading] = useState(true);
  const [empSubmitting, setEmpSubmitting] = useState(false);
  const [empEditingId, setEmpEditingId] = useState<number | null>(null);
  const [empForm, setEmpForm] = useState<EmpTypeFormData>(emptyEmp);

  // --- Dropdown options state (experience-level + benefit) ---
  const [dropItems, setDropItems] = useState<RecruitmentDropdownOptionResponse[]>([]);
  const [dropQ, setDropQ] = useState("");
  const [dropLoading, setDropLoading] = useState(false);
  const [dropSubmitting, setDropSubmitting] = useState(false);
  const [dropEditingId, setDropEditingId] = useState<number | null>(null);
  const [dropForm, setDropForm] = useState<DropdownFormData>(emptyDrop);

  const dropType = activeTab === "experience" ? "experience-level" : "benefit";

  // --- Loaders ---
  const loadEmp = useCallback(async () => {
    setEmpLoading(true);
    try {
      const res = await adminApi.getEmploymentTypes(true);
      setEmpItems(res.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setEmpLoading(false);
    }
  }, [t, toast]);

  const loadDrop = useCallback(async (type: string) => {
    setDropLoading(true);
    try {
      const res = await adminApi.getRecruitmentDropdownOptions(type, true);
      setDropItems(res.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setDropLoading(false);
    }
  }, [t, toast]);

  useEffect(() => { loadEmp(); }, [loadEmp]);

  useEffect(() => {
    if (activeTab !== "employment") {
      loadDrop(activeTab === "experience" ? "experience-level" : "benefit");
      setDropEditingId(null);
      setDropForm(emptyDrop);
      setDropQ("");
    }
  }, [activeTab, loadDrop]);

  // --- Filtered lists ---
  const filteredEmp = useMemo(() => {
    const q = empQ.trim().toLowerCase();
    return q ? empItems.filter((i) => i.name.toLowerCase().includes(q) || i.code.toLowerCase().includes(q)) : empItems;
  }, [empItems, empQ]);

  const filteredDrop = useMemo(() => {
    const q = dropQ.trim().toLowerCase();
    return q ? dropItems.filter((i) => i.name.toLowerCase().includes(q) || i.code.toLowerCase().includes(q)) : dropItems;
  }, [dropItems, dropQ]);

  // --- Employment type CRUD ---
  const startEmpCreate = () => { setEmpEditingId(null); setEmpForm({ ...emptyEmp, sortOrder: empItems.length + 1 }); };
  const startEmpEdit = (item: EmploymentTypeResponse) => {
    setEmpEditingId(item.id);
    setEmpForm({ code: item.code, name: item.name, isActive: item.isActive, sortOrder: item.sortOrder });
  };
  const submitEmp = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!empForm.code.trim() || !empForm.name.trim()) {
      toast({ title: t("form.required"), description: t("empTypes.codeNameRequired"), variant: "destructive" });
      return;
    }
    setEmpSubmitting(true);
    try {
      const payload = { code: empForm.code.trim(), name: empForm.name.trim(), isActive: empForm.isActive, sortOrder: empForm.sortOrder || 0 };
      if (empEditingId == null) { await adminApi.createEmploymentType(payload); toast({ title: t("form.created") }); }
      else { await adminApi.updateEmploymentType(empEditingId, payload); toast({ title: t("form.updated") }); }
      setEmpEditingId(null); setEmpForm(emptyEmp); await loadEmp();
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    } finally { setEmpSubmitting(false); }
  };
  const removeEmp = async (item: EmploymentTypeResponse) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteEmploymentType(item.id);
      setEmpItems((prev) => prev.filter((i) => i.id !== item.id));
      toast({ title: t("form.deleted") });
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    }
  };

  // --- Dropdown option CRUD ---
  const startDropCreate = () => { setDropEditingId(null); setDropForm({ ...emptyDrop, sortOrder: dropItems.length + 1 }); };
  const startDropEdit = (item: RecruitmentDropdownOptionResponse) => {
    setDropEditingId(item.id);
    setDropForm({ code: item.code, name: item.name, isActive: item.isActive, sortOrder: item.sortOrder });
  };
  const submitDrop = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!dropForm.code.trim() || !dropForm.name.trim()) {
      toast({ title: t("form.required"), description: t("empTypes.codeNameRequired"), variant: "destructive" });
      return;
    }
    setDropSubmitting(true);
    try {
      const payload = { type: dropType, code: dropForm.code.trim(), name: dropForm.name.trim(), isActive: dropForm.isActive, sortOrder: dropForm.sortOrder || 0 };
      if (dropEditingId == null) { await adminApi.createRecruitmentDropdownOption(payload); toast({ title: t("form.created") }); }
      else { await adminApi.updateRecruitmentDropdownOption(dropEditingId, payload); toast({ title: t("form.updated") }); }
      setDropEditingId(null); setDropForm(emptyDrop); await loadDrop(dropType);
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    } finally { setDropSubmitting(false); }
  };
  const removeDrop = async (item: RecruitmentDropdownOptionResponse) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteRecruitmentDropdownOption(item.id);
      setDropItems((prev) => prev.filter((i) => i.id !== item.id));
      toast({ title: t("form.deleted") });
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    }
  };

  // --- Export ---
  const handleExport = () => {
    if (activeTab === "employment") {
      downloadCsv({ filename: createCsvFilename("employment-types"), columns: [
        { header: "ID", value: "id" }, { header: t("empTypes.code"), value: "code" },
        { header: t("empTypes.displayName"), value: "name" },
        { header: t("empTypes.active"), value: (r: EmploymentTypeResponse) => r.isActive ? "Yes" : "No" },
        { header: t("empTypes.sortOrder"), value: "sortOrder" },
      ], rows: filteredEmp });
    } else {
      downloadCsv({ filename: createCsvFilename(`recruitment-${dropType}`), columns: [
        { header: "ID", value: "id" }, { header: t("empTypes.code"), value: "code" },
        { header: t("empTypes.displayName"), value: "name" },
        { header: t("empTypes.active"), value: (r: RecruitmentDropdownOptionResponse) => r.isActive ? "Yes" : "No" },
        { header: t("empTypes.sortOrder"), value: "sortOrder" },
      ], rows: filteredDrop });
    }
  };

  const tabs: { key: Tab; label: string }[] = [
    { key: "employment", label: t("empTypes.employmentTypes") },
    { key: "experience", label: t("empTypes.experienceRequired") },
    { key: "benefit", label: t("empTypes.benefits") },
  ];

  const addLabel = activeTab === "employment" ? t("empTypes.addType") : activeTab === "experience" ? t("empTypes.addExpLevel") : t("empTypes.addBenefit");
  const isEmp = activeTab === "employment";
  const loading = isEmp ? empLoading : dropLoading;
  const filtered = isEmp ? filteredEmp : filteredDrop;
  const total = isEmp ? empItems.length : dropItems.length;
  const q = isEmp ? empQ : dropQ;
  const setQ = isEmp ? setEmpQ : setDropQ;

  // Bulk selection for employment types
  const empVisibleIds = useMemo(() => filteredEmp.map((i) => i.id), [filteredEmp]);
  const {
    selectedIds: empSelectedIds,
    bulkDeleting: empBulkDeleting,
    allVisibleSelected: empAllSelected,
    someVisibleSelected: empSomeSelected,
    toggleAllVisible: empToggleAll,
    toggleOne: empToggleOne,
    clearSelection: empClearSelection,
    handleBulkDelete: empBulkDelete,
  } = useBulkSelection<number>({
    visibleIds: empVisibleIds,
    deleteOne: (id) => adminApi.deleteEmploymentType(id),
    onAfter: async () => { await loadEmp(); },
  });
  useEffect(() => {
    empClearSelection();
  }, [empQ, empClearSelection]);

  // Bulk selection for dropdown options
  const dropVisibleIds = useMemo(() => filteredDrop.map((i) => i.id), [filteredDrop]);
  const {
    selectedIds: dropSelectedIds,
    bulkDeleting: dropBulkDeleting,
    allVisibleSelected: dropAllSelected,
    someVisibleSelected: dropSomeSelected,
    toggleAllVisible: dropToggleAll,
    toggleOne: dropToggleOne,
    clearSelection: dropClearSelection,
    handleBulkDelete: dropBulkDelete,
  } = useBulkSelection<number>({
    visibleIds: dropVisibleIds,
    deleteOne: (id) => adminApi.deleteRecruitmentDropdownOption(id),
    onAfter: async () => { await loadDrop(dropType); },
  });
  useEffect(() => {
    dropClearSelection();
  }, [dropQ, activeTab, dropClearSelection]);

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("empTypes.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {loading ? "..." : `${filtered.length} / ${total}`}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <AdminExportButton onClick={handleExport} disabled={loading || filtered.length === 0} />
          <button
            onClick={isEmp ? startEmpCreate : startDropCreate}
            className="admin-btn-primary inline-flex items-center gap-2"
            type="button"
          >
            <Plus className="w-4 h-4" /> {addLabel}
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 mb-5 border-b" style={{ borderColor: "hsl(var(--admin-border))" }}>
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => setActiveTab(tab.key)}
            className={[
              "px-4 py-2.5 text-sm font-semibold border-b-2 transition -mb-px",
              activeTab === tab.key
                ? "border-primary text-primary"
                : "border-transparent text-muted-foreground hover:text-foreground",
            ].join(" ")}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Form */}
      <div className="admin-card p-5 mb-5">
        {isEmp ? (
          <form onSubmit={submitEmp} className="grid grid-cols-1 lg:grid-cols-5 gap-3 mb-4">
            <input value={empForm.code} onChange={(e) => setEmpForm((p) => ({ ...p, code: e.target.value }))} placeholder={t("empTypes.codePlaceholder")} className="admin-input" required />
            <input value={empForm.name} onChange={(e) => setEmpForm((p) => ({ ...p, name: e.target.value }))} placeholder={t("empTypes.namePlaceholder")} className="admin-input" required />
            <input type="number" value={empForm.sortOrder} onChange={(e) => setEmpForm((p) => ({ ...p, sortOrder: Number(e.target.value) }))} placeholder={t("empTypes.sortOrder")} className="admin-input" />
            <label className="inline-flex items-center gap-2 px-3 rounded-xl border" style={{ borderColor: "hsl(var(--admin-border))" }}>
              <input type="checkbox" checked={empForm.isActive} onChange={(e) => setEmpForm((p) => ({ ...p, isActive: e.target.checked }))} />
              <span className="text-sm font-semibold">{t("empTypes.active")}</span>
            </label>
            <div className="flex items-center gap-2">
              <button type="submit" className="admin-btn-primary" disabled={empSubmitting}>{empEditingId == null ? t("form.create") : t("form.update")}</button>
              {empEditingId != null && (
                <button type="button" className="admin-btn-primary opacity-70" onClick={() => { setEmpEditingId(null); setEmpForm(emptyEmp); }}>{t("common.cancel")}</button>
              )}
            </div>
          </form>
        ) : (
          <form onSubmit={submitDrop} className="grid grid-cols-1 lg:grid-cols-5 gap-3 mb-4">
            <input value={dropForm.code} onChange={(e) => setDropForm((p) => ({ ...p, code: e.target.value }))} placeholder={t("empTypes.codePlaceholder")} className="admin-input" required />
            <input value={dropForm.name} onChange={(e) => setDropForm((p) => ({ ...p, name: e.target.value }))} placeholder={t("empTypes.namePlaceholder")} className="admin-input" required />
            <input type="number" value={dropForm.sortOrder} onChange={(e) => setDropForm((p) => ({ ...p, sortOrder: Number(e.target.value) }))} placeholder={t("empTypes.sortOrder")} className="admin-input" />
            <label className="inline-flex items-center gap-2 px-3 rounded-xl border" style={{ borderColor: "hsl(var(--admin-border))" }}>
              <input type="checkbox" checked={dropForm.isActive} onChange={(e) => setDropForm((p) => ({ ...p, isActive: e.target.checked }))} />
              <span className="text-sm font-semibold">{t("empTypes.active")}</span>
            </label>
            <div className="flex items-center gap-2">
              <button type="submit" className="admin-btn-primary" disabled={dropSubmitting}>{dropEditingId == null ? t("form.create") : t("form.update")}</button>
              {dropEditingId != null && (
                <button type="button" className="admin-btn-primary opacity-70" onClick={() => { setDropEditingId(null); setDropForm(emptyDrop); }}>{t("common.cancel")}</button>
              )}
            </div>
          </form>
        )}

        <div className="flex items-center gap-2 max-w-md">
          <SearchIcon className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t("empTypes.searchPlaceholder")} className="admin-input flex-1" />
        </div>
      </div>

      {/* Table */}
      <div className="admin-card overflow-hidden">
        <BulkActionBar
          selectedCount={isEmp ? empSelectedIds.size : dropSelectedIds.size}
          bulkDeleting={isEmp ? empBulkDeleting : dropBulkDeleting}
          onClear={isEmp ? empClearSelection : dropClearSelection}
          onBulkDelete={() => void (isEmp ? empBulkDelete() : dropBulkDelete())}
        />
        <table className="w-full text-sm">
          <thead style={{ background: "hsl(var(--admin-bg))" }}>
            <tr className="text-left">
              <th className="w-10 px-3 py-2 text-left">
                <Checkbox
                  checked={
                    isEmp
                      ? empAllSelected
                        ? true
                        : empSomeSelected
                          ? "indeterminate"
                          : false
                      : dropAllSelected
                        ? true
                        : dropSomeSelected
                          ? "indeterminate"
                          : false
                  }
                  onCheckedChange={(v) => (isEmp ? empToggleAll(v === true) : dropToggleAll(v === true))}
                  aria-label={t("common.selectAll")}
                />
              </th>
              <th className="px-5 py-3 font-semibold">{t("empTypes.code")}</th>
              <th className="px-5 py-3 font-semibold">{t("empTypes.displayName")}</th>
              <th className="px-5 py-3 font-semibold">{t("empTypes.active")}</th>
              <th className="px-5 py-3 font-semibold">{t("empTypes.sortOrder")}</th>
              <th className="px-5 py-3 font-semibold text-right">{t("common.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={6} className="px-5 py-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>...</td></tr>
            ) : filtered.length === 0 ? (
              <tr><td colSpan={6} className="px-5 py-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>{t("empTypes.noData")}</td></tr>
            ) : isEmp ? (
              (filteredEmp).map((item) => (
                <tr key={item.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                    <Checkbox
                      checked={empSelectedIds.has(item.id)}
                      onCheckedChange={(v) => empToggleOne(item.id, v === true)}
                      aria-label={`${t("common.selectAll")} · ${item.name}`}
                    />
                  </td>
                  <td className="px-5 py-3 font-mono text-xs">{item.code}</td>
                  <td className="px-5 py-3 font-semibold">{item.name}</td>
                  <td className="px-5 py-3">{item.isActive ? <Check className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} /> : <X className="w-4 h-4" style={{ color: "hsl(var(--admin-danger))" }} />}</td>
                  <td className="px-5 py-3">{item.sortOrder}</td>
                  <td className="px-5 py-3 text-right">
                    <button onClick={() => startEmpEdit(item)} className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted mr-2"><Pencil className="w-3.5 h-3.5" /> {t("common.edit")}</button>
                    <button onClick={() => removeEmp(item)} className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted" style={{ color: "hsl(var(--admin-danger))" }}><Trash2 className="w-3.5 h-3.5" /> {t("common.delete")}</button>
                  </td>
                </tr>
              ))
            ) : (
              (filteredDrop).map((item) => (
                <tr key={item.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                    <Checkbox
                      checked={dropSelectedIds.has(item.id)}
                      onCheckedChange={(v) => dropToggleOne(item.id, v === true)}
                      aria-label={`${t("common.selectAll")} · ${item.name}`}
                    />
                  </td>
                  <td className="px-5 py-3 font-mono text-xs">{item.code}</td>
                  <td className="px-5 py-3 font-semibold">{item.name}</td>
                  <td className="px-5 py-3">{item.isActive ? <Check className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} /> : <X className="w-4 h-4" style={{ color: "hsl(var(--admin-danger))" }} />}</td>
                  <td className="px-5 py-3">{item.sortOrder}</td>
                  <td className="px-5 py-3 text-right">
                    <button onClick={() => startDropEdit(item)} className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted mr-2"><Pencil className="w-3.5 h-3.5" /> {t("common.edit")}</button>
                    <button onClick={() => removeDrop(item)} className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted" style={{ color: "hsl(var(--admin-danger))" }}><Trash2 className="w-3.5 h-3.5" /> {t("common.delete")}</button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </AdminLayout>
  );
};

export default EmploymentTypes;
