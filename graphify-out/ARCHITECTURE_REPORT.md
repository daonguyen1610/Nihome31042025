# Repository architecture report

This is a static structural analysis of the working tree, including existing uncommitted edits. It is not a runtime trace, security audit, test execution, business-delivery assessment, or proof of complete call resolution.

Generated at 2026-11-06T18:01:48.927383+00:00.

## Scope

1,533 source/configuration files; 23,277 normalized nodes; 85,530 directed relationship records; 589 detected communities.

| Area | Included files |
|---|---:|
| .github | 1 |
| Nihome31042025.sln | 1 |
| auto-deployment.sh | 1 |
| docker-compose-dev.yaml | 1 |
| docker-compose-e2e.yaml | 1 |
| docker-compose.yaml | 1 |
| nihomebackend.integration.tests | 122 |
| nihomebackend.tests | 169 |
| nihomebackend | 850 |
| nihomeweb | 380 |
| tools | 6 |

Generated EF designers and model snapshots, build/dependency/output directories, lockfiles, agent tooling, static asset directories, images, PDFs, Office files, and other non-code content are excluded. Authored migration code, test sources, source configuration and scripts are included. Secrets flagged by Graphify (including `.env`) remain excluded. `scope.json` contains the exact included-file list.

## Structure and coupling

Backend nodes group into controllers, services, models/DTOs, persistence, seed data and migration code. Frontend nodes include application/page components, shared library functions and browser-test sources. Unit and integration tests are extracted alongside production code so resolved references can be traversed across project boundaries. These are static source groupings, not claims that every architectural boundary is enforced.

| Cross-layer source | Target | Relationship occurrences |
|---|---|---:|
| nihomeweb/src/pages | nihomeweb/src/components | 4232 |
| nihomebackend.tests | nihomebackend/Services | 3709 |
| nihomebackend/Services | nihomebackend/Models | 3419 |
| nihomebackend/Controllers | nihomebackend/Services | 2811 |
| nihomebackend.tests | nihomebackend/Models | 2173 |
| nihomeweb/src/pages | nihomeweb/src/lib | 1434 |
| nihomebackend/Controllers | nihomebackend/Models | 1073 |
| nihomebackend.integration.tests | nihomebackend/Models | 894 |
| nihomeweb/src/pages | nihomeweb/src/services | 865 |
| nihomebackend/Data | nihomebackend/Models | 736 |
| nihomeweb/src/pages | nihomeweb/src/hooks | 724 |
| nihomeweb/e2e/smoke | nihomeweb/e2e/fixtures | 516 |
| nihomeweb/src/components | nihomeweb/src/lib | 422 |
| nihomebackend.tests | nihomebackend/Data | 385 |
| nihomeweb/src/pages | nihomeweb | 322 |
| nihomebackend/Services | nihomebackend/Data | 277 |
| nihomebackend/Extensions | nihomebackend/Services | 248 |
| nihomebackend.tests | nihomebackend/Controllers | 242 |
| nihomebackend.integration.tests | nihomebackend/Services | 187 |
| nihomeweb/src/App.tsx | nihomeweb/src/pages | 187 |

## Architectural hubs

| Symbol | Distinct graph degree | Source |
|---|---:|---|
| AppDbContext | 792 | nihomebackend/Data/AppDbContext.cs L7 |
| NihomeBackend.Models | 494 | nihomebackend/Models/AboutSectionContent.cs L1 |
| NihomeBackend.Services | 445 | nihomebackend/Services/AboutSectionService.cs L9 |
| useI18n() | 385 | nihomeweb/src/lib/i18n.tsx L131 |
| NihomeBackend.Models.DTOs.Requests | 347 | nihomebackend/Models/DTOs/Requests/AcceptanceRecordRequests.cs L1 |
| NihomeBackend.Data | 328 | nihomebackend/Data/AppDbContext.cs L5 |
| Button | 310 | nihomeweb/src/components/ui/button.tsx L38 |
| NihomeBackend.Models.DTOs.Responses | 302 | nihomebackend/Models/DTOs/Responses/AboutSectionResponse.cs L1 |
| OperationalProject | 269 | nihomebackend/Models/OperationalProject.cs L9 |
| ApplicationUser | 266 | nihomebackend/Models/ApplicationUser.cs L6 |

High degree indicates structural coupling, not a defect. Namespace/type references can dominate this metric. `AppDbContext` is a broad persistence hub and `useI18n()` is a broadly shared frontend dependency; changes to these warrant targeted impact queries.

## Relationship inventory

| Relation | Records |
|---|---:|
| references | 27673 |
| calls | 24728 |
| method | 8121 |
| defines | 7877 |
| contains | 6073 |
| imports | 5849 |
| imports_from | 2910 |
| dispatches_to | 699 |
| case_of | 519 |
| inherits | 507 |
| implements | 352 |
| dynamic_import | 94 |
| declares | 75 |
| rationale_for | 35 |
| indirect_call | 16 |
| re_exports | 2 |

## Coverage and integrity

- All 85,530 exported relationship records have existing source and target nodes.
- 9 self-loop records are preserved for inspection.
- 2,396 relationships share directed endpoints. The clustering projection has 83,134 edges; `graph.json` preserves individual records.
- 80 included files have no source-attributed graph nodes; see `files-without-symbols.txt`. Scanning a file does not imply its behavior was modeled.
- Nodes retain 9,916 unresolved-call metadata entries. This static graph cannot establish every runtime dependency.
- The SQL parser dependency is unavailable; SQL content has not been structurally analyzed. No dependencies were installed.
- AST resolution can miss reflection, dependency injection bindings, dynamic dispatch, HTTP endpoint-to-client matching, runtime configuration, framework behavior and callbacks in browser tests. Test extraction is not test coverage and does not prove tests pass.
- Documentation and binary customer evidence were excluded from this code-analysis scope. No business-rule or delivery-status conclusions are asserted.
- Community labels are heuristic names derived from symbol/file names; read source locations before drawing design conclusions.

## Reproducibility and outputs

- `source-hashes.json`: SHA-256 fingerprints of every included source/configuration file.
- `scope.json`: exact source scope and exclusion description.
- `graph.json`: directed normalized multigraph with all extracted relationships and community labels.
- Raw extraction and cache files are local intermediates and are excluded from version control.
- `file-dependencies.json`: source-file dependency projection; repeated symbol references are counted.
- `GRAPH_REPORT.md`: Graphify community/cohesion report, hubs, candidate cross-community links and suggested questions.
- `graph.html`: aggregated interactive community view; detailed nodes remain in JSON.
- `health.json` and `HEALTH_REPORT.txt`: raw-extraction integrity diagnostics.

AST extraction used no external LLM calls: 0 API input tokens / 0 API output tokens. Host-session reasoning usage is not included in this tool cost figure. No application code was changed and no application test suites were run for this analysis-only task.

## Suggested exploration

1. Which services and test cases depend on AppDbContext?
2. Which admin pages share useI18n and other common frontend helpers?
3. Which hard-delete services connect document handling and persistence?

The candidate connections in the generated Graphify report are algorithmic suggestions, not validated business relationships.

## Node review before commit

Node IDs are unique and every relationship endpoint resolves. All included source fingerprints match the analyzed working tree. External package nodes `docx-preview` and `pdfjs-dist` are explicitly marked as external modules rather than repository files. Namespace and framework nodes remain synthetic structural nodes; heuristic community labels are not verified business boundaries.
