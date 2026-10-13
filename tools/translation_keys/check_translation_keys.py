#!/usr/bin/env python3
"""Find translation keys referenced by source code that have no seeded translation.

The application resolves UI text through ``nihomeweb/src/lib/i18n.tsx`` and, on the
server, through the ``translations`` table. Both fall back to the raw key when a
translation row is absent, so a missing key renders as ``auth.login.title`` in the
middle of the screen instead of failing loudly. This tool closes that gap: it
collects every key the code can ask for and compares it against
``nihomebackend/Data/Seeds/i18n/*.json``, which is the source of truth for the
``TranslationSeeder``.

Usage:
    python tools/translation_keys/check_translation_keys.py [--repo-root PATH]
        [--format text|markdown|json] [--output FILE] [--no-warnings]
Exit code:
    0 when every referenced key resolves in all four languages (vi, en, zh, ja).
    1 when at least one referenced key is missing (so CI fails).
    2 when the repository layout cannot be read.
"""

from __future__ import annotations

import argparse
import bisect
import json
import os
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Sequence

LANGUAGES = ("vi", "en", "zh", "ja")

SEED_DIR = Path("nihomebackend/Data/Seeds/i18n")
FRONTEND_SRC = Path("nihomeweb/src")
BACKEND_SRC = Path("nihomebackend")
NOTIFICATION_SEED = Path("nihomebackend/Data/Seeds/notification-templates/defaults.json")
MASTER_DATA_SEED = Path("nihomebackend/Data/Seeds/master-data/defaults.json")
RBAC_SEED = Path("nihomebackend/Data/Rbac/rbac-defaults.json")

# Migrations are skipped on purpose: they are historical, hand-written data fixes that
# reference keys as they existed at the time. A key renamed later cannot be repaired by
# editing a shipped migration, so failing on one would be a false alarm.
BACKEND_SKIP_DIRS = frozenset({"bin", "obj", "Migrations", "wwwroot"})

# `t("key")` / `t('key')` — the only literal call shape the i18n context supports.
FE_CALL_LITERAL = re.compile(r"""\bt\(\s*(["'])([^"'\n]+)\1""")
# `t(`prefix.${value}`)` — the key is only knowable at runtime.
FE_CALL_TEMPLATE = re.compile(r"\bt\(\s*`([^`\n]*)`")
# `labelKey: "aboutAdmin.tab.intro"` — dotted string constants fed to `t(variable)`.
FE_KEY_CONSTANT = re.compile(r"""\b[A-Za-z]*[Kk]ey\s*:\s*(["'])([A-Za-z][\w.]*\.[\w.]+)\1""")

# Backend files resolve keys through a local helper rather than a framework call.
# Discover the helper per file so we only inspect genuine translation lookups:
#   string T(string key, string fallback) => text.GetValueOrDefault(key, fallback);
#   Func<string, string, string> text  (the helper passed as a delegate)
BE_HELPER_LOCAL = re.compile(r"string\s+([A-Za-z_]\w*)\s*\([^)]*GetValueOrDefault\(", re.S)
BE_HELPER_PARAM = re.compile(r"Func<\s*string\s*,\s*string\s*,\s*string\s*>\s*([A-Za-z_]\w*)")
BE_CALL_TEMPLATE = r'\b(?:{names})\s*\(\s*(\$?"[^"\n]*")'
# Keys persisted onto entities/templates and later resolved by the client or a renderer.
BE_KEY_ASSIGN = re.compile(
    r'\b(?:TranslationKey|LabelKey|TitleKey|BodyKey|DisplayKey|displayKey)'
    r'\s*[=:]\s*(\$?"[^"\n]+")'
)


class ToolError(Exception):
    """Raised when the repository layout cannot be read."""


@dataclass(frozen=True)
class CheckDef:
    """Static description of one check, used for the summary table."""

    id: str
    label: str
    short: str


CHECKS: tuple[CheckDef, ...] = (
    CheckDef("fe-literal", "frontend t() literals", "frontend literal"),
    CheckDef("fe-family", "frontend dynamic families", "frontend family"),
    CheckDef("fe-keyconst", "frontend *Key constants", "frontend constant"),
    CheckDef("be-literal", "backend literal keys", "backend literal"),
    CheckDef("be-family", "backend dynamic families", "backend family"),
    CheckDef("be-notification", "notification template keys", "notification template"),
    CheckDef("be-masterdata", "master data label keys", "master data label"),
    CheckDef("be-rbac", "rbac permission keys", "rbac permission"),
    CheckDef("seed-language", "seed language completeness", "seed language"),
)
CHECK_BY_ID = {check.id: check for check in CHECKS}


