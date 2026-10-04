#!/usr/bin/env bash
# Copy the opt-in balance log (BepInEx/HiredHands/balance/*.jsonl) from a Valheim server running in a CubeCoders AMP
# container to ./balance/<container>/, over ssh and docker exec (the instance files belong to the amp user, so they're
# read from inside the container). Read-only on the server.
#
#   scripts/fetch-balance.sh AMP_Valheim01                 # from gameserver.spronglehump.com
#   scripts/fetch-balance.sh AMP_Valheim01 other.host
#   BEPINEX=/some/other/BepInEx scripts/fetch-balance.sh AMP_Valheim01
set -euo pipefail

CONTAINER="${1:?usage: fetch-balance.sh <AMP container, e.g. AMP_Valheim01> [ssh host]}"
HOST="${2:-gameserver.spronglehump.com}"
BEPINEX="${BEPINEX:-/AMP/Valheim/896660/BepInEx}"
REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
DEST="${DEST:-$REPO_DIR/balance/$CONTAINER}"

mkdir -p "$DEST"
# tar on the far side so a whole folder comes over in one go; nothing there is changed.
ssh -o BatchMode=yes "$HOST" "docker exec $CONTAINER sh -c 'cd $BEPINEX/HiredHands/balance 2>/dev/null && ls *.jsonl >/dev/null 2>&1 && tar cf - *.jsonl || true'" \
    | tar xf - -C "$DEST" 2>/dev/null || true

count=$(find "$DEST" -name '*.jsonl' | wc -l)
if [[ $count -eq 0 ]]; then
    echo "No balance log on $CONTAINER yet (is BalanceLog on in its Spronglehump.HiredHands.cfg?)"
    exit 1
fi
echo "Fetched $count day file(s) to $DEST ($(du -sh "$DEST" | cut -f1)). Report: scripts/balance-report.py $DEST"
