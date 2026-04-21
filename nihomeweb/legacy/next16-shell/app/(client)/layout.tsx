import type { ReactNode } from "react";
import { ClientNavbar } from "@/components/layout/client-navbar";

export default function ClientLayout({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen">
      <ClientNavbar />
      <main>{children}</main>
    </div>
  );
}
