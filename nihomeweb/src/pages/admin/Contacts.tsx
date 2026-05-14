import { useState } from "react";
import { Mail, Phone, CheckCircle2, Clock, Trash2, Send } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useContacts } from "@/hooks/useContentApi";
import { adminApi, type ContactMessageResponse } from "@/services/adminApi";
import { PageLoading, PageError } from "@/components/PageState";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";

const AdminContacts = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { data: list, loading, error, refetch } = useContacts();
  const [active, setActive] = useState<ContactMessageResponse | null>(null);
  const [replyText, setReplyText] = useState("");
  const [sending, setSending] = useState(false);

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={refetch} /></AdminLayout>;
  if (!list) return null;

  const newCount = list.filter((c) => !c.isReplied).length;

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
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-7">
        <div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("contacts.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {newCount} {t("contacts.new")} · {list.length} {t("common.showing")}
          </p>
        </div>
        <AdminExportButton onClick={handleExport} disabled={list.length === 0} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
        {/* List */}
        <div className="admin-card lg:col-span-5 xl:col-span-4 p-3 max-h-[calc(100vh-240px)] overflow-y-auto">
          <p className="px-3 py-2 text-[10px] uppercase tracking-wider font-bold" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("contacts.inbox")} ({list.length})
          </p>
          {list.length === 0 && (
            <p className="px-3 py-8 text-center text-sm" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("contacts.empty") || "Không có tin nhắn nào"}
            </p>
          )}
          {list.map((c) => (
            <button
              key={c.id}
              onClick={() => { setActive(c); setReplyText(""); }}
              className="w-full text-left p-4 rounded-2xl transition flex gap-3 mb-1"
              style={
                active?.id === c.id
                  ? { background: "hsl(var(--admin-primary-soft))" }
                  : { background: "transparent" }
              }
            >
              <div
                className="w-10 h-10 rounded-full flex items-center justify-center font-bold text-sm shrink-0 text-white"
                style={{
                  background: c.isReplied
                    ? "hsl(var(--admin-muted))"
                    : "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))",
                }}
              >
                {c.name[0]}
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-bold text-sm truncate">{c.name}</p>
                  {!c.isReplied && (
                    <span className="w-2 h-2 rounded-full shrink-0" style={{ background: "hsl(var(--admin-danger))" }} />
                  )}
                </div>
                <p className="text-xs font-semibold truncate mt-0.5">{c.subject}</p>
                <p className="text-xs mt-1 flex items-center gap-1" style={{ color: "hsl(var(--admin-muted))" }}>
                  <Clock className="w-3 h-3" /> {formatDate(c.createdAt)}
                </p>
              </div>
            </button>
          ))}
        </div>

        {/* Detail */}
        <div className="admin-card lg:col-span-7 xl:col-span-8 p-7">
          {!active ? (
            <p className="text-center py-20" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("contacts.selectOne")}
            </p>
          ) : (
            <>
              <div className="flex items-start justify-between gap-4 mb-6 pb-6 border-b" style={{ borderColor: "hsl(var(--admin-border))" }}>
                <div className="flex gap-4">
                  <div
                    className="w-14 h-14 rounded-full text-white flex items-center justify-center text-lg font-extrabold"
                    style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))" }}
                  >
                    {active.name[0]}
                  </div>
                  <div>
                    <h2 className="font-display text-xl font-extrabold">{active.name}</h2>
                    <div className="flex flex-wrap gap-3 text-xs mt-2" style={{ color: "hsl(var(--admin-muted))" }}>
                      <span className="flex items-center gap-1"><Mail className="w-3 h-3" /> {active.email}</span>
                      {active.phone && <span className="flex items-center gap-1"><Phone className="w-3 h-3" /> {active.phone}</span>}
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {active.isReplied ? (
                    <span
                      className="admin-chip"
                      style={{ background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }}
                    >
                      <CheckCircle2 className="w-3 h-3" /> {t("contacts.replied")}
                    </span>
                  ) : (
                    <span
                      className="admin-chip"
                      style={{ background: "hsl(var(--admin-danger-soft))", color: "hsl(var(--admin-danger))" }}
                    >
                      {t("contacts.new")}
                    </span>
                  )}
                  <button
                    onClick={() => handleDelete(active.id)}
                    className="p-2 rounded-lg hover:bg-red-50 text-red-500 transition"
                    title={t("common.delete")}
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>

              <h3 className="font-display text-lg font-extrabold mb-3">{active.subject}</h3>
              <p className="text-xs uppercase tracking-wider font-bold mb-2" style={{ color: "hsl(var(--admin-muted))" }}>
                {t("contacts.content")}
              </p>
              <p
                className="leading-relaxed p-5 rounded-2xl mb-6"
                style={{ background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-text))" }}
              >
                {active.message}
              </p>

              {active.replyContent && (
                <div className="mb-6">
                  <p className="text-xs uppercase tracking-wider font-bold mb-2" style={{ color: "hsl(var(--admin-success))" }}>
                    {t("contacts.replyContent") || "Nội dung phản hồi"}
                  </p>
                  <p
                    className="leading-relaxed p-5 rounded-2xl"
                    style={{ background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-text))" }}
                  >
                    {active.replyContent}
                  </p>
                </div>
              )}

              {!active.isReplied && (
                <>
                  <textarea
                    rows={4}
                    value={replyText}
                    onChange={(e) => setReplyText(e.target.value)}
                    placeholder={t("contacts.reply") + "..."}
                    className="w-full rounded-2xl p-4 text-sm border outline-none resize-none focus:border-primary"
                    style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
                  />
                  <div className="flex gap-3 mt-4">
                    <button
                      onClick={handleReply}
                      disabled={sending || !replyText.trim()}
                      className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm disabled:opacity-50"
                    >
                      <Send className="w-4 h-4" /> {sending ? "..." : t("contacts.reply")}
                    </button>
                    <button
                      onClick={() => handleMarkReplied(active.id)}
                      className="inline-flex items-center gap-2 px-5 py-2.5 text-sm font-bold rounded-xl border"
                      style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-success))" }}
                    >
                      <CheckCircle2 className="w-4 h-4" /> {t("contacts.markReplied")}
                    </button>
                  </div>
                </>
              )}
            </>
          )}
        </div>
      </div>
    </AdminLayout>
  );
};

export default AdminContacts;
