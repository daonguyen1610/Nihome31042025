# NICON Platform -- User Guide

Version 1.0

Last Updated: 7 May 2026

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
- **Design and Engineering**: Control design documents across three phases (Concept, Basic Design, Shop Drawing), manage revisions, and issue construction drawings (IFC).
- **Project Management**: Track construction schedules (Gantt), daily site logs, partial and full acceptance inspections, as-built documentation, and defect management (Punchlist).
- **Procurement**: Manage vendors and subcontractors, compare bids, control material BOQ, process material requests, track warehouse inventory, and alert on overruns.
- **Finance and Contracts**: Manage primary contracts, subcontracts, variation orders (VO), project cash flow, and profit/loss reporting.
- **Administration**: Manage website content, handle contact inquiries, process job applications, configure email templates, and maintain multi-language translations.
- **Public Visitors**: Browse the company website for information about services, projects, news, activities, and open positions.

This guide describes the business requirements, module analysis, feature specifications, user workflows, and the complete API specification for the currently implemented components.

For development setup, database management, build procedures, and deployment instructions, refer to `application_developer.md`.

---

## 2. Business Overview

### 2.1 Company Context

NICON operates as a Design and Build construction company based in Ho Chi Minh City, Vietnam. The business model covers the full lifecycle of construction projects: from initial customer engagement and design through permitting, construction execution, quality acceptance, and final handover.

- **Site Name**: Nihome (NICON Platform)
- **Primary Contact**: 1900 3311
- **Primary Email**: nihome@nihome.vn
- **Address**: 123 Nguyen Hue Street, District 1, Ho Chi Minh City, Vietnam

### 2.2 System Scope

The platform is organized into eight functional modules spanning the entire project lifecycle:

| Module | Scope | Objective |
|--------|-------|-----------|
| 1. CRM / Sale / Contract | Customer management, lead tracking, quotations, tenders, site surveys, customer contracts | Manage customer acquisition and business development flexibly |
| 2. Design Management | Three-phase design control (Concept, Basic Design, Shop Drawing), revision management, IFC issuance | Control technical documents and prevent discrepancies before construction |
| 3. Permitting | Legal document checklists, permit application tracking | Ensure legal compliance and administrative clearance |
| 4. Construction and Acceptance | Gantt scheduling, daily site logs, partial/full acceptance, as-built records, punchlist | Manage site operations, quality control, and schedule adherence |
| 5. Procurement | Vendor management, bid comparison, BOQ control, material requests, warehouse management, material alerts | Manage suppliers, subcontractors, and project materials |
| 6. Finance and Contract | Primary contracts, subcontracts, variation orders, cash flow tracking, P&L reporting | Maintain financial transparency and contract control |
| 7. Digital Assets | Automated project folder structure, Google Drive integration, document digitization | Organize, secure, and provide rapid access to project files |
| 8. Dashboard and Analytics | Progress reports, acceptance reports, financial dashboards, procurement reports, risk alerts | Provide management oversight and early risk detection |

Additionally, the system includes cross-cutting capabilities:

| Capability | Description |
|------------|-------------|
| Multi-Language | Vietnamese and English support across the interface |
| Responsive Design | Support for desktop and mobile devices |
| Role-Based Access | User creation, role assignment, and menu-level permission control |
| Workflow Approval | Configurable approval flows for quotations, documents, material requests, and variation orders |
| Notifications | In-app and email notifications triggered by system events |
| Audit Log | History tracking for all create, update, delete, approve, and file download actions |
| Global Search | Cross-module search with advanced filtering and saved filter presets |
| Master Data | Configurable reference data (statuses, lead sources, contract types, material types, checklist templates) |

### 2.3 Project Estimation Summary

Based on the task breakdown analysis, the full platform scope comprises:

| Category | Task Count | Estimated Effort (Mandays) |
|----------|------------|----------------------------|
| Core Module Tasks | 96 | 204 |
| Supplementary Frontend Tasks | 27 | 61.5 |
| **Total** | **123** | **265.5** |

Note: Estimates cover frontend implementation only and do not include backend API development, QA testing, code review, or deployment. Tasks of high complexity (interactive Gantt chart, workflow builder, floor plan annotation) require prototype spikes before final estimation. All estimates carry a variance of plus or minus 20 percent depending on UI design complexity and component reuse.

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
| CRM module (customers, leads, opportunities) | Not yet implemented |
| Quotation management (direct and tender) | Not yet implemented |
| Site survey digitization | Not yet implemented |
| Design management (3-phase) | Not yet implemented |
| Permitting module | Not yet implemented |
| Construction management (Gantt, site logs) | Not yet implemented |
| Acceptance and handover | Not yet implemented |
| Punchlist management | Not yet implemented |
| Procurement module (vendors, BOQ, MR, warehouse) | Not yet implemented |
| Finance module (contracts, VO, cash flow, P&L) | Not yet implemented |
| Google Drive integration | Not yet implemented |
| Dashboard and analytics | Not yet implemented |

