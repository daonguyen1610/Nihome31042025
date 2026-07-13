import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Search as SearchIcon, Pencil, Trash2, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { adminApi, type EmploymentTypeResponse, type RecruitmentDropdownOptionResponse } from "@/services/adminApi";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
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
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("empTypes.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {loading ? "..." : `${filtered.length} / ${total}`}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <AdminExportButton onClick={handleExport} disabled={loading || filtered.length === 0} />
            <Button
              type="button"
              onClick={isEmp ? startEmpCreate : startDropCreate}
            >
              <Plus className="mr-1.5 h-4 w-4" /> {addLabel}
            </Button>
          </div>
        </header>

        {/* Tabs */}
        <div className="flex gap-1 border-b">
          {tabs.map((tab) => (
            <button
              key={tab.key}
              type="button"
              onClick={() => setActiveTab(tab.key)}
              className={cn(
                "-mb-px border-b-2 px-4 py-2 text-sm font-medium transition",
                activeTab === tab.key
                  ? "border-primary text-primary"
                  : "border-transparent text-muted-foreground hover:text-foreground",
              )}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Form */}
        <section className="space-y-3 rounded-lg border bg-card p-3">
          {isEmp ? (
            <form onSubmit={submitEmp} className="grid grid-cols-1 gap-3 lg:grid-cols-5">
              <Input value={empForm.code} onChange={(e) => setEmpForm((p) => ({ ...p, code: e.target.value }))} placeholder={t("empTypes.codePlaceholder")} required />
              <Input value={empForm.name} onChange={(e) => setEmpForm((p) => ({ ...p, name: e.target.value }))} placeholder={t("empTypes.namePlaceholder")} required />
              <Input type="number" value={empForm.sortOrder} onChange={(e) => setEmpForm((p) => ({ ...p, sortOrder: Number(e.target.value) }))} placeholder={t("empTypes.sortOrder")} />
              <label className="inline-flex items-center gap-2 rounded-md border bg-background px-3">
                <Checkbox
                  checked={empForm.isActive}
                  onCheckedChange={(v) => setEmpForm((p) => ({ ...p, isActive: v === true }))}
                />
                <span className="text-sm font-medium">{t("empTypes.active")}</span>
              </label>
              <div className="flex items-center gap-2">
                <Button type="submit" disabled={empSubmitting}>
                  {empEditingId == null ? t("form.create") : t("form.update")}
                </Button>
                {empEditingId != null && (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => { setEmpEditingId(null); setEmpForm(emptyEmp); }}
                  >
                    {t("common.cancel")}
                  </Button>
                )}
              </div>
            </form>
          ) : (
            <form onSubmit={submitDrop} className="grid grid-cols-1 gap-3 lg:grid-cols-5">
              <Input value={dropForm.code} onChange={(e) => setDropForm((p) => ({ ...p, code: e.target.value }))} placeholder={t("empTypes.codePlaceholder")} required />
              <Input value={dropForm.name} onChange={(e) => setDropForm((p) => ({ ...p, name: e.target.value }))} placeholder={t("empTypes.namePlaceholder")} required />
              <Input type="number" value={dropForm.sortOrder} onChange={(e) => setDropForm((p) => ({ ...p, sortOrder: Number(e.target.value) }))} placeholder={t("empTypes.sortOrder")} />
              <label className="inline-flex items-center gap-2 rounded-md border bg-background px-3">
                <Checkbox
                  checked={dropForm.isActive}
                  onCheckedChange={(v) => setDropForm((p) => ({ ...p, isActive: v === true }))}
                />
                <span className="text-sm font-medium">{t("empTypes.active")}</span>
              </label>
              <div className="flex items-center gap-2">
                <Button type="submit" disabled={dropSubmitting}>
                  {dropEditingId == null ? t("form.create") : t("form.update")}
                </Button>
                {dropEditingId != null && (
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => { setDropEditingId(null); setDropForm(emptyDrop); }}
                  >
                    {t("common.cancel")}
                  </Button>
                )}
              </div>
            </form>
          )}

          <div className="w-full sm:max-w-sm">
            <Label className="text-xs" htmlFor="emp-search">{t("common.search")}</Label>
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="emp-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("empTypes.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>
        </section>

        {/* Table */}
        <div className="space-y-2">
          <BulkActionBar
            selectedCount={isEmp ? empSelectedIds.size : dropSelectedIds.size}
            bulkDeleting={isEmp ? empBulkDeleting : dropBulkDeleting}
            onClear={isEmp ? empClearSelection : dropClearSelection}
            onBulkDelete={() => void (isEmp ? empBulkDelete() : dropBulkDelete())}
          />
          <div className="overflow-x-auto rounded-lg border">
            <table className="min-w-[700px] w-full divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="w-10 px-3 py-3 text-left">
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
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("empTypes.code")}</th>
                  <th className="px-3 py-3 text-left font-medium">{t("empTypes.displayName")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("empTypes.active")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("empTypes.sortOrder")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {loading ? (
                  <tr><td colSpan={6} className="px-5 py-10 text-center text-muted-foreground">...</td></tr>
                ) : filtered.length === 0 ? (
                  <tr><td colSpan={6} className="px-5 py-10 text-center text-muted-foreground">{t("empTypes.noData")}</td></tr>
                ) : isEmp ? (
                  (filteredEmp).map((item) => (
                    <tr key={item.id} className="hover:bg-muted/40 transition">
                      <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={empSelectedIds.has(item.id)}
                          onCheckedChange={(v) => empToggleOne(item.id, v === true)}
                          aria-label={`${t("common.selectAll")} · ${item.name}`}
                        />
                      </td>
                      <td className="whitespace-nowrap px-3 py-3 font-mono text-xs">{item.code}</td>
                      <td className="px-3 py-3 font-medium">{item.name}</td>
                      <td className="px-3 py-3">{item.isActive ? <Check className="h-4 w-4 text-emerald-600" /> : <X className="h-4 w-4 text-muted-foreground" />}</td>
                      <td className="px-3 py-3">{item.sortOrder}</td>
                      <td className="whitespace-nowrap px-3 py-3 text-right">
                        <div className="inline-flex items-center gap-1">
                          <Button variant="ghost" size="sm" onClick={() => startEmpEdit(item)}><Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}</Button>
                          <Button variant="ghost" size="sm" onClick={() => removeEmp(item)} className="text-destructive hover:text-destructive"><Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}</Button>
                        </div>
                      </td>
                    </tr>
                  ))
                ) : (
                  (filteredDrop).map((item) => (
                    <tr key={item.id} className="hover:bg-muted/40 transition">
                      <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={dropSelectedIds.has(item.id)}
                          onCheckedChange={(v) => dropToggleOne(item.id, v === true)}
                          aria-label={`${t("common.selectAll")} · ${item.name}`}
                        />
                      </td>
                      <td className="whitespace-nowrap px-3 py-3 font-mono text-xs">{item.code}</td>
                      <td className="px-3 py-3 font-medium">{item.name}</td>
                      <td className="px-3 py-3">{item.isActive ? <Check className="h-4 w-4 text-emerald-600" /> : <X className="h-4 w-4 text-muted-foreground" />}</td>
                      <td className="px-3 py-3">{item.sortOrder}</td>
                      <td className="whitespace-nowrap px-3 py-3 text-right">
                        <div className="inline-flex items-center gap-1">
                          <Button variant="ghost" size="sm" onClick={() => startDropEdit(item)}><Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}</Button>
                          <Button variant="ghost" size="sm" onClick={() => removeDrop(item)} className="text-destructive hover:text-destructive"><Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}</Button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default EmploymentTypes;
