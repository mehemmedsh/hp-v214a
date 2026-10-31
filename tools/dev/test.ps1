# Sessiz test: sadece hatalar ve ozet.
# Varsayilan: butun cozum (Windows). -Portable: sadece platformdan bagimsiz testler (Linux'ta da calisanlar).
# Kullanim: powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/test.ps1 [-Portable] [-Configuration Release]
param([switch]$Portable, [string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '../..')
try {
    $target = if ($Portable) { 'tests/MonitorTuner.Tests/MonitorTuner.Tests.csproj' } else { 'MonitorTuner.slnx' }
    dotnet test $target -c $Configuration -nologo -v q -tl:off
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $code