@dataclass(frozen=True)
class Reference:
    """A key (or key family) some source file can ask the translation store for."""

    check: str
    key: str
    where: str
    is_family: bool = False


@dataclass
class CheckResult:
    definition: CheckDef
    checked: int = 0
    findings: list[tuple[str, str]] = field(default_factory=list)

    @property
    def missing(self) -> int:
        return len(self.findings)


@dataclass(frozen=True)
class Conflict:
    """A key defined more than once with differing values for the same language."""

    key: str
    language: str
    values: tuple[str, ...]
    files: tuple[str, ...]


@dataclass
class SeedIndex:
    keys: set[str]
    sorted_keys: list[str]
    defining_files: dict[str, list[str]]
    values: dict[str, dict[str, set[str]]]
    entry_count: int

    def has_family(self, prefix: str) -> bool:
        """True when at least one seeded key starts with ``prefix``."""
        position = bisect.bisect_left(self.sorted_keys, prefix)
        return position < len(self.sorted_keys) and self.sorted_keys[position].startswith(prefix)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def load_json(path: Path) -> object:
    try:
        return json.loads(read_text(path))
    except (OSError, ValueError) as error:
        raise ToolError(f"cannot read {path}: {error}") from error


def iter_source_files(root: Path, suffixes: tuple[str, ...], skip_dirs: frozenset[str] = frozenset()) -> Iterable[Path]:
    """Yield source files in a deterministic order, skipping build output."""
    if not root.is_dir():
        raise ToolError(f"source directory not found: {root}")
    for dir_path, dir_names, file_names in os.walk(root):
        dir_names[:] = sorted(name for name in dir_names if name not in skip_dirs)
        for file_name in sorted(file_names):
            if file_name.endswith(suffixes):
                yield Path(dir_path) / file_name


def relative(repo_root: Path, path: Path) -> str:
    return path.relative_to(repo_root).as_posix()


def locate(repo_root: Path, path: Path, source: str, offset: int) -> str:
    """Render a `file:line` reference from a regex match offset."""
    line = source.count("\n", 0, offset) + 1
    return f"{relative(repo_root, path)}:{line}"


def unquote(raw: str) -> str:
    """Strip the leading `$` and surrounding quotes from a C# string literal."""
    text = raw[1:] if raw.startswith("$") else raw
    if len(text) >= 2 and text.startswith('"') and text.endswith('"'):
        text = text[1:-1]
    return text


def load_seeds(repo_root: Path) -> SeedIndex:
    seed_dir = repo_root / SEED_DIR
    if not seed_dir.is_dir():
        raise ToolError(f"translation seed directory not found: {seed_dir}")

    defining_files: dict[str, list[str]] = defaultdict(list)
    values: dict[str, dict[str, set[str]]] = defaultdict(lambda: defaultdict(set))
    entries = 0

    # Sorted by file name to mirror TranslationSeeder's OrderBy on the resource name,
    # which is what decides the winner when two seed files define the same key.
    for path in sorted(seed_dir.glob("*.json"), key=lambda item: item.name):
        payload = load_json(path)
        if not isinstance(payload, list):
            raise ToolError(f"{relative(repo_root, path)} must contain a JSON array of entries")
        for entry in payload:
            if not isinstance(entry, dict):
                continue
            key = (entry.get("key") or "").strip()
            if not key:
                continue
            entries += 1
            defining_files[key].append(path.name)
            for language in LANGUAGES:
                value = entry.get(language)
                if isinstance(value, str) and value.strip():
                    values[key][language].add(value)

    keys = set(defining_files)
    return SeedIndex(
        keys=keys,
        sorted_keys=sorted(keys),
        defining_files=dict(defining_files),
        values={key: dict(langs) for key, langs in values.items()},
        entry_count=entries,
    )


