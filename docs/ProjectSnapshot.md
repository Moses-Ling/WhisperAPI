# Whisper API Server — Project Snapshot

- Repo Root: `D:\VS\source\repos\Whisper.net`
- Solution: `Whisper.sln`
- API Project: `src/WhisperAPI` (net8.0)
- Tests: `tests/WhisperAPI.Tests`
- Defaults: `whisper-base`, timeout 10s, CORS allow-all, 4 concurrent requests

## Endpoints
- GET: `/health`, `/v1/config`, `/v1/models`, `/v1/models/{id}`
- POST: `/v1/audio/transcriptions` (multipart file: `file=@...`)
- POST: `/v1/audio/transcriptions/base64` (JSON: `{ "audio": "<base64>", "filename": "..." }`)
- POST: `/v1/audio/transcriptions/url` (JSON: `{ "url": "https://..." }`)

## Features Implemented
- Config precedence: defaults → `config.json` next to EXE → `--config <file>` → `WHISPER_` env → CLI flags (highest)
- Config compatibility: snake_case keys (e.g., `model_name`, `timeout_seconds`) mapped to PascalCase for binding
- Logging: Serilog console + file with 10MB rotation under `logs/whisper-server.log` (relative to EXE)
- Model management: Auto-download via Whisper.net on first use or `--download <model>`; normalized ids (tiny/base/small/medium/large-v3)
- Transcription: NAudio normalization to 16kHz mono WAV; Whisper.net processing; OpenAI-shaped JSON responses
- Concurrency: 4 concurrent requests, 10s queue wait → 429 on overflow
- Security: CORS allow-any host; no auth (MVP)

## How To Run (dev)
1) Publish (folder-based, recommended):
   ```
   dotnet publish src/WhisperAPI -c Release -r win-x64 -p:PublishSingleFile=false -o .\artifacts_fd
   ```
2) Start:
   ```
   cd .\artifacts_fd
   WhisperAPI.exe --host localhost --port 8000 --model whisper-base --timeout 120
   ```
3) Verify:
   ```
   curl http://localhost:8000/health
   curl http://localhost:8000/v1/config
   curl -F "file=@D:\\audio.wav" http://localhost:8000/v1/audio/transcriptions
   ```

## Config File
- Location: next to EXE (auto-loaded), e.g. `D:\VS\source\repos\Whisper.net\artifacts_fd\config.json`
- Legacy shape (supported):
  ```json
  {
    "server": { "host": "localhost", "port": 8000, "timeout_seconds": 10 },
    "whisper": { "model_name": "whisper-base", "language": "en", "temperature": 0.01, "chunk_length_seconds": 25 },
    "audio": { "sample_rate": 16000, "max_file_size_mb": 100, "auto_resample": false },
    "performance": { "device": "auto", "max_concurrent_requests": 4, "enable_gpu": true },
    "logging": { "level": "Information", "file_path": "logs/whisper-server.log" }
  }
  ```
- Note: CLI flags override file values. `/v1/config` shows effective config.

## Model Download / Provisioning
- Pre-download: `WhisperAPI.exe --download whisper-base`
- Auto on first transcription if missing
- Models dir: `<EXE dir>\models` (e.g., `...\artifacts_fd\models`)
- Offline option (future): add `--import-model <path>` to copy a local `.bin`

## Packaging Notes
- Single-file EXE can hang on some systems (bundle extraction). Prefer folder-based publish.
- If single-file is required, set `DOTNET_BUNDLE_EXTRACT_BASE_DIR=%CD%\.bundle` before running.

## Key Files
- `src/WhisperAPI/Program.cs`: config precedence, CLI flags, logging, DI, `--download`
- `src/WhisperAPI/Controllers/AudioController.cs`: file/base64/url endpoints
- `src/WhisperAPI/Controllers/ModelsController.cs`: OpenAI-shaped models endpoints
- `src/WhisperAPI/Controllers/ConfigController.cs`: `/config` and `/v1/config`
- `src/WhisperAPI/Services/ModelManager.cs`: normalization + auto-download + progress
- `src/WhisperAPI/Services/WhisperTranscriber.cs`: Whisper.net integration
- `src/WhisperAPI/Services/AudioValidationService.cs`: NAudio normalization
- `src/WhisperAPI/Services/StartupInitializer.cs`: GPU/CPU mode log
- `src/WhisperAPI/Filters/ConcurrencyLimiterFilter.cs` and `Services/ConcurrencyLimiter.cs`
- `README.txt`: usage, publish, endpoints
- `tests/WhisperAPI.Tests/BasicEndpointsTests.cs`: basic health/models/config tests

## Troubleshooting
- PowerShell curl: use `curl.exe` or CMD to ensure multipart works
- Long files: increase `--timeout` (e.g., 180)
- Logs: `<EXE dir>\logs\whisper-server.log`
- If config not applying: ensure `config.json` is next to the EXE you run and no overriding flags are passed; check `/v1/config`

## Packaging (current)
```powershell
dotnet publish src/WhisperAPI -c Release -r win-x64 -p:PublishSingleFile=false -o .\artifacts_fd
cd .\artifacts_fd; .\WhisperAPI.exe --download whisper-base
cd ..
Compress-Archive -Path .\artifacts_fd\* -DestinationPath .\WhisperServer.zip -Force
```
Result: `D:\VS\source\repos\Whisper.net\WhisperServer.zip`

## Next Steps
- Optional: `--import-model <path>` for offline provisioning
- Optional: `tools\pack.ps1` to automate publish → pre-download → zip
- Tests: add tiny WAV fixture for happy-path transcription
- Revisit single-file EXE stability

