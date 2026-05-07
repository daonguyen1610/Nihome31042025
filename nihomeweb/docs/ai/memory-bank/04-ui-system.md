# UI System

Last reviewed: 2026-05-07

## Visual Direction

The current UI direction comes from the Lovable/Vite source tree and is now the official frontend baseline for NICON / Nihome.
New work should preserve the NICON / Nihome design language already encoded in `src/index.css` and the current components.

## Tokens and Styling Ownership

- Global design tokens live primarily in `src/index.css` CSS variables and `tailwind.config.ts`.
- shadcn/ui configuration lives in `components.json`.
- `src/components/ui/` is the active primitive component layer.
- Use Tailwind utilities and token-backed classes before repeating raw color, spacing, or shadow values.
- Avoid mixing unrelated token systems such as MUI, Emotion, or Materialize without a documented migration plan.

## Typography

- The current baseline imports Be Vietnam Pro and Manrope in `src/index.css`.
- Do not switch to Geist by default.
- If the team changes typography later, treat that as a documented visual-system decision and update the playbook plus memory bank together.

## Color and Surface Direction

- Primary brand direction is red/orange with neutral surfaces and selected indigo accents.
- Public pages should stay media-rich and brand-forward, using project/company assets where available.
- Admin pages should stay quieter, denser, and optimized for repeated work.
- Do not reintroduce Materialize or MUI visuals as the default admin language.

## Component Strategy

- Prefer existing shadcn/Radix primitives for common controls.
- Prefer lucide-react icons for recognizable actions.
- Start simple while the app is small, but do not let repeated route-local UI harden into inconsistent one-off patterns.
- Keep visual wrappers, content logic, and data-loading concerns reasonably separated.
- The current baseline ships with a public layout, admin layout, language toggle, page header, and shadcn primitives.
- Admin list exports use the shared `AdminExportButton` pattern and should export the current filtered list instead of adding route-local download button variants.

## Responsiveness and Accessibility

- Design for both desktop and mobile from the start.
- Use semantic HTML before adding ARIA fallbacks.
- Ensure interactive elements remain keyboard-usable and visually understandable.
- Keep contrast, text sizing, and spacing readable without relying on one perfect viewport.
- Ensure sidebar, table, filter, card, and button text does not overflow at expected viewport widths.

## Naming and Drift Control

- Name shared UI pieces by role, not by page location, once they become reusable.
- Avoid creating near-duplicate components with slightly different styling tokens unless the design system explicitly allows the variation.
- If a task introduces a new reusable pattern, record it here when it becomes part of the expected UI language.
