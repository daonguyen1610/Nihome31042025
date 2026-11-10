---
name: Nicon Admin
description: Clear project workspaces for nontechnical construction staff.
colors:
  primary: hsl(354 84% 43%)
  primary-foreground: hsl(0 0% 100%)
  foreground: hsl(222 25% 12%)
  admin-text: hsl(230 25% 14%)
  card: hsl(0 0% 100%)
  workspace: hsl(230 25% 97%)
  muted: hsl(220 20% 96%)
  muted-foreground: hsl(220 9% 40%)
  border: hsl(220 15% 90%)
  destructive: hsl(354 68% 34%)
  destructive-foreground: hsl(0 0% 100%)
  success-soft: hsl(150 62% 94%)
  success-strong: hsl(150 62% 28%)
  warning-soft: hsl(38 92% 94%)
  warning-strong: hsl(28 85% 30%)
  info-soft: hsl(195 85% 94%)
  info-strong: hsl(199 90% 28%)
  ring: hsl(354 84% 57%)
typography:
  display:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: 1.5rem
    fontWeight: 600
    lineHeight: 2rem
    letterSpacing: "-0.02em"
  headline:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: 1.125rem
    fontWeight: 600
    lineHeight: 1.75rem
    letterSpacing: "-0.02em"
  body:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: 0.875rem
    fontWeight: 400
    lineHeight: 1.25rem
  label:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: 0.875rem
    fontWeight: 500
    lineHeight: 1.25rem
  metadata:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: 0.75rem
    fontWeight: 400
    lineHeight: 1rem
rounded:
  sm: 8px
  md: 12px
  lg: 16px
  xl: 16px
  2xl: 24px
  3xl: 32px
  full: 9999px
spacing:
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 20px
  2xl: 24px
  3xl: 32px
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.primary-foreground}"
    rounded: "{rounded.md}"
    height: 40px
    padding: 8px 16px
  button-outline:
    backgroundColor: "{colors.card}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.md}"
    height: 40px
    padding: 8px 16px
  button-destructive:
    backgroundColor: "{colors.destructive}"
    textColor: "{colors.destructive-foreground}"
    rounded: "{rounded.md}"
    height: 40px
    padding: 8px 16px
  input:
    backgroundColor: "{colors.card}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.md}"
    height: 40px
    padding: 8px 12px
  card:
    backgroundColor: "{colors.card}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.lg}"
    padding: 16px
  nav-item-active:
    backgroundColor: "{colors.muted}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.md}"
---

# Design System: Nicon Admin

## Overview

**Creative North Star: "The Site Office Ledger"**

Nicon is a working system for nontechnical construction staff. Its design must
help someone understand the current project, the state of the work, and the next
safe action without a developer explaining the screen. Keep the existing Nicon
identity: Vietnamese-first typography, red for the main action, white working
surfaces, quiet rules, and readable records.

The priority order is business correctness, understandable actions, recovery,
accessibility, then visual refinement. A tidy screen that saves against the wrong
project, loses an approval reason, or conceals missing reports is a design failure.
The user should be able to answer: **Where am I? What needs attention? What can I
do? Did it work?**

This standard covers authenticated admin screens. Public marketing pages need a
separate brief; their gradients, large display typography, and decorative motion
must not become defaults for workspaces. `AGENTS.md` and the canonical business
contracts take precedence over visual choices. This file is a standard to apply,
not a claim that every existing screen already complies.

**Key Characteristics:**

- One visible project context and one page title; related panels share the frame.
- Familiar controls, sentence-case labels, readable data, and restrained colour.
- CM plans and reviews; BCH executes. Both reference the same business records.
- The next permitted action is clear; blocked actions explain their prerequisite.
- Loading, missing data, errors, conflicts, and completed work remain distinct.
- BCH supports phone use; CM supports desktop comparison and keyboard operation.

## Colors

The frontmatter preserves source HSL values rather than approximate hex copies.
Use semantic Tailwind utilities and CSS variables in product code, not values
copied out of this document. Sources are `nihomeweb/src/index.css` and
`nihomeweb/tailwind.config.ts`; keep this document aligned when those change.

