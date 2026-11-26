---
name: Nicon Admin
description: The site handover log for nontechnical construction staff.
colors:
  primary: "hsl(354 84% 43%)"
  primary-foreground: "hsl(0 0% 100%)"
  foreground: "hsl(222 25% 12%)"
  admin-text: "hsl(230 25% 14%)"
  card: "hsl(0 0% 100%)"
  workspace: "hsl(230 25% 97%)"
  muted: "hsl(220 20% 96%)"
  muted-foreground: "hsl(220 9% 40%)"
  border: "hsl(220 15% 90%)"
  ring: "hsl(354 84% 57%)"
  destructive: "hsl(354 68% 34%)"
  destructive-foreground: "hsl(0 0% 100%)"
  success-soft: "hsl(150 62% 94%)"
  success-strong: "hsl(150 62% 28%)"
  warning-soft: "hsl(38 92% 94%)"
  warning-strong: "hsl(28 85% 30%)"
  info-soft: "hsl(195 85% 94%)"
  info-strong: "hsl(199 90% 28%)"
  danger-soft: "hsl(354 78% 95%)"
  danger-strong: "hsl(354 75% 40%)"
typography:
  display:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: "1.5rem"
    fontWeight: 600
    lineHeight: "2rem"
    letterSpacing: "-0.02em"
  headline:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: "1.125rem"
    fontWeight: 600
    lineHeight: "1.75rem"
    letterSpacing: "-0.02em"
  body:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: "1.25rem"
  label:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: "0.875rem"
    fontWeight: 500
    lineHeight: "1.25rem"
  metadata:
    fontFamily: '"Be Vietnam Pro", Inter, system-ui, sans-serif'
    fontSize: "0.75rem"
    fontWeight: 400
    lineHeight: "1rem"
rounded:
  sm: "8px"
  md: "12px"
  lg: "16px"
  full: "9999px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "20px"
  2xl: "24px"
  3xl: "32px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.primary-foreground}"
    rounded: "{rounded.md}"
    height: "40px"
    padding: "8px 16px"
  button-outline:
    backgroundColor: "{colors.card}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.md}"
    height: "40px"
    padding: "8px 16px"
  button-destructive:
    backgroundColor: "{colors.destructive}"
    textColor: "{colors.destructive-foreground}"
    rounded: "{rounded.md}"
    height: "40px"
    padding: "8px 16px"
  input:
    backgroundColor: "{colors.card}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.md}"
    height: "40px"
    padding: "8px 12px"
  card:
    backgroundColor: "{colors.card}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.lg}"
    padding: "16px"
  status-pill-success:
    backgroundColor: "{colors.success-soft}"
    textColor: "{colors.success-strong}"
    rounded: "{rounded.full}"
    padding: "2px 10px"
  status-pill-warning:
    backgroundColor: "{colors.warning-soft}"
    textColor: "{colors.warning-strong}"
    rounded: "{rounded.full}"
    padding: "2px 10px"
---

# Design System: Nicon Admin

## Overview

**Creative North Star: "The Site Handover Log" (Sổ giao ca công trường)**

A handover log is read at the start of a shift: it says what is waiting, which
project it belongs to, who has it and what happens next. Every Nicon admin screen
answers the same four questions before anything else: **Where am I? What is
waiting for me? What can I do next? Did it work?**

Staff are nontechnical construction people working in Vietnamese first. The
screens are bright, flat and legible: white working surfaces on a quiet backdrop,
thin rules, Nicon red reserved for the next main action, and a small shared set of
status colours that always carry words. Records that need action stand out;
finished records stay quiet.

The voice is clear and friendly. Sentences are short, use construction and Nicon
business words, and say what happened and what to do next. No technical jargon,
error codes or internal names appear in ordinary screens.

This standard covers authenticated admin screens. The public website has its own
visual language and does not lend it to the workspace. `AGENTS.md` and the
business contracts take precedence over visual choices. This file is the standard
to apply; it does not claim that every screen already meets it.

**Key Characteristics:**

- One project context and one page title; related panels share the frame.
- Waiting and blocked work is more visible than finished work.
- The reason for a state sits beside the action that resolves it.
- Motion confirms what the user just did, and stops when they ask for less.
- Loading, empty, error, success and no-permission states look different.
- BCH works well on a phone; CM supports desktop comparison and the keyboard.

## Colors

Neutral records on a coloured frame: module colour for place, one red voice for
action, and the status tones.

