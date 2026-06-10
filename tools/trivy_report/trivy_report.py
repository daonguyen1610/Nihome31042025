#!/usr/bin/env python3
"""Generate a Markdown report from a Trivy JSON scan result.

Usage:
    python tools/trivy_report/trivy_report.py <trivy-results.json> [--output report.md]
Exit code:
    0 when no CRITICAL/HIGH findings are present.
    1 when at least one CRITICAL or HIGH finding is present (so CI fails).
    2 when the input file is missing or not valid JSON.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Iterable

BLOCKING_SEVERITIES = {"CRITICAL", "HIGH"}
SEVERITY_ORDER = {"CRITICAL": 0, "HIGH": 1, "MEDIUM": 2, "LOW": 3, "UNKNOWN": 4}


def md_escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ").strip()


def collect_findings(report: dict) -> tuple[list[dict], list[dict], list[dict]]:
    vulns: list[dict] = []
    secrets: list[dict] = []
    misconfigs: list[dict] = []

    for result in report.get("Results", []) or []:
        target = result.get("Target", "")
        for v in result.get("Vulnerabilities") or []:
            vulns.append({
                "target": target,
                "id": v.get("VulnerabilityID", ""),
                "pkg": v.get("PkgName", ""),
                "installed": v.get("InstalledVersion", ""),
                "fixed": v.get("FixedVersion", ""),
                "severity": v.get("Severity", "UNKNOWN").upper(),
                "title": v.get("Title") or v.get("Description", ""),
                "url": v.get("PrimaryURL", ""),
            })
        for s in result.get("Secrets") or []:
            secrets.append({
                "target": target,
                "rule": s.get("RuleID", ""),
                "category": s.get("Category", ""),
                "severity": s.get("Severity", "UNKNOWN").upper(),
                "title": s.get("Title", ""),
                "line": s.get("StartLine", ""),
            })
        for m in result.get("Misconfigurations") or []:
            misconfigs.append({
                "target": target,
                "id": m.get("ID", ""),
                "severity": m.get("Severity", "UNKNOWN").upper(),
                "title": m.get("Title", ""),
                "message": m.get("Message", ""),
                "url": m.get("PrimaryURL", ""),
            })

    return vulns, secrets, misconfigs


def filter_blocking(items: Iterable[dict]) -> list[dict]:
    return sorted(
        (i for i in items if i.get("severity") in BLOCKING_SEVERITIES),
        key=lambda i: (SEVERITY_ORDER.get(i["severity"], 99), i.get("target", "")),
    )


def severity_badge(sev: str) -> str:
    return {"CRITICAL": "🔴 CRITICAL", "HIGH": "🟠 HIGH"}.get(sev, sev)


def render_vuln_table(rows: list[dict]) -> str:
    if not rows:
        return ""
    out = [
        "### Vulnerabilities",
        "",
        "| Severity | Package | Installed | Fixed in | CVE | Target |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    for r in rows:
        cve_cell = (
            f"[{md_escape(r['id'])}]({r['url']})"
            if r.get("url")
            else md_escape(r["id"])
        )
        out.append(
            "| {sev} | {pkg} | `{installed}` | `{fixed}` | {cve} | `{target}` |".format(
                sev=severity_badge(r["severity"]),
                pkg=md_escape(r["pkg"]),
                installed=md_escape(r["installed"]),
                fixed=md_escape(r["fixed"] or "—"),
                cve=cve_cell,
                target=md_escape(r["target"]),
            )
        )
    out.append("")
    return "\n".join(out)


def render_secret_table(rows: list[dict]) -> str:
    if not rows:
        return ""
    out = [
        "### Secrets",
        "",
        "| Severity | Rule | Category | Line | Target |",
        "| --- | --- | --- | --- | --- |",
    ]
    for r in rows:
        out.append(
            "| {sev} | {rule} | {category} | {line} | `{target}` |".format(
                sev=severity_badge(r["severity"]),
                rule=md_escape(r["rule"]),
                category=md_escape(r["category"]),
                line=r["line"],
                target=md_escape(r["target"]),
            )
        )
    out.append("")
    return "\n".join(out)


def render_misconfig_table(rows: list[dict]) -> str:
    if not rows:
        return ""
    out = [
        "### Misconfigurations",
        "",
        "| Severity | ID | Title | Target |",
        "| --- | --- | --- | --- |",
    ]
    for r in rows:
        id_cell = (
            f"[{md_escape(r['id'])}]({r['url']})"
            if r.get("url")
            else md_escape(r["id"])
        )
        out.append(
            "| {sev} | {id} | {title} | `{target}` |".format(
                sev=severity_badge(r["severity"]),
                id=id_cell,
                title=md_escape(r["title"]),
                target=md_escape(r["target"]),
            )
        )
    out.append("")
    return "\n".join(out)


def build_report(report: dict) -> tuple[str, int]:
    vulns, secrets, misconfigs = collect_findings(report)
    blocking_vulns = filter_blocking(vulns)
    blocking_secrets = filter_blocking(secrets)
    blocking_misconfigs = filter_blocking(misconfigs)

    total = len(blocking_vulns) + len(blocking_secrets) + len(blocking_misconfigs)

    lines = ["## 🛡️ Trivy Security Scan (CRITICAL / HIGH)", ""]
    if total == 0:
        lines.append("✅ No CRITICAL or HIGH findings.")
        return "\n".join(lines) + "\n", 0

    crit = sum(1 for r in blocking_vulns + blocking_secrets + blocking_misconfigs if r["severity"] == "CRITICAL")
    high = total - crit
    lines.append(
        f"❌ **{total} finding(s) require attention** — {crit} CRITICAL, {high} HIGH. Please fix before merging."
    )
    lines.append("")

    for section in (
        render_vuln_table(blocking_vulns),
        render_secret_table(blocking_secrets),
        render_misconfig_table(blocking_misconfigs),
    ):
        if section:
            lines.append(section)

    return "\n".join(lines), 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("json_path", type=Path, help="Path to Trivy JSON output")
    parser.add_argument("--output", type=Path, help="Optional path to write the report to")
    args = parser.parse_args()

    if not args.json_path.exists():
        print(f"Trivy JSON not found: {args.json_path}", file=sys.stderr)
        return 2

    try:
        report = json.loads(args.json_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"Invalid JSON: {exc}", file=sys.stderr)
        return 2

    markdown, exit_code = build_report(report)

    if args.output:
        args.output.write_text(markdown, encoding="utf-8")

    print(markdown)
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
