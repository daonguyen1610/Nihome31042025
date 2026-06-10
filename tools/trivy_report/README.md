# Trivy Report

A small Python script that converts a [Trivy](https://aquasecurity.github.io/trivy/)
JSON scan result into a Markdown report listing **CRITICAL** and **HIGH**
findings (vulnerabilities, secrets, and misconfigurations).

The report is used by the `trivy` job in
[`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) to:

- Publish a summary to the GitHub Actions job summary
- Post (and auto-update) a PR comment with the findings
- Fail the CI job when any CRITICAL/HIGH finding exists

## Requirements

- Python 3.10+ (standard library only — no external dependencies)
- A Trivy JSON output file (e.g. produced by `trivy fs --format json`)

## Usage

```bash
python tools/trivy_report/trivy_report.py <trivy-results.json> [--output report.md]
```

### Arguments

| Argument         | Description                                                   |
| ---------------- | ------------------------------------------------------------- |
| `json_path`      | Path to a Trivy JSON output file (required).                  |
| `--output PATH`  | Optional file to write the Markdown report to.                |

The Markdown is always printed to **stdout**; `--output` additionally writes it
to a file.

### Exit codes

| Code | Meaning                                                  |
| ---- | -------------------------------------------------------- |
| `0`  | No CRITICAL or HIGH findings.                            |
| `1`  | At least one CRITICAL or HIGH finding — CI should fail.  |
| `2`  | Input file is missing or not valid JSON.                 |

## Generating the input file

Locally, you can produce a compatible JSON file with the Trivy CLI:

```bash
trivy fs \
  --scanners vuln,secret,misconfig \
  --severity CRITICAL,HIGH \
  --ignore-unfixed \
  --format json \
  --output trivy-results.json \
  .
```

Then render the Markdown report:

```bash
python tools/trivy_report/trivy_report.py trivy-results.json --output trivy-report.md
cat trivy-report.md
```

## Output sample

```markdown
## 🛡️ Trivy Security Scan (CRITICAL / HIGH)

❌ **3 finding(s) require attention** — 1 CRITICAL, 2 HIGH. Please fix before merging.

### Vulnerabilities

| Severity | Package | Installed | Fixed in | CVE | Target |
| --- | --- | --- | --- | --- | --- |
| 🔴 CRITICAL | lodash | `4.17.20` | `4.17.21` | [CVE-2024-12345](https://...) | `nihomeweb/package-lock.json` |
| 🟠 HIGH | axios | `0.21.0` | `0.21.4` | [CVE-2023-99999](https://...) | `nihomeweb/package-lock.json` |

### Misconfigurations

| Severity | ID | Title | Target |
| --- | --- | --- | --- |
| 🟠 HIGH | [DS002](https://avd.aquasec.com/misconfig/ds002) | Dockerfile runs as root | `Dockerfile` |
```

When there are no findings:

```markdown
## 🛡️ Trivy Security Scan (CRITICAL / HIGH)

✅ No CRITICAL or HIGH findings.
```
