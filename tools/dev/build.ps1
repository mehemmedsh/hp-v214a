# Sessiz derleme: sadece hatalar ve ozet.
# Kullanim: powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/build.ps1 [-Configuration Release]
param([string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '../..')
try {
    dotnet build MonitorTuner.slnx -c $Configuration -nologo -v q -tl:off '-clp:ErrorsOnly;Summary'
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $code