---

## 3. Module Analysis

This section provides detailed functional analysis for each module based on the NICON business requirements.

### 3.1 Module 1: CRM / Sale / Contract

**Objective**: Manage customer acquisition and business development flexibly.

#### 3.1.1 Customer Management (Customer/Account)

Maintain individual and corporate customer records. Support multiple contact persons per customer. Link customers to leads, opportunities, quotations, contracts, and projects. Track customer status through the lifecycle: potential, in-transaction, signed, inactive. Store legal documents (tax ID, address, legal representative). Maintain a customer care timeline with callback and visit reminders.

| Page | Functions | Estimate |
|------|-----------|----------|
| Customer List | Search, filter, navigate to detail | 1.5 days |
| Customer Create/Edit | Create and update customer information | 2 days |
| Customer Detail | Overview, contacts, care history, related records | 2.5 days |

#### 3.1.2 Lead Management

Track inbound leads with source classification (marketing, referral, etc.). Assign leads to sales personnel. Maintain consultation history. Convert qualified leads to opportunities.

| Page | Functions | Estimate |
|------|-----------|----------|
| Lead List | Filter by source, status, assigned sales | 1.5 days |
| Lead Create/Edit | Create lead, update info, assign sales | 2 days |
| Lead Detail | Lead info, consultation history, convert to opportunity | 2.5 days |

#### 3.1.3 Opportunity Management

Manage sales opportunities with deal value, probability, and stage tracking. Provide a Kanban/pipeline view for visual stage progression. Track win/loss outcomes with supporting documents.

| Page | Functions | Estimate |
|------|-----------|----------|
| Opportunity List | Filter by stage, sales, status | 1.5 days |
| Opportunity Create/Edit | Create/update value, probability, stage | 2 days |
| Opportunity Detail | Track info, documents, win/loss result | 2 days |
| Pipeline Kanban | Kanban board by sales stage | 2.5 days |

#### 3.1.4 Direct Quotation

Create quotations using investment rate or preliminary BOQ. Calculate discounts, VAT, and totals automatically. Manage quotation versions. Support internal approval workflow. Generate PDF output for client delivery. Track quotation status: sent, approved, rejected.

| Page | Functions | Estimate |
|------|-----------|----------|
| Quotation List | Filter by customer, opportunity, status | 1.5 days |
| Quotation Create/Edit | BOQ-based or rate-based pricing, discount, VAT | 3.5 days |
| Quotation Detail | Version control, internal approval, PDF export | 2.5 days |
| PDF Preview/Export | Print-ready layout, PDF template | 2.5 days |
| Version Comparison | Diff view between quotation versions | 2 days |

#### 3.1.5 Tender Management

Manage tender packages with deadlines and preparation status. Maintain a capability document repository. Track tender results (won, lost, in preparation).

| Page | Functions | Estimate |
|------|-----------|----------|
| Tender List | Status, deadline tracking | 1.5 days |
| Tender Create/Edit | Tender package setup, invitation details | 2 days |
| Tender Detail | Document checklist, preparation progress, results | 2.5 days |
| Capability Documents | Repository CRUD | 2 days |

#### 3.1.6 Site Survey Digitization

Record site surveys linked to leads, opportunities, or projects. Capture photos and videos directly from the application. Upload drawings and survey documents. Record technical notes on site conditions. Synchronize data automatically to Google Drive (folder 01_Khao_sat). Manage survey folders by project. Support role-based access (Sales, Engineering, Management).

| Page | Functions | Estimate |
|------|-----------|----------|
| Survey List | By lead, opportunity, or project | 1.5 days |
| Survey Create/Edit | Create survey record, update info | 2 days |
| Survey Detail | Notes, checklist, photo/video/file upload, Drive sync | 3 days |

#### 3.1.7 Customer Contract Management

Create contracts or generate from approved quotations. Manage contract value, scope, and terms. Track payment milestones (advance, stage-based, completion). Manage appendices and variation orders. Track contract status: in-progress, paused, completed. Manage customer care history and contract renewal.

| Page | Functions | Estimate |
|------|-----------|----------|
| Contract List | Filter by project, customer, status | 1.5 days |
| Contract Create/Edit | Create new or from quotation, update terms | 2.5 days |
| Contract Detail | Info, appendices, payment schedule, attachments, convert to project | 3 days |

### 3.2 Module 2: Design Management (3-Phase)

**Objective**: Control technical documents and prevent discrepancies before construction.

