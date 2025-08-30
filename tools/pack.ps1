# Whisper API Server packaging helper
#
# Usage (from repo root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\pack.ps1 -Model whisper-base
#
# Params:
#   -Model        Model id to pre-download (whisper-base | whisper-small | whisper-medium | whisper-large-v3)
#   -ForceConfig  Overwrite existing config.json in output folder

Param(
    [string]$Model = "whisper-base",
    [switch]$ForceConfig
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host "[pack] $msg" }
function Write-Err($msg)  { Write-Host "[pack][ERR] $msg" -ForegroundColor Red }

$root = Split-Path -Parent $PSScriptRoot
$srcProject = Join-Path $root 'src/WhisperAPI'
$outDir = Join-Path $root 'artifacts_fd'
$zipPath = Join-Path $root 'WhisperServer.zip'

Write-Info "Repo root: $root"
Write-Info "Output dir: $outDir"

if (Test-Path $zipPath) {
  Write-Info "Removing old ZIP: $zipPath"
  Remove-Item $zipPath -Force
}

Write-Info "Publishing (folder-based)..."
dotnet publish $srcProject -c Release -r win-x64 -p:PublishSingleFile=false -o $outDir | Out-Host

$exe = Join-Path $outDir 'WhisperAPI.exe'
if (!(Test-Path $exe)) { Write-Err "Publish output missing: $exe"; exit 1 }

$cfgPath = Join-Path $outDir 'config.json'
$cfgObj = [ordered]@{
  server      = @{ host='localhost'; port=8000; timeout_seconds=10 }
  whisper     = @{ model_name=$Model; language='en'; temperature=0.01; chunk_length_seconds=25 }
  audio       = @{ sample_rate=16000; max_file_size_mb=100; auto_resample=$false }
  performance = @{ device='auto'; max_concurrent_requests=4; enable_gpu=$true }
  logging     = @{ level='Information'; file_path='logs/whisper-server.log' }
}
$cfgJson = $cfgObj | ConvertTo-Json -Depth 5
if ($ForceConfig -or -not (Test-Path $cfgPath)) {
  Write-Info "Writing config.json (model=$Model)"
  Set-Content -Path $cfgPath -Value $cfgJson -Encoding UTF8
} else {
  Write-Info "Keeping existing config.json"
}

Write-Info "Pre-downloading model: $Model"
Push-Location $outDir
& $exe --download $Model
$code = $LASTEXITCODE
Pop-Location
if ($code -ne 0) {
  Write-Err "Model pre-download failed with exit code $code"
  Write-Host "You can run manually: `"$exe --download $Model`""
}

Write-Info "Creating ZIP: $zipPath"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -Force

Write-Info "Done. ZIP at: $zipPath"
Write-Output $zipPath