| Role              | Apply through                      | Meaning                                                |
| ----------------- | ---------------------------------- | ------------------------------------------------------ |
| Primary action    | `primary` / `primary-foreground`   | The main next action in the active task                |
| Main text         | `foreground`, inherited admin text | Headings, labels, record content                       |
| Supporting text   | `muted-foreground`                 | Help and metadata; still readable                      |
| Working surface   | `card`, `background`               | Forms, records, sheets and menus                       |
| Page backdrop     | `--admin-bg`                       | Separates content from the shell                       |
| Structure         | `border`, `input`                  | Quiet boundaries and dividers                          |
| Completed work    | `success-soft` / `success-strong`  | Approved or completed when the lifecycle says so       |
| Needs attention   | `warning-soft` / `warning-strong`  | Unresolved, overdue, blocked, or awaiting verification |
| Information       | `info-soft` / `info-strong`        | Reported, submitted, or explanatory context            |
| Destructive/error | `destructive`                      | Deletion, rejection and failed operations              |
| Keyboard focus    | `ring`                             | A visible outline distinct from selection              |

Use strong semantic text on soft semantic backgrounds. Bright success, warning,
and information colours are not automatically readable body text. Verify normal
text at 4.5:1 and large text at 3:1 in the actual theme; do not infer contrast from
a token name. A status needs a written label as well as colour. Charts also need
labels and distinct line patterns or markers.

Dark-theme values exist in CSS. Use their semantic mappings; do not describe a
screen as dark-mode ready until its text, controls, charts and overlays have been
checked in that theme. Legacy `.chip-*` colours and raw palette classes are not a
substitute for that verification.

## Typography

Use **Be Vietnam Pro**, with the existing Inter/system sans fallbacks. Let the
platform supply suitable Chinese and Japanese glyph fallbacks. **Manrope** is an
optional existing numeric-display face, not a monospaced font. Alignment comes
from `tabular-nums`, right-aligned number columns, and consistent units.

| Role                 | Default                     | Use                                                     |
| -------------------- | --------------------------- | ------------------------------------------------------- |
| Page title           | 24px, 600, 32px line height | One h1 naming the current workspace                     |
| Section title        | 18px, 600, 28px line height | A meaningful section or drawer title                    |
| Body and field label | 14px, 20px line height      | Instructions, forms and tables                          |
| Supporting metadata  | 12px, 16px line height      | Short secondary information, not essential instructions |
| Numeric summary      | 24px, 600                   | A real operational measure with its unit and scope      |

Use sentence case. Do not add uppercase eyebrows, widely tracked labels, or tiny
chips as a second heading system. Existing uppercase chip utilities are legacy
implementation, not the model for new content. Keep Vietnamese diacritics and
CJK text legible; allow translated labels and project names to wrap.

Keep explanation paragraphs near 65–75 characters per line. A title and breadcrumb
may share the same name; avoid repeating the entire title and introduction inside
each panel. Do not truncate the only text identifying the project or explaining a
blocked action. Inputs use the existing 16px mobile / 14px desktop text treatment.

## Layout

`AdminLayout` owns the main landmark and outer inset: 16px on mobile, 20px from
`sm`, and 32px from `lg`. The current sidebar is 288px, or 80px collapsed, with a
64px sticky top bar. Feature panels must not add another `<main>`, another page
padding frame, or an arbitrary narrow maximum width. Constrain prose locally,
not an entire comparison table.

Use the existing 4/8/12/16/20/24/32px spacing rhythm. Default to 20px between major
sections, 16px within related groups, and 16–24px panel padding. Put more space
above a new section than between its heading and content. Align fields by their
controls when labels wrap. Keep sticky content clear of the top bar and focused
controls; anchored sections need a scroll offset.

Use one project selector per workspace. Project codes and names stay visible.
When the operational project changes, validate or clear dependent design-project,
task, record and filter IDs. A design project must belong to the selected
operational project. Legacy links must translate identifiers at the compatibility
boundary; do not reinterpret a design-project ID as an operational-project ID.

The five CM areas are Schedule, Quantity and cost, Quality, Safety, and Contracts.
BCH portals group execution work by responsibility. Reuse panels and records;
do not copy a workflow merely to give the other role its own menu. Preserve valid
project, tab and record context through navigation, refresh and redirects.

