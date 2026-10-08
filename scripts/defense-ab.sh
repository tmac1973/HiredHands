#!/usr/bin/env bash
# Phase 05 of plan/combat-ai: the defense A/B run. Each matchup (VFH-DEF-M1..M4) N times with BlockAndDodge off, then N
# times on, with the balance log recording every fight. Then: scripts/balance-report.py <profile>/BepInEx/HiredHands/balance --compare-defense
#   scripts/defense-ab.sh [runs=5]          (needs ModTestBridge's mtb on PATH or MTB set; the game in the test world)
set -euo pipefail
cd "$(dirname "$0")/.."
MTB="${MTB:-$(command -v mtb || echo ../vh_mod_test_bridge/scripts/mtb)}"
runs="${1:-5}"
rows=""; for _ in $(seq "$runs"); do rows="$rows defm1 defm2 defm3 defm4"; done

"$MTB" cmd "vfh_fixture defense_chances reset" >/dev/null
"$MTB" cmd "vfh_fixture cfg_set BalanceLog true" >/dev/null
sleep 3   # let the setting take before the first fight starts
"$MTB" cmd "vfh_fixture flatten 30" >/dev/null
for mode in false true; do
  "$MTB" cmd "vfh_fixture cfg_set BlockAndDodge $mode" >/dev/null
  echo "BlockAndDodge=$mode: $runs x 4 matchups"
  "$MTB" run "vfh_test_chain $rows defdone" "row=VFH-DEF-DONE pass=" 7200 >/dev/null
done
"$MTB" cmd "vfh_fixture cfg_set BlockAndDodge true" >/dev/null
echo "waiting 70 s for the balance log to flush"
sleep 70
echo done