#### 3.2.1 Design Project Overview

| Page | Functions | Estimate |
|------|-----------|----------|
| Design Project List | All design projects | 1.5 days |
| Design Project Create/Edit | Create and update design project | 2 days |
| Design Project Detail | Overview and 3-phase progress tracking | 2.5 days |

#### 3.2.2 Phase 1: Concept Design

Manage architectural 3D models and functional floor plans. Attach presentation images and files. Record client feedback and revision notes. Manage concept versions. Submit for client approval.

| Page | Functions | Estimate |
|------|-----------|----------|
| Concept List | Concept documents by project | 1.5 days |
| Concept Create/Edit | Create proposal, assign responsible person | 2 days |
| Concept Detail | File upload, feedback, internal/client approval | 2.5 days |

#### 3.2.3 Phase 2: Basic Design

Manage drawings for construction permit applications. Maintain technical descriptions. Classify documents by discipline (Architecture, Structure, MEP). Manage drawing versions. Attach related legal documents. Submit for review and approval.

| Page | Functions | Estimate |
|------|-----------|----------|
| Basic Design List | Documents by project | 1.5 days |
| Basic Design Create/Edit | Create and update documents | 2 days |
| Basic Design Detail | Drawing catalog, descriptions, file upload, approval status | 2.5 days |

#### 3.2.4 Phase 3: Detailed Design / Shop Drawing

Manage detailed construction drawings separated by discipline (Architecture, Structure, MEP, Interior). Catalog drawings by construction work item. Link drawings to construction tasks. Manage drawing versions. Submit for review and approval.

| Page | Functions | Estimate |
|------|-----------|----------|
| Shop Drawing List | Documents by project | 1.5 days |
| Shop Drawing Create/Edit | Create and update documents | 2 days |
| Shop Drawing Detail (by discipline) | Discipline management, drawing list, cross-review | 3 days |

#### 3.2.5 Revision Control

Track drawing revision history. Create new revisions with change justification. Compare revision versions. Mark the active version. Withdraw or lock old drawings. Notify relevant departments of version updates.

| Page | Functions | Estimate |
|------|-----------|----------|
| Revision List | Revisions for a document/drawing | 1 day |
| Revision Create/Edit | New revision with change reason | 1.5 days |
| Revision Detail/History | Change history, withdrawal, audit log | 2 days |

#### 3.2.6 IFC (Issued For Construction) Issuance

Submit drawings for approval before issuance. Apply electronic "Issued For Construction" stamp. Lock issued drawings from further editing. Distribute to construction and supervision teams. Track issuance history.

| Page | Functions | Estimate |
|------|-----------|----------|
| IFC List | Issued documents by project | 1 day |
| IFC Create/Edit | Create/update issuance batch | 1.5 days |
| IFC Detail | Issuance record, electronic stamp, recipient tracking | 2 days |

### 3.3 Module 3: Permitting

**Objective**: Ensure legal compliance and administrative clearance throughout the project.

#### 3.3.1 Legal Document Checklist

Manage required legal documents by project: construction permit (GPXD), fire safety (PCCC), electrical supply, water supply, sidewalk permit, completion certificate, and others. Upload and store scanned documents, drawings, and official letters. Track document status: not prepared, in preparation, submitted, approved. Assign responsible personnel. Alert on approaching expiry dates.

#### 3.3.2 Permit Application Tracking

Track submission status at government agencies. Monitor processing progress: submitted, under review, supplementary documents required, approved, rejected. Record working history with regulatory bodies. Upload response documents and supplementary requests. Alert on delayed applications. Notify when applications are approved.

| Page | Functions | Estimate |
|------|-----------|----------|
| Legal Document List | Checklist by project | 1.5 days |
| Legal Document Create/Edit | Create and update checklist | 2 days |
| Legal Document Detail | Upload files, status updates, timeline, expiry alerts | 2.5 days |

### 3.4 Module 4: Construction and Acceptance

**Objective**: Manage site operations, control quality and schedule, and ensure safety at each construction phase.

#### 3.4.1 Construction Schedule (Gantt)

Create construction schedules with phases and work items (WBS). Assign responsible personnel. Set start and end dates. Track actual completion percentage. Update progress from the field. Display Gantt chart with dependency lines. Alert on overdue tasks. Support drag-and-drop task bars, resize duration, and zoom (day/week/month).

| Page | Functions | Estimate |
|------|-----------|----------|
| Task List | Tasks/WBS by project and phase | 1.5 days |
| Task Create/Edit | Create task, update plan/actual/percentage | 2 days |
| Gantt Chart Detail | Interactive Gantt, dependencies, personnel assignment, alerts | 5 days |
| Interactive Gantt UI | Drag-drop, resize, zoom, dependency lines | 6 days |

