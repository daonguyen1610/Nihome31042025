# UI System

Last reviewed: 2026-04-21

## Visual Direction

The current UI direction comes from the imported starter-kit and is now the official admin baseline for Nihome.
New work should preserve that baseline while replacing demo and vendor-facing copy with Nihome-specific language.

## Tokens and Styling Ownership

- Global theme ownership lives primarily in the MUI theme, `themeConfig`, and the starter-kit styling layer.
- `styles/globals.css` is the current home for shared global CSS used by `_app.tsx`.
- Before repeating raw color, spacing, or shadow values across screens, promote them into the theme or a reusable MUI-aware pattern.
- Avoid mixing unrelated token systems without a deliberate migration plan.

## Typography

- The current baseline uses the starter-kit typography system and a Google Fonts import through `_document.tsx`.
- Do not switch to Geist by default.
- If the team changes typography later, treat that as a documented visual-system decision and update the playbook plus memory bank together.

## Component Strategy

- Start simple while the app is small, but do not let repeated route-local UI harden into inconsistent one-off patterns.
- Prefer starter-kit layout primitives, theme overrides, and shared MUI composition over ad hoc route-local styling.
- Keep visual wrappers, content logic, and data-loading concerns reasonably separated.
- The current baseline ships with a shared shell layer based on `UserLayout`, navigation config, dropdowns, footer, and page cards.

## Responsiveness and Accessibility

- Design for both desktop and mobile from the start.
- Use semantic HTML before adding ARIA fallbacks.
- Ensure interactive elements remain keyboard-usable and visually understandable.
- Keep contrast, text sizing, and spacing readable without relying on one perfect viewport.

## Naming and Drift Control

- Name shared UI pieces by role, not by page location, once they become reusable.
- Avoid creating near-duplicate components with slightly different styling tokens unless the design system explicitly allows the variation.
- If a task introduces a new reusable pattern, record it here when it becomes part of the expected UI language.
