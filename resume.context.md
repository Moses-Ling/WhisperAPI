# Resume Context

## Summary
- Investigating runtime error: “Cannot dispose while processing, please use DisposeAsync instead.”
- Root cause: synchronous disposal of the async whisper processor.
- Fix staged: use `await using` for processor disposal in `WhisperTranscriber`.

## Environment
- Shell: PowerShell 5.1 (`Desktop`)
- .NET SDK: 9.0.304; Runtimes 6/8/9 available
- Solution: `Whisper.sln`
- Working dir: `D:\\VS\\source\\repos\\Whisper.net`

## Branch & Changes
- Branch: `fix/disposeasync-error`
- File changed: `src/WhisperAPI/Services/WhisperTranscriber.cs`
  - Replaced `using var processor = ...` with `await using var processor = ...`
  - Kept `WhisperFactory` disposal synchronous (`_factory?.Dispose()`), as it lacks `DisposeAsync`.

## Build & Tests
- Build: `dotnet build Whisper.sln -c Release` → Success
- Tests: `dotnet test Whisper.sln -c Release --no-build` → Passed 4/4

## How To Run
- Executable: `src/WhisperAPI/bin/Release/net8.0/WhisperAPI.exe`
- RID-specific: `src/WhisperAPI/bin/Release/net8.0/win-x64/WhisperAPI.exe`
- Example:
  - `& ".\\src\\WhisperAPI\\bin\\Release\\net8.0\\WhisperAPI.exe" --host localhost --port 8000 --model whisper-base`
  - With config: `& ".\\src\\WhisperAPI\\bin\\Release\\net8.0\\WhisperAPI.exe" --config .\\config.json`

## Next Steps
- Run the server and verify the disposal error no longer appears.
- If clean, commit on this branch and push for PR.
- Optional: scan code for any other `Dispose()` calls on async processors; convert to `await using`.

## Notes
- Contributor guide: `AGENTS.md` added at repo root.
- If model files are missing, use `--download <model>` or ensure they exist under `src/WhisperAPI/bin/Release/net8.0/models/`.
