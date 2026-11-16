#!/usr/bin/env bash
# Construye el paquete y lo sube a Thunderstore (comunidad R.E.P.O., equipo vicxelcode).
# Token: cuenta de servicio del equipo, en ../../.keys/thunderstore.env como TCLI_AUTH_TOKEN=...
# El token nunca se imprime: se carga en una variable y se pasa directo a tcli.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
keys="${BBR_KEYS:-$here/../../.keys/thunderstore.env}"
tcli="${TCLI:-$HOME/.dotnet/tools/tcli.exe}"

[ -f "$keys" ] || { echo "Falta $keys (TCLI_AUTH_TOKEN=... de la cuenta de servicio del equipo)"; exit 1; }
set -a; . "$keys"; set +a
[ -n "${TCLI_AUTH_TOKEN:-}" ] || { echo "$keys no define TCLI_AUTH_TOKEN"; exit 1; }

zip_path=$(bash "$here/scripts/package.sh" | tail -1)
echo "Subiendo $(basename "$zip_path")…"
(cd "$here" && "$tcli" publish --config-path thunderstore.toml --file "$zip_path" --token "$TCLI_AUTH_TOKEN")
