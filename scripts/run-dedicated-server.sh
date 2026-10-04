#!/usr/bin/env bash
# Start a local Valheim dedicated server with the vikingsforhire-dev Gale profile's BepInEx + mods.
#
# Usage: scripts/run-dedicated-server.sh [--no-vfh] [--vfh-dll <path>]
#   --no-vfh          run the server without Hired Hands (removes any copy left from earlier runs)
#   --vfh-dll <path>  run the server with this HiredHands.dll instead of the profile's copy
#
# Env overrides: SERVER_DIR, PROFILE, SAVE_DIR
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

SERVER_DIR="${SERVER_DIR:-/games/SteamLibrary/steamapps/common/Valheim dedicated server}"
PROFILE="${PROFILE:-$HOME/.local/share/com.kesomannen.gale/valheim/profiles/vikingsforhire-dev}"
# Kept apart from the client's ~/.config/unity3d/IronGate/Valheim so test worlds never mix with real ones.
SAVE_DIR="${SAVE_DIR:-$SERVER_DIR/vfh-save}"

no_vfh=0
vfh_dll=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-vfh) no_vfh=1; shift ;;
        --vfh-dll) vfh_dll="$(realpath "$2")"; shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

[[ -x "$SERVER_DIR/valheim_server.x86_64" ]] || { echo "No dedicated server at $SERVER_DIR" >&2; exit 1; }
[[ -d "$PROFILE/BepInEx/core" ]] || { echo "No BepInEx in profile $PROFILE" >&2; exit 1; }

mkdir -p "$SERVER_DIR/BepInEx" "$SAVE_DIR"
rsync -a --delete "$PROFILE/BepInEx/core/" "$SERVER_DIR/BepInEx/core/"
rsync -a --delete "$PROFILE/doorstop_libs/" "$SERVER_DIR/doorstop_libs/"
rsync -a "$PROFILE/doorstop_config.ini" "$SERVER_DIR/"

plugin_args=(-a --delete)
if [[ $no_vfh -eq 1 ]]; then
    plugin_args+=(--exclude Spronglehump-HiredHands --delete-excluded)
fi
rsync "${plugin_args[@]}" "$PROFILE/BepInEx/plugins/" "$SERVER_DIR/BepInEx/plugins/"

# Seed configs once; never overwrite server-side edits.
mkdir -p "$SERVER_DIR/BepInEx/config"
rsync -a --ignore-existing "$PROFILE/BepInEx/config/" "$SERVER_DIR/BepInEx/config/"
# Test macros always track the repo copy.
cp "$REPO_DIR/test/alias_vfh.yaml" "$SERVER_DIR/BepInEx/config/alias_vfh.yaml"

if [[ -n "$vfh_dll" && $no_vfh -eq 0 ]]; then
    cp "$vfh_dll" "$SERVER_DIR/BepInEx/plugins/Spronglehump-HiredHands/HiredHands.dll"
fi

echo "Hired Hands on server: $([[ $no_vfh -eq 1 ]] && echo no || echo "yes${vfh_dll:+ ($vfh_dll)}")"
echo "Save dir: $SAVE_DIR (adminlist.txt lives here)"

# Make the local player an admin so devcommands (and the vfh_t_* test macros) work. The ID is the plain Steam
# number the server logs as "Got connection SteamID ...", taken from the dev profile's client log.
ADMIN_ID="${ADMIN_ID:-$( (grep -oE 'Steam_[0-9]{17}' "$PROFILE/BepInEx/LogOutput.log" 2>/dev/null || true) | head -1 | sed 's/Steam_//')}"
if [[ -n "$ADMIN_ID" ]] && ! grep -qx "$ADMIN_ID" "$SAVE_DIR/adminlist.txt" 2>/dev/null; then
    echo "$ADMIN_ID" >> "$SAVE_DIR/adminlist.txt"
    echo "Added $ADMIN_ID to adminlist.txt"
fi
echo "Log: $SERVER_DIR/BepInEx/LogOutput.log"

cd "$SERVER_DIR"
# Same environment as BepInExPack's Linux start_server_bepinex.sh
export DOORSTOP_ENABLED=1
export DOORSTOP_TARGET_ASSEMBLY=./BepInEx/core/BepInEx.Preloader.dll
export LD_LIBRARY_PATH="./doorstop_libs:./linux64:${LD_LIBRARY_PATH:-}"
export LD_PRELOAD="libdoorstop_x64.so:${LD_PRELOAD:-}"
export SteamAppId=892970

# No -password: LAN-only test server (-public 0), so it runs open.
exec ./valheim_server.x86_64 -name VikingsForHireTest -port 2456 -world VikingsForHireTest \
    -public 0 -savedir "$SAVE_DIR"