Use the existing Tailwind breakpoints: 640, 768, 1024 and 1280px. At phone widths,
stack fields, wrap or accessibly scroll tabs, and lead with actionable records.
Tables can scroll horizontally when comparison requires columns. Maintain a
visible scroll cue and readable column headings. Each independently paginated
dataset gets its own pagination; never silently stop after an API page limit.

## Elevation & Depth

Prefer surface tone, spacing and a quiet 1px border for ordinary working content.
Reserve stronger elevation for overlays that actually float above the page. Do
not nest cards inside cards to express every subsection; use headings, fieldsets,
rows and separators instead.

The existing `.admin-card` combines a border with a small two-layer ambient
shadow. It is a compatibility style, not a reason to put both a shadow and border
on every new panel. Existing CSS also exposes `--shadow-soft`, `--shadow-card`,
and `--shadow-elegant`; use them only when a floating surface needs that depth.

Do not apply glow, gradients, hover lift or decorative blur to routine admin
controls. The existing translucent sticky top bar serves navigation separation;
it does not justify glass effects on records or forms. Do not animate page
entrance or numeric values as decoration. Keep ordinary feedback short and
respect reduced-motion preferences; current global animation utilities are not
proof that reduced-motion behavior is implemented.

## Shapes

The existing radius scale is 8px compact, 12px controls, and 16px cards. In this
project `rounded-lg` and `rounded-xl` both resolve to the 16px card radius;
`rounded-md` is 12px and `rounded-sm` is 8px. `rounded-2xl` and `rounded-3xl` are
24px and 32px, for exceptional surfaces rather than routine work panels.

Use circles for avatars and compact pills for statuses. Buttons remain recognisable
controls. Icons come from the existing Lucide set: usually 16px in controls,
with consistent stroke and an accessible name when the icon is the whole action.

Shared controls currently default to 40px high, with 36px small buttons. For site
phone workflows, provide a touch target of at least 44px, including icon-only
buttons; do not claim the 36px default meets that requirement. Keep visible focus
and enough separation to avoid approving, rejecting or deleting by mistake.

## Components

### Page headers and navigation

Use `ConstructionWorkspaceShell` for project-scoped construction pages. It owns
one h1, a short task description, and the project selector. An embedded panel
owns section headings and actions, not another shell or duplicate project picker.
Sidebar, breadcrumb, page title and tab names use consistent business nouns.
Active navigation uses a quiet selected surface and written label, with
`aria-current` where appropriate.

Show the user's role in the work through available actions and concise guidance.
Navigation visibility is not authorization: the API must enforce scope and
independent review. Missing permission, missing project data, and an unavailable
feature need different messages.

### Actions and decisions

Give the active task one visually dominant next action. A long list may contain
repeated row actions; keep these quiet until a row or decision becomes the active
context. Use specific labels such as “Save plan”, “Send for approval”, or “Reject
dossier”, not an unexplained “Confirm”.

Use the shared `Button` variants. Primary is filled Nicon red; secondary and
outline controls support the task without competing. Destructive is the existing
separate dark-red variant. Prefer outline or ghost treatment for a row-level
remove action; reserve a filled destructive button for the final, clearly named
destructive decision. Do not prescribe an outline-only system while the shared
primitive implements a filled variant.

While saving, prevent duplicate submission and accidental dismissal. Close a
form or review dialog only after the caller confirms durable success. A rejected
request keeps the user's reason, entered values and relevant context visible,
with a localized error and a safe next step. Keep DOM IDs unique when multiple
instances can exist. Return focus to the initiating control after dismissal.

Deletion follows the permanent aggregate deletion contract in `AGENTS.md`:
show impact, blockers and file consequences; require the resource code and current
plan token; revalidate on the server. A generic yes/no dialog or a direct DELETE
request is not an equivalent design, even for a draft record.

### Forms and evidence

Use a persistent `Label`, a stable unique ID, and the existing `FieldScope` where
appropriate. Mark required/optional inputs consistently. Put format rules and
useful examples beside the field before submission. Validate on the client for
feedback and on the server for protection. Placeholder text is an example, not
the label.