def collect_frontend(repo_root: Path) -> list[Reference]:
    references: list[Reference] = []
    for path in iter_source_files(repo_root / FRONTEND_SRC, (".ts", ".tsx")):
        source = read_text(path)

        for match in FE_CALL_LITERAL.finditer(source):
            references.append(
                Reference("fe-literal", match.group(2), locate(repo_root, path, source, match.start()))
            )

        for match in FE_CALL_TEMPLATE.finditer(source):
            template = match.group(1)
            where = locate(repo_root, path, source, match.start())
            if "${" in template:
                references.append(
                    Reference("fe-family", template.split("${", 1)[0], where, is_family=True)
                )
            else:
                # A backtick string without interpolation is just a literal key.
                references.append(Reference("fe-literal", template, where))

        for match in FE_KEY_CONSTANT.finditer(source):
            references.append(
                Reference("fe-keyconst", match.group(2), locate(repo_root, path, source, match.start()))
            )

    return references


def collect_backend(repo_root: Path) -> list[Reference]:
    references: list[Reference] = []
    for path in iter_source_files(repo_root / BACKEND_SRC, (".cs",), BACKEND_SKIP_DIRS):
        source = read_text(path)
        where = relative(repo_root, path)

        helper_names = set(BE_HELPER_LOCAL.findall(source)) | set(BE_HELPER_PARAM.findall(source))
        candidates: list[tuple[str, int]] = []
        if helper_names:
            pattern = BE_CALL_TEMPLATE.format(
                names="|".join(re.escape(name) for name in sorted(helper_names))
            )
            for match in re.finditer(pattern, source):
                candidates.append((unquote(match.group(1)), match.start()))
        for match in BE_KEY_ASSIGN.finditer(source):
            candidates.append((unquote(match.group(1)), match.start()))

        for value, offset in candidates:
            if "{" in value:
                references.append(
                    Reference("be-family", value.split("{", 1)[0], locate(repo_root, path, source, offset), is_family=True)
                )
            else:
                references.append(Reference("be-literal", value, locate(repo_root, path, source, offset)))

    return references


def require_list(payload: object, path: Path, field_name: str) -> list:
    if not isinstance(payload, dict):
        raise ToolError(f"{path} must contain a JSON object")
    value = payload.get(field_name)
    if not isinstance(value, list):
        raise ToolError(f"{path} has no '{field_name}' array")
    return value


def collect_derived(repo_root: Path) -> list[Reference]:
    """Keys the backend derives from catalog/template codes rather than writing out."""
    references: list[Reference] = []

    notification_path = repo_root / NOTIFICATION_SEED
    if notification_path.is_file():
        where = relative(repo_root, notification_path)
        for template in require_list(load_json(notification_path), notification_path, "templates"):
            code = (template.get("code") or "").strip() if isinstance(template, dict) else ""
            if not code:
                continue
            # NotificationTemplateSeeder.cs derives both keys from the template code.
            references.append(Reference("be-notification", f"notification.{code}.title", where))
            references.append(Reference("be-notification", f"notification.{code}.body", where))

    master_data_path = repo_root / MASTER_DATA_SEED
    if master_data_path.is_file():
        where = relative(repo_root, master_data_path)
        for category in require_list(load_json(master_data_path), master_data_path, "categories"):
            if not isinstance(category, dict):
                continue
            name = (category.get("category") or "").strip()
            if not name:
                continue
            references.append(Reference("be-masterdata", f"masterData.category.{name}.title", where))
            for option in category.get("options") or []:
                code = (option.get("code") or "").strip() if isinstance(option, dict) else ""
                if code:
                    # MasterDataSeeder.cs stores this as the row's LabelKey.
                    references.append(Reference("be-masterdata", f"masterData.{name}.{code}.label", where))

    rbac_path = repo_root / RBAC_SEED
    if rbac_path.is_file():
        where = relative(repo_root, rbac_path)
        modules: set[str] = set()
        for entry in require_list(load_json(rbac_path), rbac_path, "baseCatalog"):
            if not isinstance(entry, dict):
                continue
            module = (entry.get("module") or "").strip()
            action = (entry.get("action") or "").strip()
            if not module or not action:
                continue
            modules.add(module.split(".", 1)[0])
            references.append(Reference("be-rbac", f"rbac.perm.{module}.{action}.label", where))
            references.append(Reference("be-rbac", f"rbac.perm.{module}.{action}.description", where))
        for module in sorted(modules):
            references.append(Reference("be-rbac", f"adminRbac.module.{module}", where))

    return references