#### 3.4.2 Electronic Site Log

Record daily construction conditions. Upload site photos and videos. Update weather conditions. Record workforce count. Record materials and equipment delivered to site. Note incidents and issues. Maintain site log history.

| Page | Functions | Estimate |
|------|-----------|----------|
| Site Log List | Daily logs by project | 1.5 days |
| Site Log Create/Edit | Create and update daily log | 2 days |
| Site Log Detail | Photos/videos, incidents, acknowledgment signatures | 2.5 days |
| Mobile Media Upload | Camera picker, gallery, upload progress, thumbnails | 2 days |

#### 3.4.3 Partial Acceptance (Phase/Section Acceptance)

Create acceptance requests for specific work items (piles, foundations, beams/slabs, masonry, etc.). Establish inspection checklists with acceptance criteria. Upload inspection photos. Record acceptance results. Manage status: pending acceptance, passed, failed. Note remediation requirements for failed items. Store acceptance records.

| Page | Functions | Estimate |
|------|-----------|----------|
| Acceptance List | Requests by project | 1.5 days |
| Acceptance Create/Edit | Create by work item or phase | 2 days |
| Acceptance Detail | Checklist, photos, records, remediation tracking | 2.5 days |

#### 3.4.4 Full Acceptance (Handover)

Establish comprehensive building inspection checklists. Manage commissioning (trial operation) procedures. Upload handover acceptance records. Manage multi-party signatures. Update handover status. Store records for occupancy.

| Page | Functions | Estimate |
|------|-----------|----------|
| Handover List | Handover records by project | 1.5 days |
| Handover Create/Edit | Create and update handover records | 2 days |
| Handover Detail | Checklist, commissioning, acceptance record | 2.5 days |

#### 3.4.5 As-Built Records

Automatically compile as-built drawings. Consolidate acceptance records for all work items. Upload and store as-built drawings. Manage as-built document catalog by project. Export as-built document packages. Manage document versions.

| Page | Functions | Estimate |
|------|-----------|----------|
| As-Built List | Document packages by project | 1 day |
| As-Built Create/Edit | Create and update packages | 1.5 days |
| As-Built Detail | Drawing compilation, acceptance records, completeness check, catalog export | 2 days |

#### 3.4.6 Defect Management (Punchlist)

Record defects at the construction site with location and description. Upload defect photos and videos. Assign responsible person for remediation. Set remediation deadlines. Track status: unresolved, in-progress, completed. Confirm remediation completion. Support floor plan annotation with pin-drop for defect location.

| Page | Functions | Estimate |
|------|-----------|----------|
| Punchlist List | Defects by project | 1 day |
| Punchlist Create/Edit | Create defect, assign handler, set deadline | 1.5 days |
| Punchlist Detail | Description, photos/video, status, re-open capability | 2 days |
| Floor Plan Annotation | Upload drawing, pin defect locations, popup details | 3.5 days |

### 3.5 Module 5: Procurement

**Objective**: Manage suppliers, subcontractors, and project materials.

#### 3.5.1 Vendor and Subcontractor Management

Maintain vendor/subcontractor profiles (company name, tax ID, address, contacts). Upload capability documents. Classify by specialty (materials, construction, MEP, interior, etc.). Track cooperation history. Evaluate partners after each project based on four criteria: quality, schedule, cost, safety.

| Page | Functions | Estimate |
|------|-----------|----------|
| Vendor List | All vendors and subcontractors | 1.5 days |
| Vendor Create/Edit | Create and update vendor information | 2 days |
| Vendor Detail | Capability documents, licenses, cooperation history, evaluations | 2.5 days |

#### 3.5.2 Bid Comparison (Bid Tabulation)

Create requests for quotation (RFQ). Collect quotes from multiple vendors. Compare prices by BOQ line item in a matrix (rows = work items, columns = vendors). Automatically highlight the lowest prices. Evaluate and recommend the optimal vendor. Store vendor selection history.

| Page | Functions | Estimate |
|------|-----------|----------|
| RFQ List | Requests for quotation | 1.5 days |
| RFQ Create/Edit | Create and update RFQ | 2 days |
| Bid Tabulation Detail | Comparison matrix, vendor selection/approval | 3 days |
| Interactive Comparison Matrix | Multi-vendor matrix with auto-highlight | 3 days |

#### 3.5.3 Material BOQ Management

Create material BOQ by project and construction work item. Define maximum allowable quantities (norms) per project. Link BOQ to construction schedule. Track BOQ amendments. Support Excel import for bulk data entry.

