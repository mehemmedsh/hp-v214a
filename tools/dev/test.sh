#!/usr/bin/env bash
# Sessiz test: sadece hatalar ve özet.
# Varsayılan: platformdan bağımsız testler (Linux'ta çalışır). --all: bütün çözüm (sadece Windows).
# Kullanım: tools/dev/test.sh [--all] [Debug|Release]
set -euo pipefail
cd "$(dirname "$0")/../.."
target="tests/MonitorTuner.Tests/MonitorTuner.Tests.csproj"
if [[ "${1:-}" == "--all" ]]; then target="MonitorTuner.slnx"; shift; fi
dotnet test "$target" -c "${1:-Debug}" -nologo -v q -tl:off