def collect_seed_language_gaps(repo_root: Path) -> CheckResult:
    """Every seed entry must carry all four languages; the seeder silently skips blanks."""
    result = CheckResult(CHECK_BY_ID["seed-language"])
    seed_dir = repo_root / SEED_DIR
    for path in sorted(seed_dir.glob("*.json"), key=lambda item: item.name):
        payload = load_json(path)
        if not isinstance(payload, list):
            continue
        for entry in payload:
            if not isinstance(entry, dict):
                continue
            key = (entry.get("key") or "").strip()
            if not key:
                continue
            result.checked += 1
            for language in LANGUAGES:
                value = entry.get(language)
                if not (isinstance(value, str) and value.strip()):
                    result.findings.append((f"{key} [{language}]", relative(repo_root, path)))

    return result


def find_conflicts(seeds: SeedIndex) -> list[Conflict]:
    """Keys defined twice with different text — the seeder keeps the first and drops the rest."""
    conflicts: list[Conflict] = []
    for key in sorted(seeds.defining_files):
        if len(seeds.defining_files[key]) < 2:
            continue
        for language in LANGUAGES:
            distinct = seeds.values.get(key, {}).get(language, set())
            if len(distinct) > 1:
                conflicts.append(
                    Conflict(
                        key=key,
                        language=language,
                        values=tuple(sorted(distinct)),
                        files=tuple(sorted(set(seeds.defining_files[key]))),
                    )
                )
    return conflicts


def find_orphans(seeds: SeedIndex, references: Sequence[Reference]) -> Counter:
    """Seeded keys nothing appears to reference, grouped by prefix.

    Informational only: keys resolved from data (PDF/Excel exports, notification
    rendering, master-data labels) are legitimately invisible to a static scan.
    """
    literal_keys = {ref.key for ref in references if not ref.is_family}
    family_prefixes = {ref.key for ref in references if ref.is_family and ref.key}

    orphans: Counter = Counter()
    for key in seeds.keys:
        if key in literal_keys:
            continue
        if any(key.startswith(prefix) for prefix in family_prefixes):
            continue
        prefix = ".".join(key.split(".")[:2])
        orphans[prefix] += 1
    return orphans


def count_unresolvable(references: Sequence[Reference]) -> int:
    """Call sites whose key is built entirely at runtime, so no static check is possible."""
    return len({ref.where for ref in references if is_unresolvable(ref)})


def is_unresolvable(reference: Reference) -> bool:
    """A key built entirely at runtime (`t(`${value}`)`) has no static prefix to check."""
    return reference.is_family and not reference.key


def build_results(seeds: SeedIndex, references: Sequence[Reference]) -> dict[str, CheckResult]:
    """Count each distinct key once, noting every call site of the ones that are missing."""
    grouped: dict[str, dict[str, list[Reference]]] = defaultdict(lambda: defaultdict(list))
    for reference in references:
        if is_unresolvable(reference):
            continue
        grouped[reference.check][reference.key].append(reference)

    results = {check.id: CheckResult(check) for check in CHECKS}
    for check_id, by_key in grouped.items():
        result = results[check_id]
        for key, occurrences in by_key.items():
            result.checked += 1
            first = occurrences[0]
            present = seeds.has_family(key) if first.is_family else key in seeds.keys
            if present:
                continue
            locations = {occurrence.where for occurrence in occurrences}
            where = first.where
            if len(locations) > 1:
                where = f"{where} (+{len(locations) - 1} more)"
            result.findings.append((f"{key}*" if first.is_family else key, where))

    for result in results.values():
        result.findings.sort()
    return results


def render_ascii_table(headers: Sequence[str], rows: Sequence[Sequence[str]], right: Sequence[int] = ()) -> str:
    widths = [len(header) for header in headers]
    for row in rows:
        for index, cell in enumerate(row):
            widths[index] = max(widths[index], len(cell))

    border = "+" + "+".join("-" * (width + 2) for width in widths) + "+"

    def format_row(row: Sequence[str]) -> str:
        cells = []
        for index, cell in enumerate(row):
            cells.append(f" {cell.rjust(widths[index]) if index in right else cell.ljust(widths[index])} ")
        return "|" + "|".join(cells) + "|"

    lines = [border, format_row(headers), border]
    lines.extend(format_row(row) for row in rows)
    lines.append(border)
    return "\n".join(lines)