| Page | Functions | Estimate |
|------|-----------|----------|
| BOQ List | By project | 1.5 days |
| BOQ Create/Edit | Create/update BOQ, Excel import | 3 days |
| BOQ Detail | Material catalog, norms, quantities | 2 days |
| Excel Import Wizard | Upload, column mapping preview, validation, confirm | 3 days |

#### 3.5.4 Material Request (MR)

Site engineers submit material requests referencing the BOQ. Specify quantities and required delivery dates. Route requests to the office for approval. Track request status: pending approval, approved, rejected. Maintain request history.

| Page | Functions | Estimate |
|------|-----------|----------|
| MR List | Material requests by project | 1.5 days |
| MR Create/Edit | Create and update request | 2 days |
| MR Detail | Material info, BOQ comparison, approval workflow | 2.5 days |

#### 3.5.5 Warehouse and Material Distribution

Manage warehouse receipts (inbound) and issue slips (outbound). Track inventory at the construction site. Record material distribution to construction crews. Maintain inbound/outbound/stock history. Report material usage by work item.

| Page | Functions | Estimate |
|------|-----------|----------|
| Warehouse List | Inbound/outbound/stock by site | 2 days |
| Receipt/Issue Create/Edit | Create warehouse transactions | 2 days |
| Warehouse Detail | Transaction details, crew distribution, inventory audit | 2.5 days |

#### 3.5.6 Material Alerts

Monitor actual material usage against BOQ norms. Alert when usage exceeds allocated quantities. Alert on low inventory levels. Notify project managers on discrepancies. Track alert history. Report budget overruns by project.

| Page | Functions | Estimate |
|------|-----------|----------|
| Material Alerts | Overrun alerts and reports | 1.5 days |

### 3.6 Module 6: Finance and Contract

**Objective**: Maintain financial transparency and contract control.

#### 3.6.1 Primary Contract Management (Client/Owner)

Manage primary contracts linked to projects and customers. Define contract value, duration, and work scope. Establish payment milestones tied to acceptance progress. Track advances and stage-based payments. Upload and store contract files and appendices. Track contract status: not started, in-progress, completed, paused. Monitor contract modification history.

| Page | Functions | Estimate |
|------|-----------|----------|
| Primary Contract List | Contracts with clients/owners | 1.5 days |
| Primary Contract Create/Edit | Create and update contracts | 2.5 days |
| Primary Contract Detail | Value, payment milestones, receivables | 2.5 days |

#### 3.6.2 Subcontractor and Supplier Contract Management

Manage input contracts with subcontractors and material suppliers. Define contract value, terms, and payment schedules. Track contract execution progress. Monitor payment history. Upload contract files.

| Page | Functions | Estimate |
|------|-----------|----------|
| Input Contract List | Subcontractor and supplier contracts | 1.5 days |
| Input Contract Create/Edit | Create and update contracts | 2.5 days |
| Input Contract Detail | Value, terms, payments, payables | 2.5 days |

#### 3.6.3 Variation Order (VO) Management

Record design changes or scope modifications. Calculate cost increases or decreases. Link VOs to primary or subcontractor contracts. Upload appendix files. Track approval workflow status. Maintain contract modification history.

| Page | Functions | Estimate |
|------|-----------|----------|
| VO List | Variation orders by project | 1 day |
| VO Create/Edit | Create cost increase/decrease records | 2 days |
| VO Detail | VO information, approval workflow | 2 days |

#### 3.6.4 Project Cash Flow

Record income from clients/owners. Record expenditures for materials and subcontractors. Track actual income vs. actual expenditure by project. Manage payment schedules and receivables/payables. Update payment status: unpaid, paid, overdue. Provide visual timeline charts (bar and line combination).

| Page | Functions | Estimate |
|------|-----------|----------|
| Cash Flow Dashboard | Cash in/out tracking, planned vs. actual | 3.5 days |
| Cash Flow Chart | Timeline visualization, filter by project/period | 2.5 days |

#### 3.6.5 Profit and Loss (P&L) Report

Aggregate revenue by project. Aggregate material and subcontractor costs. Calculate profit or loss per project. Compare actual costs against budget estimates. Analyze economic efficiency by project. Generate P&L reports by month, quarter, or year. Provide stacked bar charts with drill-down by project.

| Page | Functions | Estimate |
|------|-----------|----------|
| P&L Report | Revenue/cost/profit analysis, cost breakdown | 3 days |
| P&L Chart | Stacked bar chart with project drill-down | 2.5 days |

### 3.7 Module 7: Digital Assets (Google Drive Integration)

**Objective**: Organize, secure, and provide rapid access to all project files.

#### 3.7.1 Automated Project Folder Structure

Automatically create an 8-tier folder structure when a new project is created: CRM, Survey, Design, Legal, Construction, Acceptance, Finance, As-Built. Synchronize between the application and Google Drive. Control folder access by role (Admin, Sales, Engineering, Accounting). Track folder creation and modification history.

