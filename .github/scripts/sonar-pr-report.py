"""Generate an actionable SonarQube Cloud pull request summary for GitHub."""

import html
import json
import os
from pathlib import Path
from urllib.parse import quote, urlencode
from urllib.request import Request, urlopen

BASE_URL = "https://sonarcloud.io"
PROJECT = os.environ["SONAR_PROJECT_KEY"]
PR_NUMBER = os.environ["PR_NUMBER"]
TOKEN = os.environ["SONAR_TOKEN"]
LIMIT = 5


def sonar_api(endpoint, **params):
    url = f"{BASE_URL}/api/{endpoint}?{urlencode(params)}"
    request = Request(
        url,
        headers={
            "Authorization": f"Bearer {TOKEN}",
            "Accept": "application/json",
        },
    )
    with urlopen(request, timeout=30) as response:
        return json.load(response)


def safe(value):
    """Escape SonarQube-provided values before inserting into Markdown."""
    text = html.escape(" ".join(str(value).split()), quote=False)
    return (
        text.replace("|", "&#124;")
        .replace("`", "&#96;")
        .replace("[", "&#91;")
        .replace("]", "&#93;")
    )


def badge(label, value, color):
    """Generate a static Shields.io badge; no custom CSS needed."""
    def segment(text):
        return quote(
            str(text).replace("-", "--").replace("_", "__").replace(" ", "_"),
            safe="",
        )

    image_url = (
        f"https://img.shields.io/badge/{segment(label)}-"
        f"{segment(value)}-{color}?style=flat-square"
    )
    return f"![{safe(label)}: {safe(value)}]({image_url})"


def issue_link(issue):
    return f"{BASE_URL}/project/issues?{urlencode({'id': PROJECT, 'open': issue['key']})}"


def severity_details(issue):
    impacts = issue.get("impacts") or []
    rank = {"BLOCKER": 6, "CRITICAL": 5, "HIGH": 4, "MAJOR": 3,
            "MEDIUM": 3, "MINOR": 2, "LOW": 2, "INFO": 1}
    if impacts:
        impact = max(
            impacts,
            key=lambda item: rank.get(str(item.get("severity", "")).upper(), 0),
        )
        quality = impact.get("softwareQuality", "Issue")
        severity = str(impact.get("severity", "UNKNOWN")).upper()
    else:
        quality = issue.get("type", "Issue")
        severity = str(issue.get("severity", "UNKNOWN")).upper()

    icon = (
        "🔴" if severity in {"BLOCKER", "CRITICAL", "HIGH"}
        else "🟠" if severity in {"MAJOR", "MEDIUM"}
        else "🟡" if severity in {"MINOR", "LOW"}
        else "⚪"
    )
    return f"{icon} **{safe(severity)}** · {safe(quality).title()}"


def issue_row(issue):
    component = issue.get("component", "")
    filename = component.removeprefix(PROJECT + ":")
    line = issue.get("line") or (issue.get("textRange") or {}).get("startLine")
    location = f"{filename}:{line}" if line else filename
    message = safe(issue.get("message", "No description"))
    return (
        f"| {severity_details(issue)} | "
        f"[{message}]({issue_link(issue)}) | `{safe(location)}` |"
    )


# SonarQube Cloud has already finished analyzing this PR because the
# workflow uses sonar.qualitygate.wait=true.
response = sonar_api(
    "measures/component",
    component=PROJECT,
    pullRequest=PR_NUMBER,
    metricKeys="quality_gate_details,new_coverage,new_duplicated_lines_density",
)
measures = {
    item["metric"]: item.get("value")
    for item in response["component"].get("measures", [])
}
gate = json.loads(measures.get("quality_gate_details") or "{}")
gate_status = gate.get("level", "UNKNOWN")

pr_issues = sonar_api(
    "issues/search",
    componentKeys=PROJECT,
    pullRequest=PR_NUMBER,
    issueStatuses="OPEN,CONFIRMED",
    sinceLeakPeriod="true",
    s="SEVERITY",
    asc="false",
    ps=LIMIT,
)
main_issues = sonar_api(
    "issues/search",
    componentKeys=PROJECT,
    branch="main",
    issueStatuses="OPEN,CONFIRMED",
    s="SEVERITY",
    asc="false",
    ps=LIMIT,
)

