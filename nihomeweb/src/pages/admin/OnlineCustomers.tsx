import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";

type Online = {
  id: string;
  customer: string;
  ip: string;
  location: string;
  lastActivity: string;
  lastPage: string;
};

const data: Online[] = [
  { id: "1", customer: "tringuyen@nicon.vn", ip: "14.224.221.31", location: "Vietnam", lastActivity: "vài giây trước", lastPage: "/admin" },
  { id: "2", customer: "Guest", ip: "113.179.249.166", location: "Vietnam", lastActivity: "1 phút trước", lastPage: "/projects" },
  { id: "3", customer: "Guest", ip: "13.217.253.5", location: "United States", lastActivity: "2 phút trước", lastPage: "/" },
  { id: "4", customer: "Guest", ip: "193.186.4.156", location: "Ireland", lastActivity: "5 phút trước", lastPage: "/services" },
  { id: "5", customer: "Guest", ip: "14.162.105.168", location: "Vietnam", lastActivity: "5 phút trước", lastPage: "/news" },
  { id: "6", customer: "Guest", ip: "51.195.244.151", location: "United Kingdom", lastActivity: "9 phút trước", lastPage: "/contact" },
  { id: "7", customer: "Guest", ip: "167.114.139.51", location: "Canada", lastActivity: "10 phút trước", lastPage: "/projects/bma" },
  { id: "8", customer: "Guest", ip: "113.164.91.80", location: "Vietnam", lastActivity: "15 phút trước", lastPage: "/profile" },
  { id: "9", customer: "Guest", ip: "98.83.94.113", location: "United States", lastActivity: "20 phút trước", lastPage: "/clients" },
];

const OnlineCustomers = () => {
  const { t } = useI18n();
  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("online.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {data.length} người dùng
          </p>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[800px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-5 py-3 font-semibold">{t("online.customer")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.ip")}</th>
                <th className="px-5 py-3 font-semibold">{t("online.location")}</th>
                <th className="px-5 py-3 font-semibold">{t("cust.lastActivity")}</th>
                <th className="px-5 py-3 font-semibold">{t("online.lastPage")}</th>
              </tr>
            </thead>
            <tbody>
              {data.map((o) => (
                <tr key={o.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-5 py-3">
                    <span
                      className="inline-flex items-center gap-2 font-semibold"
                      style={{ color: o.customer === "Guest" ? "hsl(var(--admin-muted))" : "hsl(var(--admin-primary))" }}
                    >
                      <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" /> {o.customer}
                    </span>
                  </td>
                  <td className="px-5 py-3 font-mono text-xs">{o.ip}</td>
                  <td className="px-5 py-3 text-xs">{o.location}</td>
                  <td className="px-5 py-3 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{o.lastActivity}</td>
                  <td className="px-5 py-3 text-xs">{o.lastPage}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </AdminLayout>
  );
};

export default OnlineCustomers;
