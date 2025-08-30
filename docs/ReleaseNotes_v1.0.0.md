# v1.0.0 — Whisper API Server MVP

## Highlights
- OpenAI-compatible Whisper endpoints (Windows-native):
  - GET `/v1/models`, GET `/v1/models/{id}`
  - POST `/v1/audio/transcriptions` (multipart), `/base64`, `/url`
  - GET `/v1/config`, GET `/health`
- Default model `whisper-base` with automatic model download; `--download <model>` for prefetch.
- Audio normalization to 16kHz mono WAV; OpenAI-shaped JSON response (text, segments, duration, language).
- Concurrency (4) + 10s queue timeout (429 on overflow), structured logging with 10MB rotation.

## Install (Portable ZIP)
1) Download the release ZIP (WhisperServer.zip) from Assets.
2) Unzip anywhere (no admin required).
3) Optional: edit `config.json` next to the EXE or pass CLI flags.
4) Run: `WhisperAPI.exe --host localhost --port 8000`.

## Configuration
- Precedence: defaults → `config.json` next to EXE → `--config` → `WHISPER_` env → CLI flags (highest).
- Snake_case keys in config are supported (e.g., `model_name`, `timeout_seconds`).

## Models
- Valid IDs: `whisper-tiny`, `whisper-base`, `whisper-small`, `whisper-medium`, `whisper-large-v3`.
- Pre-download: `WhisperAPI.exe --download whisper-base`.
- Models stored under `<EXE dir>\models`.

## Known Notes
- Single-file EXE can hang on some systems due to bundle extraction. Prefer folder-based ZIP. If needed, set `DOTNET_BUNDLE_EXTRACT_BASE_DIR=%CD%\.bundle`.

## Verify
- Health: `curl http://localhost:8000/health`
- Config: `curl http://localhost:8000/v1/config`
- Transcribe (CMD): `curl -F "file=@D:\\audio.wav" http://localhost:8000/v1/audio/transcriptions`