def render_text(
    results: dict[str, CheckResult],
    orphans: Counter,
    conflicts: Sequence[Conflict],
    show_warnings: bool,
    unresolvable: int = 0,
) -> str:
    missing_total = sum(result.missing for result in results.values())
    checked_total = sum(result.checked for result in results.values())

    lines: list[str] = []
    if missing_total:
        lines.append(f"Translation key check — {missing_total} missing")
    else:
        lines.append("Translation key check — all referenced keys are seeded")
    lines.append("")
    lines.append("Summary")
    summary_rows = [
        [result.definition.label, str(result.checked), str(result.missing)]
        for result in results.values()
    ]
    summary_rows.append(["TOTAL", str(checked_total), str(missing_total)])
    lines.append(render_ascii_table(["Check", "Checked", "Missing"], summary_rows, right=(1, 2)))
    lines.append("")

    findings = [
        (CHECK_BY_ID[check_id].short, key, where)
        for check_id, result in results.items()
        for key, where in result.findings
    ]
    findings.sort()
    if findings:
        lines.append(f"Missing keys ({len(findings)})")
        lines.append(render_ascii_table(["Check", "Key", "Where"], findings))
        lines.append("")
    else:
        lines.append("No missing keys.")
        lines.append("")

    if show_warnings:
        lines.append("Warnings (informational — these never fail the check)")
        lines.append("")
        if conflicts:
            conflict_rows = [
                [conflict.key, conflict.language, " | ".join(conflict.values), ", ".join(conflict.files)]
                for conflict in conflicts
            ]
            keys_in_conflict = len({conflict.key for conflict in conflicts})
            lines.append(
                f"Conflicting duplicate keys ({keys_in_conflict} keys, {len(conflicts)} language rows) — "
                "TranslationSeeder keeps the alphabetically first file and silently discards the rest"
            )
            lines.append(render_ascii_table(["Key", "Lang", "Values", "Defined in"], conflict_rows))
        else:
            lines.append("Conflicting duplicate keys: none")
        lines.append("")

        if unresolvable:
            lines.append(
                f"{unresolvable} call site(s) build the key entirely at runtime (e.g. t(`${{value}}`)) "
                "and cannot be checked statically."
            )
            lines.append("")

        if orphans:
            lines.append(f"Seeded but not referenced statically ({sum(orphans.values())} keys, by prefix)")
            lines.append(
                render_ascii_table(
                    ["Prefix", "Keys"],
                    [[prefix, str(count)] for prefix, count in sorted(orphans.items(), key=lambda item: (-item[1], item[0]))],
                    right=(1,),
                )
            )
        else:
            lines.append("Seeded but not referenced statically: none")
        lines.append("")

    return "\n".join(lines)


def md_escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ").strip()


def render_markdown(
    results: dict[str, CheckResult],
    orphans: Counter,
    conflicts: Sequence[Conflict],
    show_warnings: bool,
    unresolvable: int = 0,
) -> str:
    missing_total = sum(result.missing for result in results.values())
    checked_total = sum(result.checked for result in results.values())

    lines = ["## 🌐 Translation Key Check", ""]
    if missing_total == 0:
        lines.append(f"✅ All {checked_total} referenced keys resolve in all four languages.")
        return "\n".join(lines) + "\n"

    lines.append(
        f"❌ **{missing_total} referenced key(s) have no seeded translation.** "
        "They render as raw key strings in the UI. Add the missing rows under "
        "`nihomebackend/Data/Seeds/i18n/`."
    )
    lines.append("")
    lines.append("| Check | Checked | Missing |")
    lines.append("| --- | --- | --- |")
    for result in results.values():
        lines.append(f"| {md_escape(result.definition.label)} | {result.checked} | {result.missing} |")
    lines.append(f"| **TOTAL** | **{checked_total}** | **{missing_total}** |")
    lines.append("")

    findings = [
        (CHECK_BY_ID[check_id].short, key, where)
        for check_id, result in results.items()
        for key, where in result.findings
    ]
    findings.sort()
    lines.append("### Missing keys")
    lines.append("")
    lines.append("| Check | Key | Where |")
    lines.append("| --- | --- | --- |")
    for check, key, where in findings:
        lines.append(f"| {md_escape(check)} | `{md_escape(key)}` | `{md_escape(where)}` |")
    lines.append("")

    if show_warnings:
        lines.append("<details><summary>Warnings (informational, never fail the check)</summary>")
        lines.append("")
        keys_in_conflict = len({conflict.key for conflict in conflicts})
        lines.append(f"#### Conflicting duplicate keys ({keys_in_conflict} keys, {len(conflicts)} language rows)")
        lines.append("")
        if conflicts:
            lines.append("`TranslationSeeder` keeps the alphabetically first file and silently discards the rest.")
            lines.append("")
            lines.append("| Key | Lang | Values | Defined in |")
            lines.append("| --- | --- | --- | --- |")
            for conflict in conflicts:
                values = " / ".join(md_escape(value) for value in conflict.values)
                lines.append(
                    f"| `{md_escape(conflict.key)}` | {conflict.language} | {values} | "
                    f"`{md_escape(', '.join(conflict.files))}` |"
                )
        else:
            lines.append("None.")
        lines.append("")

        total_orphans = sum(orphans.values())
        lines.append(f"#### Seeded but not referenced statically ({total_orphans})")
        lines.append("")
        if orphans:
            lines.append("| Prefix | Keys |")
            lines.append("| --- | --- |")
            for prefix, count in sorted(orphans.items(), key=lambda item: (-item[1], item[0])):
                lines.append(f"| `{md_escape(prefix)}` | {count} |")
        else:
            lines.append("None.")
        lines.append("")

        if unresolvable:
            lines.append(f"#### Fully dynamic keys ({unresolvable} call sites)")
            lines.append("")
            lines.append(
                "These build the key entirely at runtime (e.g. ``t(`${value}`)``) and cannot be "
                "checked statically."
            )
            lines.append("")
        lines.append("</details>")
        lines.append("")

    return "\n".join(lines)


