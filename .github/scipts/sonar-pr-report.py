import json
import os
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import Request, urlopen

BASE_URL = "https://sonarcloud.io"
PROJECT = os.environ["SONAR_PROJECT_KEY"]
PR_NUMBER = os.environ["PR_NUMBER"]
TOKEN = os.environ["SONAR_TOKEN"]


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


def issue_url(issue):
    query = urlencode({"id": PROJECT, "open": issue["key"]})
    return f"{BASE_URL}/project/issues?{query}"


def clean(value):
    return " ".join(str(value).split()).replace("|", "\\|").replace("`", "'")


def format_issue(issue):
    impacts = issue.get("impacts") or []
    severity = (
        ", ".join(
            f"{x['softwareQuality']} {x['severity']}"
            for x in impacts
        )
        if impacts
        else issue.get("severity", "UNKNOWN")
    )

    component = issue.get("component", "")
    file = component.removeprefix(PROJECT + ":")
    line = issue.get("line") or issue.get("textRange", {}).get("startLine")
    location = f"{file}:{line}" if line else file

    return (
        f"- **{clean(severity)}** — {clean(issue['message'])}\n"
        f"  - `{clean(location)}` · [Inspect and fix]({issue_url(issue)})"
    )


# Read the completed PR analysis.
measures_response = sonar_api(
    "measures/component",
    component=PROJECT,
    pullRequest=PR_NUMBER,
    metricKeys=(
        "quality_gate_details,"
        "new_coverage,"
        "new_duplicated_lines_density"
    ),
)

measures = {
    item["metric"]: item.get("value")
    for item in measures_response["component"]["measures"]
}

gate = json.loads(measures.get("quality_gate_details") or "{}")
gate_status = gate.get("level", "UNKNOWN")

# Issues introduced by this pull request.
pr_issues = sonar_api(
    "issues/search",
    componentKeys=PROJECT,
    pullRequest=PR_NUMBER,
    issueStatuses="OPEN,CONFIRMED",
    sinceLeakPeriod="true",
    s="SEVERITY",
    asc="false",
    ps=5,
)

# Existing unresolved issues on main.
main_issues = sonar_api(
    "issues/search",
    componentKeys=PROJECT,
    branch="main",
    issueStatuses="OPEN,CONFIRMED",
    s="SEVERITY",
    asc="false",
    ps=5,
)

pr_link = (
    f"{BASE_URL}/dashboard?"
    + urlencode({"id": PROJECT, "pullRequest": PR_NUMBER})
)

main_link = (
    f"{BASE_URL}/project/issues?"
    + urlencode({"id": PROJECT, "branch": "main"})
)

lines = [
    "## EventRelay — Code Quality Report",
    "",
    f"**Quality Gate:** {gate_status}",
    "",
    "| Metric | Result |",
    "|---|---:|",
    f"| New open issues | {pr_issues['total']} |",
    f"| Existing open issues on main | {main_issues['total']} |",
    f"| New-code coverage | {measures.get('new_coverage') or 'N/A'}% |"
    if measures.get("new_coverage") is not None
    else "| New-code coverage | N/A |",
    f"| New-code duplication | {measures.get('new_duplicated_lines_density') or 'N/A'}% |"
    if measures.get("new_duplicated_lines_density") is not None
    else "| New-code duplication | N/A |",
    "",
]

failed_conditions = [
    condition
    for condition in gate.get("conditions", [])
    if condition.get("level") == "ERROR"
]

if failed_conditions:
    lines.extend(["### Failed Quality Gate conditions", ""])
    for condition in failed_conditions:
        lines.append(
            f"- `{condition['metric']}`: "
            f"actual `{condition.get('actual', 'N/A')}`, "
            f"threshold `{condition.get('error', 'N/A')}`"
        )
    lines.append("")

lines.extend(["### Issues introduced by this PR", ""])

if pr_issues["issues"]:
    lines.extend(format_issue(issue) for issue in pr_issues["issues"])
else:
    lines.append("No new open issues detected.")

lines.extend(["", "### Existing issues on main", ""])

if main_issues["issues"]:
    lines.extend(format_issue(issue) for issue in main_issues["issues"])
else:
    lines.append("No existing open issues detected.")

lines.extend([
    "",
    f"[Full PR analysis]({pr_link}) "
    f"| [All existing issues]({main_link})",
    "",
    "_Showing up to five issues per section. "
    "Existing issues are informational and do not "
    "automatically fail this PR's Quality Gate._",
])

Path("sonar-pr-report.md").write_text(
    "\n".join(lines) + "\n",
    encoding="utf-8",
)
