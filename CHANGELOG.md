# Changelog

## v1.0.0 — Whisper API Server MVP

- OpenAI-compatible endpoints: `/v1/models`, `/v1/audio/transcriptions` (+ base64, URL), `/v1/config`, `/health`.
- Config precedence: defaults → config.json next to EXE → `--config` → `WHISPER_` env → CLI flags (highest).
- Whisper models: automatic download or `--download <model>`; default model `whisper-base`.
- Audio pipeline: accepts wav/mp3/m4a/flac/ogg; normalizes to 16kHz mono; OpenAI-shaped JSON responses.
- Concurrency: 4 concurrent requests; 10s queue wait → 429.
- Logging: Serilog console + 10MB-rotating file under `logs/`.
- Packaging: documented folder-based ZIP publish; guidance for single-file EXE.

Notes
- Single-file EXE can hang in some environments; prefer folder-based ZIP for MVP.
- Valid model ids: `whisper-tiny`, `whisper-base`, `whisper-small`, `whisper-medium`, `whisper-large-v3`.

