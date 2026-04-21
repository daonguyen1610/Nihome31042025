import type { ReactNode } from "react";
import { AdminHeader } from "@/components/layout/admin-header";
import { AdminSidebar } from "@/components/layout/admin-sidebar";
import { PageContainer } from "@/components/layout/page-container";

export default function AdminLayout({ children }: { children: ReactNode }) {
  return (
    <main className="min-h-screen py-6 sm:py-8">
      <PageContainer className="grid gap-6 lg:grid-cols-[18rem_minmax(0,1fr)]">
        <AdminSidebar />
        <div className="space-y-6">
          <AdminHeader />
          {children}
        </div>
      </PageContainer>
    </main>
  );
}