def render_json(
    results: dict[str, CheckResult],
    orphans: Counter,
    conflicts: Sequence[Conflict],
    unresolvable: int = 0,
) -> str:
    payload = {
        "checkedTotal": sum(result.checked for result in results.values()),
        "missingTotal": sum(result.missing for result in results.values()),
        "unresolvableCallSites": unresolvable,
        "checks": [
            {
                "id": result.definition.id,
                "label": result.definition.label,
                "checked": result.checked,
                "missing": result.missing,
            }
            for result in results.values()
        ],
        "missing": [
            {"check": check_id, "key": key, "where": where}
            for check_id, result in results.items()
            for key, where in result.findings
        ],
        "warnings": {
            "conflicts": [
                {
                    "key": conflict.key,
                    "language": conflict.language,
                    "values": list(conflict.values),
                    "files": list(conflict.files),
                }
                for conflict in conflicts
            ],
            "orphans": [
                {"prefix": prefix, "count": count}
                for prefix, count in sorted(orphans.items(), key=lambda item: (-item[1], item[0]))
            ],
        },
    }
    return json.dumps(payload, indent=2, ensure_ascii=False) + "\n"


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
        help="Repository root (defaults to the directory two levels above this script)",
    )
    parser.add_argument("--format", choices=("text", "markdown", "json"), default="text")
    parser.add_argument("--output", type=Path, help="Also write the rendered report to this file")
    parser.add_argument("--no-warnings", action="store_true", help="Hide duplicate-key and orphan warnings")
    args = parser.parse_args(argv)

    if hasattr(sys.stdout, "reconfigure"):
        # Seed values contain Vietnamese, Chinese and Japanese text; the Windows
        # console defaults to cp1252 and would raise UnicodeEncodeError on them.
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    repo_root = args.repo_root.resolve()
    try:
        seeds = load_seeds(repo_root)
        references = collect_frontend(repo_root) + collect_backend(repo_root) + collect_derived(repo_root)
        results = build_results(seeds, references)
        results["seed-language"] = collect_seed_language_gaps(repo_root)
        conflicts = find_conflicts(seeds)
        orphans = find_orphans(seeds, references)
        unresolvable = count_unresolvable(references)
    except ToolError as error:
        print(f"translation key check failed: {error}", file=sys.stderr)
        return 2

    if args.format == "json":
        report = render_json(results, orphans, conflicts, unresolvable)
    else:
        show_warnings = not args.no_warnings
        if args.format == "markdown":
            report = render_markdown(results, orphans, conflicts, show_warnings, unresolvable)
        else:
            report = render_text(results, orphans, conflicts, show_warnings, unresolvable)

    sys.stdout.write(report)
    if args.output:
        args.output.write_text(report, encoding="utf-8")

    return 1 if any(result.missing for result in results.values()) else 0


if __name__ == "__main__":
    sys.exit(main())
