"""Generate a readable SonarQube Cloud pull-request report for a GitHub PR comment."""

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
    """Escape untrusted SonarQube text for Markdown tables and headings."""
    text = html.escape(" ".join(str(value).split()), quote=False)
    return (
        text.replace("|", "&#124;")
        .replace("`", "&#96;")
        .replace("[", "&#91;")
        .replace("]", "&#93;")
    )


def badge(label, value, color):
    """Return a small, GitHub-compatible Shields.io badge."""
    def segment(text):
        return quote(
            str(text).replace("-", "--").replace("_", "__").replace(" ", "_"),
            safe="",
        )

    url = (
        f"https://img.shields.io/badge/{segment(label)}-"
        f"{segment(value)}-{color}?style=flat-square"
    )
    return f"![{safe(label)}: {safe(value)}]({url})"


def measures_for(**scope):
    response = sonar_api(
        "measures/component",
        component=PROJECT,
        metricKeys=",".join(
            (
                "quality_gate_details", "new_coverage", "coverage",
                "new_duplicated_lines_density", "duplicated_lines_density",
                "new_lines", "ncloc", "security_hotspots", "security_hotspots_reviewed",
            )
        ),
        **scope,
    )
    return {
        measure["metric"]: metric_value(measure)
        for measure in response["component"].get("measures", [])
    }


def metric_value(measure):
    """Read both standard and new-code period representations from the API."""
    if measure.get("value") is not None:
        return measure["value"]
    if isinstance(measure.get("period"), dict):
        return measure["period"].get("value")
    periods = measure.get("periods") or []
    return periods[-1].get("value") if periods else None


def percent(value):
    return f"{safe(value)}%" if value is not None else "N/A"


def coverage_badge(label, value):
    # Blue indicates a measurement, not a judgment against the Quality Gate.
    return badge(label, percent(value), "blue" if value is not None else "lightgrey")


def issue_link(issue):
    return f"{BASE_URL}/project/issues?{urlencode({'id': PROJECT, 'open': issue['key']})}"


def severity_details(issue):
    impacts = issue.get("impacts") or []
    rank = {
        "BLOCKER": 6, "CRITICAL": 5, "HIGH": 4, "MAJOR": 3,
        "MEDIUM": 3, "MINOR": 2, "LOW": 2, "INFO": 1,
    }
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
    return (
        f"| {severity_details(issue)} | "
        f"[{safe(issue.get('message', 'No description'))}]({issue_link(issue)}) | "
        f"`{safe(location)}` |"
    )


# The workflow sets sonar.qualitygate.wait=true, so PR analysis is complete here.
pr_metrics = measures_for(pullRequest=PR_NUMBER)
main_metrics = measures_for(branch="main")
gate = json.loads(pr_metrics.get("quality_gate_details") or "{}")
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

pr_count = pr_issues["total"]
main_count = main_issues["total"]
pr_url = f"{BASE_URL}/dashboard?{urlencode({'id': PROJECT, 'pullRequest': PR_NUMBER})}"
main_url = f"{BASE_URL}/project/issues?{urlencode({'id': PROJECT, 'branch': 'main'})}"

if gate_status == "OK":
    gate_label, gate_color = "PASSED", "brightgreen"
elif gate_status == "ERROR":
    gate_label, gate_color = "FAILED", "red"
else:
    gate_label, gate_color = "UNKNOWN", "lightgrey"

# Five badges, authored on a single Markdown line. GitHub may wrap on mobile.
lines = [
    "## 🛡️ EventRelay · Code Quality Report",
    "",
    f"**Pull request #{safe(PR_NUMBER)}** · Target: `main` · SonarQube Cloud",
    "",
    " &nbsp; ".join([
        badge("Quality Gate", gate_label, gate_color),
        badge("New Issues", pr_count, "brightgreen" if pr_count == 0 else "red"),
        badge("Existing Issues", main_count, "brightgreen" if main_count == 0 else "orange"),
        coverage_badge("PR Coverage", pr_metrics.get("new_coverage")),
        coverage_badge("Main Coverage", main_metrics.get("coverage")),
    ]),
    "",
]

if gate_status == "ERROR":
    lines.extend([
        "> [!WARNING]",
        "> The Quality Gate failed. Review its failed conditions before merging.",
        "",
    ])

lines.extend([
    "### 📊 Code metrics",
    "",
    "| Metric | This PR (new code) | `main` (overall code) |",
    "|:--|--:|--:|",
    f"| Test coverage | **{percent(pr_metrics.get('new_coverage'))}** | **{percent(main_metrics.get('coverage'))}** |",
    f"| Duplicated lines | {percent(pr_metrics.get('new_duplicated_lines_density'))} | {percent(main_metrics.get('duplicated_lines_density'))} |",
])

pr_lines = pr_metrics.get("new_lines")
main_lines = main_metrics.get("ncloc")
if pr_lines is not None or main_lines is not None:
    lines.append(
        f"| Code size | {safe(pr_lines) + ' new lines' if pr_lines is not None else 'N/A'} | "
        f"{safe(main_lines) + ' non-comment lines' if main_lines is not None else 'N/A'} |"
    )

hotspots = main_metrics.get("security_hotspots")
if hotspots is not None:
    reviewed = main_metrics.get("security_hotspots_reviewed")
    detail = (
        f"{safe(hotspots)} total ({percent(reviewed)} reviewed)"
        if reviewed is not None
        else f"{safe(hotspots)} total"
    )
    lines.append(f"| Security hotspots | — | {detail} on `main` |")

lines.extend([
    "",
    "<sub>PR coverage measures added/changed coverable code; main coverage measures the entire "
    "analyzed main branch. N/A means SonarQube did not return that measurement, "
    "not necessarily 0%. Code-size figures use different line definitions.</sub>",
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
    lines.append("✅ **No new open issues detected in this PR.**")

lines.extend(["", "---", ""])
if main_count:
    lines.extend([
        "<details>",
        f"<summary>📁 Existing issues on main — <strong>{main_count} open</strong> "
        f"(showing up to {LIMIT})</summary>",
        "",
        "These are existing issues on `main`; they do not automatically fail this PR's Quality Gate.",
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
    "This GitHub comment is updated when CI reruns.</sub>",
])

output = Path("sonar-pr-report.md")
output.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Generated {output} ({output.stat().st_size} bytes)")
