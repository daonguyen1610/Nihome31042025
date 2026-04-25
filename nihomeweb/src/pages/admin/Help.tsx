import { useMemo, useState } from "react";
import { ChevronDown, Search, BookOpen, MessageCircle, Keyboard, HelpCircle, FileText, Building2, Workflow, Mail, Phone, Clock, Send, Info } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { cn } from "@/lib/utils";

type Tab = "faq" | "guide" | "contact" | "shortcuts";

const FAQS = [
  {
    q: { vi: "Làm sao để tạo bài đăng mới?", en: "How do I create a new post?" },
    a: { vi: "Vào menu Quản lý nội dung → Bài đăng → bấm nút \"Tạo bài đăng\". Điền tiêu đề, danh mục, ảnh đại diện và nội dung rồi bấm Lưu.", en: "Open Content Management → Posts → click \"Create post\". Fill in title, category, cover image and content, then save." },
  },
  {
    q: { vi: "Tôi muốn thay đổi ngôn ngữ giao diện?", en: "How do I change the interface language?" },
    a: { vi: "Bấm vào biểu tượng địa cầu ở góc phải trên cùng và chọn một trong 4 ngôn ngữ: Tiếng Việt, English, 中文, 日本語.", en: "Click the globe icon at the top right and choose one of 4 languages: Vietnamese, English, Chinese, Japanese." },
  },
  {
    q: { vi: "Làm sao để thêm quy trình mới?", en: "How to add a new process?" },
    a: { vi: "Mở menu Quy trình quản lý công việc, chọn nhóm quy trình, bấm \"Thêm quy trình\", chọn loại nội dung (văn bản hoặc hình ảnh) và lưu.", en: "Open Work processes menu, select a group, click \"Add process\", choose content type (text or image) and save." },
  },
  {
    q: { vi: "Dữ liệu được lưu ở đâu?", en: "Where is data stored?" },
    a: { vi: "Hiện tại dữ liệu lưu trong trình duyệt (localStorage). Để chuyển sang database thật, cần kích hoạt Lovable Cloud.", en: "Currently data is stored in browser (localStorage). To use a real database, enable Lovable Cloud." },
  },
  {
    q: { vi: "Tài khoản admin demo là gì?", en: "What is the demo admin account?" },
    a: { vi: "Email admin@nicon.vn với mật khẩu bất kỳ sẽ được cấp quyền admin. Đây là cấu hình demo, không dùng cho môi trường thật.", en: "Email admin@nicon.vn with any password grants admin role. This is demo only — do not use in production." },
  },
  {
    q: { vi: "Làm sao để xoá hàng loạt email trong hàng đợi?", en: "How to bulk delete queued emails?" },
    a: { vi: "Vào Hệ thống → Hàng đợi email, tick các email cần xoá rồi bấm \"Xoá mục đã chọn\", hoặc \"Xoá tất cả\".", en: "Go to System → Message queue, tick emails to delete and click \"Delete selected\" or \"Delete all\"." },
  },
  {
    q: { vi: "Sao lưu dữ liệu hệ thống ở đâu?", en: "Where to back up system data?" },
    a: { vi: "Hệ thống → Bảo trì → mục Sao lưu CSDL → bấm \"Sao lưu ngay\" để tạo bản backup.", en: "System → Maintenance → Database backups → click \"Backup now\" to create a backup." },
  },
];

const GUIDES = [
  { icon: FileText, title: { vi: "Quản lý bài đăng", en: "Posts management" }, desc: { vi: "Tạo, chỉnh sửa, phân loại và xuất bản bài viết tin tức/hoạt động.", en: "Create, edit, categorize and publish news/activity articles." } },
  { icon: Building2, title: { vi: "Quản lý dự án", en: "Projects management" }, desc: { vi: "Thêm dự án mới, cập nhật trạng thái (đang triển khai/hoàn thành), gallery ảnh.", en: "Add new projects, update status (ongoing/completed), photo gallery." } },
  { icon: Workflow, title: { vi: "Quy trình công việc", en: "Work processes" }, desc: { vi: "Tổ chức 8 nhóm quy trình: chung, KH, đấu thầu, thiết kế, thi công...", en: "Organize 8 process groups: general, customer, bidding, design, construction..." } },
  { icon: Info, title: { vi: "Cài đặt & cấu hình", en: "Settings & config" }, desc: { vi: "Quản lý cửa hàng, ngôn ngữ, quốc gia, email, và 600+ cài đặt nâng cao.", en: "Manage stores, languages, countries, emails, and 600+ advanced settings." } },
];

