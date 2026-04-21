import Link from "next/link";
import { PageHeader } from "@/components/common/page-header";

export const metadata = {
  title: "Login",
};

export default function LoginPage() {
  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="Auth placeholder"
        title="Sign in will land here in Phase 2."
        description="Phase 1 intentionally avoids auth logic. This screen establishes the shell, spacing, and visual language for the future login flow."
      />

      <div className="surface-card space-y-5 p-6 sm:p-8">
        <label className="block space-y-2">
          <span className="text-sm font-semibold text-[#334e68]">Email</span>
          <input
            className="input-shell"
            name="email"
            placeholder="name@company.com"
            type="email"
          />
        </label>
        <label className="block space-y-2">
          <span className="text-sm font-semibold text-[#334e68]">Password</span>
          <input
            className="input-shell"
            name="password"
            placeholder="Placeholder only"
            type="password"
          />
        </label>
        <div className="flex flex-wrap gap-3">
          <button className="button-primary opacity-70" type="button">
            Sign in coming soon
          </button>
          <Link className="button-secondary" href="/forgot-password">
            Forgot password
          </Link>
        </div>
      </div>
    </section>
  );
}
