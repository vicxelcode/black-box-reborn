#!/usr/bin/env bash
# Compila en Release (sin DevTools) y construye el zip de Thunderstore en dist/ con tcli, a partir de
# thunderstore.toml. La versión va en thunderstore.toml y en Plugin.Version: se comprueba que coinciden.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
tcli="${TCLI:-$HOME/.dotnet/tools/tcli.exe}"
version=$(sed -n 's/^versionNumber = "\(.*\)"/\1/p' "$here/thunderstore.toml")
grep -q "public const string Version = \"$version\"" "$here/src/Plugin.cs" \
  || { echo "La versión de thunderstore.toml ($version) no coincide con Plugin.Version"; exit 1; }

dotnet build "$here/BlackBoxReborn.csproj" -c Release -nologo -v q
rm -f "$here/dist/"*.zip
(cd "$here" && "$tcli" build --config-path thunderstore.toml >/dev/null)
echo "$here/dist/vicxelcode-BlackBoxReborn-$version.zip"