#### 3.7.2 Document Digitization

Upload and manage all project documents through the application. Synchronize files with Google Drive. Manage IFC drawings, signed acceptance records, and site photos/videos centrally. Control document versions. Share documents across departments with role-based access.

| Page | Functions | Estimate |
|------|-----------|----------|
| Folder Structure Configuration | Template setup, project folder creation, sync log | 3 days |
| Document Repository | Tree view, search, role-based access | 2.5 days |

### 3.8 Module 8: Dashboard and Analytics

**Objective**: Provide management oversight and early risk detection.

#### 3.8.1 Executive Dashboard

Overview of leads, opportunities, contracts, and projects. Construction progress tracking. Financial summary. Procurement status. Lead funnel chart (Lead to Opportunity to Quotation to Contract with conversion rates). Project progress bars with schedule overrun alerts. Tender win-rate visualization.

| Page | Functions | Estimate |
|------|-----------|----------|
| Executive Dashboard | Multi-module overview with charts and widgets | 5 days |
| Lead Funnel Chart | Funnel visualization with conversion rates | 2 days |
| Project Progress Widget | Progress bars with overrun alerts | 2 days |
| Tender Win-Rate Chart | Donut/pie chart with period filtering | 1.5 days |

#### 3.8.2 Reports

Progress reports comparing planned vs. actual (S-Curve). Acceptance reports showing pass/fail/pending ratios. Financial reports on revenue, receivables, and expenditures. Procurement reports on material distribution and vendor performance. Risk alerts for overdue tasks, expiring permits, budget overruns, BOQ exceedances, and failed acceptance items.

| Page | Functions | Estimate |
|------|-----------|----------|
| Business Reports | Acceptance, risk, cost, permit, task reports | 3.5 days |
| Advanced Report Filters | Multi-condition filters, date range, saved presets | 2 days |
| Report Export | Excel and PDF export with progress indicator | 1.5 days |

### 3.9 Cross-Cutting Capabilities

| Capability | Page/Screen | Functions | Estimate |
|------------|-------------|-----------|----------|
| Authentication | Login Page | Login form, validation, error handling, redirect | 1.5 days |
| Authentication | Forgot Password | Email input, OTP/link, password reset | 1.5 days |
| Authentication | User Profile | View/edit personal info, change password, avatar upload | 1.5 days |
| Navigation | Sidebar + Layout | Sidebar navigation, collapse/expand, responsive mobile drawer | 3 days |
| Navigation | Header Bar | Logo, breadcrumb, notification badge, user dropdown | 1.5 days |
| Navigation | Error Pages | 404, 403, 500 pages with navigation guidance | 0.5 days |
| Workflow | Approval Configuration | Drag-drop step builder, role assignment, skip conditions | 4 days |
| Permissions | Roles and Permissions | Admin, Sales, Design, PM, QS, Accounting, Warehouse, Management | 3.5 days |
| Notifications | Notification Center | Pull-down panel, module categorization, mark as read, link to record | 2 days |
| Search | Global Search and Filter | Cross-module search, advanced filter, saved filter presets | 2 days |
| Audit Log | Audit Log Viewer | Filter by user, action, module, date range; view change details | 1.5 days |
| Master Data | Reference Data Management | CRUD for statuses, lead sources, contract types, material types, checklist templates | 2.5 days |

---

## 4. User Roles and Permissions

The platform defines three user roles with increasing levels of access. As additional modules are implemented, the role structure will expand to include specialized roles such as Sales, Design, Project Manager (PM), Quantity Surveyor (QS), Accounting, Warehouse, and Management (BGD).

### Currently Implemented Roles

| Role        | Access Level                                                                       |
|-------------|------------------------------------------------------------------------------------|
| USER        | Default role for registered users. Can access the public website and user profile.  |
| ADMIN       | Can manage all content, recruitment, contacts, translations, and email templates.    |
| SUPER_ADMIN | Full system access. Includes all ADMIN permissions plus user management.             |

### Permission Matrix

| Action                          | USER | ADMIN | SUPER_ADMIN |
|---------------------------------|------|-------|-------------|
| View public content             | Yes  | Yes   | Yes         |
| Register and login              | Yes  | Yes   | Yes         |
| Manage own profile              | Yes  | Yes   | Yes         |
| Create/edit/delete content      | No   | Yes   | Yes         |
| Manage slideshow                | No   | Yes   | Yes         |
| Manage job positions            | No   | Yes   | Yes         |
| View/manage job applications    | No   | Yes   | Yes         |
| View/reply to contact messages  | No   | Yes   | Yes         |
| Manage translations             | No   | Yes   | Yes         |
| Configure email templates       | No   | Yes   | Yes         |
| Manage logos                    | No   | Yes   | Yes         |
| Manage processes                | No   | Yes   | Yes         |
| Upload images                   | No   | Yes   | Yes         |
| System administration           | No   | No    | Yes         |

