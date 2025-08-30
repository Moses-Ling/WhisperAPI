# Repository Guidelines

## Project Structure & Module Organization
- Source code: `src/<ProjectName>` per library/app. Example: `src/Whisper`.
- Tests: `tests/<ProjectName>.Tests` (mirrors source namespaces and folders).
- Samples/tools (if present): `samples/` or `tools/` with their own projects.
- Assets and test data: `tests/Whisper.Tests/Assets` (small, checked-in files only).
- Solution file: `Whisper.sln` at repo root to orchestrate all projects.

## Build, Test, and Development Commands
- Build: `dotnet build Whisper.sln -c Release` — compile all projects.
- Test: `dotnet test Whisper.sln -c Release` — run unit tests with diagnostics.
- Format: `dotnet format` — apply editorconfig and fix style issues.
- Pack (libraries): `dotnet pack src/Whisper/Whisper.csproj -c Release -o ./artifacts`.
- Run sample app: `dotnet run --project samples/Whisper.Sample` (if present).
- Windows MSBuild: `"C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe" Whisper.sln /p:Configuration=Release`.

## Coding Style & Naming Conventions
- Language: C#; indent 4 spaces; UTF-8; `nullable enable` in projects.
- Types/methods/properties: PascalCase. Locals/params: camelCase. Private fields: `_camelCase`.
- Files mirror namespaces and class names (`Transcriber.cs` in `Whisper.Transcription`).
- Public APIs are documented with XML docs; prefer `readonly`, `sealed` where appropriate.
- Use analyzers; fix warnings before PR. Run `dotnet format` prior to push.

## Testing Guidelines
- Framework: xUnit (typical). Test files mirror source (e.g., `TranscriberTests.cs`).
- Naming: `MethodName_State_ExpectedBehavior` (e.g., `Transcribe_EmptyInput_Throws`).
- Coverage: aim ≥80% lines on core libraries; measure via `dotnet test /p:CollectCoverage=true`.
- Keep tests deterministic; store tiny fixtures under `tests/*/Assets`.

## Commit & Pull Request Guidelines
- Commits: clear, present tense. Prefer Conventional Commits (e.g., `feat: add streaming API`).
- PRs: include purpose, key changes, and linked issues. Add screenshots/CLI output when relevant.
- CI: PRs must build, test, and pass analyzers. Rebase onto main before merge.
- Scope: keep PRs focused; follow existing project/namespace layout.

## Security & Configuration
- Do not commit secrets or large model files. Use environment variables and `.gitignore`.
- Validate untrusted input; avoid loading arbitrary paths by default.
- If native dependencies are used, pin versions and document runtime requirements in `README`.

## Coding Standard 
- After making changes, ALWAYS make sure to start up a new server so I can test it.
- Always look for existing code to iterate on instead of creating new code.
- Do not drastically change the patterns before trying to iterate on existing patterns.
- Always kill all existing related servers that may have been created in previous testing before trying to start a new server.
- Always prefer simple solutions
- Avoid duplication of code whenever possible, which means checking for other areas of the codebase that might already have similar code and functionality
- Write code that takes into account the different environments: dev, test, and prod
- You are careful to only make changes that are requested or you are confident are well understood and related to the change being requested
- When fixing an issue or bug, do not introduce a new pattern or technology without first exhausting all options for the existing implementation. And if you finally do this, make sure to remove the old implementation afterwards so we don't have duplicate logic.
- Keep the codebase very clean and organized
- Avoid writing scripts in files if possible, especially if the script is likely only to be run once
- Avoid having files over 200-300 lines of code. Refactor at that point.
- Mocking data is only needed for tests, never mock data for dev or prod
- Never add stubbing or fake data patterns to code that affects the dev or prod environments
- Never overwrite my .env file without first asking and confirming
- Focus on the areas of code relevant to the task
- Do not touch code that is unrelated to the task
- Write thorough tests for all major functionality
- Avoid making major changes to the patterns and architecture of how a feature works, after it has shown to work well, unless explicitly instructed
- Always think about what other methods and areas of code might be affected by code changes