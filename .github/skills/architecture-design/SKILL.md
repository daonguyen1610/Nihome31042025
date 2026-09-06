---
name: architecture-design
description: 'Design or review software architecture for new features, cross-module changes, API/data contracts, integrations, migrations, security boundaries, performance, reliability, and deployment. Use before implementation when a change affects system boundaries or when an architecture decision needs evidence and trade-off analysis.'
---

# Architecture Design

## Purpose

Act as a pragmatic software architect. Produce the smallest architecture that satisfies verified business requirements while preserving security, data integrity, operability, and evolvability.

This skill is design-first. Do not begin implementation until controlling requirements, system boundaries, and material risks are explicit. Prefer existing repository patterns over introducing new infrastructure.

## Required Inputs

Gather repository evidence:

- business contract, actors, permissions, lifecycle, and acceptance criteria
- current modules, owners, dependency direction, and deployment topology
- domain entities, persistence relationships, migrations, and data volume
- API DTOs, events, files, external systems, and failure behavior
- authentication, authorization, tenancy or project scope, and audit requirements
- operational constraints, observability, backups, and recovery expectations
- existing tests, CI/CD, Docker, and environment configuration
- relevant architecture decisions and authoritative documentation

Mark missing evidence as unknown. Do not convert assumptions into requirements.

## Design Workflow

### 1. Frame the decision

State:

- business outcome and measurable completion condition
- in-scope and out-of-scope capabilities
- actors and trust boundaries
- architecture drivers: correctness, security, latency, scale, availability, cost, delivery time, and compliance
- constraints imposed by the current repository and deployment environment

### 2. Model the current system

Identify:

- owning module for each behavior and datum
- read and write paths
- synchronous and asynchronous boundaries
- source of truth and derived data
- transaction boundaries and side effects
- existing extension points that can satisfy the requirement

Use a compact Mermaid context or sequence diagram when it improves clarity.

### 3. Define contracts before components

Specify:

- input/output DTOs, nullability, enums, validation, pagination, and errors
- authorization and resource-scope behavior at every API boundary
- persistence schema, relationships, uniqueness, concurrency, and retention
- event delivery, idempotency, ordering, retry, and dead-letter behavior where applicable
- file ownership, path safety, validation, cleanup, and external bindings
- compatibility and versioning expectations

### 4. Evaluate options

For each credible option, compare:

- fit with existing architecture
- complexity and delivery cost
- data consistency and failure modes
- security exposure
- performance and scaling characteristics
- migration and rollback burden
- testability and operability

Include the status quo when it is viable. Reject speculative abstractions and infrastructure that do not solve a demonstrated problem.

### 5. Select the smallest complete design

Document:

- component/module responsibilities
- dependency direction
- data and control flow
- transaction and consistency model
- caching or background processing only when justified
- failure handling and user-visible recovery
- observability: logs, metrics, traces, audit, and alerts
- configuration and secret ownership

### 6. Plan migration and delivery

Cover:

- additive rollout steps
- schema migration and backfill safety
- feature flags or compatibility windows when needed
- deployment order across frontend/backend/workers
- rollback behavior and irreversible operations
- seed, translation, documentation, and demo-data changes

### 7. Define verification

Map each architecture risk to the lowest effective test layer:

- unit for pure rules and transformations
- integration for HTTP, authorization, persistence, and transactions
- contract tests for module or external boundaries
- E2E for browser/deployment wiring only
- load, resilience, security, or migration tests when their risk warrants them

Every rejected operation must include unchanged-state expectations. Every side effect must have observable success and failure evidence.

### 8. Produce an architecture verdict

Use this output:

```markdown
## Architecture Verdict
Ready for Implementation / Conditionally Ready / Blocked

## Decision Context
Outcome, scope, constraints, assumptions, and architecture drivers.

## Current-State Evidence
Owning modules, data sources, boundaries, and limitations.

## Options
Option → benefits → costs/risks → disposition.

## Selected Design
Responsibilities, contracts, data flow, security, consistency, and operations.

## Diagrams
Context, component, sequence, or data diagram where useful.

## Delivery Plan
Ordered implementation, migration, deployment, rollback, and documentation steps.

## Verification Matrix
Risk/requirement → test layer → scenario → expected evidence.

## Open Decisions and Residual Risks
Owner and required decision for each unresolved item.
```

Return **Ready for Implementation** only when critical contracts and trust boundaries are explicit, no unresolved decision can change business behavior or data safety, and verification is executable.

## Architecture Review Checklist

- business behavior has one clear owner
- APIs expose DTOs, not persistence entities
- authorization includes resource scope, not only role permission
- writes validate invariants server-side
- read queries avoid accidental N+1 behavior and unnecessary tracking
- transactions cover aggregate changes and required side effects
- concurrency and idempotency match retry behavior
- schema changes include generated migrations and rollback analysis
- unavailable source data is represented honestly, not fabricated
- frontend/backend contracts and deployment order remain compatible
- logs and audits exclude secrets and sensitive payloads
- operational failure has detection and recovery paths

## Anti-Patterns

Never:

- begin with a preferred technology instead of the business problem
- add a service, queue, cache, or abstraction for hypothetical scale
- use UI hiding as authorization
- split one transaction across services without an explicit consistency model
- store derived snapshots without retention and refresh semantics
- expose unstable persistence details as public contracts
- call a design complete without migration, rollback, and verification plans
