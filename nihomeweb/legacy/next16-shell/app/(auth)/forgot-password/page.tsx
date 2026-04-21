import Link from "next/link";
import { PageHeader } from "@/components/common/page-header";

export const metadata = {
  title: "Forgot password",
};

export default function ForgotPasswordPage() {
  return (
    <section className="space-y-8">
      <PageHeader
        eyebrow="Auth placeholder"
        title="Password recovery will be added after auth is chosen."
        description="This Phase 1 page exists so the auth route group has a realistic shell without pretending the final auth strategy is already settled."
      />

      <div className="surface-card space-y-5 p-6 sm:p-8">
        <label className="block space-y-2">
          <span className="text-sm font-semibold text-[#334e68]">
            Account email
          </span>
          <input
            className="input-shell"
            name="recovery-email"
            placeholder="name@company.com"
            type="email"
          />
        </label>
        <div className="flex flex-wrap gap-3">
          <button className="button-primary opacity-70" type="button">
            Recovery flow deferred
          </button>
          <Link className="button-secondary" href="/login">
            Back to login
          </Link>
        </div>
      </div>
    </section>
  );
}