Prefer searchable business names and codes to internal numeric IDs. Pair a
reference with its supporting document. Distinguish editable requirements from
read-only verification results; do not present a derived status as a disabled
switch that appears user-editable. Avoid raw JSON in ordinary staff workflows;
technical configuration requires an explicitly advanced, validated editor.

Use shared number, money, date and file helpers. Always show currency, percent,
quantity units and relevant date context. Source-language content editing and
content translations follow the existing translation-management contract; do not
ask a creator to fill all four languages in the ordinary create dialog.

### Status, severity and progress

Use `ConstructionStatusBadge` and the typed maps in `constructionStatus.ts` for
construction records. Keep status and severity distinct. Do not assign a colour
by matching an English word across unrelated lifecycles.

| Lifecycle  | State interpretation                                                                                                 |
| ---------- | -------------------------------------------------------------------------------------------------------------------- |
| CM review  | Draft neutral; pending informational; approved success; rejected danger                                              |
| HSE        | Reported informational; confirmed warning because the violation remains open; remediated in progress; closed success |
| Punch list | Open neutral; in progress informational; fixed warning until verified; verified success                              |
| Acceptance | Draft/cancelled neutral; submitted informational; approved success; rejected danger                                  |

An HSE violation being confirmed is not a successful resolution. Missing actual
progress is not 0%. A draft plan is not a published baseline. A file waiting for
Drive synchronization is not synchronized. State the distinction in words and
show the permitted next action.

For an S-curve, name CRM master schedule, selected CM plan and BCH site actuals.
Label the date axis, completion percentage, selected period and plan version.
Use different line patterns as well as colour and expose the same values in an
accessible table. Preserve gaps for unavailable actuals; do not fabricate an
illustrative curve or calculate an unapproved business formula to fill space.
Explain prerequisites when there is no baseline and link to the relevant source
record. Totals must describe their scope and completeness.

### Lists, tables and recovery states

Reuse `ListPagination` for independently paginated data. Show the actual total
and active filters, preserve search focus, and reset or clamp the page when its
data scope changes. Large lists need server paging or the shared lookup collector,
not an undocumented first-200 truncation. Keep identifiers readable, comparable
numbers right-aligned and tabular, and row actions accessible by keyboard.

Reuse `PageLoading`, `PageEmpty`, and `PageError`. The current loading primitive is
an accessible spinner; prefer an in-place skeleton when it preserves meaningful
layout, without pretending the component already supplies one. Initial loading
must not appear as “no records”. Refreshing must not destroy entered work.

Distinguish first use, no matches, missing prerequisites, no access, and load
failure. A data-load failure is not evidence that the project contains no work.
If a requested project cannot be opened, retain usable project choices and
explain how to select another. If the session expired, guide the user to sign in;
do not leave a disabled selector and a generic retry loop. Unexpected failures
keep safe wording and a support/request reference when available.

## Do's and Don'ts

### Do:

- **Do** make project, record, state and next permitted action visible together.
- **Do** retain Nicon's type, red accent and semantic tokens through shared components.
- **Do** use one page frame, sentence-case labels, readable help and accessible focus.
- **Do** validate dependent IDs whenever the selected project changes.
- **Do** keep input and decision reasons until the server confirms success.
- **Do** apply status meaning by lifecycle, with words as well as colour.
- **Do** check real content at phone, tablet and desktop widths and 200% zoom.
- **Do** verify keyboard flow, all supported languages, and real error/retry paths.
- **Do** follow server authorization, independent-review and deletion-impact contracts.

### Don't:

- **Don't** invent extra records, formulas, claims or counts to make a dashboard look complete.
- **Don't** make documentation claims of accessibility or business readiness from styling alone.
- **Don't** use nested cards, gradients, glow or animated entrances as the structure of a work screen.
- **Don't** replace clear business names with internal IDs, abbreviations or raw JSON.
- **Don't** hide critical names, reasons or prerequisites behind truncation or tooltips alone.
- **Don't** turn every approve button into the page's dominant red action.
- **Don't** close a dialog after a failed operation or silently reset a user's work.
- **Don't** treat a generic confirmation as authorization for permanent deletion.
- **Don't** call a chart, list or state complete while its source data is missing or truncated.
