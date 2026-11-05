#!/usr/bin/env bash
set -euo pipefail
# Cloud-only helper; Windows developers use dotnet directly.
export DOTNET_ROOT="${WINCC_DOTNET_ROOT:-/workspace/tools/dotnet}"
export DOTNET_CLI_HOME="${WINCC_CLI_HOME:-/workspace/.wincc-dotnet-home}"
export NUGET_PACKAGES="${WINCC_NUGET_PACKAGES:-/workspace/.wincc-nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
exec "$DOTNET_ROOT/dotnet" "$@"
