# forlabs-mcp

An [MCP](https://modelcontextprotocol.io/) server for [Forlabs](https://bki.forlabs.ru) (the LMS used by ИГУ БКИ and others), letting AI agents (Claude Code, Codex, OpenClaw, etc.) read a student's schedule, homework, announcements, grades and teacher chat, and download assignment attachments.

Built by reverse-engineering a HAR capture of the Angular frontend's `lm-vendor/repositories/*` JSON API. It is **read-only**: submitting homework and taking tests were not present in the captured traffic, so those flows aren't implemented (see [Limitations](#limitations)).

## Build

```
dotnet build src/ForlabsMcp
```

Requires .NET 8 SDK.

## Configure

The server authenticates as a normal Forlabs user (email/login + password — the same Laravel session/CSRF flow the web app uses). Credentials are read from environment variables, never from a file in this repo:

| Variable | Required | Default |
|---|---|---|
| `FORLABS_USERNAME` | yes | — |
| `FORLABS_PASSWORD` | yes | — |
| `FORLABS_BASE_URL` | no | `https://bki.forlabs.ru` |
| `FORLABS_DOWNLOAD_DIR` | no | `%TEMP%/forlabs-mcp/downloads` |

### Claude Code / Claude Desktop / Codex — `mcp.json`

```json
{
  "mcpServers": {
    "forlabs": {
      "command": "dotnet",
      "args": ["run", "--project", "R:/forlabs-mcp/src/ForlabsMcp", "-c", "Release"],
      "env": {
        "FORLABS_USERNAME": "you@example.com",
        "FORLABS_PASSWORD": "your-password"
      }
    }
  }
}
```

For faster startup, publish once and point `command` at the built exe instead of `dotnet run`:

```
dotnet publish src/ForlabsMcp -c Release -o publish
```

```json
"command": "R:/forlabs-mcp/publish/ForlabsMcp.exe"
```

## Tools

Login and CSRF/session handling is automatic and transparent (lazy on first call, auto re-login on session expiry). Most tools accept a `subject` parameter that matches a subject by **name** (substring, e.g. `"Философия"`) or numeric `study_id` — call `forlabs_list_subjects` once to see what's available, you rarely need to pass `stream_id` at all since it defaults to the logged-in student's own group.

**Discovery**
- `forlabs_whoami` — student profile + own group.
- `forlabs_list_subjects` — subjects for the current semester with their `study_id`.

**Schedule**
- `forlabs_get_schedule_for_date` — one day's classes (defaults to today). Good for a morning briefing.
- `forlabs_get_schedule_for_week` — 7-day schedule.
- `forlabs_get_raw_two_week_schedule` — the raw, unresolved rotating 2-week grid.

**Homework**
- `forlabs_get_homework` — task list for a subject (title, points, deadline, status).
- `forlabs_get_homework_details` — full task content + attachment URLs + linked `assignment_id`.
- `forlabs_get_upcoming_homework` — scans **every** subject for deadlines in the next N days. This is the one to use for an "what's due soon" agent briefing.
- `forlabs_download_task_file` — downloads an attachment URL to local disk so a coding agent can open/use it directly.
- `forlabs_get_course_materials` — chapter/material index for a subject.

**Communication**
- `forlabs_get_announcements` — per-subject posts/announcements.
- `forlabs_get_task_chat` — per-assignment chat thread with the teacher (read-only).

**Progress**
- `forlabs_get_scores_summary` — credits/grade overview across all subjects.
- `forlabs_get_scoring_log`, `forlabs_get_attendance`, `forlabs_get_exams` — per-subject detail.

## Limitations

- **Read-only.** No homework submission, no chat replies, no test-taking — none of these appeared in the recorded HAR.
- **Course material content** (`forlabs_get_course_materials`) only returns the chapter index (titles, `has_content`, block counts); the endpoint that returns a chapter's actual content blocks was never called during the capture, so it isn't implemented.
- **Task status codes** (`pivot_status` 1/2/3) aren't documented anywhere in the API; the `status_hint` field is a best-effort guess from observed data, not a guarantee.
- **Upper/lower week resolution.** Forlabs encodes its 2-week rotating schedule as a single `day` index 1-14 (1-7 and 8-14 = the two week variants) and tells you, live, which half is "this week" via `sched/get_grid`'s `upperweek` field. `forlabs_get_schedule_for_date/_for_week` derive any other date's half from the ISO-week parity distance to today — this was validated against the captured HAR (the grid's `upperweek: 2` on a Monday capture matched exactly the `day: 8-11` entries observed for that week and `day: 2/4/5` for the adjacent week), but hasn't been checked against a live account since the HAR is a single snapshot.
- File attachment URLs point at a separate CDN (`matecdn.ru`) that appeared to require no auth in the capture.
