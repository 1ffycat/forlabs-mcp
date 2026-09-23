# forlabs-mcp

An [MCP](https://modelcontextprotocol.io/) server for [Forlabs](https://bki.forlabs.ru) (the LMS used by ИГУ БКИ and others), letting AI agents (Claude Code, Codex, OpenClaw, etc.) read a student's schedule, homework, announcements, grades and teacher chat, and download assignment attachments.

Built by reverse-engineering a HAR capture of the Angular frontend's `lm-vendor/repositories/*` JSON API. It is **read-only**: submitting homework and taking tests were not present in the captured traffic, so those flows aren't implemented (see [Limitations](#limitations)).

## Build

```
dotnet build src/ForlabsMcp
```

Requires the .NET 10 SDK (pinned in `global.json`, `rollForward: latestMinor` so any 10.x SDK works). Or skip installing a toolchain entirely and use the [Nix flake](#nix--nixos).

## Configure

The server authenticates as a normal Forlabs user (email/login + password — the same Laravel session/CSRF flow the web app uses). Credentials are read from environment variables, never from a file in this repo:

| Variable | Required | Default |
|---|---|---|
| `FORLABS_USERNAME` | yes | — |
| `FORLABS_PASSWORD` | yes | — |
| `FORLABS_BASE_URL` | no | `https://bki.forlabs.ru` |
| `FORLABS_DOWNLOAD_DIR` | no | `%TEMP%/forlabs-mcp/downloads` |

## Nix / NixOS

A flake in this repo packages the server with `buildDotnetModule`, fully offline/reproducible (NuGet deps are locked in `nix/deps.json`).

```
nix build .#forlabs-mcp          # -> ./result/bin/ForlabsMcp
nix run .#forlabs-mcp             # build + run directly
```

Try it end to end (reads FORLABS_USERNAME/PASSWORD from the environment):

```
FORLABS_USERNAME=you@example.com FORLABS_PASSWORD=yourpass nix run github:1ffycat/forlabs-mcp
```

### Importing into another flake (e.g. nix-openclaw)

```nix
{
  inputs.forlabs-mcp.url = "github:1ffycat/forlabs-mcp"; # or "path:/abs/path" for local dev

  outputs = { self, nixpkgs, forlabs-mcp, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs {
        inherit system;
        overlays = [ forlabs-mcp.overlays.default ]; # exposes pkgs.forlabs-mcp
      };
    in {
      # use pkgs.forlabs-mcp, or forlabs-mcp.packages.${system}.default directly
    };
}
```

Either the overlay (`pkgs.forlabs-mcp`) or the direct package output (`forlabs-mcp.packages.${system}.default`) works — both point at the same derivation; `mainProgram` is set so `lib.getExe pkgs.forlabs-mcp` / `"${pkgs.forlabs-mcp}/bin/ForlabsMcp"` both resolve.

### Regenerating `nix/deps.json`

Needed whenever a `PackageReference` changes:

```
nix build .#forlabs-mcp.fetch-deps --no-link --print-out-paths
$(nix build .#forlabs-mcp.fetch-deps --no-link --print-out-paths) ./nix/deps.json
```

Validated on NixOS-WSL (`nix build`, `nix flake check`, and a live stdio JSON-RPC round trip against the built binary all pass) as part of building this out.

## Adding to Claude Code

Easiest is the CLI, from this repo's directory:

```
claude mcp add forlabs -s user \
  -e FORLABS_USERNAME=you@example.com \
  -e FORLABS_PASSWORD=your-password \
  -- dotnet run --project src/ForlabsMcp -c Release
```

`-s user` registers it globally (available in every project); use `-s project` instead to write it into this repo's `.mcp.json` and share it with collaborators (don't commit real credentials in that case — use `-s local` or a wrapper script that reads a `.env`).

Faster startup — publish once and point at the binary directly instead of `dotnet run`:

```
dotnet publish src/ForlabsMcp -c Release -o publish
claude mcp add forlabs -s user -e FORLABS_USERNAME=... -e FORLABS_PASSWORD=... -- R:/forlabs-mcp/publish/ForlabsMcp.exe
```

Or, via Nix (no .NET SDK needed at all):

```
nix build .#forlabs-mcp
claude mcp add forlabs -s user -e FORLABS_USERNAME=... -e FORLABS_PASSWORD=... -- /nix/store/.../bin/ForlabsMcp
```

Check it's connected with `claude mcp list` / `claude mcp get forlabs`, or `/mcp` inside a Claude Code session.

Equivalent manual config, if you prefer editing JSON directly (`~/.claude.json` for user scope, or `.mcp.json` for project scope):

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