const SHORTCUTS = [
  { keys: ["Ctrl", "K"], desc: { vi: "Mở thanh tìm kiếm nhanh", en: "Open quick search" } },
  { keys: ["Ctrl", "S"], desc: { vi: "Lưu thay đổi", en: "Save changes" } },
  { keys: ["Ctrl", "N"], desc: { vi: "Tạo mục mới", en: "Create new item" } },
  { keys: ["Esc"], desc: { vi: "Đóng dialog/sidebar", en: "Close dialog/sidebar" } },
  { keys: ["?"], desc: { vi: "Mở trang trợ giúp", en: "Open help page" } },
  { keys: ["G", "D"], desc: { vi: "Đi đến Dashboard", en: "Go to Dashboard" } },
  { keys: ["G", "P"], desc: { vi: "Đi đến Bài đăng", en: "Go to Posts" } },
  { keys: ["G", "S"], desc: { vi: "Đi đến Cài đặt", en: "Go to Settings" } },
];

const HelpPage = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const [tab, setTab] = useState<Tab>("faq");
  const [q, setQ] = useState("");
  const [openIdx, setOpenIdx] = useState<number | null>(0);
  const [form, setForm] = useState({ subject: "", message: "", priority: "normal" });

  const pickLang = (obj: { vi: string; en: string }) => (lang === "en" ? obj.en : obj.vi);

  const filteredFaqs = useMemo(() => {
    if (!q.trim()) return FAQS;
    const k = q.toLowerCase();
    return FAQS.filter((f) => pickLang(f.q).toLowerCase().includes(k) || pickLang(f.a).toLowerCase().includes(k));
  }, [q, lang]);

  const submitTicket = (e: React.FormEvent) => {
    e.preventDefault();
    toast({ title: t("help.contact.sent"), description: `${t("help.contact.sentDesc")} #${Math.floor(Math.random() * 90000) + 10000}` });
    setForm({ subject: "", message: "", priority: "normal" });
  };

  const tabs: { id: Tab; icon: typeof HelpCircle; label: string }[] = [
    { id: "faq", icon: HelpCircle, label: t("help.tab.faq") },
    { id: "guide", icon: BookOpen, label: t("help.tab.guide") },
    { id: "contact", icon: MessageCircle, label: t("help.tab.contact") },
    { id: "shortcuts", icon: Keyboard, label: t("help.tab.shortcuts") },
  ];

  return (
    <AdminLayout>
      <div className="mb-6">
        <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("help.title")}</h1>
        <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>{t("help.desc")}</p>
      </div>

      {/* Tabs */}
      <div className="flex flex-wrap gap-2 mb-6">
        {tabs.map((tb) => (
          <button
            key={tb.id}
            onClick={() => setTab(tb.id)}
            className={cn(
              "inline-flex items-center gap-2 px-4 py-2.5 rounded-full text-sm font-bold transition-all",
              tab === tb.id ? "text-white shadow" : "bg-muted hover:bg-muted/70",
            )}
            style={tab === tb.id ? { background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))" } : undefined}
          >
            <tb.icon className="w-4 h-4" /> {tb.label}
          </button>
        ))}
      </div>

      {/* FAQ */}
      {tab === "faq" && (
        <div className="space-y-4">
          <div className="admin-card p-2 flex items-center gap-2">
            <Search className="w-4 h-4 ml-3" style={{ color: "hsl(var(--admin-muted))" }} />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder={t("help.search.ph")}
              className="bg-transparent outline-none text-sm flex-1 py-2 placeholder:opacity-60"
            />
          </div>
          <div className="admin-card divide-y" style={{ borderColor: "hsl(var(--admin-border))" }}>
            {filteredFaqs.length === 0 ? (
              <p className="p-6 text-sm text-center" style={{ color: "hsl(var(--admin-muted))" }}>{t("help.faq.empty")}</p>
            ) : (
              filteredFaqs.map((f, i) => (
                <div key={i}>
                  <button
                    onClick={() => setOpenIdx(openIdx === i ? null : i)}
                    className="w-full flex items-center justify-between gap-4 p-5 text-left hover:bg-muted/40 transition"
                  >
                    <span className="font-semibold text-sm">{pickLang(f.q)}</span>
                    <ChevronDown className={cn("w-4 h-4 shrink-0 transition-transform", openIdx === i && "rotate-180")} />
                  </button>
                  {openIdx === i && (
                    <div className="px-5 pb-5 text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>
                      {pickLang(f.a)}
                    </div>
                  )}
                </div>
              ))
            )}
          </div>
        </div>
      )}

      {/* Guide */}
      {tab === "guide" && (
        <div>
          <h2 className="font-display text-lg font-extrabold mb-4">{t("help.guide.title")}</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {GUIDES.map((g, i) => (
              <div key={i} className="admin-card p-6 flex gap-4">
                <span
                  className="w-12 h-12 rounded-2xl text-white flex items-center justify-center shrink-0"
                  style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))" }}
                >
                  <g.icon className="w-6 h-6" />
                </span>
                <div>
                  <h3 className="font-display font-extrabold mb-1">{pickLang(g.title)}</h3>
                  <p className="text-sm leading-relaxed" style={{ color: "hsl(var(--admin-muted))" }}>{pickLang(g.desc)}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Contact */}
      {tab === "contact" && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <form onSubmit={submitTicket} className="admin-card p-6 lg:col-span-2 space-y-4">
            <div>
              <h2 className="font-display text-lg font-extrabold">{t("help.contact.title")}</h2>
              <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>{t("help.contact.desc")}</p>
            </div>
            <div>
              <label className="text-xs font-bold uppercase tracking-wider block mb-2">{t("help.contact.subject")}</label>
              <input
                required
                value={form.subject}
                onChange={(e) => setForm({ ...form, subject: e.target.value })}
                placeholder={t("help.contact.subjectPh")}
                className="w-full bg-muted rounded-xl px-4 py-2.5 text-sm border border-transparent focus:border-primary outline-none transition"
              />
            </div>
            <div>
              <label className="text-xs font-bold uppercase tracking-wider block mb-2">{t("help.contact.priority")}</label>
              <select
                value={form.priority}
                onChange={(e) => setForm({ ...form, priority: e.target.value })}
                className="w-full bg-muted rounded-xl px-4 py-2.5 text-sm border border-transparent focus:border-primary outline-none transition"
              >
                <option value="low">{t("help.contact.low")}</option>
                <option value="normal">{t("help.contact.normal")}</option>
                <option value="high">{t("help.contact.high")}</option>
                <option value="urgent">{t("help.contact.urgent")}</option>
              </select>
            </div>
            <div>
              <label className="text-xs font-bold uppercase tracking-wider block mb-2">{t("help.contact.message")}</label>
              <textarea
                required
                rows={6}
                value={form.message}
                onChange={(e) => setForm({ ...form, message: e.target.value })}
                placeholder={t("help.contact.messagePh")}
                className="w-full bg-muted rounded-xl px-4 py-2.5 text-sm border border-transparent focus:border-primary outline-none transition resize-none"
              />
            </div>
            <button
              type="submit"
              className="inline-flex items-center gap-2 px-5 py-2.5 rounded-full text-white text-sm font-bold"
              style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))" }}
            >
              <Send className="w-4 h-4" /> {t("help.contact.send")}
            </button>
          </form>

          <div className="space-y-4">
            <div className="admin-card p-6">
              <h3 className="font-display font-extrabold mb-4">{t("help.contact.channels")}</h3>
              <ul className="space-y-3 text-sm">
                <li className="flex items-start gap-3">
                  <Mail className="w-4 h-4 mt-0.5 text-primary shrink-0" />
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-muted))" }}>{t("help.contact.email")}</p>
                    <a href="mailto:support@nicon.vn" className="font-semibold hover:text-primary">support@nicon.vn</a>
                  </div>
                </li>
                <li className="flex items-start gap-3">
                  <Phone className="w-4 h-4 mt-0.5 text-primary shrink-0" />
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-muted))" }}>{t("help.contact.phone")}</p>
                    <a href="tel:0909450266" className="font-semibold hover:text-primary">0909 450 266</a>
                  </div>
                </li>
                <li className="flex items-start gap-3">
                  <Clock className="w-4 h-4 mt-0.5 text-primary shrink-0" />
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-muted))" }}>{t("help.contact.hours")}</p>
                    <p className="font-semibold">{t("help.contact.hoursVal")}</p>
                  </div>
                </li>
              </ul>
            </div>
            <div
              className="rounded-2xl p-6 text-white"
              style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))" }}
            >
              <p className="text-xs uppercase tracking-wider font-bold opacity-80 mb-1">{t("help.version")}</p>
              <p className="font-display text-2xl font-extrabold">NICON Admin v1.4.0</p>
              <p className="text-xs mt-2 opacity-80">Build 2026.04.24</p>
            </div>
          </div>
        </div>
      )}

      {/* Shortcuts */}
      {tab === "shortcuts" && (
        <div>
          <div className="mb-4">
            <h2 className="font-display text-lg font-extrabold">{t("help.shortcut.title")}</h2>
            <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>{t("help.shortcut.desc")}</p>
          </div>
          <div className="admin-card divide-y" style={{ borderColor: "hsl(var(--admin-border))" }}>
            {SHORTCUTS.map((s, i) => (
              <div key={i} className="flex items-center justify-between gap-4 p-4">
                <span className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{pickLang(s.desc)}</span>
                <div className="flex items-center gap-1.5">
                  {s.keys.map((k, j) => (
                    <kbd
                      key={j}
                      className="px-2.5 py-1 rounded-md bg-muted border text-xs font-mono font-bold"
                      style={{ borderColor: "hsl(var(--admin-border))" }}
                    >
                      {k}
                    </kbd>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default HelpPage;