Values are the source HSL values. Use semantic Tailwind utilities and CSS
variables in product code, never literal values or raw palette classes such as
`text-amber-800`. Sources are `nihomeweb/src/index.css` and
`nihomeweb/tailwind.config.ts`; keep this file aligned with them.

### Primary

- **Nicon red** (`primary`): the one main next action of the active task, the
  active navigation item and keyboard focus (`ring`).

### Neutral

- **Ink** (`foreground`): headings, labels and record content.
- **Quiet ink** (`muted-foreground`): help and metadata that is still readable.
- **Paper** (`card`) on the **desk** (`--admin-bg`, `workspace`): records and
  forms are paper; the page behind them is the desk.
- **Rule** (`border`, `input`): quiet 1px boundaries.

### Status tones

One tone map serves every module: five general tones and one narrow one. A status
is a tone **and** a written label.

| Tone    | Pair                              | Means                                             |
| ------- | --------------------------------- | ------------------------------------------------- |
| Neutral | `muted` / `muted-foreground`      | Draft, not started, cancelled, closed without win |
| Info    | `info-soft` / `info-strong`       | Submitted, sent, in review, in progress           |
| Warning | `warning-soft` / `warning-strong` | Waiting on someone, overdue, needs verification   |
| Success | `success-soft` / `success-strong` | Approved, completed, synchronized                 |
| Danger  | `danger-soft` / `danger-strong`   | Rejected, failed, blocked, lost or broken         |
| Indigo  | `indigo` at 12% / `indigo`        | Fixed and awaiting closure (HSE remediated)       |

`destructive` is for deleting and for error text. It is not a status fill.

### Construction lifecycles

| Lifecycle  | State interpretation                                                             |
| ---------- | -------------------------------------------------------------------------------- |
| CM review  | Draft neutral; pending info; approved success; rejected danger                   |
| HSE        | Reported info; confirmed warning (still open); remediated indigo; closed success |
| Punch list | Open neutral; in progress info; fixed warning until verified; verified success   |
| Acceptance | Draft or cancelled neutral; submitted info; approved success; rejected danger    |

`constructionStatus.ts` holds these maps. Missing actual progress is not 0%.

### Module colour

Chosen on 2026-11-27: the workspace should feel lively, so each area has one
colour that follows the person from the menu to the page.

| Area                 | Tone    | Area               | Tone    |
| -------------------- | ------- | ------------------ | ------- |
| Dashboard, analytics | indigo  | Construction (BCH) | cyan    |
| CRM                  | blue    | CM office          | teal    |
| Design               | violet  | Content            | pink    |
| Procurement          | orange  | Branding           | fuchsia |
| Finance              | emerald | Processes, config  | slate   |
| Legal                | amber   | Users              | indigo  |

The tone colours the menu icon and active item, the page icon and the wash behind
the top of the page, table headings, row hover, active tabs and quick-view counts.
Headline numbers use the gradient `StatTile`. `lib/moduleTheme.ts` maps areas;
`index.css` (`.tone-*`) holds the values.

Gradients carry white text at 4.5:1 or better at both ends. Coloured text on a
tint uses `--tone-ink` (85% tone with black). Check both when adding a tone.
Module colour says **where you are**; status tones say **what state a record is
in**. A status never takes its colour from the module, and red stays the main
action.

### Named Rules

**The One Red Voice Rule.** Each working area has at most one filled red action.
Everything else is outline or ghost.

**The Words First Rule.** Remove all colour and every status must still be
understood from its label.

**The Same Word, Same Tone Rule.** A lifecycle meaning maps to one tone across
modules. Do not assign a colour by matching an English status name; decide what
the state means in its own lifecycle. A confirmed HSE violation is still open
(warning), not a success.

Normal text needs 4.5:1 and large text 3:1, checked in the rendered theme. Dark
values exist in CSS but a screen is not dark-ready until its text, controls,
charts and overlays have been checked there.

## Typography

**Body and display font:** Be Vietnam Pro (with Inter, system-ui, sans-serif).
**Numeric accent:** Manrope, optional and not monospaced.

**Character:** a Vietnamese-first sans that keeps diacritics clear at small sizes.
Let the platform supply Chinese and Japanese glyphs.

### Hierarchy

- **Display** (600, 24px/32px): the one page title.
- **Headline** (600, 18px/28px): a section or drawer title.
- **Body** (400, 14px/20px): instructions, forms and tables. Prose stays within
  65–75 characters per line.
- **Label** (500, 14px/20px): field and control labels.
- **Metadata** (400, 12px/16px): short secondary facts, never an instruction the
  user needs.

