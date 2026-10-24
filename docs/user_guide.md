# NICON Platform -- User Guide

Version 1.1

Last Updated: 16 October 2026

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Business Overview](#2-business-overview)
3. [Module Analysis](#3-module-analysis)
4. [User Roles and Permissions](#4-user-roles-and-permissions)
5. [Authentication](#5-authentication)
6. [Public Website Features](#6-public-website-features)
7. [Administration Panel](#7-administration-panel)
8. [Content Management](#8-content-management)
9. [Recruitment Management](#9-recruitment-management)
10. [Contact Management](#10-contact-management)
11. [Internationalization](#11-internationalization)
12. [Email System](#12-email-system)
13. [File Uploads](#13-file-uploads)
14. [API Reference](#14-api-reference)
15. [Data Models](#15-data-models)

---

## 1. Introduction

NICON is an enterprise management platform for a Design and Build construction company operating in Vietnam. The system encompasses the full project lifecycle -- from customer acquisition and sales through design, permitting, construction, acceptance, procurement, finance, and final handover documentation. A corporate website layer provides public-facing content including services, projects, news, activities, recruitment, and contact management.

The platform serves the following audiences:

- **Sales and Business Development**: Manage leads, opportunities, quotations, tenders, site surveys, and customer contracts.
- **Design and Engineering**: Control design documents across three phases (Concept, Basic Design, Detail Design), manage revisions, and issue construction drawings (IFC).
- **Project Management**: Track construction schedules (Gantt), daily site logs, partial and full acceptance inspections, as-built documentation, and defect management (Punchlist).
- **Procurement**: Manage vendors and subcontractors, compare bids, control material BOQ, process material requests, track warehouse inventory, and alert on overruns.
- **Finance and Contracts**: Manage primary contracts, subcontracts, variation orders (VO), project cash flow, and profit/loss reporting.
- **Administration**: Manage website content, handle contact inquiries, process job applications, configure email templates, and maintain multi-language translations.
- **Public Visitors**: Browse the company website for information about services, projects, news, activities, and open positions.

This guide describes the business requirements, module analysis, feature
specifications and user workflows. The API section is a curated operator-facing
reference for public/content endpoints, not a complete OpenAPI contract; use the
running Swagger document and controller/DTO source for the authoritative current
endpoint shape.

The controlling business baseline for this guide is
`docs/Nicon/MoMs/2026-10-06/NICON_Meeting_2026-10-03_Tom-tat-yeu-cau-moi.md`.
Where current behavior differs, this guide labels the difference as an
implementation gap rather than treating it as a changed customer requirement.

For development setup, database management, build procedures, and deployment instructions, refer to `application_developer.md`.

---

## 2. Business Overview

### 2.1 Company Context

NICON operates as a Design and Build construction company based in Ho Chi Minh City, Vietnam. The business model covers the full lifecycle of construction projects: from initial customer engagement and design through permitting, construction execution, quality acceptance, and final handover.

- **Site Name**: NICON
- **Primary Contact**: 028 7300 1976
- **Primary Email**: info@nihome.vn
- **Address**: 92 Đường 56, Phường Bình Trưng, TP. Hồ Chí Minh

### 2.2 System Scope

The platform is organized into eight functional modules spanning the entire project lifecycle:

All eight modules share the same operational hierarchy: one customer (company
or individual) can own many operational projects, and one operational project
can contain many contracts. The operational project code is the traceability
key used from sales through handover. Public website projects and design-stage
projects are separate views and must not be used as substitutes for this key.
When creating an operational project, the system proposes the next project
code. An authorized creator may adjust that code before saving, subject to the
displayed format and global uniqueness checks. The code becomes immutable once
the project has been created.
The CRM menu orders Projects, Opportunities, Contracts, Leads, Customers,
Quotes, Material Rates, Tenders, Capability Documents, Surveys, and Project
Reports. Permissions hide items that the current user cannot access.

| Module | Scope | Objective |
|--------|-------|-----------|
| 1. CRM / Sale / Contract | Customer management, lead tracking, quotations, tenders, site surveys, customer contracts | Manage customer acquisition and business development flexibly |
| 2. Design Management | Three-phase design control (Concept, Basic Design, Detail Design), revision management, IFC issuance | Control technical documents and prevent discrepancies before construction |
| 3. Permitting | Legal document checklists, permit application tracking | Ensure legal compliance and administrative clearance |
| 4. Construction and Acceptance | Gantt scheduling, daily site logs, partial/full acceptance, as-built records, punchlist | Manage site operations, quality control, and schedule adherence |
| 5. Procurement (Cung ứng) | Vendor management, bid comparison, BOQ control, material requests, warehouse management, material alerts | Manage suppliers, subcontractors, and project materials |
| 6. Finance and Contract | Primary contracts, subcontracts, variation orders, cash flow tracking, P&L reporting | Maintain financial transparency and contract control |
| 7. Digital Assets | Automated project folder structure, Google Drive integration, document digitization | Organize, secure, and provide rapid access to project files |
| 8. Dashboard and Analytics | Progress reports, acceptance reports, financial dashboards, procurement reports, risk alerts | Provide management oversight and early risk detection |

Within a Design project, the design-specific manager is labelled **Design
Manager** (**Chủ nhiệm thiết kế**). This role is distinct from the Project
Manager of the shared Operational Project; existing access rules remain
unchanged.

#### 2.2.1 Customer Reference Pack and Status Labels

The repository retains the NICON source pack under `docs/Nicon/`: the full PRD
and workflow DOCX files, representative design/permitting/construction schedule
PDFs, real BOQ workbooks, and dated meeting/BA records. These artifacts explain
the target operating model and provide realistic import examples.

Use the following labels consistently in this guide:

- **Implemented** means the current API, persistence, permission checks, and UI
  support the described workflow.
- **Partially implemented** means a usable subset exists and the missing scope
  is named explicitly.
- **Target/planned** means the customer requirement is documented but must not
  be presented to users as available.
- A sample schedule or BOQ demonstrates real data shape only. Sheet names,
  colors, dates, customer names, and layout are not fixed product rules.

When the source pack conflicts internally, the dated meeting decision is used
for the working target. For example, the newer meeting decision of one approver
per design task plus CC supersedes the older PRD's generic multi-tier wording.

Additionally, the system includes cross-cutting capabilities:

| Capability | Description |
|------------|-------------|
| Multi-Language | Vietnamese, English, Chinese, and Japanese across the current interface; the original BreakTask only requested Vietnamese and English |
| Responsive Design | Support for desktop and mobile devices |
| Role-Based Access | User creation, role assignment, and menu-level permission control |
| Workflow Approval | Selected approval flows exist; a unified NICON-approved matrix for all dossier types remains open |
| Notifications | In-app and email notifications triggered by system events |
| Audit Log | History for key workflows; complete coverage of file downloads and actions remains target work |
| Global Search | Module-level search and filters exist; cross-module search and saved presets remain target work |
| Master Data | Configurable shared references exist; project-specific templates and the approval matrix remain open |

### 2.3 Customer Task Breakdown Baseline

`Nicon_BreakTask_v1.xlsx` is retained as customer planning evidence, not as an
automatically current delivery estimate. The progress review on 20 October 2026
uses source revision `d884b3c6`. **Tiến độ hiện tại** (`H2:H108`) and **Nicon**
(`B4:B348`, excluding section headings) use `Implemented`, `Partial`, `Target`
and `Open decision`, with their status dropdowns aligned. Their **Ghi chú** cells
identify the delivered subset and remaining gaps. **Cập nhật Demo Ngày 2506**
now records the current assessment of its original requests without rewriting
those requests. The source-scope sheets, estimates, formulas and workbook layout
are preserved. These classifications describe repository delivery, not customer
deployment or UAT; the workbook still does not provide a complete numeric manday
column.

The previously published task-count and frontend-effort totals cannot be
reconciled to the current workbook and are therefore withdrawn. Do not use a task
count or effort total for commercial or delivery planning unless the workbook
version, included rows, formula, backend/QA/deployment scope, assumptions, and
approval date are recorded. Interactive Gantt, workflow builder, offline mobile,
multi-sheet BOQ import, and floor-plan annotation require separate discovery or
prototype estimates.

The earlier guide's page-level day estimates have been removed: the workbook's
main `Nicon` sheet has an `Estimate(manday)` column but no numeric values to
reconcile them against. A dash in a page table means that page has not been
classified separately; use the module status and source-backed workflow above.
Re-estimate remaining work only after the open business decisions are recorded.

### 2.4 Implementation Status

The platform is being developed incrementally. The following components are currently implemented:

| Component | Status |
|-----------|--------|
| Authentication (registration, login, OTP, password reset) | Implemented |
| Content management (news, activities, projects, services, slideshow) | Implemented |
| Recruitment (job positions, applications) | Implemented |
| Contact management (submission, admin reply) | Implemented |
| Multi-language translations (Vietnamese, English, Chinese, Japanese) | Implemented |
| Logo management (clients, partners, suppliers) | Implemented |
| Process document management | Implemented |
| Email system (OTP, notifications, contact replies) | Implemented |
| File upload (images, CV documents) | Implemented |
| Site settings and email template configuration | Implemented |
| In-app admin notifications | Implemented |
| CRM module (customers, leads, opportunities) | Implemented, including configurable Lead segments and the controlled five-step sales pipeline |
| Central operational projects (customer, sales, contract, and design rollup) | Implemented as the target cross-module root; several legacy construction and closeout records still use `DesignProjectId` and require controlled migration |
| Quotations, capability documents, and tenders | Implemented, including governed material-rate pricing, localized Excel quotation workbooks, and versioned Tender estimates |
| Site survey digitization | Implemented, including project routing, structured conditions, CSV import, media/geolocation, Drive sync, and PDF export |
| Customer contracts, appendices, attachments, and variation orders | Implemented |
| Design management | Partial — projects, Concept options with typed revisions, files, client feedback and signed acceptance; Basic/Shop/IFC, Project Documents with Drive sync, linked Team/Schedule, one-approver task completion, CC, internal comments linked to a dossier or one submitted file, Design Manager consolidation and client decision gates are delivered; native 3D viewing, in-drawing markup and full MS Project import remain target work |
| Permitting checklists | Partial — lifecycle, documents, risk filters, versioned template authoring/independent review and manual application with mandatory items are delivered; CM/NICON approval of actual type/scale catalogues and automatic template matching remain open |
| Construction management (tasks/Gantt and site diaries) | Partial — core tasks/diaries plus CM preparation/readiness, BCH resource/QA-QC/QS workspaces and Budget Baseline references are delivered; editable WBS/S-Curve baseline, reproducible delay alert, portfolio review and offline/reconnect remain target work |
| Acceptance and handover | Partial — core records and CM review of QA/QC/QS are delivered; upstream/downstream QS payment eligibility, three-section dossier and complete handover gate remain target work |
| Punchlist management | Partial — core records are delivered; CM overdue supervision and expanded field evidence remain target work |
| HSE violations | Partial — Operational-Project-scoped lifecycle, evidence paths, correction, confirmation/closure and concurrency are delivered; the BCH-versus-CM approval/delegation matrix remains open |
| Procurement vendor management | Implemented |
| Procurement BOQ, material requests, and warehouse | Implemented for connected use — BOQ, Material Request, warehouse list/create/edit/detail, stock, allocation, posting, and reversal are live; offline queue/synchronization remains a cross-cutting platform dependency |
| Finance module | Partial — shared contracts, variation orders, receivables, downstream payment requests and accounting-period corrections are delivered; two-direction payment dossiers and complete cash-flow/P&L reporting remain target work |
| Google Drive integration | Implemented for current Operational Project file sources; deployment activation and live credentials are required. Survey and CRM Pre-Design remain separate configurable categories pending an approved topology migration |
| Dashboard and analytics | Partial — operational dashboard exists; full cross-module reporting remains target work |
| KPI | Partial — 19 metrics across six current scorecard groups are delivered; the complete NICON 11-position framework is not yet delivered |

BGD or SUPER_ADMIN can permanently delete only a failed/cancelled design
dossier. Open the deletion impact dialog, resolve every file, IFC or shared
review blocker, then type the exact dossier code shown by the dialog. The plan
expires when related data changes, so refresh the impact before retrying. Shop
Drawing bulk deletion is not available because each dossier must be reviewed
and confirmed separately.

---

## 3. Module Analysis

This section provides detailed functional analysis for each module based on the NICON business requirements.

### 3.0 Operational Project Workspace

Open **CRM → Operational Projects** to create or find the shared project before
working across modules. The list shows customer, lifecycle status, and counts
of linked opportunities, quotations, and contracts. The detail view provides
direct links to those records and to the design workflow.

Search by project code or name updates results automatically after a short
pause in typing. The search field retains focus while results load, and the
status filter remains available even when a search fails or returns no matches.

The detail view also includes a **Payment timeline**. It combines payment
milestones from every Contract linked to the same operational project and shows
the Contract number on each entry so similarly named milestones remain
distinguishable. The planned date comes from the milestone due date. The
actual date comes from the date confirmed on the Contract payment milestone and
is never estimated from a technical update timestamp. When marking a milestone
**Paid**, the system proposes today's date; users with Contract edit permission
can choose another date or correct it later. Returning the milestone to
**Pending** or **Requested** clears the actual date. Historical rows can remain
empty until a user confirms them. Contract changes appear automatically because
the timeline reads the milestone records directly instead of maintaining a copy.

A new project starts in **Planning**. It can move to **Active**, **On hold**,
**Completed**, or **Cancelled** according to the displayed lifecycle. Only an
empty Planning project can be deleted; cancel a project that already contains
business history. The customer cannot be changed after records are linked.
Operational Projects are not archived or restored as a whole. **Completed** and
**Cancelled** projects remain visible to authorized users so their Contracts,
transactions, files, and audit history stay traceable. Archive actions within a
document workflow affect only that document and do not archive its project.

Users normally see projects they created or manage. Portfolio roles with the
`operations.projects.view.all` permission see all projects. Creating or editing
requires `operations.projects.manage`.

Changing an existing project code is a separate governed action. A user with
`operations.projects.rename` opens the project detail, selects **Change project
code**, enters the new 2–40 character code and a reason, then submits the current
record version. The database code and immutable change history are saved
together. If the project already has Drive folders, the detail page shows the
background folder-rename status. A failed rename can be queued again without
creating another folder or changing its Drive ID. A stale screen is rejected;
reload before retrying. The project list also finds a renamed project when you
search for its former code; normal project visibility rules still apply.
Completed or Cancelled projects cannot be renamed until reopened through the
governed project lifecycle.

The detail view's **Project documents** section provides the project-wide file
catalog. Users with view permission can inspect synchronization status,
download private content, and open an available Drive copy. Users with manage
permission can upload files up to 100 MiB into a category supplied by the
server, retry an eligible synchronization, classify an unknown Drive import,
confirm **Keep both** for concurrent changes, and delete manual uploads or
Drive imports. Files owned by Quote, Contract, Survey, Design, Permit,
Acceptance, As-Built, or Handover records must be replaced or deleted in that
source screen; the generic catalog does not bypass the business record.

#### 3.0.1 Department work packages

Open **CRM → Work packages**. Use **Project / contract work** for work attached
to the shared Operational Project, or **Internal work** for activities such as
company training that have no customer, project, or contract.

For project work, the Opportunity owner or Project PM selects the shared
project, optional contract, receiving Procurement/Finance department,
department lead, and planned dates. Design and Construction work stays in its
specialist screen rather than creating another project wrapper. The Project
and Opportunity screens also provide **Assign department work** shortcuts.
Adding a contract later does not create or move the Project.

For internal work, select the lead department and every additional
participating department. Each task then selects its responsible department;
the assignee list is limited to active members of that department.

An authorized package manager can select **Reopen: In progress** on a Completed
package to add work again. Existing task completion, verified KPI and approved
finance checklists stay intact and retain their individual edit guards.
Cancelled packages cannot be reopened.

**Delete** opens the shared permanent-deletion preview. Inspect Delete, Unlink
and Block groups, then type the package code. Parent Projects and Contracts are
preserved; independent permit/tender checklists are unlinked. Used pricing or
finance evidence, legal activation and remaining files must be resolved first.
Deleting verified tasks also removes their contribution to task-based KPI
calculations; review the preview carefully before confirming.

The receiving department lead opens the package and creates tasks. Enter a WBS
code, assignee from the same department, dates, inputs/resources, optional
predecessors and CC followers. If the task should contribute to KPI, select an
active compatible KPI definition. Every Project task and every KPI-linked task
needs a verifier different from the assignee; the department lead is the
default when no approver is selected.
Start the package before assignees update progress. A dependent task cannot
start until every selected predecessor is complete.

The assignee receives a notification linking directly to the task, records a
progress percentage from 1–99%, and uses **Complete** at 100%. A Project task
then remains **Completed** pending independent verification, whether or not it
has a KPI. The verifier can confirm it or return it to **In progress** with a
reason. Internal tasks without KPI do not require verification. Only a
KPI-linked task that reaches **Verified** is eligible for KPI calculation.
Close a Project package after every task has been verified.

Deleting an Operational Project includes its owned work packages in the impact
preview. Deleting an optional Contract or source Opportunity only unlinks that
reference; the work package remains under its shared Project.

#### 3.0.2 Pre-contract department work

Before a quotation or a tender goes out, the contributing departments record
their part of the package on a project work package instead of a separate
screen. Set the package's **Business purpose** to the one that matches the
department's job; the purpose ties the package to the source quotation or
tender and controls which actions the page offers.

**Cung ứng (Procurement) — input prices.** Procurement runs first and supplies
the supplier and subcontractor prices that Sales needs to build the customer
quotation. The purpose is **Cung ứng báo giá đầu vào** (Procurement input
pricing). The department records each offer with supplier/subcontractor,
reference, currency, valid-until date, and priced lines (item, code,
description, unit, quantity, unit price), and the total is shown in the
localized number format. A draft offer can be edited or removed; submit it when
ready. The offer follows **Draft → Submitted → Approved/Rejected**, and only
**Draft** or **Rejected** offers can be edited or resubmitted. The approver must
be a different user than the author. Only an **Approved**, unexpired offer from
the quotation's own Operational Project can be chosen as the quotation's input
price; once chosen, the accepted total is snapshotted onto the quotation, so
editing the offer afterwards does not change what was quoted.

**Tài chính – Kế toán — commercial review.** After Sales approves the quotation,
Finance reviews contract conditions, guarantees, and payment terms. The purpose
is **Tài chính rà soát thương mại** (Finance commercial review), linked to that
quotation. The reviewer ticks the checklists, records findings, and a different
user performs the independent approval. Sales can open **Send to customer** only
after the quotation belongs to a shared Operational Project and its Finance
commercial review is **Approved**; a quotation with no project is asked to be
attached to one first. The review records which quotation version it covers, so
changing a price after approval returns the quotation to **Draft** and it must
be re-approved by Sales and reviewed again by Finance before it can be sent.

**Tài chính – Kế toán — tender review.** Finance reviews the financial dossier
of a tender submission pack before the package is submitted. The purpose is
**Tài chính rà soát hồ sơ thầu** (Finance tender review), linked to the tender.
A tender can move from Preparing to Submitted only when, in addition to a ready
checklist and an approved estimate, it carries a shared Operational Project and
an **Approved** Finance tender review.

The signed-BOQ control that Procurement performs is a separate, post-contract
step and does not replace input pricing. The department label shown for
Procurement in the platform is **Cung ứng**.

### 3.1 Module 1: CRM / Sale / Contract

**Objective**: Manage customer acquisition and business development flexibly.

#### 3.1.1 Customer Management (Customer/Account)

Maintain individual and corporate customer records. Support multiple contact persons per customer. Link customers to leads, opportunities, quotations, contracts, and projects. Track customer status through the lifecycle: potential, in-transaction, signed, inactive. Store legal documents (tax ID, address, legal representative). Maintain a customer care timeline with callback and visit reminders.

| Page | Functions |
|------|-----------|
| Customer List | Search, filter, navigate to detail |
| Customer Create/Edit | Create and update customer information |
| Customer Detail | Overview, contacts, care history, related records |

#### 3.1.2 Lead Management

Track inbound leads with source classification (marketing, referral, etc.). Assign leads to sales personnel. Maintain consultation history. Convert qualified leads to opportunities.

Converting a lead creates (or reuses) the customer and opens, in one step, a new
opportunity together with a **Planning** operational project for that customer.
Surveys, quotations, and contracts raised later already carry this project, and
the project page lists the opportunity and its quotations. Converting into an
existing opportunity creates no project. Undoing a recent, untouched conversion
removes the auto-created project only while nothing else is attached to it.

Each Lead has one segment selected from managed `lead_segment` master data. The
seeded values are Unclassified, Residential, Commercial, and Hospitality;
administrators can maintain this taxonomy centrally. Lists and forms support
segment filtering while consultation activities retain the care history.

| Page | Functions |
|------|-----------|
| Lead List | Filter by source, status, assigned sales |
| Lead Create/Edit | Create lead, update info, assign sales |
| Lead Detail | Lead info, consultation history, convert to opportunity |

#### 3.1.3 Opportunity Management

Manage sales opportunities with deal value, probability, and stage tracking.
Every new opportunity starts at **Approach** and follows the sequence
**Approach → Survey → Quotation/Tender → Negotiation → Contract signed**.
Users cannot skip or move backward between these stages. **Lost** is an
alternate terminal outcome from an open stage and requires a reason and note.
Moving to **Contract signed** requires a linked Contract with a signed date and
a non-draft, non-cancelled status. The API retains the stable enum values
`Prospecting`, `Qualification`, `Proposal`, `Negotiation`, and `Won` for stored
data and client compatibility; the interface displays the customer terms.
Historical records are converted once during deployment: valid contract-backed
Won rows remain Contract signed, unsupported Won rows become Negotiation, and
Lost rows receive complete controlled reason metadata. There is no separate
legacy workflow or reconciliation queue. The final qualifying Contract of a
Contract-signed Opportunity cannot be cancelled, unlinked, invalidated, or
deleted unless another qualifying Contract remains.

The Opportunity detail shows each linked Contract to users who can view that
Contract. Select a Contract number to open its detail page and upload the
signed scan or update its status.

Price the deal before signing: the opportunity detail offers **Create quote** at
every open stage (and on Contract signed while no winning quote is set). The
**Quotes** tab lists each quotation of the opportunity with its status, version,
total, and the winning mark; an empty tab offers the same action.
On the Quotes page, the new-quote Opportunity picker searches the complete
server-side Opportunity set by ID, name, customer, or owner. Each result shows
the Opportunity ID, customer, stage, estimated value, expected close date, and
owner. Lost Opportunities and Contract-signed Opportunities that already have
a winning quote are not offered for selection.

Design may start before the Contract. Sales can link an Operational Project of
the same customer when creating or editing an Opportunity. In the Opportunity
flow, creating the shared Operational Project requires both CRM Opportunity
management and Operational Project management permissions. A PM can manage an
assigned project but cannot create this Sales record. In the Opportunity
detail, **Link an operational project** opens the editable selector, while
**Create an operational project** opens the CRM Project form with the customer
selected. After creating the project, return to the Opportunity and save the
preselected project to confirm the link. On a non-Lost Opportunity,
**Create design project** beside **Create quote** opens a form to
confirm the project and design name before saving. If the Opportunity has no
Operational Project, choose an existing one in that form or create one through
**CRM → Projects**; the new project is selected on return. Saving links the
Operational Project to the Opportunity and opens one Design Project at
**Concept**, without requiring or creating a Contract. Repeating the save
returns the existing Design Project instead of creating a duplicate. Design
roles receive the Concept after project assignment. Direct creation from the
Design list requires Design Project management, CRM Opportunity management,
and Operational Project team management, so a Design-only role cannot start
the handoff.

For an Opportunity that is not Lost, **Create contract** opens a draft contract
with its customer and project. A quotation is not required when the customer
has agreed on the value. If an approved, available quotation exists, the form
also prefills that quotation and its price. Approved/Sent quotations must be
unexpired; Customer-approved quotations remain eligible. A tender estimate is
not itself a CRM customer quotation.

| Page | Functions |
|------|-----------|
| Opportunity List | Filter by stage, sales, status |
| Opportunity Create/Edit | Create/update value, probability, stage |
| Opportunity Detail | Track info, documents, win/loss result |
| Pipeline Kanban | Kanban board by sales stage |

#### 3.1.4 Direct Quotation

Create quotations using an approved material-rate revision or a preliminary
BOQ. **Material norms & rates** has separate **Investment rate** and **BOQ**
pages. For catalogs without category field settings, download the localized Excel form from the intended page, open the
**Entry** sheet, and retain its column names and order. Investment-rate forms
use material code/name, unit, norm per m², unit rate, and waste percentage; BOQ
forms use item code/name, unit, quantity, and unit price. The workbook provides
number formats, an automatic amount formula, and a separate guide with examples;
the customer does not need to delete the examples or convert the file to CSV.
Return and import the same `.xlsx` file into a Draft revision. UTF-8 CSV remains
available for system compatibility. Import is atomic. Investment rates calculate
`NormPerSqm × UnitRate × (1 + WastePercent / 100)`; BOQ lines calculate
`Quantity × UnitPrice`. In a Draft Investment-rate or BOQ revision, authorized
users may also add items manually and edit or delete individual imported or
manual rows. These actions recalculate the revision total immediately, avoiding
a full-file re-import for small corrections. Approved, Rejected, and Retired
revisions remain read-only. Approve non-overlapping effective revisions before
use.
**BOQ with category-specific fields.** Six initial category forms are based on
customer workbooks: Design/Permit, Factory/MEP/Fire, Serviced Apartment Fire,
CSA/Building Systems, Integrated Factory and Turnkey (manifest:
`nihomebackend/Data/Seeds/boq-categories.json`). Select the intended category
before entering data. For a new category, open **Configure fields** before its
first revision, copy an existing structure and adjust sheet/field names, text
or number types, required values, length and decimal limits. Core pricing fields
remain protected. All names require Vietnamese, English, Chinese and Japanese.
Once a revision exists, changing the field set requires another category.

Create a Draft revision, choose a sheet and use **Add / edit sheet entry**, or
use **Download Excel form** in the revision's **Prepare and import rates**
panel. The download is beside the instructions, above the file drop area;
manual row entry has its own panel. Drop or choose the completed `.xlsx` file
(up to 20 MB), check the displayed filename, then select **Validate file**.
Fill data from row 3 of that downloaded workbook;
retain sheet names, visible headers and hidden markers. Enter numeric Excel
cells rather than numeric text or formulas. **Validate file** shows the entries;
**Confirm Draft replacement** replaces all rows only after confirmation. An
empty file, wrong category/sheet/columns, invalid number, formula or stale
revision is rejected. Reload a stale revision instead of overwriting it.
The configured form cannot accept the old generic CSV/five-column workbook.

Different sheets may reuse a line code; the code must be unique within its
sheet. Fields shown and saved always belong to the selected category and sheet.
Reference sheets (such as project scale/material specifications) do not feed
prices. Enter checked total quantity and total unit price explicitly; stored
component prices and dimensions do not automatically calculate them. Approved
catalog prices continue to feed Quote snapshots; this does not turn a Quote BOQ
into an approved Execution BOQ allowance. Configured catalogs are deactivated
rather than permanently deleted. The source workbooks are discovery evidence;
the new downloads are normalized safe entry templates, not copies of their
external links, formulas or full presentation layout.

For unconfigured legacy catalogs, authorized users can select one or more catalogs for permanent deletion. This
irreversible action removes every revision and rate line in each selected
catalog. A catalog already used by a Quote or Quote version history cannot be
deleted, preserving the quotation's pricing source and audit trail. If only
some selected catalogs can be deleted, the unsuccessful catalogs remain
selected and the page lists each catalog with its reason so the user can review
or retry it.

To apply the catalog on **Quotes**, create a quote with the **Unit Cost** method,
enter the area, select an active catalog, and choose a date covered by an
Approved revision. The system resolves the total catalog rate per square metre
automatically. Only Investment-rate catalogs are available for this method.
Authorized overrides require a Vietnamese reason and remain visible in versions
and the Excel quotation. Downloadable quotation workbooks are localized in
Vietnamese, English, Chinese, or Japanese. They preserve customer and
Opportunity context, BOQ or unit-cost pricing, discount, VAT, and the final
total as typed spreadsheet values.

If no active catalog exists, the quote form shows the three setup steps instead
of an empty list and links to the matching catalog page. That page opens the
create dialog and shows **Back to the quote** until the revision is approved;
the link returns to the same quote form. Users without catalog permission are
asked to contact a QS or the sales manager. A BOQ quotation can still be
entered manually without a catalog only for a quote-specific exception;
reusable BOQ data belongs in the BOQ catalog.

For a BOQ quotation, either select an active BOQ catalog and pricing date or add
rows manually. Applying an Approved effective BOQ revision replaces the current
rows after confirmation, copies its items into the quote, and retains catalog,
revision, and date provenance. The copied name, unit, quantity, and unit price
remain editable without changing the approved catalog revision; the quote marks
this source as an editable catalog reference rather than an exact catalog price.
BOQ quantities support up to four decimal places and unit prices up to two. To
add quote-specific exception rows, open **Enter multiple rows** and enter each
value under its explicit code, item, unit, quantity, and unit-price column. Add
or remove rows inside the table and correct every validation error before
confirming. This action does not import a workbook or update the catalog. The
form previews each calculation basis and the
subtotal, discount, VAT, and grand total before saving. Blank rows, quantities
at or below zero, negative prices, invalid percentages, and values above the
supported accounting range are rejected in both the browser and API. Reopening
or editing a BOQ preserves its item set; submitting and approving follow the
same quotation workflow and project/owner permissions. Retrying the same create
request with its idempotency key does not create a duplicate quotation.

Prepare reusable BOQ data under **Material norms & rates → BOQ**, not from a
quotation detail. Create or select a catalog and a Draft revision. For a new
workbook, download the standard NICON form. For an existing NICON workbook,
choose **Import multi-sheet BOQ**, preview every sheet in workbook order, select
only detail sheets, and verify the header row plus code, description, unit,
quantity, and unit-price columns. Leave cover and summary sheets unselected.
The wizard shows sheets in a compact list and displays actual sample rows for
the active sheet. Confirm columns by their Excel letter and header text; raw
column numbers and template maintenance are available under **Advanced settings**.
A detail sheet may contain section headers, repeated headers, and subtotal rows;
the preview excludes these structural rows from its expected-line count. A row
that looks like a priced detail item but lacks a valid unit, positive quantity,
or non-negative unit price remains an error and must be corrected in the source
workbook or excluded through the selected sheet/mapping.
A recurring structure can be named and saved as a mapping template; materially
different workbook structures can use separate templates without changing the
standard form.

Confirmation atomically replaces the selected Draft revision's lines and stores
the source filename, SHA-256 hash, mapping, actor, and time. A changed file,
invalid mapping, duplicate code, or invalid row leaves the revision unchanged.
Approve the revision through the normal catalog lifecycle, then apply that
approved revision to a BOQ quotation. Workbook import never edits a quotation
directly.

| Page | Functions |
|------|-----------|
| Quotation List | Filter by customer, opportunity, status |
| Quotation Create/Edit | BOQ-based or rate-based pricing, discount, VAT |
| Quotation Detail | Version control, internal approval, Excel export |
| Excel Export | Customer-ready workbook with priced lines and totals |
| Version Comparison | Diff view between quotation versions |

#### 3.1.5 Tender Management

Manage tender packages with deadlines and preparation status. First record the
invitation receipt date (before the submission deadline), invitation source,
receiving user, and, when available, its customer reference and portal URL.

The **Tender win rate** panel above the list shows wins divided by all Tenders
first submitted in the chosen period. Select inclusive submission dates (Vietnam
time), Customer and Preparer, then **Apply**. It starts with the current Vietnam
calendar year. Supplementary submissions count once; pending results and
cancellations after submission stay in the denominator. For example, 3 wins out
of 10 submitted Tenders is 30%, even if 5 still await a result. No submissions
shows an empty state, distinct from a measured 0%.

**View included tenders** opens the matching paginated list below. Its banner
identifies the period. List status/search filters may narrow those rows without
changing the chart. **Clear report filter** removes the submission-period and
preparer restriction. Changing the list Customer also leaves reconciliation
mode. Report filters are independent of normal list filters and change results
only after **Apply**; **Refresh** reloads the applied report. Results use current
outcomes, so older periods may change when results arrive. Update time is UTC+7.
An amber notice discloses records without first-submission evidence across the
selected Customer/Preparer regardless of period; they are excluded until their
history is verified. View permission matches Tenders; locked KPI snapshots are
separate and remain unchanged.

Invitation files can be uploaded from the Tender detail page. The checklist starts with seven standard dossier rows;
tender drawings are optional by default. Users may add package-specific rows,
choose whether each row is mandatory under the invitation, or mark an optional
row **Not applicable** with a reason. Only empty package-specific rows can be
deleted; standard rows remain for audit. A user with Tender management
permission can assign each checklist item to an active user whose
role can manage Tenders and set an internal deadline from today through the
Tender submission date. A changed assignment sends the assignee an in-app
notification linked directly to the Tender; saving the same assignee again does
not create a duplicate notification. The **Capability profile** checklist item
must use an existing document from the shared capability-document library;
project-specific items such as BOQ, method statement, and tender drawings accept
a direct upload. A row can be marked **Done** or **Submitted** only after its
required evidence is attached; submission rechecks every row and rejects a
shared-library reference on project-specific items. The selected file is
previewed securely inside the Tender detail page, and every replacement remains
in evidence history with its checksum. Moving a row to **Submitted** is an
independent verification: the verifier cannot be the user who completed it.
The **Readiness** panel lists each blocking condition and its correction path.
Submitting records the channel and optional receipt reference and preserves an
immutable numbered snapshot with the exact evidence revisions, approved estimate,
completed plan, approved Finance review, submitting user, and timestamp. If the
buyer requests additional material, users may add a new evidence revision and
create a supplementary submission; earlier snapshots and the first submission
time never change. Checklist changes are locked after **Won**, **Lost**, or
**Cancelled**. The **Tender plan** tab uses the shared
cross-department work-package workflow: create one package for each responsible
department, then define its task owner, independent verifier, planned start/end,
dependencies and progress. The approximately one-month duration described by
NICON is planning guidance, not a hard system limit. Tender-plan packages use
the dedicated `TenderPlanning` purpose, so Sales can coordinate every active
department without duplicating that department's specialist design, legal,
construction, procurement, or finance records. Their package and task dates must
fit before the Tender submission deadline. A Tender-plan task can be independently
verified without attaching it to a KPI; KPI linkage remains optional.

The **Estimate** tab provides a customer-fillable UTF-8 CSV template containing
item code, description, unit, quantity, internal unit cost, bid unit price, VAT,
and note. Each valid import atomically creates a hashed Draft revision and
calculates cost subtotal, bid subtotal, VAT, and grand bid total. Submit the
revision for approval; authorized users approve or reject it with a reason.
The Tender can move from Preparing to Submitted only when at least one active
Tender-plan package exists, all active plan packages are completed after their
tasks are independently verified, every required checklist item is ready with the correct
shared or tender-specific evidence, an approved
estimate exists, and the Tender carries a shared
Operational Project with an approved Finance tender review of its financial
dossier (see 3.0.2). Submitting requires the dedicated Tender submit permission;
recording Won/Lost remains a separate result permission. Won and Lost are available only from
Submitted. When marking a tender Won, choose either **Create an opportunity from
this tender** (default) or an active existing opportunity of the same customer
and Operational Project. Either path continues the opportunity in Negotiation;
terminal or different-project opportunities are rejected. A created opportunity
carries the tender name, is valued at the latest approved bid estimate, and
reuses the Tender's existing Operational Project. The result panel links to the opportunity and offers **Continue
negotiation and create contract**; contract creation remains a deliberate user
action after commercial negotiation. Tender codes `TD-YYYY-NNNN` continue from the highest code in use,
so deleting a tender never blocks creating new ones.

| Page | Functions |
|------|-----------|
| Tender List | Status, deadline tracking |
| Tender Create/Edit | Tender package setup, invitation details |
| Tender Detail | Document checklist, preparation progress, results |
| Capability Documents | Repository CRUD |

#### 3.1.6 Site Survey Digitization

Record site surveys linked to opportunities or projects. Capture photos and
videos from the application, upload files, record technical notes, capture
time, and store latitude/longitude coordinates. The Conditions tab records
right-of-way, elevation, and managed infrastructure statuses through responsive
editors or an atomic UTF-8 CSV import. Required access-width and site-elevation
rows use metres; the generated Survey PDF includes all structured conditions.
Access is controlled by
`crm.surveys.view` and `crm.surveys.manage`; the default roles grant Sales both
permissions and PM view-only access, while administrators can assign them to
other roles.

**Connecting the current Google Drive integration:**

1. Ask the system administrator to authorize the approved Google Drive account using OAuth 2.0. Do not provide or store its password in Nicon.
2. While signed in as that account, create the target folder in **My Drive** or an approved Shared Drive and provide its folder URL to the administrator. The ID is the value after `/folders/` in the URL.
3. A user with `system.settings.manage` opens **Settings > Google Drive**, enters and saves the OAuth client identity, write-only client secret, callback URL, root folder, deployment identity, folder paths, and synchronization settings.
4. The same user selects **Connect Google Drive**. Nicon opens Google's sign-in and consent screen in a popup; the user chooses the approved account and grants access. The popup closes after authorization. No OAuth secret or refresh token is copied into an application settings file.
5. Confirm that the authenticated account and status show **Connected**. Upload a small test image; the media card changes from Pending/Processing to Synced and displays a Drive link. Use **Check connection** if it remains pending.

To change accounts, select **Disconnect** and then **Connect Google Drive**, or select **Switch Google account** to disconnect first and immediately open Google's account chooser. If the replacement sign-in is cancelled or fails, Nicon remains disconnected. Disconnecting removes Nicon's stored access but does not sign the user out of their broader Google browser session.

Every Survey persists an Operational Project and stores media in that project's configured `01_Khao_sat` category. Opportunity, project, and customer relationships must agree. Reassigning a Survey stages current media into the new project. Deployment backfills historical Surveys from their linked Opportunity and aborts atomically if any row cannot be routed; unrouted Surveys are not supported at runtime. Files added or changed directly in a managed project folder are reflected in the Nicon catalog. Deletion through Nicon moves the Drive replica to trash so it remains recoverable. `Read only` means the OAuth account can see the folder but cannot add files, and `Invalid folder` means the configured ID is not a live folder. The worker uploads only while the connection is `Connected`, makes at most three claims per file, and rejects retry or deletion while a file is actively synchronizing.

| Page | Functions |
|------|-----------|
| Survey List | By lead, opportunity, or project |
| Survey Create/Edit | Create survey record, update info |
| Survey Detail | Notes, checklist, photo/video/file upload, Drive sync |

#### 3.1.7 Customer Contract Management

The information tab includes **Contract execution progress** with three separate
sections. **Work** counts directly linked work packages and their tasks within
your viewing scope; Completed and Verified are separate states, and cancelled
packages do not contribute tasks. **Acceptance / QS** shows directly linked QS
dossiers for the Contract's direction, including separate history, pending CM
review and effective approval counts. Superseded/reversed records are not current
approvals. These counts are not a combined Contract completion percentage.

Select a recent source to inspect its work package or QS dossier. A preview lists
up to ten recent sources; totals include all visible qualifying records. The QS
workspace shows the selected project and Contract filter; Reset removes that
filter, and changing project clears it. **Payment** reports the existing schedule
and milestone statuses using the base Contract value, not cash actually received
or spent. **View payment schedule** opens the existing payment tab. Restricted
sources display an access/context message rather than misleading zero counts.
Use **Refresh** to reload current source states or **Retry** after an error.

In an Upstream Contract's information tab, **Source leads** shows the Leads
converted into its source Opportunity. Select a Lead name to open its detail;
the conversion date is displayed in Vietnam time. Only Leads within your Lead
viewing scope appear. Contract access alone does not grant Lead access. An empty
result does not mean there are no other permission-restricted Leads.

Direct Contracts without a source Opportunity explain that no source can be
traced. The system never guesses a Lead from the customer or project, and this
panel does not add or change links. If loading fails, use **Retry**. Existing
Opportunity, Quote and Project links keep their current behavior.

Create contracts or generate from approved quotations. Manage contract value, scope, and terms. Track payment milestones (advance, stage-based, completion). Manage appendices and variation orders. Track contract status: in-progress, paused, completed.

For a customer (Upstream) contract, open **Customer care** in the Contract
registry to read calls, emails, meetings and notes for that contract. Users with
Contract manage permission and owner/CRM manager scope can add an entry with
1–4,000 characters. The app records the author and time and lists the newest
interactions first; use Previous/Next to read older entries. A failed save keeps
the draft for retry. These entries are separate from the audit Timeline and
Customer-wide history. They do not change contract terms, approve variations,
mark payments or schedule renewal reminders. Finance views can read this history
within their existing Contract scope. Permanent contract deletion includes this
history in its confirmation preview and preserves other contracts' notes.
After the first customer-care entry, the contract's Upstream/Downstream direction
cannot be changed. Keep its current direction to preserve access to its history.

**Automatic renewal/end-date email:** in the same Customer care tab, choose
Renewal or End-date notice, a send date (Vietnam calendar date, today or later),
then **Preview email**. Select a customer contact with a valid email; the primary
contact is suggested first. Confirm the preview to schedule automatic delivery.
The contract must be Upstream, have an end date, and be Signed, InProgress or
OnHold. Contract manage permission plus owner/CRM manager scope is required.
There is one pending schedule per type; cancel a Scheduled entry before replacing
it. The worker checks due/overdue schedules every five minutes while the app runs.
This notice does not renew, terminate or change the contract's business status.

The approved recipient, subject and HTML body are saved with the schedule.
Administrators edit both templates under `/admin/email-templates`; changes apply
to new schedules. If contract identity, end date, recipient or scheduler access
changes, delivery requires review and a fresh preview. A successful SMTP handoff
adds an Email entry to contract customer care; it does not prove inbox delivery.
The screen lists the newest 100 schedules and their delivery states. If delivery
is uncertain, consult the mail administrator before scheduling another email;
automatic retries are disabled to prevent duplicate customer messages. A Sending
entry that persists after a service interruption requires operational review.
After the first reminder delivery attempt starts, the contract direction is
locked, including an uncertain SMTP outcome. A schedule that has not started
delivery does not create this lock. This preserves access to email care history
while SMTP is in flight and after reconciliation.

**Renewal / contract development:** open an eligible Upstream contract's
Customer care tab. Choose **Create follow-on contract** when a new deal value
is agreed, or **Track follow-on opportunity** to manage negotiation and loss
through CRM. Select Renewal or New business and enter a reason (1–2000
characters). The customer stays the same; select an accessible project of that
customer, or use the existing project-creation flow. Confirm all new values and
dates. The contract starts as a separate Draft; the source is unchanged.

If loading the source fails, use **Refresh** in the open form. If the source
changes before saving, review it with **Refresh source contract**, then retry;
entered deal terms and the reason remain in the form. Cancelling with Cancel,
the close button or Escape ends the continuation flow. A subsequent New action
opens an independent form with an editable customer. These recovery actions
require the application page to have loaded; opening uncached pages fully
offline is not supported.

An opportunity-derived contract carries its original source/purpose/reason and
may use an eligible approved quotation. A Lost pursuit keeps its reason and
cannot create a contract; start a new opportunity if the customer returns.
Links on contract care and opportunity detail let authorized users trace the
relationship. The contract view lists the newest 100 accessible records per
collection. Related contracts and continuation opportunities cannot be permanently
deleted; their source history is retained. Existing-contract term changes use
an appendix instead. Email reminders do not initiate renewal automatically.

Every new or edited contract must belong to one Operational Project. The
selected project must belong to the same customer as the contract; contracts
created from an opportunity or quotation inherit that source project's identity.
When a customer has exactly one project, the API may resolve that project when
the request omits the field. It rejects an omitted project when the
customer has no project or multiple possible projects. A project can contain
any number of upstream and downstream contracts of compatible types.

When the selected customer has no suitable project, for example on an
opportunity created before automatic project creation, select **Create a new
project** below the project field. Users with project management permission get
the project created for that customer and selected in the form immediately.

An upstream customer contract may be created without a quotation when the
customer has already agreed on the value. If using a quotation, choose one in
**Approved**, **Sent to customer**, or **Customer approved** status. Approved
or Sent quotations must still be within their validity period; a Customer
approved quotation is a terminal customer decision. A won tender's estimate
does not count as a CRM customer quotation. Design work may start before either
quotation or contract. Downstream supplier/subcontractor contracts cannot link
a CRM customer quotation. Use **Link an approved quote** in the Source section
when an existing contract needs that source added; it must belong to the same
customer, opportunity (when set), and project. A quotation feeds one live
contract.
Deleting a quotation is blocked while a contract references it; review the
deletion-impact dialog to find the linked contract.

The Contract List can be filtered by Operational Project, customer, status,
direction, type, signed-date range, and value range, and can be sorted by
contract number, signed date, end date, value, or last update. Results are
paginated; CSV export includes every matching row across all pages while using
the same access scope as the list. Operational Project
detail shows separate upstream and downstream current values, excluding
cancelled contracts and including approved VO adjustments. Payment progress is
the paid milestone amount divided by the total scheduled milestone amount.
Each summary drills down to the contributing contracts and their payment
milestones. Access to the aggregate remains governed by Operational Project
manager, creator, team-member, and global-view permissions; opening the full
Contract detail additionally requires the normal Contract permission.

Use **CRM → Contract registry** as the single place to create, edit, transition,
attach documents or appendices to, and delete a Contract. Finance screens do
not create a second contract record: **Receivables** filters the same registry
to Upstream contracts, while **Payables** filters it to Downstream contracts.
Selecting either Finance row opens that same canonical Contract ID and keeps a
return link to the filtered Finance list. Accountants can maintain the
receivable/payable schedule, responsible accountant, request state, and actual
collection/payment date there. They cannot edit the Contract header, legal
state, documents, or VO unless another assigned role grants the corresponding
CRM permission.

When opening **New contract**, the system suggests the next Contract Number in
`HD-YYYY-NNNN` format. The number remains editable for an authorized user who
needs to follow an external numbering convention. An unchanged suggestion is
allocated authoritatively when the form is saved, so it may advance if another
contract was created in the meantime. Custom numbers must be unique and cannot
exceed 40 characters.

| Page | Functions |
|------|-----------|
| Contract List | Filter by project, customer, status |
| Contract Create/Edit | Create new or from quotation, update terms |
| Contract Detail | Info, appendices, payment schedule, attachments, convert to project |

### 3.2 Module 2: Design Management (3-Phase)

**Objective**: Control technical documents and prevent discrepancies before construction.

#### 3.2.1 Design Project Overview

A design project is created only through an explicit action, including before
any contract exists (design first, contract later). Changing a contract to In
progress does not create one. In **Create design project**, choose the
Operational Project, or a contract that already belongs to one. A design
project started from the Opportunity remains the same project when a contract
for that Operational Project is added; creating from the contract does not
make a duplicate. Sales must have Opportunity and Operational Project manage
permissions, plus authority to manage the project team, to initiate this flow.

In the project **Design schedule**, every task keeps an internal NICON assignee
and an independent Design Lead approver. For outsourced work, select both the
vendor and its Downstream Design/Subcontract contract. The contract must belong
to the same Operational Project and match the selected vendor; selecting only
one field is rejected. This reference does not copy vendor or contract master
data into the Design module.

Use **Complete** to submit a task automatically to its single designated
approver. Generic task editing cannot mark work complete. Pending and approved
tasks cannot be edited; a rejected task can be corrected and resubmitted. Both
the assignee and approver must retain an effective project assignment when
submitting or reviewing.

| Page | Functions |
|------|-----------|
| Design Project List | All design projects |
| Design Project Create/Edit | Create and update design project |
| Design Project Detail | Overview and 3-phase progress tracking |

#### 3.2.2 Phase 1: Concept Design

Manage architectural 3D models and functional floor plans. Attach presentation images and files. Record client feedback and revision notes. Manage concept versions. Submit for client approval.

| Page | Functions |
|------|-----------|
| Concept List | Concept documents by project |
| Concept Create/Edit | Create proposal, assign responsible person |
| Concept Detail | File upload, feedback, internal/client approval |

#### 3.2.3 Phase 2: Basic Design

Manage drawings for construction permit applications. Maintain technical descriptions. Classify documents by discipline (Architecture, Structure, MEP). Manage drawing versions. Attach related legal documents. Submit for review and approval.

| Page | Functions |
|------|-----------|
| Basic Design List | Documents by project |
| Basic Design Create/Edit | Create and update documents |
| Basic Design Detail | Drawing catalog, descriptions, file upload, approval status |

#### 3.2.4 Phase 3: Detail Design

Manage detailed construction drawings separated by discipline (Architecture, Structure, MEP, Interior). Catalog drawings by construction work item. Link drawings to construction tasks. Manage drawing versions. Submit for review and approval.

The current implementation manages drawing metadata, workflow status, revision
reasons, and IFC distribution records. Basic Design and Shop Drawing records
support managed binary upload plus authorized preview/download, and the
Documents tab exposes those files. For each Concept option, use the dossier
panel to upload a 3D source model, perspective, site/roof plan, floor plans
with floor names, section and presentation PDF. Images and PDFs can be previewed
inside Nicon with page navigation for PDFs; source models can be downloaded.
File selection and signed dates follow the active interface language. Correct a
draft by detaching an incorrect file
or checklist exception; the stored Project Document remains available. Record
client feedback verbatim after submission to the client, with the representative,
channel, time and an evidence reference (meeting minutes, email or document).
Record both a response and its resolution outcome before
submitting a replacement R01+ revision. Submitted revisions stay frozen.
The expanded dossier uses the full available width. Its checklist shows attached
documents, recorded exceptions and missing components; sync status remains
visible for each file. Refreshing or saving feedback keeps the revision and
in-progress form context. Customer feedback becomes available after client
submission; a revised-version action appears only when the latest review permits
it. Finalize remains disabled until client approval and signed acceptance are
recorded. Internal submission numbers are labelled separately from option
revisions in Reviews. Discarding an option requires confirmation and preserves
submitted documents and history. The Concept header links directly to Reviews.
An unsubmitted draft does not replace a submitted client decision. If another
review already submitted the same revision, the unused draft remains visible
for reference and its submission button is disabled. For a historical shared
submission, an eligible client decision opens a new revision from the Concept
dossier; the old submission remains unchanged and cannot finalize an option.
Signing dates are displayed in the browser's local calendar.
The selected design-project tab is reflected in the URL and survives refresh
and browser back/forward navigation.
In the Reviews tab, select the latest revision; the review uses exactly its
files. After client approval, select a separate synced signed TK-M03 file and
enter the client signatory and signing date. Finalize only that approved option.
Native 3D viewing, walkthrough video and page/drawing markup remain target work.
An assigned review recipient may comment on the whole submitted dossier or select one
of its attached Project Documents. The selected file is recorded with the
comment; other project files cannot be attached to that review. Existing
general comments remain available. A generated IFC package export is deferred. DWG is not
rendered natively; preview depends on the supported managed
file type and the server content endpoint.

Once a design dossier has been submitted to the client, it cannot be
permanently deleted even if the later decision is Rejected. Correct it with a
recorded client decision and a replacement revision so the submitted evidence
and lineage remain auditable.

| Page | Functions |
|------|-----------|
| Detail Design List | Documents by project |
| Detail Design Create/Edit | Create and update documents |
| Detail Design by Discipline | Discipline management, drawing list, cross-review |

#### 3.2.5 Revision Control

Track drawing revision history. Create new revisions with change justification. Compare revision versions. Mark the active version. Withdraw or lock old drawings. Notify relevant departments of version updates.

| Page | Functions |
|------|-----------|
| Revision List | Revisions for a document/drawing |
| Revision Create/Edit | New revision with change reason |
| Revision Detail/History | Change history, withdrawal, audit log |

#### 3.2.6 IFC (Issued For Construction) Issuance

Submit drawings for approval before issuance. Apply electronic "Issued For Construction" stamp. Lock issued drawings from further editing. Distribute to construction and supervision teams. Track issuance history.

| Page | Functions |
|------|-----------|
| IFC List | Issued documents by project |
| IFC Create/Edit | Create/update issuance batch |
| IFC Detail | Issuance record, electronic stamp, recipient tracking |

### 3.3 Module 3: Permitting

**Objective**: Ensure legal compliance and administrative clearance throughout the project.

#### 3.3.1 Legal Document Checklist

Open **Admin > Design and Permits > Permits** or navigate to `/admin/permits`.
Creating a Design Project does not create permits. When an eligible Upstream
Design, Construction, or Design & Build Contract is moved from **Signed** to
**In progress**, the Project Manager or Admin must complete the Legal activation
wizard on the Contract detail page:

1. Check the Contract and Operational Project.
2. Select a Published legal checklist template by its scope and version. Required
   items cannot be unchecked; choose optional items and additional active permit
   types as applicable. A new package cannot be confirmed without a published
   template. If none exists, ask Legal/CM to author and independently approve one.
3. Select the Legal lead, assignee, independent approver, dates, and predecessor
   for each permit. Assignees and approvers must be active members of that
   Operational Project and may belong to different departments.
4. Review and confirm. Nothing is created and the Contract status does not
   change before confirmation succeeds.

The first confirmation creates one Legal work package for the Operational
Project. Later eligible Contracts for the same project reuse that package and
do not duplicate permits. Legacy projects or already-active Contracts use
**Set up Legal** from the Permit workspace; legacy Design Project links remain
available for compatibility.

Existing configured permits remain available as predecessors when adding a new
permit; they are not created again. Setup requires the latest package version.
Confirmed changes to package dates or Legal lead are recorded in history.
Completed and Cancelled packages cannot receive setup changes. Another contract
may reuse a Completed package with an applied checklist without changing its
template, lead, dates or tasks; a Cancelled package cannot be reused.

Expand **Legal checklist templates** in the Permit workspace. Legal/Admin can
draft a code, names and applicable scope in Vietnamese, English, Chinese and
Japanese, then select permit types in checklist order and mark required items.
CM/Admin reviewers check every item and scope, enter the CM/NICON approval
reference and choose **Approve and publish**. A creator or editor cannot publish
their own version, even as Admin. The development example stays Draft until a
real independent approval; it is not an approved NICON legal matrix.

Published definitions cannot be edited. **Draft a new version** prepares a new
draft; **Retire from new use** preserves earlier project snapshots. The PM
selects a template manually; the system does not infer project type or scale.

Existing projects without a template remain usable. PM/Admin can choose one
through **Set up Legal**, review existing items versus new items and confirm.
Existing files, task progress and package schedule remain unchanged; missing
items are linked/created. Applied templates cannot be switched or waived in
this release. A required checklist item is not automatically a prerequisite for
every construction stage; stage-specific construction gates keep their rules.

After a stale-data error, refresh to inspect the latest version while retaining
your input. In the template editor, review the current definition and explicitly
confirm the latest version before saving your draft again.

Track construction permits (GPXD), fire safety (PCCC), electrical supply, water supply, sidewalk permits, completion certificates, and other configured types. Record status, issuing agency, responsible person, submission and issue dates, expiry, target deadline, and notes. Risk cards and filters highlight overdue, due-soon, and expiring items.

Permit rows that belong to the Legal package cannot be created, regenerated, or
deleted independently. Adjust assignment and schedule through the linked work
package; aggregate deletion remains protected by the deletion-impact workflow.

#### 3.3.2 Permit Application Tracking

Track submission status at government agencies. Monitor processing progress: submitted, under review, supplementary documents required, approved, rejected. Record working history with regulatory bodies. Upload response documents and supplementary requests. Alert on delayed applications. Notify when applications are approved.

The authority status and internal task status are separate. Upload the submitted
package and record the submission date before using **Submitted**, **Under
review**, or **Need more documents**. Upload the issued permit and record its
issue date before using **Issued**. Only then can the assignee submit the linked
task as **Completed**. The selected approver can verify it or return it with a
mandatory reason; returning a task does not change the authority status.

| Page | Functions | Delivery |
|------|-----------|----------|
| Legal workspace | Project/type/status/assignee/approver/deadline filters, risk summary, history, and responsive cards/table | Implemented |
| Contract activation wizard | Manual permit selection, Legal lead, assignment, dates, dependencies, review, and atomic confirmation | Implemented |
| Permit and task lifecycle | Authority status/documents plus independent progress, approval/return, and audit history | Implemented |

### 3.4 Module 4: Construction and Acceptance

**Objective**: Manage site operations, control quality and schedule, and ensure safety at each construction phase.

The customer-approved model separates duties between **Phòng CM** at the office
and the **Ban Chỉ huy Công trình (BCH)** at the site. The first Operational
Project-based workflow slice is available from the split Construction menu;
the remaining target controls are identified explicitly below.

#### 3.4.0 Target Operating Model

**Phase 1 - CM preparation after an Upstream D&B/construction contract is
signed**

1. Create the WBS, construction Gantt, Baseline S-Curve, milestones, and
   critical path.
2. Establish the BCH organization: Site Manager, Field/QA-QC Engineer, Site QS,
   HSE Officer, and Storekeeper.
3. Plan materials, equipment/machinery, and direct labour by schedule milestone;
   coordinate Bid Tabulation and Downstream selection with Procurement.
4. Build and lock the Budget Baseline for materials, labour, machinery, and BCH
   overhead. Later changes require authorization, history, and a linked VO when
   they change contract scope or value.

The signed contract starts **CM preparation**; it does not by itself authorize
construction. The target start gate also requires the applicable released IFC,
issued/non-expired permits, published schedule baseline, approved BCH/resource
plan and locked Budget Baseline. Site handover, method statement and HSE-plan
evidence are project-template conditions pending NICON's approved ISO checklist.
The application now evaluates this start gate server-side and stores the gate
snapshot and actor. WBS, schedule, S-Curve, BCH organization, resource plan and
Budget Baseline are currently governed as required versioned references rather
than editable planning structures. Site handover, method statement and HSE plan
are enforced when CM marks them required for that preparation version.

**Phase 2 - parallel execution and cross-control**

| BCH field responsibility | CM office responsibility |
|---|---|
| Raise mobile resource requests and compare them with BOQ limits | Perform level-2 review for over-limit or unplanned requests before Procurement |
| Maintain daily weather, labour, equipment, progress, incident, and photo logs | Reconcile daily progress with the Baseline S-Curve; issue a red alert and require recovery explanation when delay exceeds 5% |
| Create QA/QC records, use issued IFC drawings, and drive Punchlist items to closure | Review QA/QC evidence, perform spot checks, and supervise overdue Punchlist items |
| Record HSE activity and violations | Review PPE/HSE compliance and process field violation records |
| Measure Upstream quantities for CĐT/TVGS acceptance and Downstream quantities for subcontractor/team payment | Review both QS directions at level 2 and appraise VO dossiers |

Quality records synchronize to the `04_Thi_cong_Nghiem_thu` Drive folder, but
the application remains authoritative for metadata, workflow state, permission,
version, and audit history. Approved Upstream QS evidence triggers Module 6
collection milestones; approved Downstream QS evidence supports payment review.

**Separation-of-duty rule:** the author of an MR, QS record, QA/QC dossier, HSE
violation, or VO cannot satisfy the independent CM review for the same item.
Every transition records actor, time, comments, and the version reviewed.

The target mobile experience is divided into three BCH portals:

1. **BCH Operations:** resource requests for materials, machinery/equipment,
   and direct labour, plus daily field coordination.
2. **QA/QC:** sampling records, test results, certificates, IFC-based
   inspection, site photos, Punchlist, daily diary, and HSE evidence.
3. **Site QS:** separate Upstream quantity acceptance with CĐT/TVGS and
   Downstream quantity acceptance for teams/subcontractors.

When adding QA/QC evidence, enter each active project document ID and classify
it as an acceptance record, sample, laboratory result, material certificate,
site diary, or other evidence. The same document can appear only once in a
dossier. Older integrations that send document IDs without a type remain
supported. New IDs from those integrations are stored as **Other**, while
updates retain an existing classification for unchanged IDs. Classification
is not inferred from a filename. The published project QA/QC template and
released IFC still apply. Mapping the newly received NICON ISO forms into
mandatory project-type checklists remains pending CM approval; a file in the
source pack is not automatically a published template. Each dossier card shows
the document IDs and classifications for review.

Material requests use the target BOQ warning bands from the customer PRD:
yellow at 85%, orange at 95%, and red above 100%. The red state blocks ordering
and requires an approved VO. These bands are planned until the denominator,
rounding, zero-allowance behavior, and server workflow are approved and
implemented; the current material-request behavior remains the product truth.

For Downstream payment against accepted quantities, the target evidence gate is:

```text
Signed Downstream Contract
  + valid posted Warehouse Receipt when the package includes material
  + approved Downstream QS acceptance
  = eligible for Accounting payment review
```

Eligibility does not mean automatic payment. Missing, mismatched, reversed, or
superseded evidence blocks the handoff and must leave financial state unchanged.
Material packages in that flow require three-way matching; the document matrix
for the four package types still requires FA/CM confirmation. The received ISO
sources also describe supplier requests before receipt and advances. Their
eligibility, evidence and approvals are separate open decisions; this diagram
does not define their future gate.

**Target — NICON clarification received 18 October 2026:** each project has a
separate BCH, while a person may participate in multiple projects concurrently.
CM's portfolio covers schedule, completed quantities (separate output/input
acceptance), executed value versus estimate, QA/QC acceptance dossiers and safety.
After CM approves equipment/labour, Procurement contracts and supplies the site,
then FA processes payment. Templates and package-specific payment documents come
from the ISO pack NICON already sent; identify missing forms individually, with
payment-document gaps referred to FA (chị K/Kiara). The source pack is now in
`docs/Nicon/ISO/`, but its forms have not been approved as product checklists or
a four-package payment matrix. These confirmed requirements do not mean the
complete portfolio or Finance handoff is delivered.

**Current implementation boundary:** The split menu now provides BCH Resource,
QA/QC, Site QS and CM Control workspaces. It delivers versioned CM preparation
references and a server start gate; typed material/equipment/labour requests;
published multilingual QA/QC templates and IFC-linked dossiers; explicit
Upstream/Downstream QS batches; CM review; row-version concurrency;
idempotency; and server-side author/reviewer separation. Material approval
creates and submits the existing Material Request aggregate atomically.
**Partial — Procurement handoff:** open Procurement Control, choose the project
and **Approved equipment and labour**. Filter by resource type or supply state.
Procurement can draft a Downstream contract by choosing its vendor, business
type and value; the existing contract service validates it. The draft is not
automatically linked or signed. Select/save its link on the approved request;
an authorized contract manager completes the existing signing workflow.
Multiple requests may use one contract, while each request has one contract.
Relinking is allowed only before supply confirmation. Select an active project
document to confirm supply; the server records actor, time and history. An
unsigned/on-hold/cancelled/closed contract cannot authorize a new confirmation.
Supply recording does not approve QS or payment and creates no Payment Request.
Evidence cannot be deleted while referenced; failed document uploads remain
retryable from project documents. Contract deletion impact discloses blocking
resource links/history. Read-only users have no write controls.

CM can open **CM control from source records** from the CM workspace or project
reports. It separates active approved Upstream/Downstream QS, QA/QC and HSE
source records with drill-down and source permission checks. This section is
current state even when the surrounding report has date filters; it is not a
historical snapshot or a sum of quantities. Each approved QS source shows its
own certified amount and period. Budget comparison remains explicitly
unavailable until its sources and calculation contract are confirmed.

QA/QC evidence must already
be an active Project Document, so durable Drive synchronization and retry state
remain owned by the existing document workflow.

Construction Tasks, Site Diaries, Punchlist, Partial Acceptance, As-Built and
Handover still persist against a Design Project and have not been migrated to
the Operational Project aggregates in this slice. Editable WBS/Gantt/S-Curve
baseline data, the reproducible greater-than-5% variance alert, offline queues,
NICON ISO checklists mapped and approved for each project type, automatic
contract-triggered preparation,
and payment eligibility/handoff remain target work pending the documented
business decisions and controlled compatibility migration.

#### 3.4.1 Construction Schedule (Gantt)

Create construction schedules with phases and work items (WBS). Assign responsible personnel. Set start and end dates. Track actual completion percentage. Update progress from the field. Display Gantt chart with dependency lines. Alert on overdue tasks. Support drag-and-drop task bars, resize duration, and zoom (day/week/month).

| Page | Functions |
|------|-----------|
| Task List | Tasks/WBS by project and phase |
| Task Create/Edit | Create task, update plan/actual/percentage |
| Gantt Chart Detail | Interactive Gantt, dependencies, personnel assignment, alerts |
| Interactive Gantt UI | Drag-drop, resize, zoom, dependency lines |

##### Construction drawings

To link drawings, open a task at **Admin > Construction > Construction
schedule** and find **Construction drawings** in its detail panel. Users with
task management permission can select up to 200 released IFC drawings from
the same project and click **Save drawing links**. This saves independently
from task dates/progress. Clearing the selection removes links only.

The linked list identifies the drawing code, title, IFC release and revision
at release. Use the file preview when a file is attached. View-only users can
read linked drawings but cannot change links. If no candidates appear, the
design team must first release the relevant IFC packet. After a conflict,
refresh the drawing section and reapply the selection; an unsent/offline change
is not saved. This screen does not implement automatic revision replacement or
offline synchronization.

#### 3.4.2 Electronic Site Log

Open **Admin > Construction > Site Diary** to record daily weather, workforce, equipment, materials, completed work, incidents, and internal notes. The list provides explicit View, Edit, and Delete actions on desktop and mobile, plus multi-select deletion.

The lifecycle is **Draft → Submitted → Confirmed**. Only Draft diaries can be edited; reopen a Submitted or Confirmed diary to Draft when its content needs correction. Users with management permission can permanently delete a diary in any status when it was created or transitioned accidentally. Single and bulk deletion always show a destructive confirmation and cannot be undone; the single-delete audit event retains the removed record snapshot.

| Page | Functions | Delivery status |
|------|-----------|----------|
| Site Log List | Filters, status summary, responsive CRUD actions, and bulk delete | Implemented |
| Site Log Create/Edit | Create Draft logs; edit after reopening when required | Implemented |
| Site Log Detail | View workflow metadata, submit, confirm, reopen, and force delete | Implemented |
| Mobile Media Upload | Camera picker, gallery, upload progress, thumbnails | — |

#### 3.4.3 Partial Acceptance (Phase/Section Acceptance)

Open **Admin > Construction > Partial Acceptance** to manage phase or work-item acceptance records. Authorized users can search, filter, sort, and paginate records by project, linked construction task, responsible person, acceptance date, and workflow status. Create or edit records with participants, findings, and related document or photo paths, then move them through Draft, Submitted, Approved, Rejected, or Cancelled states. Rejected records can be revised and resubmitted; approved and cancelled records are locked. Unless granted the view-all permission, users only see records connected to their project assignment, linked task ownership, creation, or workflow participation.

| Page | Functions |
|------|-----------|
| Acceptance List | Scoped records, operational filters, sorting, pagination, summary counts |
| Acceptance Create/Edit | Project/task linkage, participants, findings, documents |
| Acceptance Detail | Workflow history, responsible users, documents, permitted actions |

#### 3.4.4 Full Acceptance (Handover)

Open **Admin > Construction > Project Handover** to manage one handover record
per Design Project in the current compatibility model. The list supports
project, responsible user, planned date, status, readiness, text search, sorting,
pagination, and CSV export. Users without the view-all permission only see
records they created, are responsible for, or whose project they manage or lead.
The NICON target is one handover aggregate under the shared Operational Project;
do not create a second handover when both IDs refer to the same business project.

Create or edit a Draft/Reopened record with the planned date, responsible user, commissioning result, checklist, supporting HTTP(S) or host-relative document links, and signatories. Host-relative links must begin with a single `/`; protocol-relative links and non-HTTP(S) schemes are rejected and legacy unsafe values are shown as text rather than clickable links. Readiness is calculated from canonical project data: at least one approved partial acceptance record, at least one configured required as-built category with every required category approved, no unresolved punch items, commissioning complete, and every handover checklist item complete. An empty required-category configuration blocks readiness; an administrator must configure it explicitly.

The lifecycle is **Draft → Ready for Handover → Handed Over**. Managers may return a ready record to Draft, cancel it, or reopen a completed handover for controlled correction. Final completion requires the dedicated completion permission and at least one signatory; it records the actual handover date, actor, timestamp, and immutable status history. Completed records are locked until reopened. If another user changes the same record first, the API returns a conflict and the operator must reload before retrying so a newer change is not overwritten.

Handover access is permission-based. `construction.handover.view` opens the workspace, `view.all` removes project-assignment restrictions for reads and CSV export, `manage` enables scoped create/update/delete and preparation-state transitions, `manage.all` removes project-assignment restrictions for mutations, and `complete` authorizes final completion. Read-all access never implies write-all access. A scoped responsible user cannot reassign the record unless they are also the project manager or design lead.

| Page | Functions |
|------|-----------|
| Handover List | Handover records by project |
| Handover Create/Edit | Create and update handover records |
| Handover Detail | Checklist, commissioning, acceptance record |

#### 3.4.5 As-Built Records

Automatically compile as-built drawings. Consolidate acceptance records for all work items. Upload and store as-built drawings. Manage as-built document catalog by project. Export as-built document packages. Manage document versions.

Open **Admin > Construction > As-Built Records** to search, filter, sort, and paginate documents by project, category, and workflow status. Authorized users can create and edit draft/submitted records, move them through Draft, Submitted, Approved, Archived, or Cancelled states, inspect the lifecycle history, and export the complete filtered list to CSV. Approved and archived records are locked; duplicate document titles within the same project are rejected. The summary cards show required-category completeness for the selected project.

| Page | Functions |
|------|-----------|
| As-Built List | Document packages by project |
| As-Built Create/Edit | Create and update packages |
| As-Built Detail | Drawing compilation, acceptance records, completeness check, catalog export |

#### 3.4.6 Defect Management (Punchlist)

Record defects at the construction site with location and description. Upload defect photos and videos. Assign responsible person for remediation. Set remediation deadlines. Track status: unresolved, in-progress, completed. Confirm remediation completion. Support floor plan annotation with pin-drop for defect location.

The current delivered record supports code, description, free-text location,
severity, root-cause attribution, responsible designer, assignee, deadline,
resolution, verification and reopen count. Its lifecycle is **Open → In Progress
→ Fixed → Verified**, plus Cancelled/reopen. Dedicated persisted photo/video
attachments and floor-plan pin annotation are target features, not current
delivered behavior.

| Page | Functions |
|------|-----------|
| Punchlist List | Defects by project |
| Punchlist Create/Edit | Create defect, assign handler, set deadline |
| Punchlist Detail | Description, photos/video, status, re-open capability |
| Floor Plan Annotation | Upload drawing, pin defect locations, popup details |

#### 3.4.7 Permanent Delete Behavior

Permanent deletion requires the relevant permission and record scope, a current
deletion-impact preview, the exact typed resource code, and concurrency
confirmation. The preview distinguishes records that will be deleted, independent
records that will be unlinked and preserved, and blockers that must be resolved.
Customer deletion is blocked by independent opportunities, tenders, contracts
and projects; quotes block Opportunity deletion. Resolve each through its own
authorized workflow rather than assuming a cascading delete.

Project-dependent files must be removed through their owning module before the
parent project can be deleted. Managed file cleanup may return a pending durable
operation; wait for Completed before treating the record as deleted. Ownership
conflicts or failed cleanup require resolution and retry. Audit history remains;
physical file treatment follows the preview and verified ownership rules.
See [Permanent Aggregate Deletion](application_developer.md#77-permanent-aggregate-deletion)
for the full dependency and recovery contract.

### 3.5 Module 5: Procurement

**Objective**: Manage suppliers, subcontractors, and project materials.

#### 3.5.1 Vendor and Subcontractor Management

Open **Admin > Procurement > Vendors / Subcontractors** or navigate to `/admin/vendors`. Authorized users can search by code, company, tax code, contact, phone, email, or trade category; filter by vendor type and active state; sort and paginate the results; and export the complete filtered result. Users with management permission can create, view, edit, and delete vendor records from the desktop list, mobile cards, or detail page.

Select a company name or the view action to open its detail page. The page shows company, contact, document, active-status, creator, and timestamp information. Set a vendor to inactive when its identity must remain available for historical reporting. Delete is permanent, requires confirmation, and is intended for obsolete, duplicate, or test records whose history is not required.

At least one valid phone number or email address is required, and vendor codes and company names must be unique. The detail page also shows the last responsible editor, linked Contracts and Operational Projects, Vendor Ratings, and the Vendor change timeline. Capability documents can be uploaded and previewed; cancelling a form discards its unassigned upload, and abandoned unassigned uploads expire automatically after 24 hours.

Before permanent deletion, the system shows an impact preview and requires the exact vendor code. Linked Contracts, Vendor Ratings, Payment Requests, shared files, or unsafe file references block deletion. Use the links in the impact dialog to resolve dependencies, or keep the Vendor inactive when its identity is required for historical reporting. Bid comparison, warehouse transactions, and creation of procurement ratings remain in their dedicated Module 5 workspaces.

| Page | Functions | Delivery status |
|------|-----------|----------|
| Vendor List | Search, filter, sort, paginate, export, view, edit, and delete vendors and subcontractors | Implemented |
| Vendor Create/Edit | Create and update profile, contact, document-link, and active-status information | Implemented |
| Vendor Detail | View company metadata and edit or permanently delete the record | Implemented |

#### 3.5.2 Bid Comparison (Bid Tabulation)

Implemented at **Procurement → RFQs & bid comparison**. See the
[RFQ workflow and operating contract](application_developer.md#718-rfq-and-supplier-comparison) for roles,
BOQ prerequisites, quotation revisions, scoring, allocation, and award rules.

Procurement selects lines and quantities from an approved project BOQ, invites
active vendors and issues the RFQ. Vendors with an email receive an expiring
secure portal link and may submit revised quotations until the deadline;
Procurement may still record a quotation on their behalf. Quotations retain
currency, manual exchange rate to VND, freight, discount, VAT and immutable
revision history. Missing prices differ from zero and inactive vendors receive
no lowest-price highlight. Vendors can download attached RFQ package documents;
partial quotations can participate only for the lines they actually quote.

During evaluation, Procurement records the commercial score and evidence. The
matrix combines it with price, delivery time and approved vendor-rating weights.
The score is advisory; selecting a lower-scored bid requires a reason. BGD or an
explicitly authorized award role allocates every RFQ line and quantity across
one or more valid vendors. Supply quantities must reference approved Material
Requests; Subcontract quantities do not. The system blocks incomplete allocation
and Supply demand over-allocation, then atomically creates one
Draft downstream Supply/Subcontract contract per selected vendor. Awarded values
and lines remain protected while signing, warehouse operations and payable
processing continue through their existing workflows.

| Page | Functions |
|------|-----------|
| RFQ List | Requests for quotation |
| RFQ Create/Edit | Create and update RFQ |
| Bid Tabulation Detail | Comparison matrix, vendor selection/approval |
| Interactive Comparison Matrix | Multi-vendor matrix with auto-highlight |

#### 3.5.3 Material BOQ Management

Create material BOQ by project and construction work item. Define maximum allowable quantities (norms) per project. Link BOQ to construction schedule. Track BOQ amendments. Support Excel import for bulk data entry.

The NICON reference pack contains both a one-sheet estimate and large
multi-sheet workbooks with cover, total/summary, preliminaries, building,
infrastructure, fire-fighting, electrical, plumbing, HVAC, and material-list
sheets. The target import experience therefore uses an explicit workflow:

1. Upload the workbook without changing the approved BOQ.
2. Detect sheets and let the operator classify each as ignored metadata,
   summary, or importable detail.
3. Map required columns and show the detected unit, quantity, unit price,
   formula/value, and hierarchy before import.
4. Validate every detail row and report errors by sheet and cell. Blank,
   duplicated, merged, formula-only, malformed, negative, and unsupported rows
   must not be silently coerced.
5. Preview totals and reconcile them with recognized summary totals.
6. Confirm once to create a Draft version with source filename, hash, importer,
   import time, mapping version, and per-sheet results.
7. Review and approve through the normal BOQ lifecycle. Import never bypasses
   approval or replaces the current approved revision automatically.

This generalized multi-sheet workflow is target/planned unless the current
screen explicitly exposes it. The existing approved-BOQ and MR controls remain
authoritative until the importer is delivered.

| Page | Functions |
|------|-----------|
| BOQ List | By project |
| BOQ Create/Edit | Create/update BOQ, Excel import |
| BOQ Detail | Material catalog, norms, quantities |
| Excel Import Wizard | Upload, column mapping preview, validation, confirm |

#### 3.5.4 Material Request (MR)

Site engineers submit material requests referencing the BOQ. Specify quantities and required delivery dates. Route requests to the office for approval. Track request status: pending approval, approved, rejected. Maintain request history.

Open **Procurement control**, choose an Operational Project, and select
**Material requests**. The list is always restricted to the selected project.
Use the keyword field to search request codes, notes, assigned users, and BOQ
items. Status, procurement owner, and required-date filters can be combined;
the required-date button switches ascending and descending order. The list is
paginated and can be exported to CSV with the same project and filters.

Each request shows the requester, responsible site user, procurement owner,
required date, requested quantity, net quantity received through posted warehouse
receipts, and the remaining approved BOQ allowance. Users outside the project
scope receive no record visibility, and create/submit/approve actions remain
hidden unless their role has the corresponding permission.

Select **View details** to review Customer and Project context, all related
Contracts, requested and received quantities, remaining BOQ allowance, notes,
and lifecycle history. The requester may edit a Draft without losing entered
data when validation or an API call fails. Draft requests can be submitted for
approval; authorized reviewers may approve or reject them, and non-terminal
requests may be cancelled with a recorded reason. Submitted and decided records
are locked against editing. The assigned procurement owner and requester receive
notifications when responsibility changes through the workflow.

| Page | Functions | Delivery status |
|------|-----------|----------|
| MR List | Project-scoped search, filters, sorting, pagination, export, quantities, and permission-aware actions | Implemented |
| MR Create/Edit | Create/update Drafts with project users, future required date, unique current-BOQ items, and remaining-allowance validation | Implemented |
| MR Detail | Customer/Project/Contract context, BOQ comparison, lifecycle, edit, submit, approve/reject, and cancel | Implemented |

#### 3.5.5 Warehouse and Material Distribution

Manage warehouse receipts (inbound) and issue slips (outbound). Track inventory at the construction site. Record material distribution to construction crews. Maintain inbound/outbound/stock history. Report material usage by work item.

| Page | Functions | Delivery status |
|------|-----------|----------|
| Warehouse List | Project-scoped inbound/outbound/stock, filters, pagination, and CSV export | Implemented |
| Receipt/Issue Create/Edit | Create and update Draft warehouse transactions with source and stock validation | Implemented |
| Warehouse Detail | Business context, transaction lines, allocation evidence, posting, and reversal | Implemented |

Open **Procurement control**, select an Operational Project, and choose
**Warehouse receipts / issues**. The stock section shows posted receipt, issue,
and on-hand quantities after reversal records. Use the filters to find a document
by code, material, responsible user, work item, type, status, or occurred date.

Warehouse users may create receipt and issue Drafts. Only the creator may edit a
Draft. Posting makes the document immutable and updates inventory; correct a
posted document with **Reverse transaction** and a recorded reason. Receipt
reversal is blocked when it would make stock negative. Issue documents retain
the responsible site user and work-item code so material distribution remains
traceable to the receiving team or activity.

#### 3.5.6 Material Alerts

Monitor actual material usage against BOQ norms. Alert when usage exceeds allocated quantities. Alert on low inventory levels. Notify project managers on discrepancies. Track alert history. Report budget overruns by project.

Open **Procurement control**, select an Operational Project, and choose
**Material alerts**. The tab count shows unresolved alerts. By default the list
shows items that have not been handled; use the status summary or filters to
review acknowledged and automatically resolved history. Search by alert code,
material code, description, or assignee. Type, status, severity, assignee,
sorting, pagination, and CSV export can be combined.

**Usage over BOQ** means net posted warehouse issues exceed the current approved
BOQ allowance. **Material shortage** means posted receipts are insufficient for
approved Material Request demand. Alerts update automatically after BOQ
approval, Material Request approval/cancellation, and warehouse posting or
reversal. Users with management permission can use **Recheck source data** after
a transient system failure; it does not edit the warehouse or BOQ source.

Open an alert to compare approved demand or BOQ allowance with received, issued,
on-hand, and variance quantities. The detail also shows Customer, Project,
related Contracts without financial values, the source document, assigned user,
and immutable event history. The assigned user or Project Manager can select
**Acknowledge**, then record the owner, action, and expected timing. This records
responsibility but does not close the alert. The system closes it only after the
source discrepancy is corrected and reopens it if the condition returns.

| Page | Functions | Delivery status |
|------|-----------|----------|
| Material Alerts | Project-scoped exceptions, filters, status summary, source reconciliation, and CSV export | Implemented |
| Material Alert Detail | Source metrics, Customer/Project/Contract context, acknowledgement, and lifecycle history | Implemented |

### 3.6 Module 6: Finance and Contract

**Objective**: Maintain financial transparency and contract control.

#### 3.6.1 Upstream Receivables Monitoring

Manage primary contracts linked to projects and customers. Define contract value, duration, and work scope. Establish payment milestones tied to acceptance progress. Track advances and stage-based payments. Upload and store contract files and appendices. Track contract status: not started, in-progress, completed, paused. Monitor contract modification history.

| Page | Functions | Delivery status |
|------|-----------|----------|
| Receivables List | Finance > Receivables; upstream contracts with owner/project/status/end-date filters, current value after approved VO, collection-risk summary, next receivable, pagination, contextual detail return, and complete filtered CSV export | Implemented |
| Contract master maintenance | Create and edit the shared Upstream Contract only in CRM > Contract registry; Finance does not duplicate this form | Implemented |
| Contract detail | A Finance row opens the one canonical Contract record and preserves a return link to the filtered Receivables workspace; no second Finance detail form exists | Implemented |

Sales can create an Upstream customer contract in either of two ways. Use
**Create contract** directly from the Opportunity when the customer already
knows and has agreed the contract value; no quotation is required for this
path. Alternatively, create the contract from an internally approved,
unexpired and unused quotation. In the quotation path, Finance first records
and approves the commercial review of contract conditions, guarantees, and
payment terms on the quotation's Operational Project work package. The source
quotation must match the contract's customer, project and Opportunity. A
downstream supplier contract does not use a customer quotation.

To send the quotation, open **Send to customer**, check or enter the customer
email address, review the subject and message, then send. Users with
quote-management permission may edit the subject and message; send permission
is required to dispatch. The dialog identifies the generated `.xlsx`
attachment before sending. The email contains the priced lines and total and
attaches the localized Excel quotation.

Every new primary contract starts as **Draft**. In **Contract registry**, use the
explicit status actions on the detail page to sign, start, pause, complete, or
cancel it; editing general
information cannot skip lifecycle checks. Signed and active contracts require a
signed date, and contract dates must follow signing → start → end order.
Milestone status and accounting responsibility are changed through the milestone
action, while schedule editing preserves the milestone identity and its history.
Processed milestones or milestones already referenced by a payment request
cannot be deleted. When an unpaid primary-contract collection milestone reaches
its due date, the active contract owner and assigned accountant receive one
notification; changing its due date schedules a new reminder for the revised
date.

#### 3.6.2 Downstream Payables Monitoring

Manage input contracts with subcontractors and material suppliers. Define contract value, terms, and payment schedules. Track contract execution progress. Monitor payment history. Upload contract files.

| Page | Functions | Delivery status |
|------|-----------|----------|
| Payables List | Finance > Payables; Downstream supplier/subcontractor contracts with vendor/project/owner/status/date/value filters, payment-obligation summary, pagination, and complete filtered CSV export | Implemented |
| Contract master maintenance | Create and edit the shared Downstream Supply/Subcontract Contract only in CRM > Contract registry | Implemented |
| Contract detail | A Finance row opens the same canonical Contract ID and preserves a return link to the filtered Payables workspace; no second Finance detail form exists | Implemented |

The Payables workspace is fixed to **Downstream**. More than one supplier
or subcontractor contract may belong to the same Operational Project. Choose an
active Vendor compatible with the contract type: Supplier or Both for Supply,
and Subcontractor or Both for Subcontract. Payment milestones represent payable
obligations; the next unpaid milestone and overdue/due-within-30-days counts are
calculated from the current filtered result. CSV export uses the same server-side
direction, filters, sorting, and access scope as the screen.

Accountants can review downstream contracts across the full Operational Project
portfolio and maintain their financial schedules, responsible accountants, and
payment states. They cannot create Contracts or edit headers, transition legal
status, attach files, manage appendices, or delete Contracts. Finance rows link
to the canonical Contract detail; only a user who also holds
contract-management permission sees master-data actions there.
Lifecycle rules, immutable processed payment milestones, API error handling,
and concurrency protection remain authoritative at the shared Contract API.
Payment Request contract choices, request lists, and every workflow action are
limited to the caller's Operational Project scope. Project members see their
assigned projects; portfolio finance users require the global project-view
permission in addition to the relevant Finance permission.
When creating a Downstream Payment Request, Finance may select an approved
Downstream QS batch from the chosen Contract. Leave QS unselected for an advance
or request without accepted quantity. A Draft request can be reopened from the
Finance list to correct its Contract, QS and documents before submission; a
stale QS is marked in the selector. Review whether an eligible replacement QS
is needed or the request is a legitimate advance without QS. If a linked QS is
later superseded, validation, approval and payment stop. Use **Create revision**
with a reason on the same request; the earlier version and its documents remain
visible in revision history, and the invoice number stays with that request.
Edit the new Draft to select the corrected QS when applicable and update its
documents, then submit it for accounting validation and independent approval
again. Submitted or approved requests can be revised when their linked QS is no
longer eligible; rejected requests can be revised to correct their dossier.
The QS link records provenance;
it does not replace invoice, attachment, Contract or eventual ISO dossier checks.
A user who prepared, revised, submitted or validated a Payment Request cannot
approve the same request.

#### 3.6.3 Variation Order (VO) Management

Record design changes or scope modifications. Calculate cost increases or decreases. Link VOs to primary or subcontractor contracts. Upload appendix files. Track approval workflow status. Maintain contract modification history.

In the current system, each VO belongs to exactly one customer Contract and
therefore inherits that Contract's operational Project. Create and edit the VO
as **Draft**, submit it for review, then approve or reject the submitted record.
A rejected VO can be revised and submitted again. Only **Approved** VOs change
the Contract's Current value; Draft, Submitted, and Rejected records remain
traceable without changing that total. Anyone with permission to edit VOs can
permanently delete one in any status after confirmation. Deleting an Approved
VO removes its adjustment from the Contract's Current value. These states
describe current system behavior and do not replace NICON's pending approval
of the remaining official VO policy.

| Page | Functions |
|------|-----------|
| VO List | Variation orders by project |
| VO Create/Edit | Create cost increase/decrease records |
| VO Detail | VO information, approval workflow |

#### 3.6.4 Project Cash Flow

Record income from clients/owners. Record expenditures for materials and subcontractors. Track actual income vs. actual expenditure by project. Manage payment schedules and receivables/payables. Update payment status: unpaid, paid, overdue. Provide visual timeline charts (bar and line combination).

| Page | Functions |
|------|-----------|
| Cash Flow Dashboard | Cash in/out tracking, planned vs. actual |
| Cash Flow Chart | Timeline visualization, filter by project/period |

#### 3.6.5 Profit and Loss (P&L) Report

Aggregate revenue by project. Aggregate material and subcontractor costs. Calculate profit or loss per project. Compare actual costs against budget estimates. Analyze economic efficiency by project. Generate P&L reports by month, quarter, or year. Provide stacked bar charts with drill-down by project.

| Page | Functions |
|------|-----------|
| P&L Report | Revenue/cost/profit analysis, cost breakdown |
| P&L Chart | Stacked bar chart with project drill-down |

### 3.7 Module 7: Digital Assets (Google Drive Integration)

**Objective**: Organize, secure, and provide rapid access to all project files.

**Current delivered scope:** the Operational Project document catalog supports
current project-linked Quote, Contract, Survey, Basic Design, Shop Drawing,
Permit, Acceptance, As-Built, and Handover files, plus direct project uploads.
Google Drive synchronization is disabled until an administrator supplies the
OAuth client, callback, root-folder, and deployment identity configuration and
connects an approved account from **Settings > Google Drive**. Historical records
are not imported automatically.

Changing the root folder, deployment identity, or business folder paths affects
new folder resolution after the change. Nicon retains historical catalog rows
and Drive files and does not automatically move or delete them.
For existing folder bindings, first save synchronization as disabled, wait for
Processing files to finish, then save the changed topology while still
disabled. Re-enable it after checking the intended paths. The server rejects a
topology change while processing is active or existing bindings are enabled.

#### 3.7.1 Automated Project Folder Structure

Folders are created lazily when the first file in a category synchronizes. The
current server has nine configurable document categories. For compatibility,
`01_Khao_sat` and `01_CRM_PreDesign` are separate sibling paths, followed by the
three nested folders under `02_Thiet_ke`, `03_Xin_phep_Phap_ly`,
`04_Thi_cong_Nghiem_thu`, `05_Cung_ung_Vat_tu`, and
`06_Tai_chinh_Hop_dong`. The NICON target places survey beneath CRM Pre-Design,
but files must not be moved until an approved, reversible topology migration is
run. Stable internal identity is independent of displayed project and folder
names. Nicon permissions control access; Drive group sharing is not synchronized
in the current release.

#### 3.7.2 Document Digitization

Upload remains available when Google Drive is disconnected: Nicon temporarily
stores the file on the host and displays Pending. Once connected, the worker
uploads it, verifies its contents and removes the temporary host copy. Downloads
remain authorized through Nicon. For failed files, use Send to Drive again;
manual retry is available even after automatic attempts are exhausted. Delete
removes an unlinked host-only upload immediately or queues its Drive deletion.
Files owned by another business module must be removed through that module.
Files referenced by QA/QC or QS dossiers cannot be deleted through the catalog
or through their source module while those links remain. Resolve the evidence
at its source dossier first; approved records retain their evidence.
Deleting an Operational Project also deletes its verified empty Nicon folder
tree on Drive after dependent-file cleanup. Unknown/shared files and independent
CRM documents block deletion and must be handled explicitly first. Files added or edited directly in a managed
Drive category are reflected in the catalog, and files moved to Drive trash are
marked deleted. Native Google Docs, Sheets, and Slides can be opened through
their Drive link but cannot be downloaded through the binary-content action.
Deleting through Nicon moves the Drive file to trash instead of permanently
erasing it. Existing files owned by Quote, Contract, Survey, Design, Permit,
Acceptance, As-Built, or Handover retain their source-module lifecycle during
the compatibility migration; for those sidecars, remote deletion is restored
and concurrent edits preserve both versions for confirmation.

| Page | Functions | Delivery status |
|------|-----------|----------|
| Folder Structure Configuration | Admin-managed category paths and Drive connection settings | Implemented |
| Project Document Catalog | Responsive list, upload, status, download, classification, retry, conflict and delete controls | Implemented |

### 3.8 Module 8: Dashboard and Analytics

**Objective**: Provide management oversight and early risk detection.

#### 3.8.1 Executive Dashboard

Target executive scope includes leads, opportunities, contracts, projects,
construction progress, financial/procurement summaries, lead funnel and schedule
alerts. The general admin Dashboard currently covers website content and
recruitment; this target scope does not claim delivered executive analytics.
Tender win-rate reporting is available above the **Tenders** list.

| Page | Functions |
|------|-----------|
| Executive Dashboard | Multi-module overview with charts and widgets |
| Lead Funnel Chart | Funnel visualization with conversion rates |
| Project Progress Widget | Progress bars with overrun alerts |
| Tender Win-Rate Chart | Implemented in Tenders: donut, first-submission dates, Customer/Preparer filters and list reconciliation |

#### 3.8.2 Reports

Progress reports comparing planned vs. actual (S-Curve). Acceptance reports showing pass/fail/pending ratios. Financial reports on revenue, receivables, and expenditures. Procurement reports on material distribution and vendor performance. Risk alerts for overdue tasks, expiring permits, budget overruns, BOQ exceedances, and failed acceptance items.

**Current delivered scope:** users with `reports.projects.view` can open
**CRM → Project Reports** (`/admin/reports/projects`) to review accessible
projects as a portfolio or select one project. The project and inclusive date
filters are stored in the URL so a filtered view can be reloaded or shared.
The page shows current weighted design progress when its baseline is complete,
construction and acceptance status counts, overdue work, permit warnings, and
contractual quote/contract/milestone values. Users with
`reports.projects.export` can download the same filters as XLSX or PDF in the
current interface language.

Milestone amounts are contractual schedule values, not cash, revenue, or
receivables. Historical S-Curve, acceptance ratios, actual-ledger finance,
inventory, BOQ usage, and vendor performance remain explicitly unavailable
until their source data exists. The page groups these gaps by data source and
explains them in the selected interface language instead of calculating
substitutes. Stable reason codes remain available in the API and exports for
audit and integration use.

| Page | Functions |
|------|-----------|
| Business Reports | Acceptance, risk, cost, permit, task reports |
| Advanced Report Filters | Multi-condition filters, date range, saved presets |
| Report Export | Excel and PDF export with progress indicator |

#### 3.8.3 Employee KPI Evaluation

Use `/admin/kpi` to select an employee/month, calculate results, inspect source
evidence and export. Configuration is separate at `/admin/kpi/configuration`
and requires `analytics.kpi.manage`; it controls targets, weights, scoring
direction and alert thresholds. Own-scope viewing and cross-user access follow
the granted KPI permissions.

Calculations use Vietnam calendar months and run automatically at 01:00 Vietnam
time, with authorized manual calculation also available. Missing source events
remain MissingData rather than a zero score; missing target configuration also
prevents a metric from becoming Available. A period can lock only
when every active metric is Available and weights total exactly 100%; locking
freezes definitions and evidence and prevents recalculation. The four-language
usage guide explains readiness and the irreversible lock operation.

The current seed contains 19 metrics across six scorecard groups:

| Delivered scorecard | Current role mapping |
|---|---|
| Sales | `SALE`, `SALES_MANAGER` |
| Tendering | `QS` |
| Design | `DESIGN`, `DESIGN_LEAD`, `ARCHITECT`, `MEP_ENGINEER`, `STRUCT_ENGINEER` |
| Site | `PM` |
| Procurement | `PROCUREMENT` |
| Project Accounting | `ACCOUNTANT` |

This mapping is not the complete NICON 11-position target. Separate scorecards
for PM CM, BCH Commander, Field/QA-QC, Site QS, Legal and HR/Admin require the
corresponding source workflows and approved definitions before activation.
See [KPI Framework and Source Evidence](application_developer.md#719-kpi-framework-and-source-evidence)
for metric sources, accountability and approved calculation decisions.

### 3.9 Cross-Cutting Capabilities

| Capability | Page/Screen | Functions | Delivery status |
|------------|-------------|-----------|----------|
| Authentication | Login Page | Login form, validation, error handling, redirect | — |
| Authentication | Forgot Password | Email input, OTP/link, password reset | — |
| Authentication | User Profile | View/edit personal info, change password, avatar upload | — |
| Navigation | Sidebar + Layout | Sidebar navigation, collapse/expand, responsive mobile drawer | — |
| Navigation | Header Bar | Logo, breadcrumb, notification badge, user dropdown | — |
| Navigation | Error Pages | 404, 403, 500 pages with navigation guidance | — |
| Workflow | Approval Configuration | Drag-drop step builder, role assignment, skip conditions | — |
| Permissions | Roles and Permissions | Admin, Sales, Design, PM, QS, Accounting, Warehouse, Management | — |
| Notifications | Notification Center | Pull-down panel, full-page notification list, module categorization, mark as read, delete, link to record | Implemented |
| Search | Global Search and Filter | Cross-module search, advanced filter, saved filter presets | — |
| Audit Log | Audit Log Viewer | Filter by user, action, module, date range; view change details | — |
| Master Data | Reference Data Management | CRUD for statuses, lead sources, contract types, material types, checklist templates | — |

---

## 4. User Roles and Permissions

The platform uses permission-based authorization. `SUPER_ADMIN`, `ADMIN`, and `USER` remain system identities, while backend-defined business roles carry a database role ID and an editable permission set. Controllers enforce permission codes; frontend route and action gates mirror them for usability but are not the security boundary.

### System Roles

| Role        | Access Level                                                                       |
|-------------|------------------------------------------------------------------------------------|
| USER        | Default registered account; receives own-profile permissions only.                    |
| ADMIN       | Broad catalog access except protected user-management and audit-management capabilities. |
| SUPER_ADMIN | Full permission catalog and lockout-safety protections.                               |

### Business Roles

The seeded role catalog is implemented and can be adjusted through **Admin > Roles**. Business-role permission edits persist in the database.

| Role | Module Access |
|------|---------------|
| `SALE`, `SALES_MANAGER` | CRM, quotations, tenders, surveys, and contracts according to assigned permissions |
| `DESIGN`, `DESIGN_LEAD`, `ARCHITECT`, `MEP_ENGINEER`, `STRUCT_ENGINEER` | Design projects and discipline workflows |
| `PM`, `SITE_COMMANDER`, `SITE_QAQC`, `LEGAL_OFFICER` | CM preparation/review, BCH site operations, QA/QC, permitting, and construction workflows according to assigned permissions |
| `QS`, `PROCUREMENT`, `ACCOUNTANT`, `WAREHOUSE` | Site QS/tendering, procurement, finance-adjacent, and operational access currently present in the catalog |
| `HR` | Recruitment applications, positions, recruitment options, contacts, and shared master data; no Finance payment or approval permission |
| `BGD` | Broad read-oriented management access |

Permissions are grouped by module and action, for example `crm.quotes.approve`, `design.ifc.release`, `construction.punch.verify`, and `construction.handover.complete`. Some operational modules also separate assigned-scope access from global access with actions such as `view.all` or `manage.all`. See the dedicated RBAC guide for the complete behavior.

The customer PRD defines eleven business positions for the target operating
model. They are business responsibilities, not permission-code aliases:

| Target position | Primary responsibility | Current catalog note |
|---|---|---|
| PM Phòng CM | Portfolio schedule, budget, QA/QC, HSE, QS and VO control | First CM preparation/review slice is delivered under `PM`; full portfolio/baseline scope remains partial |
| Chỉ huy trưởng BCH | Coordinate one assigned construction site | `SITE_COMMANDER` and BCH routes are delivered; full offline/site scope remains partial |
| Kỹ sư hiện trường / QA-QC | Daily field evidence, QA/QC and Punchlist | Map through explicit construction permissions, not role name alone |
| Kỹ sư QS hiện trường | Upstream and Downstream quantity acceptance | Existing `QS` must not automatically imply both site and tendering duties |
| Nhân viên Cung ứng | RFQ, award, procurement and vendor performance | `PROCUREMENT` capability must remain distinct from warehouse custody |
| Nhân viên Kinh doanh / CRM | Lead, opportunity, quotation and Upstream revenue | `SALE` / `SALES_MANAGER` |
| Kỹ sư Đấu thầu / QS văn phòng | Tender plan, submission and estimating | Existing `QS` currently represents this KPI position |
| Kỹ sư / Kiến trúc sư Thiết kế | Design tasks, revisions and discipline output | `DESIGN`, `ARCHITECT`, `MEP_ENGINEER`, `STRUCT_ENGINEER` |
| Kỹ sư Pháp lý & Xin phép | Permit checklist and authority submissions | `LEGAL_OFFICER` |
| Kế toán Dự án | Receivables, payables, cash flow and P&L evidence | `ACCOUNTANT` |
| Hành chính Nhân sự | Recruitment, KPI closing/export and internal requests | Dedicated `HR` access is delivered for recruitment administration; KPI/internal-request scope remains open |

`Design Manager / Chủ nhiệm thiết kế` is the customer-facing name for the
department-level design owner; `Design Lead` remains the team-level role. The
target rule is one approver per task and CC for observers. Do not implement a
second mandatory approval chain from the older PRD without a new decision.

---

## 5. Authentication

### 5.1 Registration Flow

1. The user submits their phone number, full name, email, and password.
2. If OTP verification is enabled (configurable in site settings), the system sends a one-time password to the user's email address.
3. The user enters the OTP code to verify their identity.
4. Upon verification, the account is created with the USER role.

OTP verification can be toggled on or off by administrators through site settings. When disabled, registration completes immediately after submission.
The resend action is disabled while the current OTP is valid and shows a countdown before another code can be requested.

### 5.2 Login Flow

1. The user submits their phone number and password.
2. On success, the system returns an access token (valid for 7 days) and a refresh token (valid for 30 days).
3. The access token is included in subsequent API requests for authentication.

### 5.3 Token Refresh

When the access token expires, the client sends the refresh token to obtain a new access token. The old refresh token is revoked and a new one is issued. This allows sessions to persist without requiring re-authentication.

### 5.4 Password Reset Flow

1. The user submits their registered phone number.
2. If OTP verification is enabled, the system sends an OTP code to the registered email.
3. After OTP verification, the user sets a new password.
4. If OTP is disabled, the password can be reset directly.

The password reset OTP screen uses the same resend countdown as registration.

### 5.5 Logout

The client sends the refresh token to the logout endpoint. The refresh token is revoked, ending the session.

---

## 6. Public Website Features

### 6.1 Pages

| Page               | Route                  | Description                                                    |
|--------------------|------------------------|----------------------------------------------------------------|
| Homepage           | `/`                    | Slideshow banner, featured content sections                     |
| Services           | `/services`            | List of all service offerings                                   |
| Service Detail     | `/services/:slug`      | Full service description with structured sections               |
| Projects           | `/projects`            | Portfolio of completed and ongoing projects                     |
| Project Detail     | `/projects/:slug`      | Project details including gallery, challenges, solutions        |
| News               | `/news`                | Company news and announcements                                  |
| News Detail        | `/news/:slug`          | Full news article content                                       |
| Activities         | `/activities`          | Company activities and events                                   |
| Activity Detail    | `/activities/:slug`    | Full activity report with content paragraphs                    |
| Clients            | `/clients`             | Client, partner, and supplier logos                              |
| Recruitment        | `/recruitment`         | Open job positions with application submission                  |
| Contact            | `/contact`             | Contact form for visitor inquiries                               |
| Login              | `/login`               | User authentication                                              |
| Register           | `/register`            | New user registration                                            |
| Forgot Password    | `/forgot-password`     | Password reset                                                   |
| Company Profile    | `/profile`             | Public company/about information                                 |
| My Profile         | `/my-profile`          | Authenticated profile, password, and personal document management |
| Forbidden          | `/forbidden`           | Permission-denied result                                         |

The homepage project search uses current API categories, exact published scale
labels and locations. Submitting opens Projects with category, scale and
location query parameters; filters combine with status and text search. Remove
an individual filter using its chip. Refresh and shared URLs retain these filters.
The footer links to Contact; it does not collect newsletter subscriptions.

Translation management uses Previous/Next and a moving page window, including
pages after page 10. KPI configuration uses labelled cards below the desktop
breakpoint and a comparison table on desktop. Dialogs scroll within the visible
viewport on short screens.

### 6.2 Language Selection

Visitors can switch between four supported languages using the language toggle in the navigation. The selected language applies to all UI text and, where available, content translations.

### 6.3 Contact Form

The contact form collects:
- Name
- Email
- Phone
- Subject
- Message

Submitted messages are stored in the system and visible to administrators. No authentication is required.

### 6.4 Job Application

Visitors can browse open positions and submit applications with:
- Candidate name
- Email
- Phone
- Years of experience
- Cover letter
- CV file upload

Applications are submitted publicly without authentication. Administrators review applications through the admin panel.

---

## 7. Administration Panel

The admin panel is accessible at `/admin` to authenticated users who hold the required backend permission for a route. Business roles therefore see only the navigation and actions granted by their permission set.

### 7.1 Dashboard

The main admin page provides an overview of system status and quick access to management sections.

### 7.2 Admin Navigation

| Group | Routes | Purpose |
|-------|--------|---------|
| Dashboard | `/admin`, `/admin/notifications` | Operational overview and personal notifications |
| CRM | `/admin/leads`, `/admin/customers`, `/admin/opportunities`, `/admin/quotes`, `/admin/capability-documents`, `/admin/tenders`, `/admin/surveys`, `/admin/contracts` | Sales and contract lifecycle |
| Design and permits | `/admin/design-projects`, `/admin/permits` | Concept, basic design, detail design, revision, IFC, and permit workflows |
| Construction | `/admin/construction/tasks`, `/diary`, `/punchlist`, `/acceptance`, `/asbuilt`, `/handover` | Delivery, quality, acceptance, and handover |
| Content | `/admin/activities`, `/admin/news`, `/admin/projects`, `/admin/services`, `/admin/about`, `/admin/categories` | Public website content and categories |
| Recruitment and contacts | `/admin/recruitment`, `/admin/recruitment/employment-types`, `/admin/contacts` | Positions, applications, options, and inquiries |
| Branding and processes | `/admin/clients`, `/admin/partners`, `/admin/suppliers`, `/admin/awards`, `/admin/processes/*` | Logo catalogs and internal process documents |
| Configuration | `/admin/settings`, `/admin/email-templates`, `/admin/languages`, `/admin/translations`, `/admin/master-data`, `/admin/workflows` | Site, translation, master-data, and workflow configuration |
| Security and audit | `/admin/users`, `/admin/roles`, `/admin/activity-log` | Users, dynamic role permissions, and audit history |

Legacy `/admin/posts/*` URLs redirect to the activities workspace; `/admin/project-categories` and `/admin/slideshow` redirect to their current tab locations.

### 7.3 Export Excel

API-backed admin lists include an **Export Excel** action that downloads the currently filtered data as an Excel-compatible CSV file with UTF-8 encoding. The export is available for contacts, recruitment applications, posts, projects, translations, activity categories, employment types, and logo catalog pages for clients, partners, suppliers, and awards.

The export button is disabled when the current filtered list is empty. Demo/localStorage-only screens and media-heavy editors are intentionally excluded from this export pass.

---

## 8. Content Management

### 8.1 Common Operations

All content entities support the following operations:

- **Create**: Add a new entry with a unique slug, title, content, images, and metadata.
- **Read**: View entries in a list or retrieve a single entry by slug.
- **Update**: Modify any field of an existing entry.
- **Delete**: Permanently remove an entry.
- **Reorder**: Adjust display order through the sort order field.
- **Translate**: Provide content in multiple languages through entity translations.

### 8.2 Activities

Activities represent company events, milestones, and team activities. Each activity contains:

| Field       | Description                                                |
|-------------|------------------------------------------------------------|
| Slug        | URL-friendly unique identifier                              |
| Date        | Event date                                                  |
| Image URL   | Cover image                                                 |
| Category    | Activity category reference                                 |
| Author      | Author name                                                 |
| Title       | Activity title                                              |
| Excerpt     | Short summary for listing pages                             |
| Content     | Array of paragraphs forming the full article body           |
| Sort Order  | Numeric ordering for display                                |

Activities are organized by categories. Categories can be activated or deactivated independently.

### 8.3 News Articles

News articles follow the same structure as activities. They represent company announcements, press releases, and industry news.

### 8.4 Projects

Projects represent the company's portfolio of completed and ongoing work. Each project contains:

| Field        | Description                                               |
|--------------|-----------------------------------------------------------|
| Slug         | URL-friendly unique identifier                             |
| Image URL    | Primary project image                                      |
| Gallery      | JSON array of additional images                            |
| Name         | Project name                                               |
| Client       | Client name                                                |
| Location     | Project location                                           |
| Scale        | Project scale description                                  |
| Scope        | Scope of work                                              |
| Status       | `ongoing` or `completed`                                   |
| Year         | Project year                                               |
| Category     | Project category                                           |
| Description  | Full project description                                   |
| Challenges   | JSON array of project challenges                           |
| Solutions    | JSON array of solutions applied                            |
| Highlights   | JSON array of project highlights                           |
| Sort Order   | Numeric ordering for display                               |

### 8.5 Services

Services describe the company's service offerings. Each service contains:

| Field        | Description                                               |
|--------------|-----------------------------------------------------------|
| Slug         | URL-friendly unique identifier                             |
| Title        | Full service title                                         |
| Short Title  | Abbreviated title for navigation                           |
| Tagline      | Brief tagline                                              |
| Intro        | Introduction text                                          |
| Sections     | JSON array of structured sections (heading + body)         |
| Highlights   | JSON array of service highlights                           |
| Sort Order   | Numeric ordering for display                               |

### 8.6 Slideshow Items

Slideshow items appear as banner slides on the homepage. Each item contains:

| Field      | Description                                                 |
|------------|-------------------------------------------------------------|
| Slug       | URL-friendly unique identifier                               |
| Image URL  | Slide background image                                       |
| Title      | Slide title text                                             |
| Subtitle   | Slide subtitle text                                          |
| Link URL   | Optional destination URL                                     |
| Link Text  | Optional call-to-action text                                 |
| Is Active  | Whether the slide is currently displayed                     |
| Sort Order | Numeric ordering for display                                 |

### 8.7 Logos

Logos represent business relationships and are grouped by kind:

| Kind     | Description                              |
|----------|------------------------------------------|
| Client   | Companies the business has served         |
| Partner  | Business partners and collaborators       |
| Supplier | Material and service suppliers            |

Each logo entry includes a name, image URL, optional hyperlink, kind classification, and sort order.

### 8.8 Process Documents

Process documents are internal procedure descriptions grouped by category (using a `GroupKey`). Each document has a code, title, sort order, and optional attached assets (images and files).

Authorized users can search, create, edit, delete, and bulk-delete process documents. The editor supports image and file assets in addition to the seeded baseline. Seed data remains important for initial content, so operators must review seed behavior before changing manifests.

**Viewing images:**
- Each document that has image assets displays a thumbnail grid below its title.
- Clicking any thumbnail opens a fullscreen lightbox with previous/next navigation.
- A toggle button switches between the default thumbnail grid and a vertical stack view that shows all images at full width.

**Downloading files:**
- Each document that has file assets displays a downloadable file list.
- Clicking a file entry downloads it directly from the server.

**Asset storage:**
- Physical asset files are served as static files from `nihomebackend/wwwroot/process-assets/`.
- Images are served from `/process-assets/images/` and files from `/process-assets/files/`.
- Asset metadata (display name, URL, MIME type, file size, sort order) is stored as JSON in the `ImagesJson` and `FilesJson` columns on the `process_documents` table and seeded from `nihomebackend/Data/Seeds/content/processes.json`.

---

## 9. Recruitment Management

### 9.1 Job Positions

Administrators create and manage job positions with the following details:

| Field            | Description                                          |
|------------------|------------------------------------------------------|
| Title            | Position title                                        |
| Department       | Department name                                       |
| Location         | Work location                                         |
| Employment Type  | Employment type code managed in Employment Types API  |
| Experience Level | `junior`, `mid`, or `senior`                          |
| Description      | Full position description                             |
| Requirements     | JSON array of position requirements                   |
| Is Active        | Whether the position is publicly visible              |
| Sort Order       | Numeric ordering for display                          |

Positions can be activated or deactivated. Only active positions are visible to public visitors. Administrators can view all positions including inactive ones.

### 9.2 Job Applications

When a candidate submits an application, it is created with the `new` status. The application lifecycle follows these statuses:

| Status    | Description                                                |
|-----------|------------------------------------------------------------|
| new       | Freshly submitted application, awaiting review             |
| interview | Candidate has been selected for an interview               |
| hired     | Candidate has been hired                                   |
| rejected  | Application has been declined                              |

Administrators can:
- View all applications, optionally filtered by position and status
- Update the status of individual applications
- Delete applications

When a new application is submitted, the system can send an email notification to the configured notification address using a customizable email template.

### 9.3 Application Data

Each application contains:
- Candidate name, email, and phone
- Years of experience
- Cover letter (text)
- CV file URL (uploaded separately)
- Application status and timestamps

---

## 10. Contact Management

### 10.1 Message Submission

Visitors submit contact messages through the public contact form. Each message contains:
- Name
- Email
- Phone
- Subject
- Message body

No authentication is required to submit a contact message.

### 10.2 Message Processing

Administrators can:
- View all contact messages in a list
- Filter messages by replied or unreplied status
- View individual message details
- Reply to a message (the reply content is sent to the contact's email address via SMTP)
- Mark a message as replied without sending an email (for messages handled through other channels)
- Delete messages

Each message tracks whether it has been replied to, the reply content, and the reply timestamp.

---

## 11. Internationalization

### 11.1 Supported Languages

| Code | Language   |
|------|------------|
| vi   | Vietnamese |
| en   | English    |
| zh   | Chinese    |
| ja   | Japanese   |

Vietnamese is the primary language. All other languages are translations.

### 11.2 Static UI Translations

UI labels, button text, navigation items, error messages, and other static text are managed through the translation system. Each translation entry consists of:
- A unique key (e.g., `nav.home`, `btn.submit`)
- A language code
- The translated value
- A category for organizational grouping

Translations are managed through the admin panel at `/admin/translations`. Administrators can:
- View all keys with values across all languages
- Filter by category and search terms
- Create or update individual translation pairs
- Bulk import or update translations
- Delete keys across all languages

### 11.3 Entity Translations

The **Content translations** tab centralizes field-level translations for activities, news, projects, services, homepage slideshow items, job positions, about sections, and the activity, news, project, and as-built document category catalogs. Category CRUD screens only maintain the Vietnamese source name; English, Chinese, and Japanese values are edited here.

Each translation record specifies:
- Entity type (for example, `Activity` or `Project`)
- Entity ID
- Field name (e.g., `Title`, `Excerpt`)
- Language code
- Translated value

The list shows translation progress across all three target languages. Reset removes the entity's translations and restores source-content fallback. Structured fields must contain valid JSON with the same keys, value types, array lengths, and ordering as the Vietnamese source; this prevents translated content from changing the public data contract.

Users need `content.translations.view` to open this module and `content.translations.manage` to save or reset translations. Category API payloads containing `nameEn`, `nameZh`, or `nameJa` also require translation-manage permission.

### 11.4 Translation Categories

Translations are organized into categories for management convenience. Administrators can view the list of available categories and filter translations accordingly.

---

## 12. Email System

### 12.1 Email Types

The platform sends automated emails or lets authorized staff send them for the following events:

| Event                    | Recipient              | Trigger                                    |
|--------------------------|------------------------|--------------------------------------------|
| Registration OTP         | Registering user       | User starts registration (OTP enabled)     |
| Password Reset OTP       | Account holder         | User initiates password reset (OTP enabled)|
| New Application Notice   | Notification address   | Candidate submits a job application         |
| Contact Reply            | Contact message sender | Administrator replies to a contact message  |
| Approved CRM Quote       | Selected customer email | Sales sends an approved, valid quote          |

### 12.2 Email Templates

OTP, job application notification, and CRM quote emails use configurable templates with `{{tokenName}}` substitution. The default OTP and CRM quote templates use branded HTML. Open `/admin/email-templates` and choose **Báo giá** to edit the subject and HTML body; the right pane previews its layout. Keep `{{quoteLines}}` and `{{grandTotal}}` in the body. Only basic HTML and safe inline styles are accepted; scripts, event attributes, and plain-text quote bodies are rejected. Saving requires `system.settings.manage`. When sending an approved quote, check the recipient and the rendered preview in the send dialog; only users with `crm.quotes.manage` may edit the subject or body.

**OTP Template Placeholders:**

| Placeholder   | Substituted Value                |
|---------------|----------------------------------|
| `{{siteName}}`  | The site name from settings      |
| `{{otpCode}}`   | The generated one-time password  |

**Job Application Template Placeholders:**

| Placeholder       | Substituted Value                |
|-------------------|----------------------------------|
| `{{siteName}}`      | The site name from settings      |
| `{{candidateName}}` | The applicant's full name        |
| `{{positionTitle}}`  | The job position title           |

**Quote template placeholders:** `{{customerName}}`, `{{quoteCode}}`, `{{opportunityName}}`, `{{quoteLines}}`, `{{discountPercent}}`, `{{vatPercent}}`, `{{grandTotal}}`, and `{{validUntil}}`. The body must retain `{{quoteLines}}` and `{{grandTotal}}` so the customer sees priced items and the total. Variables are HTML-encoded before rendering; use the preview to check the final layout and figures before sending.

Templates are configured through the admin panel at `/admin/email-templates` or via the Site Settings API.

### 12.3 Site Settings for Email

The following settings control email behavior:

| Setting                              | Description                                           |
|--------------------------------------|-------------------------------------------------------|
| Enable OTP for Registration          | Toggle OTP verification during registration           |
| Enable OTP for Forgot Password       | Toggle OTP verification during password reset         |
| OTP Email Subject Template           | Subject line template for OTP emails                  |
| OTP Email Body Template              | Body template for OTP emails                          |
| New Application Email Subject        | Subject template for application notification emails  |
| New Application Email Body           | Body template for application notification emails     |
| Notification Email                   | Email address that receives application notifications |
| Quote Email Subject Template        | Subject prefilled when sending a CRM quote            |
| Quote Email Body Template           | Plain-text body prefilled when sending a CRM quote    |

Administrators can toggle registration and forgot-password OTP verification from `/admin/settings` in the Security tab.

---

## 13. File Uploads

### 13.1 Image Uploads

The system accepts image uploads through the `/api/system/upload-image` endpoint.

| Constraint       | Value                              |
|------------------|------------------------------------|
| Maximum file size | 5 MB                              |
| Accepted formats | JPG, JPEG, PNG, GIF, WebP, SVG    |

Uploaded images are stored in the backend's `wwwroot/` directory and served as static files. A background cleanup service periodically removes orphaned uploaded images that are no longer referenced by any content entity.

### 13.2 CV Uploads

Job applications support CV file uploads. The CV file URL is stored with the application record.

---

## 14. API Reference

For the standard Docker Compose development setup, the backend API is served at `http://localhost:5043/api`. All request and response bodies use JSON format.

Interactive API documentation is available at:

- Swagger UI: `http://localhost:5043/swagger`
- OpenAPI JSON: `http://localhost:5043/swagger/v1/swagger.json`

Protected endpoints require the access token in the `Authorization` header:

```
Authorization: Bearer <access-token>
```

This chapter contains selected request examples, not a frozen exhaustive contract. The controller families currently implemented are:

| Area | API bases |
|------|-----------|
| Authentication and profile | `/api/auth`, `/api/users/me`, `/api/users`, `/api/admin/rbac` |
| Content and communication | `/api/about-sections`, `/api/activities`, `/api/activity-categories`, `/api/news`, `/api/news-categories`, `/api/projects`, `/api/project-categories`, `/api/services`, `/api/slideshow`, `/api/logos`, `/api/processes`, `/api/contacts`, `/api/translations`, `/api/mail` |
| Recruitment and system | `/api/job-positions`, `/api/job-applications`, `/api/employment-types`, `/api/recruitment-dropdown-options`, `/api/site-settings`, `/api/notifications`, `/api/audit-logs`, `/api/master-data`, `/api/workflows`, `/api/system` |
| CRM and contracts | `/api/leads`, `/api/customers`, `/api/opportunities`, `/api/quotes`, `/api/capability-documents`, `/api/tenders`, `/api/surveys`, `/api/contracts` |
| Design and permitting | `/api/design-projects`, `/api/concept-options`, `/api/basic-design-docs`, `/api/shop-drawings`, `/api/drawing-revisions`, `/api/ifc-releases`, `/api/permits` |
| Construction | `/api/construction-tasks`, `/api/site-diaries`, `/api/punch-items`, `/api/acceptance-records`, `/api/as-built-documents`, `/api/handover-records` |

Most newer controllers also expose the same contract below `/api/v1`; use the controller attributes or Development Swagger as the authoritative route source. Protected operations are permission-based rather than role-name-based.

### 14.1 Authentication

| Method | Endpoint                          | Auth     | Description                              |
|--------|-----------------------------------|----------|------------------------------------------|
| POST   | `/api/auth/register/start`        | Public   | Start registration (triggers OTP if enabled) |
| POST   | `/api/auth/register/verify-otp`   | Public   | Verify OTP during registration           |
| POST   | `/api/auth/register/complete`     | Public   | Complete registration after OTP          |
| POST   | `/api/auth/register/resend-otp`   | Public   | Resend registration OTP                  |
| POST   | `/api/auth/login`                 | Public   | Login with phone number or email and password; send the identifier in the existing `phoneNumber` field |
| POST   | `/api/auth/refresh`               | Public   | Refresh access token                     |
| POST   | `/api/auth/logout`                | Public   | Revoke refresh token                     |
| POST   | `/api/auth/forgot/start`          | Public   | Start password reset                     |
| POST   | `/api/auth/forgot/verify-otp`     | Public   | Verify password reset OTP                |
| POST   | `/api/auth/forgot/complete`       | Public   | Complete password reset                  |
| POST   | `/api/auth/forgot/reset-direct`   | Public   | Reset password directly (OTP disabled)   |
| POST   | `/api/auth/forgot/resend-otp`     | Public   | Resend password reset OTP                |

#### Login Request

```json
{
  "phoneNumber": "<registered-phone>",
  "password": "<account-password>"
}
```

#### Login Response

```json
{
  "user": {
    "id": 1,
    "phone": "<registered-phone>",
    "fullName": "Example User",
    "email": "user@example.com",
    "role": "USER"
  },
  "accessToken": "<jwt-token>",
  "refreshToken": "<refresh-token>",
  "otpRequired": false
}
```

#### Register Start Request

```json
{
  "phoneNumber": "0901234567",
  "fullName": "Nguyen Van A",
  "email": "user@example.com",
  "password": "SecurePassword@1"
}
```

#### Refresh Request

```json
{
  "refreshToken": "<refresh-token>"
}
```

### 14.2 Activities

| Method | Endpoint                     | Auth   | Description                   |
|--------|------------------------------|--------|-------------------------------|
| GET    | `/api/activities?lang=vi`    | Public | List all activities           |
| GET    | `/api/activities/{slug}`     | Public | Get activity by slug          |
| POST   | `/api/activities`            | Admin  | Create activity               |
| PUT    | `/api/activities/{id}`       | Admin  | Update activity               |
| DELETE | `/api/activities/{id}`       | Admin  | Delete activity               |

#### Create/Update Activity Request

```json
{
  "slug": "team-building-2026",
  "date": "2026-04-15",
  "imageUrl": "/uploads/activity-cover.jpg",
  "category": "Team Building",
  "author": "Admin",
  "title": "Annual Team Building Event",
  "excerpt": "Summary of the team building event",
  "content": ["First paragraph.", "Second paragraph."],
  "sortOrder": 1
}
```

### 14.3 Activity Categories

Opening a category or recruitment-options list does not create records. If a
list is empty, an administrator must create the needed activity, news, project,
employment-type, or as-built document categories, or recruitment-dropdown
options through the corresponding management action. Existing content is not
silently converted into category records when someone views a list.
Saving an activity, news article, or public project with an unknown category
also does not create a category: create it in category management first, then
select it in the content form. Updates containing a non-empty category without
a matching managed category are rejected and leave the record unchanged.

| Method | Endpoint                                        | Auth   | Description           |
|--------|-------------------------------------------------|--------|-----------------------|
| GET    | `/api/activity-categories?includeInactive=false` | Public | List categories       |
| POST   | `/api/activity-categories`                      | Admin  | Create category       |
| PUT    | `/api/activity-categories/{id}`                 | Admin  | Update category       |
| DELETE | `/api/activity-categories/{id}`                 | Admin  | Delete category       |

#### Create/Update Category Request

```json
{
  "name": "Training",
  "isActive": true,
  "sortOrder": 1
}
```

### 14.4 News

| Method | Endpoint                 | Auth   | Description                 |
|--------|--------------------------|--------|-----------------------------|
| GET    | `/api/news?lang=vi`      | Public | List all news articles      |
| GET    | `/api/news/{slug}`       | Public | Get news article by slug    |
| POST   | `/api/news`              | Admin  | Create news article         |
| PUT    | `/api/news/{id}`         | Admin  | Update news article         |
| DELETE | `/api/news/{id}`         | Admin  | Delete news article         |

### 14.5 Projects

| Method | Endpoint                 | Auth   | Description              |
|--------|--------------------------|--------|--------------------------|
| GET    | `/api/projects`          | Public | List all projects        |
| GET    | `/api/projects/{slug}`   | Public | Get project by slug      |
| POST   | `/api/projects`          | Admin  | Create project           |
| PUT    | `/api/projects/{id}`     | Admin  | Update project           |
| DELETE | `/api/projects/{id}`     | Admin  | Delete project           |

#### Create/Update Project Request

```json
{
  "slug": "trimas-factory",
  "imageUrl": "/uploads/project-main.jpg",
  "galleryJson": "[\"/uploads/p1.jpg\", \"/uploads/p2.jpg\"]",
  "name": "TriMas Factory Completion",
  "client": "TriMas Corporation",
  "location": "Binh Duong, Vietnam",
  "scale": "5,000 sqm",
  "scope": "Full construction and fit-out",
  "status": "completed",
  "year": 2025,
  "category": "Industrial",
  "description": "Full project description.",
  "challengesJson": "[\"Tight timeline\", \"Complex requirements\"]",
  "solutionsJson": "[\"Phased delivery\", \"Dedicated team\"]",
  "highlightsJson": "[\"On-time delivery\", \"Zero incidents\"]",
  "sortOrder": 1
}
```

### 14.6 Services

| Method | Endpoint                 | Auth   | Description              |
|--------|--------------------------|--------|--------------------------|
| GET    | `/api/services`          | Public | List all services        |
| GET    | `/api/services/{slug}`   | Public | Get service by slug      |
| POST   | `/api/services`          | Admin  | Create service           |
| PUT    | `/api/services/{id}`     | Admin  | Update service           |
| DELETE | `/api/services/{id}`     | Admin  | Delete service           |

#### Create/Update Service Request

```json
{
  "slug": "construction-management",
  "title": "Construction Management Services",
  "shortTitle": "Construction",
  "tagline": "Professional construction management",
  "intro": "Introduction text for the service.",
  "sectionsJson": "[{\"heading\": \"Approach\", \"body\": \"Our approach...\"}]",
  "highlightsJson": "[\"20+ years experience\", \"100+ projects\"]",
  "sortOrder": 1
}
```

### 14.7 Slideshow

| Method | Endpoint                                    | Auth   | Description                |
|--------|---------------------------------------------|--------|----------------------------|
| GET    | `/api/slideshow?lang=vi&activeOnly=true`    | Public | List slideshow items       |
| GET    | `/api/slideshow/{slug}`                     | Public | Get slideshow item by slug |
| POST   | `/api/slideshow`                            | Admin  | Create slideshow item      |
| PUT    | `/api/slideshow/{id}`                       | Admin  | Update slideshow item      |
| DELETE | `/api/slideshow/{id}`                       | Admin  | Delete slideshow item      |

### 14.8 Recruitment -- Job Positions

| Method | Endpoint                                    | Auth   | Description                |
|--------|---------------------------------------------|--------|----------------------------|
| GET    | `/api/job-positions?includeInactive=false`  | Public | List active job positions  |
| GET    | `/api/job-positions/{id}`                   | Public | Get job position detail    |
| POST   | `/api/job-positions`                        | Admin  | Create job position        |
| PUT    | `/api/job-positions/{id}`                   | Admin  | Update job position        |
| DELETE | `/api/job-positions/{id}`                   | Admin  | Delete job position        |

#### Create/Update Job Position Request

```json
{
  "title": "Project Engineer",
  "department": "Engineering",
  "location": "Ho Chi Minh City",
  "employmentType": "full-time",
  "experienceLevel": "mid",
  "description": "Full position description.",
  "requirementsJson": "[\"3+ years experience\", \"Engineering degree\"]",
  "isActive": true,
  "sortOrder": 1
}
```

### 14.9 Recruitment -- Employment Types

| Method | Endpoint                                      | Auth   | Description                                         |
|--------|-----------------------------------------------|--------|-----------------------------------------------------|
| GET    | `/api/employment-types?includeInactive=false` | Public | List active employment types                        |
| POST   | `/api/employment-types`                       | Admin  | Create employment type (`code`, `name`, etc.)      |
| PUT    | `/api/employment-types/{id}`                  | Admin  | Update employment type                              |
| DELETE | `/api/employment-types/{id}`                  | Admin  | Delete employment type (blocked if still in use)   |

### 14.10 Recruitment -- Job Applications

| Method | Endpoint                                           | Auth   | Description                         |
|--------|----------------------------------------------------|--------|-------------------------------------|
| GET    | `/api/job-applications?positionId=&status=`         | Admin  | List applications with filters      |
| POST   | `/api/job-applications`                             | Public | Submit a job application            |
| PATCH  | `/api/job-applications/{id}/status`                 | Admin  | Update application status           |
| DELETE | `/api/job-applications/{id}`                        | Admin  | Delete application                  |

Application statuses: `new`, `interview`, `hired`, `rejected`.

#### Submit Application Request

```json
{
  "jobPositionId": 1,
  "candidateName": "Nguyen Van B",
  "email": "candidate@example.com",
  "phone": "0901234567",
  "experienceYears": 5,
  "coverLetter": "I am interested in this position...",
  "cvUrl": "/uploads/cv-nguyen-van-b.pdf"
}
```

#### Update Status Request

```json
{
  "status": "interview"
}
```

### 14.11 Contact Messages

| Method | Endpoint                                  | Auth   | Description                          |
|--------|-------------------------------------------|--------|--------------------------------------|
| POST   | `/api/contacts`                           | Public | Submit a contact message             |
| GET    | `/api/contacts?replied=false`             | Admin  | List contact messages                |
| GET    | `/api/contacts/{id}`                      | Admin  | Get contact message detail           |
| POST   | `/api/contacts/{id}/reply`                | Admin  | Reply to contact (sends email)       |
| PATCH  | `/api/contacts/{id}/mark-replied`         | Admin  | Mark as replied without sending email|
| DELETE | `/api/contacts/{id}`                      | Admin  | Delete contact message               |

#### Submit Contact Request

```json
{
  "name": "Tran Van C",
  "email": "visitor@example.com",
  "phone": "0909876543",
  "subject": "Service inquiry",
  "message": "I would like to learn more about your services."
}
```

#### Reply Request

```json
{
  "replyContent": "Thank you for your inquiry. We will contact you shortly."
}
```

### 14.12 Logos (Clients, Partners, Suppliers)

| Method | Endpoint            | Auth   | Description                               |
|--------|---------------------|--------|-------------------------------------------|
| GET    | `/api/logos`        | Public | List all logos grouped by kind             |
| POST   | `/api/logos`        | Admin  | Create logo                               |
| PUT    | `/api/logos/{id}`   | Admin  | Update logo                               |
| DELETE | `/api/logos/{id}`   | Admin  | Delete logo                               |

Logo kinds: `Client`, `Partner`, `Supplier`.

#### Create/Update Logo Request

```json
{
  "name": "Partner Company",
  "imageUrl": "/uploads/partner-logo.png",
  "href": "https://partner.example.com",
  "kind": "Partner",
  "sortOrder": 1
}
```

### 14.13 Processes

| Method | Endpoint               | Auth   | Description                          |
|--------|------------------------|--------|--------------------------------------|
| GET    | `/api/processes`       | Public | List processes grouped by category   |
| POST   | `/api/processes`       | Admin  | Create process document              |
| PUT    | `/api/processes/{id}`  | Admin  | Update process document              |
| DELETE | `/api/processes/{id}`  | Admin  | Delete process document              |

#### GET Response (per item)

```json
{
  "id": 1,
  "groupKey": "quality-control",
  "code": "QC-001",
  "title": "Material Inspection Procedure",
  "sortOrder": 1,
  "images": [
    {
      "displayName": "Inspection checklist",
      "url": "/process-assets/images/abc123.jpg",
      "originalFileName": "checklist.jpg",
      "contentType": "image/jpeg",
      "fileSizeBytes": 204800,
      "sortOrder": 1
    }
  ],
  "files": [
    {
      "displayName": "Inspection form template",
      "url": "/process-assets/files/def456.docx",
      "originalFileName": "form-template.docx",
      "contentType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      "fileSizeBytes": 51200,
      "sortOrder": 1
    }
  ]
}
```

#### Create/Update Process Request

```json
{
  "groupKey": "quality-control",
  "code": "QC-001",
  "title": "Material Inspection Procedure",
  "sortOrder": 1
}
```

### 14.14 Site Settings

| Method | Endpoint                             | Auth   | Description              |
|--------|--------------------------------------|--------|--------------------------|
| GET    | `/api/site-settings/email-templates` | Admin  | Get email templates      |
| PUT    | `/api/site-settings/email-templates` | Admin  | Update email templates   |
| GET    | `/api/site-settings/otp-settings`    | Public | Get OTP settings         |
| PUT    | `/api/site-settings/otp-settings`    | Admin  | Update OTP settings      |

#### Update Email Templates Request

```json
{
  "newApplicationEmailSubjectTemplate": "[{{siteName}}] New Application for {{positionTitle}}",
  "newApplicationEmailBodyTemplate": "A new application has been submitted by {{candidateName}} for {{positionTitle}}.",
  "notificationEmail": "hr@nihome.vn",
  "otpEmailSubjectTemplate": "[{{siteName}}] Your verification code",
  "otpEmailBodyTemplate": "Your OTP code is: {{otpCode}}",
  "quoteEmailSubjectTemplate": "Báo giá {{quoteCode}} – {{opportunityName}}",
  "quoteEmailBodyTemplate": "Kính gửi {{customerName}},\n{{quoteLines}}\nTổng giá trị: {{grandTotal}} VND"
}
```

#### Update OTP Settings Request

```json
{
  "enableOtpForRegistration": true,
  "enableOtpForForgotPassword": true
}
```

### 14.15 Translations

| Method | Endpoint                              | Auth   | Description                              |
|--------|---------------------------------------|--------|------------------------------------------|
| GET    | `/api/translations/{lang}`            | Public | Get all UI translations for a language   |
| GET    | `/api/translations/admin`             | Admin  | Get translation pairs with filtering     |
| GET    | `/api/translations/categories`        | Admin  | Get translation categories               |
| POST   | `/api/translations/pair`              | Admin  | Create or update a translation pair      |
| POST   | `/api/translations/bulk`              | Admin  | Bulk create or update translations       |
| DELETE | `/api/translations/key/{key}`         | Admin  | Delete a translation key (all languages) |
| GET    | `/api/translations/entity/types`      | Admin  | List entity types with translatable fields|
| GET    | `/api/translations/entity/{type}`     | Admin  | List entities with translation progress   |
| GET    | `/api/translations/entity/{type}/{id}`| Admin  | Get source content and all translations   |
| POST   | `/api/translations/entity/{type}/{id}`| Admin  | Save one target language                  |
| DELETE | `/api/translations/entity/{type}/{id}`| Admin  | Reset all translations for an entity      |

#### Create/Update Translation Pair Request

```json
{
  "key": "nav.home",
  "vietnameseValue": "Trang chu",
  "translations": {
    "en": "Home",
    "zh": "首页",
    "ja": "ホーム"
  },
  "category": "navigation"
}
```

### 14.16 About Sections

| Method | Endpoint                           | Auth   | Description                                        |
|--------|------------------------------------|--------|----------------------------------------------------|
| GET    | `/api/about-sections?activeOnly=true` | Public | List sections used on the profile/about page       |
| GET    | `/api/about-sections/{slug}`       | Public | Get a specific profile/about page section by slug  |
| POST   | `/api/about-sections`              | Admin  | Create a profile/about page section                |
| PUT    | `/api/about-sections/{id}`         | Admin  | Update a profile/about page section                |
| DELETE | `/api/about-sections/{id}`         | Admin  | Delete a profile/about page section                |

#### Create/Update About Section Request

```json
{
  "slug": "timeline-main",
  "itemsJson": "[{\"year\":\"2006\",\"title\":\"Founded\",\"desc\":\"...\"}]",
  "eyebrow": "LỊCH SỬ",
  "titleA": "Dấu mốc phát triển",
  "titleB": "qua từng giai đoạn",
  "paragraph1": "",
  "paragraph2": "",
  "imageUrl": "/images/upload/timeline.jpg",
  "isActive": true,
  "sortOrder": 5
}
```

### 14.17 System

| Method | Endpoint                      | Auth   | Description              |
|--------|-------------------------------|--------|--------------------------|
| GET    | `/api/system/health`          | Public | Health check             |
| POST   | `/api/system/upload-image`    | Admin  | Upload image (max 5 MB)  |

#### Health Check Response

```json
{
  "name": "nihome-api",
  "environment": "Development",
  "status": "Healthy",
  "timestampUtc": "2026-04-26T12:00:00Z"
}
```

---

## 15. Data Models

### 15.1 User

| Field         | Type    | Description                              |
|---------------|---------|------------------------------------------|
| Id            | int     | Primary key                              |
| Phone         | string  | Phone number (unique, used for login)    |
| FullName      | string  | User's full name                         |
| Email         | string  | Email address                            |
| PasswordHash  | string  | Hashed password                          |
| Role          | enum    | SUPER_ADMIN, ADMIN, or USER              |
| IsActive      | bool    | Account active status                    |
| AvatarUrl     | string  | Optional profile image URL               |
| CreatedAt     | datetime| Account creation timestamp               |
| UpdatedAt     | datetime| Last update timestamp                    |

### 15.2 Activity

| Field       | Type     | Description                           |
|-------------|----------|---------------------------------------|
| Id          | int      | Primary key                           |
| Slug        | string   | URL-friendly identifier (unique)      |
| Date        | datetime | Event date                            |
| ImageUrl    | string   | Cover image URL                       |
| Category    | string   | Category name                         |
| Author      | string   | Author name                           |
| Title       | string   | Activity title                        |
| Excerpt     | string   | Short summary                         |
| ContentJson | string   | JSON array of content paragraphs      |
| SortOrder   | int      | Display order                         |
| CreatedAt   | datetime | Creation timestamp                    |
| UpdatedAt   | datetime | Last update timestamp                 |

### 15.3 News Article

Same structure as Activity.

### 15.4 Project

| Field           | Type     | Description                        |
|-----------------|----------|------------------------------------|
| Id              | int      | Primary key                        |
| Slug            | string   | URL-friendly identifier (unique)   |
| ImageUrl        | string   | Primary image URL                  |
| GalleryJson     | string   | JSON array of image URLs           |
| Name            | string   | Project name                       |
| Client          | string   | Client name                        |
| Location        | string   | Project location                   |
| Scale           | string   | Project scale                      |
| Scope           | string   | Scope of work                      |
| Status          | string   | `ongoing` or `completed`           |
| Year            | int      | Project year                       |
| Category        | string   | Project category                   |
| Description     | string   | Full description                   |
| ChallengesJson  | string   | JSON array of challenges           |
| SolutionsJson   | string   | JSON array of solutions            |
| HighlightsJson  | string   | JSON array of highlights           |
| SortOrder       | int      | Display order                      |
| CreatedAt       | datetime | Creation timestamp                 |
| UpdatedAt       | datetime | Last update timestamp              |

### 15.5 Service Item

| Field          | Type     | Description                         |
|----------------|----------|-------------------------------------|
| Id             | int      | Primary key                         |
| Slug           | string   | URL-friendly identifier (unique)    |
| Title          | string   | Full title                          |
| ShortTitle     | string   | Abbreviated title                   |
| Tagline        | string   | Brief tagline                       |
| Intro          | string   | Introduction text                   |
| SectionsJson   | string   | JSON array of {heading, body}       |
| HighlightsJson | string   | JSON array of highlights            |
| SortOrder      | int      | Display order                       |
| CreatedAt      | datetime | Creation timestamp                  |
| UpdatedAt      | datetime | Last update timestamp               |

### 15.6 Slideshow Item

| Field     | Type     | Description                          |
|-----------|----------|--------------------------------------|
| Id        | int      | Primary key                          |
| Slug      | string   | URL-friendly identifier (unique)     |
| ImageUrl  | string   | Slide image URL                      |
| Title     | string   | Slide title                          |
| Subtitle  | string   | Slide subtitle                       |
| LinkUrl   | string   | Optional link destination            |
| LinkText  | string   | Optional call-to-action text         |
| IsActive  | bool     | Whether the slide is displayed       |
| SortOrder | int      | Display order                        |
| CreatedAt | datetime | Creation timestamp                   |
| UpdatedAt | datetime | Last update timestamp                |

### 15.7 Job Position

| Field            | Type     | Description                       |
|------------------|----------|-----------------------------------|
| Id               | int      | Primary key                       |
| Title            | string   | Position title                    |
| Department       | string   | Department name                   |
| Location         | string   | Work location                     |
| EmploymentType   | string   | Employment type code              |
| ExperienceLevel  | string   | `junior`, `mid`, or `senior`      |
| Description      | string   | Position description              |
| RequirementsJson | string   | JSON array of requirements        |
| IsActive         | bool     | Public visibility                 |
| SortOrder        | int      | Display order                     |
| CreatedAt        | datetime | Creation timestamp                |
| UpdatedAt        | datetime | Last update timestamp             |

### 15.8 Employment Type

| Field     | Type     | Description                                 |
|-----------|----------|---------------------------------------------|
| Id        | int      | Primary key                                 |
| Code      | string   | Unique code used by `job_positions`         |
| Name      | string   | Display name                                |
| IsActive  | bool     | Whether available for new position creation |
| SortOrder | int      | Display order                               |
| CreatedAt | datetime | Creation timestamp                          |
| UpdatedAt | datetime | Last update timestamp                       |

### 15.9 Job Application

| Field           | Type     | Description                        |
|-----------------|----------|------------------------------------|
| Id              | int      | Primary key                        |
| JobPositionId   | int      | Foreign key to job position        |
| CandidateName   | string   | Applicant name                     |
| Email           | string   | Applicant email                    |
| Phone           | string   | Applicant phone                    |
| ExperienceYears | int      | Years of experience                |
| CoverLetter     | string   | Cover letter text                  |
| CvUrl           | string   | Uploaded CV file URL               |
| Status          | string   | `new`, `interview`, `hired`, or `rejected` |
| AppliedAt       | datetime | Submission timestamp               |
| UpdatedAt       | datetime | Last status change timestamp       |

### 15.10 Contact Message

| Field        | Type     | Description                         |
|--------------|----------|-------------------------------------|
| Id           | int      | Primary key                         |
| Name         | string   | Sender name                         |
| Email        | string   | Sender email                        |
| Phone        | string   | Sender phone                        |
| Subject      | string   | Message subject                     |
| Message      | string   | Message body                        |
| IsReplied    | bool     | Whether the message has been replied |
| ReplyContent | string   | Reply text (if replied)             |
| RepliedAt    | datetime | Reply timestamp                     |
| CreatedAt    | datetime | Submission timestamp                |
| UpdatedAt    | datetime | Last update timestamp               |

### 15.11 Client Logo

| Field     | Type     | Description                          |
|-----------|----------|--------------------------------------|
| Id        | int      | Primary key                          |
| Name      | string   | Company name                         |
| ImageUrl  | string   | Logo image URL                       |
| Href      | string   | Optional website link                |
| Kind      | enum     | `Client`, `Partner`, or `Supplier`   |
| SortOrder | int      | Display order                        |
| CreatedAt | datetime | Creation timestamp                   |

### 15.12 Process Document

| Field      | Type     | Description                                              |
|------------|----------|----------------------------------------------------------|
| Id         | int      | Primary key                                              |
| GroupKey   | string   | Category grouping key                                    |
| Code       | string   | Document code                                            |
| Title      | string   | Document title                                           |
| SortOrder  | int      | Display order within group                               |
| ImagesJson | string?  | JSON array of `ProcessAssetInfo` objects (images)        |
| FilesJson  | string?  | JSON array of `ProcessAssetInfo` objects (file downloads)|
| CreatedAt  | datetime | Creation timestamp                                       |

`ProcessAssetInfo` object shape:

| Field            | Type   | Description                        |
|------------------|--------|------------------------------------|
| displayName      | string | Human-readable label               |
| url              | string | Relative URL served as static file |
| originalFileName | string | Original uploaded file name        |
| contentType      | string | MIME type                          |
| fileSizeBytes    | long   | File size in bytes                 |
| sortOrder        | int    | Display order within the asset list|

### 15.13 Translation

| Field        | Type     | Description                         |
|--------------|----------|-------------------------------------|
| Id           | int      | Primary key                         |
| Key          | string   | Translation key (unique with lang)  |
| LanguageCode | string   | Language code (vi, en, zh, ja)      |
| Value        | string   | Translated text                     |
| Category     | string   | Organizational category             |
| CreatedAt    | datetime | Creation timestamp                  |
| UpdatedAt    | datetime | Last update timestamp               |

### 15.14 Entity Translation

| Field        | Type     | Description                         |
|--------------|----------|-------------------------------------|
| Id           | int      | Primary key                         |
| EntityType   | string   | Entity type name                    |
| EntityId     | int      | Entity primary key                  |
| FieldName    | string   | Translated field name               |
| LanguageCode | string   | Language code                       |
| Value        | string   | Translated value                    |
| CreatedAt    | datetime | Creation timestamp                  |
| UpdatedAt    | datetime | Last update timestamp               |

### 15.15 Site Settings

| Field                              | Type     | Description                                |
|------------------------------------|----------|--------------------------------------------|
| Id                                 | int      | Primary key                                |
| SiteName                           | string   | Platform name                              |
| SiteDescription                    | string   | Platform description                       |
| PrimaryEmail                       | string   | Main contact email                         |
| SecondaryEmail                     | string   | Secondary contact email                    |
| PrimaryPhone                       | string   | Main contact phone                         |
| SecondaryPhone                     | string   | Secondary contact phone                    |
| Address                            | string   | Physical address                           |
| EnableOtpForRegistration           | bool     | OTP toggle for registration                |
| EnableOtpForForgotPassword         | bool     | OTP toggle for password reset              |
| OtpEmailSubjectTemplate            | string   | OTP email subject template                 |
| OtpEmailBodyTemplate               | string   | OTP email body template                    |
| NewApplicationEmailSubjectTemplate | string   | Application notification subject template  |
| NewApplicationEmailBodyTemplate    | string   | Application notification body template     |
| NotificationEmail                  | string   | Address for application notifications      |
| CreatedAt                          | datetime | Creation timestamp                         |
| UpdatedAt                          | datetime | Last update timestamp                      |

---
