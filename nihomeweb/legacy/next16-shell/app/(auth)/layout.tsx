import type { ReactNode } from "react";
import { PageContainer } from "@/components/layout/page-container";

export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <main className="relative isolate min-h-screen overflow-hidden py-16">
      <div className="hero-mesh absolute inset-0 -z-20" />
      <div className="hero-glow absolute inset-x-0 top-0 -z-10 h-[28rem]" />
      <PageContainer className="flex min-h-[80vh] items-center justify-center">
        <div className="surface-panel w-full max-w-2xl px-6 py-8 sm:px-10 sm:py-12">
          {children}
        </div>
      </PageContainer>
    </main>
  );
}
