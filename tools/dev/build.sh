#!/usr/bin/env bash
# Sessiz derleme: sadece hatalar ve özet. Kullanım: tools/dev/build.sh [Debug|Release]
set -euo pipefail
cd "$(dirname "$0")/../.."
dotnet build MonitorTuner.slnx -c "${1:-Debug}" -nologo -v q -tl:off -clp:"ErrorsOnly;Summary"