pr_url = f"{BASE_URL}/dashboard?{urlencode({'id': PROJECT, 'pullRequest': PR_NUMBER})}"
main_url = f"{BASE_URL}/project/issues?{urlencode({'id': PROJECT, 'branch': 'main'})}"
pr_count = pr_issues["total"]
main_count = main_issues["total"]

if gate_status == "OK":
    gate_label, gate_color = "PASSED", "brightgreen"
elif gate_status == "ERROR":
    gate_label, gate_color = "FAILED", "red"
else:
    gate_label, gate_color = "UNKNOWN", "lightgrey"

lines = [
    "## 🛡️ EventRelay · Code Quality Report",
    "",
    f"**Pull request #{safe(PR_NUMBER)}** · Target: `main` · SonarQube Cloud",
    "",
    # All three small badges share ONE Markdown line (also on GitHub).
    " &nbsp; ".join([
        badge("Quality Gate", gate_label, gate_color),
        badge("New Issues", pr_count, "brightgreen" if pr_count == 0 else "red"),
        badge("Existing Issues", main_count, "brightgreen" if main_count == 0 else "orange"),
    ]),
    "",
]

# Avoid duplicating the 'no new issues' message in a separate tip box.
if gate_status == "ERROR":
    lines.extend([
        "> [!WARNING]",
        "> The Quality Gate failed. Review the failed conditions before merging.",
        "",
    ])

coverage = measures.get("new_coverage")
duplication = measures.get("new_duplicated_lines_density")
lines.extend([
    "### 📊 New-code metrics",
    "",
    "| Metric | Value |",
    "|:--|--:|",
    f"| Test coverage | **{safe(coverage)}%** |" if coverage is not None
    else "| Test coverage | **N/A** |",
    f"| Duplicated lines | **{safe(duplication)}%** |" if duplication is not None
    else "| Duplicated lines | **N/A** |",
    "",
])

if coverage is None or duplication is None:
    lines.extend([
        "*N/A means SonarQube did not return a value for this PR; it does not mean 0%.*",
        "",
    ])

failed = [
    item for item in gate.get("conditions", [])
    if item.get("level") == "ERROR" or item.get("status") == "ERROR"
]
if failed:
    lines.extend([
        "### 🚨 Failed Quality Gate conditions",
        "",
        "| Metric | Actual | Threshold |",
        "|:--|--:|--:|",
    ])
    for item in failed:
        lines.append(
            f"| `{safe(item.get('metric', 'unknown'))}` | "
            f"{safe(item.get('actual', 'N/A'))} | "
            f"{safe(item.get('error', 'N/A'))} |"
        )
    lines.append("")

lines.extend(["### 🔎 Findings introduced by this PR", ""])
if pr_issues.get("issues"):
    lines.extend(["| Priority | Finding | File |", "|:--|:--|:--|"])
    lines.extend(issue_row(item) for item in pr_issues["issues"])
    if pr_count > LIMIT:
        lines.append(f"\n*Showing {LIMIT} of {pr_count} new issues.*")
else:
    lines.append("✅ **Nothing to fix:** no new open issues detected in this PR.")

lines.extend(["", "---", ""])
if main_count:
    lines.extend([
        "<details>",
        f"<summary>📁 Existing issues on main — <strong>{main_count} open</strong> "
        f"(showing up to {LIMIT})</summary>",
        "",
        "These issues already exist on `main` and do not automatically fail this PR's Quality Gate.",
        "",
        "| Priority | Finding | File |",
        "|:--|:--|:--|",
    ])
    lines.extend(issue_row(item) for item in main_issues["issues"])
    lines.extend(["", f"[View all {main_count} existing issues]({main_url})", "", "</details>", ""])
else:
    lines.extend(["✅ No existing open issues on `main`.", ""])

lines.extend([
    f"🔗 **[Full PR analysis]({pr_url})** · **[Main branch backlog]({main_url})**",
    "",
    "<sub>Generated automatically from SonarQube Cloud. "
    "The GitHub comment updates when CI reruns.</sub>",
])

output = Path("sonar-pr-report.md")
output.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Generated {output} ({output.stat().st_size} bytes)")
