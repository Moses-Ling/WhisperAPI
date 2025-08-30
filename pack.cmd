@echo off
REM Convenience wrapper to package Whisper API Server
REM Usage: double-click or run with optional args (passed to pack.ps1)
REM Example: pack.cmd -Model whisper-base -ForceConfig

setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\pack.ps1" %*
endlocal