### Planned Role Expansion

The full NICON system specification defines the following specialized roles for future implementation:

| Role | Vietnamese | Module Access |
|------|------------|---------------|
| Admin | Quan tri | Full system configuration and user management |
| Sales | Sale | CRM, leads, opportunities, quotations, tenders, customer contracts |
| Design | Thiet ke | Design documents, revisions, IFC issuance |
| Project Manager (PM) | Quan ly du an | Construction schedules, site logs, acceptance, punchlist |
| Quantity Surveyor (QS) | Du toan | BOQ, material requests, cost tracking |
| Accounting | Ke toan | Contracts, cash flow, payments, P&L reports |
| Warehouse | Kho | Material receipts, issues, inventory |
| Management (BGD) | Ban giam doc | Dashboard, reports, executive overview |

---

## 5. Authentication

### 5.1 Registration Flow

1. The user submits their phone number, full name, email, and password.
2. If OTP verification is enabled (configurable in site settings), the system sends a one-time password to the user's email address.
3. The user enters the OTP code to verify their identity.
4. Upon verification, the account is created with the USER role.

OTP verification can be toggled on or off by administrators through site settings. When disabled, registration completes immediately after submission.

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
| Profile            | `/profile`             | Authenticated user profile management                            |

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

The admin panel is accessible at `/admin` and requires ADMIN or SUPER_ADMIN role authentication.

### 7.1 Dashboard

The main admin page provides an overview of system status and quick access to management sections.

### 7.2 Admin Navigation

| Section              | Route                    | Purpose                                |
|----------------------|--------------------------|----------------------------------------|
| Posts                | `/admin/posts`           | Manage news articles and activities    |
| Projects             | `/admin/projects`        | Manage project portfolio               |
| Recruitment          | `/admin/recruitment`     | Manage job positions and applications  |
| Contacts             | `/admin/contacts`        | View and reply to contact messages     |
| Email Templates      | `/admin/email-templates` | Configure automated email content      |
| Settings             | `/admin/settings`        | General site configuration             |
| Languages            | `/admin/languages`       | Language settings                      |
| Translations         | `/admin/translations`    | Manage UI translation strings          |
| Categories           | `/admin/categories`      | Manage activity categories             |
| Activity Log         | `/admin/activity-log`    | View system activity history           |
| Clients              | `/admin/clients`         | Manage client logos                    |
| Partners             | `/admin/partners`        | Manage partner logos                   |
| Suppliers            | `/admin/suppliers`       | Manage supplier logos                  |
| Awards               | `/admin/awards`          | Manage award entries                   |
| Processes            | `/admin/processes`       | Manage process documents               |
| System               | `/admin/system/*`        | System maintenance                     |

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

Process documents are internal procedure descriptions grouped by category (using a `GroupKey`). Each document has a code, title, and sort order within its group.

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

Content entities (activities, news, projects, services, slideshow items) support field-level translations. This uses a polymorphic pattern where each translation record specifies:
- Entity type (e.g., `Activity`, `NewsArticle`)
- Entity ID
- Field name (e.g., `Title`, `Excerpt`)
- Language code
- Translated value

This allows any field of any content entity to be translated without modifying the database schema.

### 11.4 Translation Categories

Translations are organized into categories for management convenience. Administrators can view the list of available categories and filter translations accordingly.

---

## 12. Email System

### 12.1 Email Types

The platform sends automated emails for the following events:

| Event                    | Recipient              | Trigger                                    |
|--------------------------|------------------------|--------------------------------------------|
| Registration OTP         | Registering user       | User starts registration (OTP enabled)     |
| Password Reset OTP       | Account holder         | User initiates password reset (OTP enabled)|
| New Application Notice   | Notification address   | Candidate submits a job application         |
| Contact Reply            | Contact message sender | Administrator replies to a contact message  |

### 12.2 Email Templates

OTP and job application notification emails use configurable templates with placeholder substitution.

**OTP Template Placeholders:**

| Placeholder   | Substituted Value                |
|---------------|----------------------------------|
| `{SiteName}`  | The site name from settings      |
| `{OtpCode}`   | The generated one-time password  |

**Job Application Template Placeholders:**

| Placeholder       | Substituted Value                |
|-------------------|----------------------------------|
| `{SiteName}`      | The site name from settings      |
| `{CandidateName}` | The applicant's full name        |
| `{PositionTitle}`  | The job position title           |

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

