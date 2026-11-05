#!/usr/bin/env bash
set -euo pipefail
# Pin from Microsoft's official .NET 10 release metadata. Never disable TLS or checksum verification.
sdk_version=10.0.403
sdk_root="${WINCC_DOTNET_ROOT:-/workspace/tools/dotnet}"
if [[ -x "$sdk_root/dotnet" ]] && [[ "$("$sdk_root/dotnet" --version)" == "$sdk_version" ]]; then
  printf 'Using .NET SDK %s\n' "$sdk_version"
  exit 0
fi
if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
  printf 'This helper supports Linux x64 only. Install the global.json SDK for your platform.\n' >&2
  exit 1
fi
download_dir=$(mktemp -d /tmp/wincc-sdk.XXXXXXXX)
trap 'rm -rf "$download_dir"' EXIT
curl --fail --show-error --silent --location --proto '=https' --tlsv1.2 \
  "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk_version/dotnet-sdk-$sdk_version-linux-x64.tar.gz" \
  -o "$download_dir/sdk.tar.gz"
sdk_hash=b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5
printf '%s  %s\n' "$sdk_hash" "$download_dir/sdk.tar.gz" | sha512sum --check --status
mkdir -p "$sdk_root"
tar -xzf "$download_dir/sdk.tar.gz" -C "$sdk_root"
"$sdk_root/dotnet" --version
