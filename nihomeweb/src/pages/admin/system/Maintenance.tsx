import { useState } from "react";
import { Trash2, Database, RotateCw } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

type Backup = { id: string; fileName: string; fileSize: string };

const Card = ({ title, children }: { title: string; children: React.ReactNode }) => (
  <div className="admin-card p-5">
    <h3 className="font-bold text-base mb-4">{title}</h3>
    {children}
  </div>
);

const Field = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <div className="flex items-center gap-3 mb-3">
    <span className="text-xs font-bold w-32 shrink-0 text-right" style={{ color: "hsl(var(--admin-muted))" }}>{label}</span>
    <div className="flex-1">{children}</div>
  </div>
);

const MaintenancePage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [backups, setBackups] = useState<Backup[]>([]);
  const [guestStart, setGuestStart] = useState("");
  const [guestEnd, setGuestEnd] = useState(new Date().toISOString().slice(0, 10));

  const [expStart, setExpStart] = useState("");
  const [expEnd, setExpEnd] = useState("");

  const backup = () => {
    const b: Backup = {
      id: Math.random().toString(36).slice(2, 9),
      fileName: `nicon_db_backup_${new Date().toISOString().slice(0, 19).replace(/[T:]/g, "-")}.bak`,
      fileSize: `${(Math.random() * 80 + 20).toFixed(1)} MB`,
    };
    setBackups((s) => [b, ...s]);
    toast({ title: t("sys.maint.backupOk") });
  };

  return (
    <AdminLayout>
      <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight mb-6">{t("sys.maint.title")}</h1>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 mb-6">
        <Card title={t("sys.maint.deleteGuests")}>
          <Field label={t("sys.maint.startDate")}>
            <input type="date" value={guestStart} onChange={(e) => setGuestStart(e.target.value)} className="admin-input w-full" />
          </Field>
          <Field label={t("sys.maint.endDate")}>
            <input type="date" value={guestEnd} onChange={(e) => setGuestEnd(e.target.value)} className="admin-input w-full" />
          </Field>
          <button onClick={() => toast({ title: t("form.deleted") })} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2 rounded-lg text-white" style={{ background: "hsl(var(--admin-danger))" }}>
            <Trash2 className="w-4 h-4" /> {t("common.delete")}
          </button>
        </Card>

        <Card title={t("sys.maint.deleteExports")}>
          <Field label={t("sys.maint.startDate")}>
            <input type="date" value={expStart} onChange={(e) => setExpStart(e.target.value)} className="admin-input w-full" />
          </Field>
          <Field label={t("sys.maint.endDate")}>
            <input type="date" value={expEnd} onChange={(e) => setExpEnd(e.target.value)} className="admin-input w-full" />
          </Field>
          <button onClick={() => toast({ title: t("form.deleted") })} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2 rounded-lg text-white" style={{ background: "hsl(var(--admin-danger))" }}>
            <Trash2 className="w-4 h-4" /> {t("common.delete")}
          </button>
        </Card>
      </div>

      <Card title={t("sys.maint.backups")}>
        <div className="overflow-x-auto mb-4">
          <table className="w-full text-sm">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-4 py-3 font-bold">{t("sys.maint.fileName")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.maint.fileSize")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.maint.download")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.maint.restore")}</th>
                <th className="px-4 py-3 font-bold">{t("common.delete")}</th>
              </tr>
            </thead>
            <tbody>
              {backups.length === 0 ? (
                <tr><td colSpan={5} className="px-4 py-8 text-center text-sm" style={{ color: "hsl(var(--admin-muted))" }}>—</td></tr>
              ) : backups.map((b) => (
                <tr key={b.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-4 py-3 font-mono text-xs">{b.fileName}</td>
                  <td className="px-4 py-3 text-xs">{b.fileSize}</td>
                  <td className="px-4 py-3"><button className="text-xs font-bold" style={{ color: "hsl(var(--admin-primary))" }}>Download</button></td>
                  <td className="px-4 py-3"><button className="text-xs font-bold" style={{ color: "hsl(var(--admin-primary))" }}><RotateCw className="w-3.5 h-3.5 inline" /></button></td>
                  <td className="px-4 py-3">
                    <button onClick={() => setBackups((s) => s.filter((x) => x.id !== b.id))} className="text-xs font-bold" style={{ color: "hsl(var(--admin-danger))" }}>
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <button onClick={backup} className="admin-btn-primary inline-flex items-center gap-2 px-4 py-2 text-sm">
          <Database className="w-4 h-4" /> {t("sys.maint.backupNow")}
        </button>
      </Card>
    </AdminLayout>
  );
};

export default MaintenancePage;
