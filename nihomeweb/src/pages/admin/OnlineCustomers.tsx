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
      <div className="space-y-4 p-4 sm:p-6">
        <header>
          <h1 className="text-2xl font-semibold">{t("online.title")}</h1>
          <p className="text-xs italic text-muted-foreground">{data.length} người dùng</p>
        </header>

        <div className="overflow-x-auto rounded-lg border">
          <table className="min-w-[800px] w-full divide-y text-sm">
            <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left font-medium">{t("online.customer")}</th>
                <th className="px-4 py-3 text-left font-medium">{t("cust.ip")}</th>
                <th className="px-4 py-3 text-left font-medium">{t("online.location")}</th>
                <th className="px-4 py-3 text-left font-medium">{t("cust.lastActivity")}</th>
                <th className="px-4 py-3 text-left font-medium">{t("online.lastPage")}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {data.map((o) => (
                <tr key={o.id} className="hover:bg-muted/40 transition">
                  <td className="px-4 py-3">
                    <span
                      className={
                        o.customer === "Guest"
                          ? "inline-flex items-center gap-2 font-medium text-muted-foreground"
                          : "inline-flex items-center gap-2 font-medium text-primary"
                      }
                    >
                      <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" /> {o.customer}
                    </span>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 font-mono text-xs">{o.ip}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-xs">{o.location}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-xs text-muted-foreground">{o.lastActivity}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-xs">{o.lastPage}</td>
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
