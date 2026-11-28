# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5. Cost-Aware Execution

**Use resources proportionally to task complexity.**

- Prefer focused investigation over exploring the entire codebase.
- Read only relevant files and dependencies.
- Avoid repeating tool calls or checks without a reason.
- Prefer targeted tests before running the full test suite.
- Do not sacrifice correctness or security just to save tokens.
- Do not repeat explanations or summarize unchanged information.

## 6. Adaptive Reasoning

**Match reasoning effort to risk and complexity.**

- Low: Simple changes, documentation, formatting,
  straightforward fixes, and routine implementation.
- Medium: Multi-file implementation, feature development,
  and changes involving business logic.
- High: Complex debugging, architectural decisions,
  security-sensitive changes, and important PR reviews.
- Extra High: Critical system changes or particularly
  difficult problems requiring extensive investigation.

Increase reasoning effort when new complexity or risks emerge.
Do not use high reasoning for every routine task.

## 7. Independent Code Review

**Review code as if it were written by someone else.**

- Do not assume implementation is correct because you wrote it.
- Inspect the complete relevant diff.
- Look for logic errors, security vulnerabilities,
  race conditions, data integrity issues, and regressions.
- Verify findings against actual code.
- Distinguish confirmed bugs from potential risks.
- Report findings by severity.
- Never claim tests passed unless they were executed.
- Do not approve changes with unresolved critical issues.

## 8. Repository Instructions

- Follow all applicable AGENTS.md instructions.
- Inspect repository instructions before making changes.
- Respect nested AGENTS.md files within their applicable directories.
- Follow DESIGN.md for all applicable authenticated admin UI work.
- If instructions conflict, identify the conflict and
  resolve it according to repository guidance.
- Do not override project-specific conventions with
  generic preferences from this document.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

## Agent skills

### Issue tracker

Issues and specs live in GitHub Issues on `daonguyen1610/Nihome31042025` (`gh` CLI). External PRs are not a request surface. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default triage labels, each named after its role. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one root `GLOSSARY.md` plus `docs/adr/`. See `docs/agents/domain.md`.