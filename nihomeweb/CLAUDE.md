Read these files before doing non-trivial work in `nihomeweb/`:

1. @AGENTS.md
2. @docs/ai/working-procedure.md
3. @docs/ai/frontend-playbook.md
4. @docs/ai/project-brief.md
5. @docs/ai/memory-bank/README.md

Follow `AGENTS.md` as the canonical shared contract.
Use the memory bank for durable project context, decisions, and handoffs rather than relying on chat history alone.
Use `docs/ai/working-procedure.md` as the day-to-day checklist for ownership, boundaries, reviews, and handoffs.

Remember that this repo is a Vite + React SPA using React Router, Tailwind, shadcn/ui, Radix UI, TanStack Query, and localStorage-backed demo stores. Do not apply the old Next.js, Materialize starter-kit, or full admin template assumptions unless a future migration is explicitly documented.

**i18n is mandatory.** Never hardcode user-visible strings in components — always use `t("key")` and add the key to `nihomebackend/Data/Seeds/` with vi/en/zh/ja translations. See the `## i18n and Translation Rules` section in `AGENTS.md` for the full checklist. Also read root `AGENTS.md` (one level up) for project-wide rules including the no-hardcode policy.
