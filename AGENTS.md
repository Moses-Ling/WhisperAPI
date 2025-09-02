# Repository Guidelines

## Project Structure & Module Organization
- Source: `src/Whisper` (C# library).
- Tests: `tests/Whisper.Tests` mirroring source namespaces/folders.
- Test assets: `tests/Whisper.Tests/Assets` (small, checked-in files only).
- Samples/tools (if present): `samples/`, `tools/`.
- Solution: `Whisper.sln` at repo root orchestrates all projects.

## Build, Test, and Development Commands
- Build: `dotnet build Whisper.sln -c Release` — compile all projects.
- Test: `dotnet test Whisper.sln -c Release` — run unit tests with diagnostics.
- Format: `dotnet format` — apply `.editorconfig` and fix style issues.
- Pack: `dotnet pack src/Whisper/Whisper.csproj -c Release -o ./artifacts` — produce NuGet.
- Run sample: `dotnet run --project samples/Whisper.Sample` (if present).
- Windows MSBuild: `"C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe" Whisper.sln /p:Configuration=Release`.

## Coding Style & Naming Conventions
- Language: C#; indent 4 spaces; UTF‑8; `nullable` enabled.
- Naming: Types/methods/properties PascalCase; locals/params camelCase; private fields `_camelCase`.
- Structure: Files mirror namespaces (e.g., `Transcriber.cs` in `Whisper.Transcription`).
- APIs: XML docs; prefer `readonly` and `sealed` where appropriate.
- Analyzers: fix warnings before PR; run `dotnet format` prior to push.

## Testing Guidelines
- Framework: xUnit; tests mirror source layout.
- Naming: `MethodName_State_ExpectedBehavior` (e.g., `Transcribe_EmptyInput_Throws`).
- Coverage: aim ≥80% for core libraries. Example: `dotnet test Whisper.sln -c Release /p:CollectCoverage=true`.
- Keep tests deterministic; keep fixtures tiny; assets live under `tests/*/Assets`.

## Commit & Pull Request Guidelines
- Commits: clear, present tense; prefer Conventional Commits (e.g., `feat: add streaming API`).
- PRs: include purpose, key changes, linked issues; add screenshots/CLI output when relevant.
- CI: PRs must build, test, and pass analyzers; rebase onto `main` before merge.

## Security & Configuration Tips
- Do not commit secrets or large model files; use environment variables and `.gitignore`.
- Validate untrusted input; pin native dependency versions; document runtime requirements in `README`.

## Agent‑Specific Instructions
- After changes, restart any running samples before testing.
- Prefer minimal, focused changes; avoid duplication; use existing utilities.
- Consider dev/test/prod behavior; use per-request processors and async disposal.
- Add thorough tests for major functionality; avoid files >300 lines.
- Never mock/stub data outside tests. Never overwrite `.env` without confirmation.

