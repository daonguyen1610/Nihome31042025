"use client";

import { useEffect, useState } from "react";

type HealthResponse = {
  name: string;
  environment: string;
  status: string;
  timestampUtc: string;
};

const highlights = [
  "Tenant records and contracts",
  "Apartment inventory and availability",
  "Operations dashboard for staff",
];

export default function Home() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    const loadHealth = async () => {
      try {
        const response = await fetch("/api/system/health", {
          cache: "no-store",
        });

        if (!response.ok) {
          throw new Error(`Backend returned ${response.status}`);
        }

        const data = (await response.json()) as HealthResponse;

        if (active) {
          setHealth(data);
        }
      } catch (loadError) {
        if (active) {
          setError(
            loadError instanceof Error
              ? loadError.message
              : "Unable to reach the backend",
          );
        }
      }
    };

    void loadHealth();

    return () => {
      active = false;
    };
  }, []);

  return (
    <main className="relative isolate overflow-hidden">
      <div className="hero-mesh absolute inset-0 -z-20" />
      <div className="hero-glow absolute inset-x-0 top-0 -z-10 h-[32rem]" />

      <section className="mx-auto flex min-h-screen w-full max-w-6xl flex-col justify-center px-6 py-16 sm:px-10 lg:px-12">
        <div className="grid gap-10 lg:grid-cols-[1.2fr_0.8fr] lg:items-end">
          <div className="space-y-8">
            <p className="w-fit rounded-full border border-white/60 bg-white/70 px-4 py-2 text-sm font-semibold tracking-[0.18em] text-[#8b5e3c] uppercase shadow-sm backdrop-blur">
              Nihome Platform
            </p>

            <div className="space-y-5">
              <h1 className="max-w-3xl font-display text-5xl leading-tight text-[#1f2933] sm:text-6xl">
                Property operations that feel calm, premium, and connected.
              </h1>
              <p className="max-w-2xl text-lg leading-8 text-[#52606d] sm:text-xl">
                <code>nihomeweb/</code> is now set up as a real Next.js
                frontend, ready to grow alongside the ASP.NET backend instead
                of living as static HTML.
              </p>
            </div>

            <div className="flex flex-wrap gap-3">
              <a className="button-primary" href="http://localhost:3000">
                Open Web App
              </a>
              <a className="button-secondary" href="http://localhost:5067/api/system/health">
                Test API Health
              </a>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              {highlights.map((item) => (
                <article className="feature-card" key={item}>
                  <span className="feature-dot" />
                  <p>{item}</p>
                </article>
              ))}
            </div>
          </div>

          <aside className="status-panel">
            <div>
              <p className="eyebrow">Backend handshake</p>
              <h2 className="mt-3 font-display text-3xl text-[#1f2933]">
                Frontend and API are wired to work together.
              </h2>
            </div>

            <div className="status-card">
              <p className="text-sm font-semibold uppercase tracking-[0.2em] text-[#9c6b46]">
                API status
              </p>

              {health ? (
                <div className="space-y-3">
                  <p className="text-3xl font-semibold text-[#1f2933]">
                    {health.status}
                  </p>
                  <dl className="space-y-2 text-sm text-[#52606d]">
                    <div className="flex items-center justify-between gap-3">
                      <dt>Name</dt>
                      <dd>{health.name}</dd>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <dt>Environment</dt>
                      <dd>{health.environment}</dd>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <dt>UTC time</dt>
                      <dd>{new Date(health.timestampUtc).toLocaleString()}</dd>
                    </div>
                  </dl>
                </div>
              ) : (
                <div className="space-y-3">
                  <p className="text-lg font-medium text-[#1f2933]">
                    {error ? "Backend not reachable yet" : "Checking backend..."}
                  </p>
                  <p className="text-sm leading-7 text-[#52606d]">
                    {error ??
                      "Start the ASP.NET app and this panel will show live API health information."}
                  </p>
                </div>
              )}
            </div>

            <div className="rounded-[28px] bg-[#1f2933] px-6 py-5 text-[#f8f1e7] shadow-xl">
              <p className="text-sm uppercase tracking-[0.2em] text-[#f3c892]">
                Next step
              </p>
              <p className="mt-3 text-base leading-7">
                Build your tenant, apartment, and billing screens inside
                <code> nihomeweb/app/ </code>
                then connect them to the controllers in
                <code> nihomebackend/Controllers/</code>.
              </p>
            </div>
          </aside>
        </div>
      </section>
    </main>
  );
}