### Named Rules

**The Sentence Case Rule.** No uppercase eyebrows, wide tracking or tiny chips as
a second heading system. Existing uppercase utilities are legacy.

**The Never Broken Word Rule.** Names, codes and reasons wrap at spaces or
hyphens. A column too narrow for that is the defect: merge reference data into one
metadata line or switch to stacked cards.

Inputs keep the 16px phone and 14px desktop text. Compare numbers with
`tabular-nums`, right alignment, and shown units. Format
money, numbers, dates and file sizes with the shared helpers.

## Layout

`AdminLayout` owns the main landmark and the outer inset: 16px on phones, 20px
from `sm`, 32px from `lg`. The sidebar is 288px, or 80px collapsed, under a 64px
sticky top bar. Feature panels add no second `<main>`, page padding or narrow
page width.

Spacing steps are 4, 8, 12, 16, 20, 24 and 32px. Use 20px between major sections,
16px inside a group, 8px between a label and its control, and 16–24px panel
padding. More space sits above a heading than below it. Tight groups, clear gaps.

Order each screen by urgency: context, what needs attention, the working records,
then history and reference.

Use one project selector per workspace, with project code and name visible. When
the project changes, validate or clear every dependent design-project, task,
record and filter ID. A design project must belong to the selected operational
project, and an old link's identifier is translated at that boundary, never
reinterpreted. Navigation, refresh and redirects keep the project, tab and record.

CM plans and reviews in five areas (Schedule, Quantity and cost, Quality, Safety,
Contracts); BCH executes in three portals grouped by responsibility. Both reuse
the same records and panels instead of copying a workflow.

Breakpoints are 640, 768, 1024, 1280px. The sidebar takes space from `lg`, so
judge a table by the width left for content: when columns cannot fit without
wrapping badges or actions, show cards until `xl`. A table that must stay wide
scrolls inside its own container with readable headings; the page never scrolls
sideways. Each paginated dataset has its own pagination and shows its true total.

### Page anatomy

Chosen on 2026-11-26. New and migrated screens follow these; Dashboard, the
contract list and the contract detail are the reference implementations.

**List.** `PageHeader` (title, total, one line of help, actions with one primary)
→ `ListTabs`, quick views with counts, overdue first after "All"; empty views are
left out → `ListPanel`, one bordered surface holding search, a "Filters" button
with the number in effect, "Columns" where a table has optional reference
columns, the records and pagination.
Filter fields open inside the panel. Do not stack a separate filter card or
summary cards above a list.

**Record detail.** Back link → title with status pill, and the next step as the
only filled button at the top right → one bordered strip of key figures → from
`xl`, working tabs on the left and a 352px column on the right saying where the
record stands and what is next. Below `xl` that column comes first.

**Dashboard.** What is waiting for this person (overdue, due soon, open, most
urgent first) → one quiet strip of company numbers → charts.

**Sidebar.** Groups sit under four headings: My work, Projects, Company,
Administration. "Find a page" searches every page the account can open and
ignores Vietnamese accents.

## Elevation & Depth

Flat by default. Ordinary content is separated by surface tone, spacing and a 1px
border. Shadow belongs only to things that really float: dialogs, drawers,
popovers, menus, select lists and toasts.

### Named Rules

**The Flat Work Surface Rule.** Records, forms and tables stay flat: border and
tone, no glow or blur. Gradient and a soft shadow belong only to `StatTile`, the
page icon and the attention counts; a stat tile that opens a list may lift 2px on
hover. The translucent sticky top bar is the only blur.

**The No Nested Cards Rule.** Express subsections with headings, rows and
separators.

## Shapes

8px for compact elements, 12px for controls, 16px for cards and dialogs
(`rounded-sm`, `rounded-md`, `rounded-lg`; `rounded-xl` equals `rounded-lg`
here). Circles for avatars and full pills for statuses only.

## Components

Firm and easy to hit: every control shows that it can be pressed and that it
heard you.

### Buttons

- **Shape:** 12px radius, 40px high (36px small); 44px on touch screens
  (`pointer-coarse`), including icon-only buttons.
