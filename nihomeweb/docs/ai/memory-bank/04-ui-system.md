# UI System

Last reviewed: 2026-04-21

## Visual Direction

The current UI direction is warm, premium, calm, and slightly editorial.
New work should preserve that direction unless the team explicitly chooses a rebrand.

## Tokens and Styling Ownership

- Global theme tokens should live in CSS variables first.
- `app/globals.css` is the current home for shared tokens and global styling rules.
- Before repeating the same raw color, radius, spacing, or shadow values in multiple places, promote them into a shared token or reusable pattern.
- Avoid mixing unrelated token systems without a deliberate migration plan.

## Typography

- The current app uses custom serif and sans-serif stacks defined in CSS.
- Do not switch to Geist by default.
- If the team chooses Geist later, treat that as a documented visual-system decision and update the playbook plus memory bank together.

## Component Strategy

- Start simple while the app is small, but do not let repeated route-local UI harden into inconsistent one-off patterns.
- Introduce shared primitives when the same pattern appears across multiple screens or states.
- Prefer composition over large monolithic page-specific components.
- Keep visual wrappers, content logic, and data-loading concerns reasonably separated.
- Phase 1 uses a small shared shell layer: sidebar, header, navbar, page container, page header, empty state, loading state, and status badge.

## Responsiveness and Accessibility

- Design for both desktop and mobile from the start.
- Use semantic HTML before adding ARIA fallbacks.
- Ensure interactive elements remain keyboard-usable and visually understandable.
- Keep contrast, text sizing, and spacing readable without relying on one perfect viewport.

## Naming and Drift Control

- Name shared UI pieces by role, not by page location, once they become reusable.
- Avoid creating near-duplicate components with slightly different styling tokens unless the design system explicitly allows the variation.
- If a task introduces a new reusable pattern, record it here when it becomes part of the expected UI language.
