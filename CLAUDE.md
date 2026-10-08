# CLAUDE.md

MCP server (C#/.NET 10, stdio transport) exposing a read-only view of the Forlabs LMS (`bki.forlabs.ru`): schedule, homework, grades, announcements, teacher chat, attachments. The API was reverse-engineered from a HAR capture of the Angular frontend's `lm-vendor/repositories/*` endpoints — there is no official documentation, so field meanings are inferred (and noted in doc comments where verified against a live account).

## Commands

```
dotnet build src/ForlabsMcp -c Release     # what CI runs; no test project exists
dotnet publish src/ForlabsMcp -c Release -o publish
nix flake check                            # CI also runs this
```

Runtime needs `FORLABS_USERNAME` / `FORLABS_PASSWORD` (optional: `FORLABS_BASE_URL`, `FORLABS_DOWNLOAD_DIR`). Never commit credentials.

## Architecture (src/ForlabsMcp)

- `ForlabsClient` — HTTP + Laravel session/CSRF login, lazy on first call, auto re-login on expiry.
- `ForlabsApi` — thin 1:1 wrappers over each endpoint, returning raw `JsonNode`. Add new endpoints here.
- `ForlabsContext` — resolves own `stream_id` and maps a subject name (substring) or `study_id` to a study; caches studies.
- `Tools/*.cs` — `[McpServerTool]` classes that shape raw JSON into model-friendly output. Registered in `Program.cs`.
- `JsonUtil` — `Pretty`, `ArrayOf`, and `Str/Int/Dbl` helpers on `JsonObject`; `ScheduleMath` — rotating 2-week schedule logic.

Adding a tool: endpoint wrapper in `ForlabsApi` → tool method in the relevant `Tools/*.cs` (reuse `ctx.ResolveOwnStreamIdAsync` / `ctx.ResolveStudyAsync`) → list it in the README "Tools" section.

## Conventions and gotchas

- Strictly read-only. Don't add submission/test-taking/chat-reply flows without a HAR capture of them.
- Undocumented enums (assignment `status`, study `status` 2=current/3=finished, `pivot_status` unrelated to grading) are documented in the README "Limitations" and in doc comments — keep both updated when you learn more.
- Schedule `upperweek` is unreliable; week parity is anchored to the calendar week of "today" (see `ScheduleMath`).
- Course program: `get_course_materials` = chapter index, `get_course_chapter` = chapter + blocks. Test questions are not exposed (endpoints never captured).
- Nix: changing a `PackageReference` requires regenerating `nix/deps.json` (see README).
- Releases: tag `vX.Y.Z` → GitHub Actions builds raw single-file binaries (standalone + runtime-dependent) for 5 platforms.
- README is partly in English; user-facing Forlabs strings are Russian — don't translate them.
