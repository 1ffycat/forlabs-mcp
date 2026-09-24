# forlabs-mcp

An [MCP](https://modelcontextprotocol.io/) server for [Forlabs](https://bki.forlabs.ru) (the LMS used by ИГУ БКИ and others), letting AI agents (Claude Code, Codex, OpenClaw, etc.) read a student's schedule, homework, announcements, grades and teacher chat, and download assignment attachments.

Built by reverse-engineering a HAR capture of the Angular frontend's `lm-vendor/repositories/*` JSON API. It is **read-only**: submitting homework and taking tests were not present in the captured traffic, so those flows aren't implemented (see [Limitations](#limitations)).

## Build

```
dotnet build src/ForlabsMcp
```

Requires the .NET 10 SDK (pinned in `global.json`, `rollForward: latestMinor` so any 10.x SDK works). Or skip installing a toolchain entirely and grab a [prebuilt release](#prebuilt-releases) or use the [Nix flake](#nix--nixos).

## Prebuilt releases

Tagging a release (`vX.Y.Z`) triggers a GitHub Actions workflow that builds single-file binaries for `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`, and publishes them (with a `SHA256SUMS` file) to the repo's [Releases](../../releases) page — two variants per platform:

- **`-standalone`** — self-contained, bundles its own .NET runtime. Larger download (~35-40 MB), zero prerequisites. Pick this unless you have a reason not to.
- **`-runtime-dependent`** — much smaller (~5 MB), but needs the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) already installed. Worth it if you already have .NET 10 around (recent, up-to-date Windows 11 installs increasingly ship it, and it's a one-line install everywhere else) or are running several .NET tools and don't want N copies of the runtime.

Each is uploaded as the raw binary — no `.zip`/`.tar.gz` — so it's ready to run as soon as it's downloaded. On Linux/macOS, mark it executable first: `chmod +x forlabs-mcp-*`.

On NixOS, prefer the [flake](#nix--nixos) instead — it stays reproducible and lets you pin/update declaratively. Everywhere else, there's no auto-update: grab a new release manually when you want one.

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
- `forlabs_list_subjects` — every subject Forlabs has on record for the group, current and past semesters alike, each tagged with its `study_id` and a `status` (current-semester subjects are `2`; finished ones are `3`).

**Schedule**
- `forlabs_get_schedule_for_date` — one day's classes (defaults to today). Good for a morning briefing.
- `forlabs_get_schedule_for_week` — 7-day schedule.
- `forlabs_get_raw_two_week_schedule` — the raw, unresolved rotating 2-week grid.

**Homework**
- `forlabs_get_homework` — task list for a subject (title, points, deadline, status). `include_completed` toggles between "what's pending" and "what's already turned in".
- `forlabs_get_homework_details` — full task content + attachment URLs + linked `assignment_id`.
- `forlabs_get_upcoming_homework` — scans every **current-semester** subject for deadlines in the next N days. This is the one to use for a "what's due soon" agent briefing; see `forlabs_get_recent_activity` below for the "what did I already do" complement.
- `forlabs_download_task_file` — downloads an attachment URL to local disk so a coding agent can open/use it directly.
- `forlabs_get_course_materials` — chapter/material index for a subject.

**Communication**
- `forlabs_get_announcements` — per-subject posts/announcements.
- `forlabs_get_task_chat` — per-assignment chat thread with the teacher (read-only).

**Progress**
- `forlabs_get_scores_summary` — credits/grade overview across all subjects.
- `forlabs_get_recent_activity` — scans every current-semester subject's grading log for entries from the last N days. The "what did I do [recently]" counterpart to `forlabs_get_upcoming_homework`'s "what's due".
- `forlabs_get_scoring_log`, `forlabs_get_attendance`, `forlabs_get_exams` — per-subject detail.

## Limitations

- **Read-only.** No homework submission, no chat replies, no test-taking — none of these appeared in the recorded HAR.
- **Course material content** (`forlabs_get_course_materials`) only returns the chapter index (titles, `has_content`, block counts); the endpoint that returns a chapter's actual content blocks was never called during the capture, so it isn't implemented.
- **Task status codes.** The task's own `pivot_status` field turned out to be unrelated to submission/grading state (a live capture showed a graded pass and a never-submitted overdue task sharing the same value), so it isn't used for `status`/`status_hint` anymore. Those are now derived from the `assignments` array returned alongside `tasks` by the same endpoint (matched by `task_id`): assignment `status` 1 = no response yet ("В очереди"/"Долг" depending on whether the deadline has passed), 2 = submitted and awaiting review, 3 = graded, 6 = response received but not numerically graded (e.g. absence-excuse tasks). Still not documented by Forlabs, but confirmed against a live account's task list, screenshot, and per-task detail responses.
- **"Current semester" filtering** (`forlabs_get_upcoming_homework`, `forlabs_get_recent_activity`) is inferred from a study's `status` field (`2` = current, `3` = finished) observed on one live account; it isn't documented by Forlabs either, so it's a best-effort heuristic, not a guarantee — though a much more reliable one than trying to guess "current" from task deadlines (see below).
- **Two-week schedule resolution.** Forlabs encodes its rotating schedule as a single `day` index 1-14 (1-7 and 8-14 = two alternating week variants). `sched/get_grid`'s `upperweek` field looks like it should say which half is "this week", but empirically it does not track that (verified live against a known-correct date/class list — see `ScheduleMath`'s doc comment); `forlabs_get_schedule_for_date`/`_for_week` instead anchor day-range 1-7 to the calendar week containing "today" as of the call and alternate by parity for other weeks. This matches everything checked against a live account so far, but the underlying rotation logic is still inferred, not documented.
- File attachment URLs point at a separate CDN (`matecdn.ru`) that appeared to require no auth in the capture.
