#!/usr/bin/env bash
# Compila en Debug, lo copia a un perfil de pruebas limpio (solo BepInEx + este mod) y lanza R.E.P.O.
# con Doorstop apuntando a ese perfil. La carpeta del juego no se toca: winhttp.dll y
# doorstop_config.ini ya los deja allí REPO Party.
#
# Antes de lanzar, mueve la ventana al escritorio «Claude» (ver CLAUDE.md del banco).
# Variables: BBR_PROFILE (perfil de pruebas), BBR_CORE_FROM (perfil del que copiar BepInEx/core).
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
profile="${BBR_PROFILE:-$here/../../.e2e/bbr-profile}"
core_from="${BBR_CORE_FROM:-$APPDATA/gg.repoparty.app/profiles/DEVLOCAL}"
steam="${STEAM_EXE:-/c/Program Files (x86)/Steam/steam.exe}"

dotnet build "$here/BlackBoxReborn.csproj" -c Debug -nologo -v q

if [ ! -f "$profile/BepInEx/core/BepInEx.Preloader.dll" ]; then
  mkdir -p "$profile/BepInEx"
  cp -r "$core_from/BepInEx/core" "$profile/BepInEx/"
fi
mkdir -p "$profile/BepInEx/plugins/BlackBoxReborn"
cp "$here/bin/Debug/BlackBoxReborn.dll" "$profile/BepInEx/plugins/BlackBoxReborn/"

if [ "${1:-}" = "--no-launch" ]; then exit 0; fi
preloader="$(cygpath -w "$profile/BepInEx/core/BepInEx.Preloader.dll")"
"$steam" -applaunch 3241660 --doorstop-enabled true --doorstop-target-assembly "$preloader" &
echo "Lanzado con el perfil $(cygpath -w "$profile")"
