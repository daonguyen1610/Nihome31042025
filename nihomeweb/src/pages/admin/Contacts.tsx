import { useState } from "react";
import { Mail, Phone, Reply, CheckCircle2, Clock } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

type Contact = {
  id: string;
  name: string;
  email: string;
  phone: string;
  subject: string;
  message: string;
  date: string;
  replied: boolean;
};

const initial: Contact[] = [
  {
    id: "c1",
    name: "Trang Lê",
    email: "trang.le@trang.com.vn",
    phone: "0901 234 567",
    subject: "Tư vấn xây dựng nhà máy 8.000m²",
    message:
      "Chúng tôi đang tìm đối tác thiết kế và thi công cho dự án nhà máy mới tại KCN Long An. Mong nhận được báo giá sớm.",
    date: "2 giờ trước",
    replied: false,
  },
  {
    id: "c2",
    name: "Mr. Yamada",
    email: "yamada@morigroup.jp",
    phone: "+81 90 1234 5678",
    subject: "Hợp tác chiến lược MORI x NICON",
    message: "Đề xuất buổi gặp trực tiếp để trao đổi về hợp tác đầu tư trong quý tới.",
    date: "5 giờ trước",
    replied: false,
  },
  {
    id: "c3",
    name: "Phạm Minh Tú",
    email: "tu.pm@bma.vn",
    phone: "0912 987 654",
    subject: "Update tiến độ dự án BMA",
    message: "Anh cập nhật giúp em tiến độ thi công tuần này nhé.",
    date: "1 ngày trước",
    replied: true,
  },
  {
    id: "c4",
    name: "Nguyễn Hồng Anh",
    email: "hong.anh@email.com",
    phone: "0987 654 321",
    subject: "Thiết kế nội thất văn phòng",
    message: "Mình cần tư vấn thiết kế văn phòng 800m² tại Quận 7.",
    date: "2 ngày trước",
    replied: false,
  },
];

const AdminContacts = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [list, setList] = useState(initial);
  const [active, setActive] = useState<Contact | null>(initial[0]);

  const markReplied = (id: string) => {
    setList((l) => l.map((c) => (c.id === id ? { ...c, replied: true } : c)));
    setActive((a) => (a?.id === id ? { ...a, replied: true } : a));
    toast({ title: t("contacts.markReplied") });
  };

  const newCount = list.filter((c) => !c.replied).length;

  return (
    <AdminLayout>
      <div className="mb-7">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("contacts.title")}</h1>
        <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
          {newCount} {t("contacts.new")} · {list.length} {t("common.showing")}
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
        {/* List */}
        <div className="admin-card lg:col-span-5 xl:col-span-4 p-3 max-h-[calc(100vh-240px)] overflow-y-auto">
          <p className="px-3 py-2 text-[10px] uppercase tracking-wider font-bold" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("contacts.inbox")} ({list.length})
          </p>
          {list.map((c) => (
            <button
              key={c.id}
              onClick={() => setActive(c)}
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
                  background: c.replied
                    ? "hsl(var(--admin-muted))"
                    : "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))",
                }}
              >
                {c.name[0]}
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-bold text-sm truncate">{c.name}</p>
                  {!c.replied && (
                    <span className="w-2 h-2 rounded-full shrink-0" style={{ background: "hsl(var(--admin-danger))" }} />
                  )}
                </div>
                <p className="text-xs font-semibold truncate mt-0.5">{c.subject}</p>
                <p className="text-xs mt-1 flex items-center gap-1" style={{ color: "hsl(var(--admin-muted))" }}>
                  <Clock className="w-3 h-3" /> {c.date}
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
                      <span className="flex items-center gap-1"><Phone className="w-3 h-3" /> {active.phone}</span>
                    </div>
                  </div>
                </div>
                {active.replied ? (
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

              <textarea
                rows={4}
                placeholder={t("contacts.reply") + "..."}
                className="w-full rounded-2xl p-4 text-sm border outline-none resize-none focus:border-primary"
                style={{ background: "hsl(var(--admin-bg))", borderColor: "hsl(var(--admin-border))" }}
              />
              <div className="flex gap-3 mt-4">
                <button
                  onClick={() => toast({ title: t("contacts.reply"), description: "Demo gửi reply." })}
                  className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm"
                >
                  <Reply className="w-4 h-4" /> {t("contacts.reply")}
                </button>
                {!active.replied && (
                  <button
                    onClick={() => markReplied(active.id)}
                    className="inline-flex items-center gap-2 px-5 py-2.5 text-sm font-bold rounded-xl border"
                    style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-success))" }}
                  >
                    <CheckCircle2 className="w-4 h-4" /> {t("contacts.markReplied")}
                  </button>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </AdminLayout>
  );
};

export default AdminContacts;
