import { useEffect, useMemo, useState } from "react";
import { Mail, Phone, CheckCircle2, Clock, Trash2, Send, Search } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { useContacts } from "@/hooks/useContentApi";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { adminApi, type ContactMessageResponse } from "@/services/adminApi";
import { PageLoading, PageError } from "@/components/PageState";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { matchesSearch } from "@/lib/utils";

const AdminContacts = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { data: list, loading, error, refetch } = useContacts();
  const [active, setActive] = useState<ContactMessageResponse | null>(null);
  const [replyText, setReplyText] = useState("");
  const [sending, setSending] = useState(false);
  const [q, setQ] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "new" | "replied">("all");

  const safeList = useMemo(() => list ?? [], [list]);
  const newCount = useMemo(() => safeList.filter((c) => !c.isReplied).length, [safeList]);
  const filteredList = useMemo(() => {
    return safeList.filter((c) => {
      if (statusFilter === "new" && c.isReplied) return false;
      if (statusFilter === "replied" && !c.isReplied) return false;
      if (!q.trim()) return true;
      return (
        matchesSearch(c.name, q) ||
        matchesSearch(c.email, q) ||
        matchesSearch(c.subject, q) ||
        matchesSearch(c.phone, q) ||
        matchesSearch(c.message, q)
      );
    });
  }, [safeList, statusFilter, q]);

  const visibleIds = useMemo(() => filteredList.map((c) => c.id), [filteredList]);
  const {
    selectedIds,
    bulkDeleting,
    allVisibleSelected,
    someVisibleSelected,
    toggleAllVisible,
    toggleOne,
    clearSelection,
    handleBulkDelete,
  } = useBulkSelection<number>({
    visibleIds,
    deleteOne: (id) => adminApi.deleteContact(id),
    onAfter: async ({ success }) => {
      if (success > 0 && active && selectedIds.has(active.id)) setActive(null);
      refetch();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [statusFilter, q, clearSelection]);

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={refetch} /></AdminLayout>;
  if (!list) return null;

  const handleExport = () => {
    downloadCsv({
      filename: createCsvFilename("admin-contacts"),
      columns: [
        { header: "ID", value: "id" },
        { header: "Name", value: "name" },
        { header: "Email", value: "email" },
        { header: "Phone", value: (row) => row.phone ?? "" },
        { header: "Subject", value: "subject" },
        { header: t("contacts.content"), value: "message" },
        {
          header: t("common.status"),
          value: (row) => (row.isReplied ? t("contacts.replied") : t("contacts.new")),
        },
        { header: t("log.createdOn"), value: "createdAt" },
        { header: t("contacts.replyContent"), value: (row) => row.replyContent ?? "" },
        { header: "Replied at", value: (row) => row.repliedAt ?? "" },
      ],
      rows: list,
    });
  };

  const handleReply = async () => {
    if (!active || !replyText.trim()) return;
    setSending(true);
    try {
      const { data } = await adminApi.replyContact(active.id, replyText.trim());
      setActive(data);
      setReplyText("");
      refetch();
      toast({ title: t("contacts.reply"), description: t("contacts.replied") });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setSending(false);
    }
  };

  const handleMarkReplied = async (id: number) => {
    try {
      const { data } = await adminApi.markContactReplied(id);
      setActive(data);
      refetch();
      toast({ title: t("contacts.markReplied") });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await adminApi.deleteContact(id);
      if (active?.id === id) setActive(null);
      refetch();
      toast({ title: t("common.deleted") });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  const formatDate = (iso: string) => {
    const d = new Date(iso);
    const now = new Date();
    const diffMs = now.getTime() - d.getTime();
    const diffH = Math.floor(diffMs / 3600000);
    if (diffH < 1) return t("contacts.justNow") || "Vừa xong";
    if (diffH < 24) return `${diffH} ${t("contacts.hoursAgo") || "giờ trước"}`;
    const diffD = Math.floor(diffH / 24);
    if (diffD < 7) return `${diffD} ${t("contacts.daysAgo") || "ngày trước"}`;
    return d.toLocaleDateString("vi-VN");
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("contacts.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {newCount} {t("contacts.new")} · {list.length} {t("common.showing")}
            </p>
          </div>
          <AdminExportButton onClick={handleExport} disabled={list.length === 0} />
        </header>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
          {/* ---------- Inbox list ---------- */}
          <aside className="flex flex-col rounded-lg border bg-card lg:col-span-5 lg:max-h-[calc(100vh-200px)] xl:col-span-4">
            <div className="sticky top-0 z-10 space-y-3 rounded-t-lg border-b bg-card p-3">
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={q}
                  onChange={(e) => setQ(e.target.value)}
                  placeholder={t("contacts.searchPlaceholder")}
                  className="h-9 pl-9"
                  aria-label={t("common.search")}
                />
              </div>
              <div className="flex gap-1 rounded-md bg-muted p-1">
                {([
                  { id: "all" as const, label: t("common.all") },
                  { id: "new" as const, label: t("contacts.new") },
                  { id: "replied" as const, label: t("contacts.replied") },
                ]).map((opt) => (
                  <button
                    key={opt.id}
                    type="button"
                    onClick={() => setStatusFilter(opt.id)}
                    className={cn(
                      "flex-1 rounded px-2 py-1 text-xs font-medium transition",
                      statusFilter === opt.id
                        ? "bg-background text-foreground shadow-sm"
                        : "text-muted-foreground hover:text-foreground",
                    )}
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
              <div className="flex items-center justify-between text-xs text-muted-foreground">
                <div className="flex items-center gap-2">
                  <Checkbox
                    checked={
                      allVisibleSelected
                        ? true
                        : someVisibleSelected
                          ? "indeterminate"
                          : false
                    }
                    onCheckedChange={(v) => toggleAllVisible(v === true)}
                    aria-label={t("common.selectAll")}
                  />
                  <span>{t("contacts.inbox")} ({filteredList.length}/{list.length})</span>
                </div>
              </div>
              <BulkActionBar
                selectedCount={selectedIds.size}
                bulkDeleting={bulkDeleting}
                onClear={clearSelection}
                onBulkDelete={() => void handleBulkDelete()}
              />
            </div>
            <div className="flex-1 overflow-y-auto p-2">
              {filteredList.length === 0 ? (
                <div className="flex flex-col items-center gap-2 px-3 py-10 text-center text-sm text-muted-foreground">
                  <div className="rounded-full bg-muted p-3">
                    <Mail className="h-5 w-5" aria-hidden />
                  </div>
                  <p>{t("contacts.empty") || "Không có tin nhắn nào"}</p>
                </div>
              ) : (
                filteredList.map((c) => (
                  <div key={c.id} className="flex items-start gap-2">
                    <div className="pt-4 pl-1" onClick={(e) => e.stopPropagation()}>
                      <Checkbox
                        checked={selectedIds.has(c.id)}
                        onCheckedChange={(v) => toggleOne(c.id, v === true)}
                        aria-label={`${t("common.selectAll")} · ${c.name}`}
                      />
                    </div>
                    <button
                      type="button"
                      onClick={() => { setActive(c); setReplyText(""); }}
                      className={cn(
                        "flex flex-1 gap-3 rounded-md p-3 text-left transition hover:bg-muted/60",
                        active?.id === c.id && "bg-primary/5 hover:bg-primary/10",
                      )}
                    >
                      <div
                        className={cn(
                          "flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-sm font-semibold",
                          c.isReplied
                            ? "bg-muted text-muted-foreground"
                            : "bg-primary/10 text-primary",
                        )}
                      >
                        {c.name[0]}
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center justify-between gap-2">
                          <p className="truncate text-sm font-semibold">{c.name}</p>
                          {!c.isReplied && (
                            <span className="h-2 w-2 shrink-0 rounded-full bg-sky-500" aria-label={t("contacts.new")} />
                          )}
                        </div>
                        <p className="mt-0.5 truncate text-xs font-medium">{c.subject}</p>
                        <p className="mt-1 flex items-center gap-1 text-xs text-muted-foreground">
                          <Clock className="h-3 w-3" /> {formatDate(c.createdAt)}
                        </p>
                      </div>
                    </button>
                  </div>
                ))
              )}
            </div>
          </aside>

          {/* ---------- Detail pane ---------- */}
          <section className="rounded-lg border bg-card p-6 lg:col-span-7 xl:col-span-8">
            {!active ? (
              <div className="flex flex-col items-center gap-3 py-20 text-center text-sm text-muted-foreground">
                <div className="rounded-full bg-muted p-3">
                  <Mail className="h-5 w-5" aria-hidden />
                </div>
                <p>{t("contacts.selectOne")}</p>
              </div>
            ) : (
              <>
                <div className="mb-6 flex flex-wrap items-start justify-between gap-4 border-b pb-6">
                  <div className="flex gap-4">
                    <div className="flex h-14 w-14 items-center justify-center rounded-full bg-primary/10 text-lg font-semibold text-primary">
                      {active.name[0]}
                    </div>
                    <div>
                      <h2 className="text-xl font-semibold">{active.name}</h2>
                      <div className="mt-2 flex flex-wrap gap-3 text-xs text-muted-foreground">
                        <span className="flex items-center gap-1"><Mail className="h-3 w-3" /> {active.email}</span>
                        {active.phone && <span className="flex items-center gap-1"><Phone className="h-3 w-3" /> {active.phone}</span>}
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {active.isReplied ? (
                      <Badge variant="outline" className="gap-1.5 whitespace-nowrap border-green-300 bg-green-100 font-medium text-green-800">
                        <CheckCircle2 className="h-3 w-3" /> {t("contacts.replied")}
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="gap-1.5 whitespace-nowrap border-sky-200 bg-sky-50 font-medium text-sky-700">
                        <span className="h-1.5 w-1.5 rounded-full bg-sky-500" />
                        {t("contacts.new")}
                      </Badge>
                    )}
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => handleDelete(active.id)}
                      title={t("common.delete")}
                      aria-label={t("common.delete")}
                      className="text-destructive hover:text-destructive"
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>

                <h3 className="mb-3 text-lg font-semibold">{active.subject}</h3>
                <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {t("contacts.content")}
                </p>
                <p className="mb-6 whitespace-pre-wrap rounded-md bg-muted p-4 leading-relaxed">
                  {active.message}
                </p>

                {active.replyContent && (
                  <div className="mb-6">
                    <p className="mb-2 text-xs font-medium uppercase tracking-wide text-green-700">
                      {t("contacts.replyContent") || "Nội dung phản hồi"}
                    </p>
                    <p className="whitespace-pre-wrap rounded-md border border-green-200 bg-green-50 p-4 leading-relaxed text-green-900">
                      {active.replyContent}
                    </p>
                  </div>
                )}

                {!active.isReplied && (
                  <>
                    <Textarea
                      rows={4}
                      value={replyText}
                      onChange={(e) => setReplyText(e.target.value)}
                      placeholder={t("contacts.reply") + "..."}
                      className="resize-none"
                    />
                    <div className="mt-4 flex flex-wrap gap-2">
                      <Button onClick={handleReply} disabled={sending || !replyText.trim()}>
                        <Send className="mr-1.5 h-4 w-4" /> {sending ? "..." : t("contacts.reply")}
                      </Button>
                      <Button variant="outline" onClick={() => handleMarkReplied(active.id)}>
                        <CheckCircle2 className="mr-1.5 h-4 w-4" /> {t("contacts.markReplied")}
                      </Button>
                    </div>
                  </>
                )}
              </>
            )}
          </section>
        </div>
      </div>
    </AdminLayout>
  );
};

export default AdminContacts;