- **Primary:** filled Nicon red, labelled with the action ("Save plan", "Send for
  approval"), never a bare "Confirm".
- **Outline / ghost:** every secondary action. Row actions are quiet; a row-level
  delete is ghost with `destructive` text and still asks for confirmation.
- **Destructive:** the filled variant is only for the final, named destructive
  decision inside its dialog.
- **States:** hover, pressed, focus ring, disabled and busy. While saving, block a
  second submit and accidental dismissal.

Custom tappable elements outside `Button` need their own 44px touch target and a
visible focus ring.

### Status pills

One shared pill: 12px medium text, 2px × 10px padding, full radius, never
wrapping. Tone comes from the status tone map; construction screens use
`ConstructionStatusBadge` and `constructionStatus.ts`. Severity and status are
shown separately.

### Notices

A problem that belongs to a record sits directly under that record, full width, on
a soft tone with an icon: the reason in body text (75 characters per line at most)
and the buttons that resolve it beside it. No coloured side stripe.

### Cards, tables and lists

16px radius, 1px border, 16px padding, no shadow. Tables keep to the columns a
person compares; reference facts go in one metadata line under the record name.

Reuse `PageLoading`, `PageEmpty`, `PageError`, `InlineWarning` and
`ListPagination`. Prefer an in-place skeleton when it keeps the layout stable.
Loading never looks like "no records"; a failed load never looks like an empty
project. Distinguish first use, no matches, missing prerequisite, no permission
and load failure, each with its next step. An expired session sends the person to
sign in. Pagination keeps search focus and resets or clamps the page when the data
changes.

### Progress curves

An S-curve names its sources (CRM master schedule, the selected CM plan and BCH
site actuals), the date axis, completion percentage, plan version and period. Use
different line patterns as well as colour, give the same values in an accessible
table, and leave gaps where actuals are missing; never draw an illustrative curve
or an unapproved formula. Without a baseline, say what is missing and link to the
record that provides it.

### Forms

A persistent label, a unique ID through `FieldScope`, required or optional marked
consistently, and the format rule with an example beside the field before
submission. Validate on the client for guidance and on the server for protection.
A rejected save keeps everything the user typed and says why, in the user's
language. Dialogs close only after the server confirms success, then return focus
to the control that opened them.

### Navigation

Sidebar, breadcrumb, page title and tab use the same business noun. The active
item has a quiet selected surface, a written label and `aria-current`. Hidden
navigation is not authorization.

### Motion

Motion answers an action. It never decorates and never delays work.

| Moment                                | Duration | Properties         |
| ------------------------------------- | -------- | ------------------ |
| Hover, focus, pressed, colour change  | 150ms    | colour, background |
| Menu, popover, select, tooltip        | 150ms    | opacity, transform |
| Dialog and toast enter or leave       | 200ms    | opacity, transform |
| Drawer (sheet) open; close is shorter | 250ms    | transform, opacity |
| Accordion and disclosure              | 200ms    | height             |
| Skeleton while loading                | looping  | opacity            |

Ease out when entering, ease in when leaving. Name the properties you animate;
avoid `transition-all`. Apart from disclosure height, do not animate layout.

**The Reduced Motion Rule.** Under `prefers-reduced-motion: reduce`, movement,
zoom and smooth scrolling stop and changes happen at once. This is one global
rule, not something each screen remembers. A busy spinner may keep turning.

No page-entrance animation, staggered lists, counting numbers, bouncing or looping
decoration.

## Do's and Don'ts

### Do:

- **Do** show project, record, state and next permitted action together.
- **Do** put waiting, overdue and blocked work where the eye lands first.
- **Do** explain a blocked action: the reason and the step that unblocks it.
- **Do** say what is true: a file waiting for Google Drive is not synchronized, a
  lost file is not "synced", missing actual progress is not 0%, a draft plan is
  not a published baseline.
- **Do** keep typed input and decision reasons until the server confirms success.
- **Do** write short, friendly Vietnamese first, with keys in all four languages.
- **Do** check real content at 390, 768, 1024 and 1440px, at 200% zoom, with the
  keyboard, and with reduced motion on.
- **Do** follow the server's authorization, independent-review and
  deletion-impact contracts: permanent deletion shows impact, blockers and file
  consequences and asks for the record code (`AGENTS.md`, Hard-delete policy).

### Don't:

- **Don't** hard-code status colours or use raw palette classes.
- **Don't** use colour alone, or a tooltip or truncation alone, to carry a name,
  reason or prerequisite.
- **Don't** make a row-level delete the loudest thing in a row.
- **Don't** replace business names with internal IDs, abbreviations or raw JSON.
- **Don't** invent records, formulas or counts to fill a dashboard, or call a
  list or chart complete when data is missing or truncated.
- **Don't** close a dialog after a failed action or reset the user's work.
- **Don't** put gradients on records, forms, buttons or table rows, and don't add
  glow, nested cards or entrance animation to work screens.
