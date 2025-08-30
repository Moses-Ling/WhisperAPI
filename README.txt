Whisper API Server (Windows) - Portable Package
================================================

Overview
- Windows-native, OpenAI-compatible Whisper speech-to-text API server.
- Portable: run a single EXE; optional config file next to it; models auto-download or can be preloaded.
- Default model: whisper-base.

Directory Layout
- WhisperAPI.exe              # main executable
- config.json (optional)      # configuration next to the EXE, auto-loaded
- models\                     # ggml model files (auto-downloaded or preloaded)
- logs\                       # runtime logs with 10MB rotation

Config & Overrides
1) The server auto-loads config.json next to the EXE if present.
2) You can specify a different file with --config <path>.
3) Environment variables (prefix WHISPER_) override files.
4) CLI flags override everything (highest priority).

Common CLI Flags
- --host <name>                 (default: localhost)
- --port <number>               (default: 8000)
- --model <id>                  (default: whisper-base)
- --language <code>             (default: en)
- --timeout <seconds>           (default: 10)
- --download <model>            (download a model and exit)

Supported Model IDs
- whisper-tiny, whisper-base, whisper-small, whisper-medium, whisper-large-v3
- English-only variants: add .en (e.g., whisper-base.en) when supported upstream

Run Examples
1) Start the server (default model whisper-base):
   .\WhisperAPI.exe --host localhost --port 8000

2) Start with a different model and larger timeout:
   .\WhisperAPI.exe --model whisper-large-v3 --timeout 180

3) Pre-download a model without starting the server:
   .\WhisperAPI.exe --download whisper-base

Endpoints
- GET  /health
- GET  /v1/config
- GET  /v1/models
- GET  /v1/models/{id}
- POST /v1/audio/transcriptions              (multipart/form-data; file=<audio>)
- POST /v1/audio/transcriptions/base64       (JSON; { "audio": "<base64>", "filename":"clip.wav" })
- POST /v1/audio/transcriptions/url          (JSON; { "url": "https://.../clip.wav" })

Example Requests
1) Multipart file upload:
   curl -F "file=@D:\\audio.wav" http://localhost:8000/v1/audio/transcriptions

2) Base64 JSON:
   # PowerShell: $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes('D:\\audio.wav'))
   curl -H "Content-Type: application/json" -d "{\"audio\":\"$b64\",\"filename\":\"audio.wav\"}" http://localhost:8000/v1/audio/transcriptions/base64

3) URL JSON:
   curl -H "Content-Type: application/json" -d "{\"url\":\"https://your.host/audio.wav\"}" http://localhost:8000/v1/audio/transcriptions/url

Packaging via script
- From repo root, run:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\pack.ps1 -Model whisper-base
- Output: .\WhisperServer.zip (includes published EXE, config.json, and attempts to pre-download the model)
- If model pre-download fails due to network, run locally after unzip: .\WhisperAPI.exe --download whisper-base
 - To skip the model download during packaging: add -SkipDownload
   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\pack.ps1 -Model whisper-base -SkipDownload

One-click packaging
- Double-click `pack.cmd` in the repo root, or run:
  pack.cmd -Model whisper-base
  (Arguments are forwarded to `tools\pack.ps1`)
 - Example to skip pre-download: pack.cmd -Model whisper-base -SkipDownload

Publishing a Portable ZIP (build step for maintainers)
1) Publish a single-file EXE (self-contained):
   dotnet publish src/WhisperAPI -c Release -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts

   # or framework-dependent (requires .NET 8 runtime):
   dotnet publish src/WhisperAPI -c Release -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=false -o .\artifacts

2) Preload the base model into artifacts\models:
   .\artifacts\WhisperAPI.exe --download whisper-base

3) Add an optional .\artifacts\config.json (if desired).

4) Zip the contents of .\artifacts (not the folder itself) into WhisperServer.zip.

Notes
- Models are large (100MB–1.5GB). They are not embedded in the EXE by design.
- Logs rotate at 10MB. Inspect logs\whisper-server.log for troubleshooting.
- For production install scenarios, consider using ProgramData for config/models/logs.
