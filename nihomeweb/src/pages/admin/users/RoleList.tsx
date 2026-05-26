import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, ShieldCheck, XCircle } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { useI18n } from "@/lib/i18n";
import { adminApi, type RoleCatalogResponse, type UserRole } from "@/services/adminApi";

const roleOrder: UserRole[] = ["SUPER_ADMIN", "ADMIN", "USER"];

export default function RoleList() {
  const { t } = useI18n();
  const [catalog, setCatalog] = useState<RoleCatalogResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getUserRoles();
      setCatalog(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : t("common.error"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const roles = useMemo(
    () => [...(catalog?.roles ?? [])].sort((a, b) => roleOrder.indexOf(a.role) - roleOrder.indexOf(b.role)),
    [catalog],
  );

  return (
    <AdminLayout>
      <div className="mb-7">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("adminUsers.rolesTitle")}</h1>
        <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("adminUsers.rolesDescription")}
        </p>
      </div>

      {loading ? (
        <PageLoading />
      ) : error ? (
        <PageError message={error} onRetry={loadData} />
      ) : catalog ? (
        <>
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-5 mb-6">
            {roles.map((role) => (
              <div key={role.role} className="admin-card p-6">
                <div className="flex items-start justify-between gap-4 mb-5">
                  <div className="w-12 h-12 rounded-2xl text-white flex items-center justify-center" style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))" }}>
                    <ShieldCheck className="w-5 h-5" />
                  </div>
                  {role.isSystemRole && <span className="admin-chip">{t("adminUsers.systemRole")}</span>}
                </div>
                <h2 className="font-display text-xl font-extrabold">{t(role.labelKey)}</h2>
                <p className="text-sm mt-2 min-h-12" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t(role.descriptionKey)}
                </p>
                <p className="font-display text-3xl font-extrabold mt-5">{role.userCount}</p>
                <p className="text-xs font-semibold" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("adminUsers.assignedUsers")}
                </p>
              </div>
            ))}
          </div>

          <div className="admin-card overflow-hidden">
            <div className="px-5 py-4 border-b" style={{ borderColor: "hsl(var(--admin-border))" }}>
              <h2 className="font-display text-lg font-extrabold">{t("adminUsers.permissionMatrix")}</h2>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left border-b" style={{ borderColor: "hsl(var(--admin-border))" }}>
                    <th className="px-5 py-3 font-semibold">{t("adminUsers.module")}</th>
                    {roles.map((role) => (
                      <th key={role.role} className="px-5 py-3 font-semibold text-center">
                        {t(role.labelKey)}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  {catalog.permissionMatrix.map((row) => (
                    <tr key={row.moduleKey}>
                      <td className="px-5 py-4 font-semibold">{t(row.moduleKey)}</td>
                      {roles.map((role) => {
                        const allowed = row.accessByRole[role.role];
                        return (
                          <td key={role.role} className="px-5 py-4">
                            <div className="flex justify-center">
                              {allowed ? (
                                <CheckCircle2 className="w-5 h-5" style={{ color: "hsl(var(--admin-success))" }} />
                              ) : (
                                <XCircle className="w-5 h-5" style={{ color: "hsl(var(--admin-muted))" }} />
                              )}
                            </div>
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      ) : null}
    </AdminLayout>
  );
}