### 14.1 Authentication

| Method | Endpoint                          | Auth     | Description                              |
|--------|-----------------------------------|----------|------------------------------------------|
| POST   | `/api/auth/register/start`        | Public   | Start registration (triggers OTP if enabled) |
| POST   | `/api/auth/register/verify-otp`   | Public   | Verify OTP during registration           |
| POST   | `/api/auth/register/complete`     | Public   | Complete registration after OTP          |
| POST   | `/api/auth/register/resend-otp`   | Public   | Resend registration OTP                  |
| POST   | `/api/auth/login`                 | Public   | Login with phone and password            |
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
  "phoneNumber": "0335240370",
  "password": "Admin@123"
}
```

#### Login Response

```json
{
  "user": {
    "id": 1,
    "phone": "0335240370",
    "fullName": "Super Admin",
    "email": "superadmin@nihome.vn",
    "role": "SUPER_ADMIN"
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

### 14.10 Contact Messages

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

### 14.11 Logos (Clients, Partners, Suppliers)

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

### 14.12 Processes

| Method | Endpoint               | Auth   | Description                          |
|--------|------------------------|--------|--------------------------------------|
| GET    | `/api/processes`       | Public | List processes grouped by category   |
| POST   | `/api/processes`       | Admin  | Create process document              |
| PUT    | `/api/processes/{id}`  | Admin  | Update process document              |
| DELETE | `/api/processes/{id}`  | Admin  | Delete process document              |

#### Create/Update Process Request

```json
{
  "groupKey": "quality-control",
  "code": "QC-001",
  "title": "Material Inspection Procedure",
  "sortOrder": 1
}
```

### 14.13 Site Settings

| Method | Endpoint                             | Auth   | Description              |
|--------|--------------------------------------|--------|--------------------------|
| GET    | `/api/site-settings/email-templates` | Admin  | Get email templates      |
| PUT    | `/api/site-settings/email-templates` | Admin  | Update email templates   |

#### Update Email Templates Request

```json
{
  "newApplicationEmailSubjectTemplate": "[{SiteName}] New Application for {PositionTitle}",
  "newApplicationEmailBodyTemplate": "A new application has been submitted by {CandidateName} for {PositionTitle}.",
  "notificationEmail": "hr@nihome.vn",
  "otpEmailSubjectTemplate": "[{SiteName}] Your verification code",
  "otpEmailBodyTemplate": "Your OTP code is: {OtpCode}"
}
```

### 14.14 Translations

| Method | Endpoint                              | Auth   | Description                              |
|--------|---------------------------------------|--------|------------------------------------------|
| GET    | `/api/translations/{lang}`            | Public | Get all UI translations for a language   |
| GET    | `/api/translations/admin`             | Admin  | Get translation pairs with filtering     |
| GET    | `/api/translations/categories`        | Admin  | Get translation categories               |
| POST   | `/api/translations/pair`              | Admin  | Create or update a translation pair      |
| POST   | `/api/translations/bulk`              | Admin  | Bulk create or update translations       |
| DELETE | `/api/translations/key/{key}`         | Admin  | Delete a translation key (all languages) |
| GET    | `/api/translations/entity/types`      | Admin  | List entity types with translatable fields|

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

### 14.15 About Sections

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

### 14.16 System

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

### 15.9 Contact Message

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

### 15.10 Client Logo

| Field     | Type     | Description                          |
|-----------|----------|--------------------------------------|
| Id        | int      | Primary key                          |
| Name      | string   | Company name                         |
| ImageUrl  | string   | Logo image URL                       |
| Href      | string   | Optional website link                |
| Kind      | enum     | `Client`, `Partner`, or `Supplier`   |
| SortOrder | int      | Display order                        |
| CreatedAt | datetime | Creation timestamp                   |

### 15.11 Process Document

| Field     | Type     | Description                          |
|-----------|----------|--------------------------------------|
| Id        | int      | Primary key                          |
| GroupKey  | string   | Category grouping key                |
| Code      | string   | Document code                        |
| Title     | string   | Document title                       |
| SortOrder | int      | Display order within group           |
| CreatedAt | datetime | Creation timestamp                   |

### 15.12 Translation

| Field        | Type     | Description                         |
|--------------|----------|-------------------------------------|
| Id           | int      | Primary key                         |
| Key          | string   | Translation key (unique with lang)  |
| LanguageCode | string   | Language code (vi, en, zh, ja)      |
| Value        | string   | Translated text                     |
| Category     | string   | Organizational category             |
| CreatedAt    | datetime | Creation timestamp                  |
| UpdatedAt    | datetime | Last update timestamp               |

### 15.13 Entity Translation

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

### 15.14 Site Settings

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
