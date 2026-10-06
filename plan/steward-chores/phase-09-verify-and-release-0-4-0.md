# Phase 09 — Check, docs and release 0.4.0

**Depends on:** 01–08 · **Enables:** the next round of features (Cook, Farmer, production orders)

## Goal
Check the Steward's chores against the overview's success criteria on single player, on the local dedicated server and on the live server's mod set. Document them, and ship 0.4.0 in one release, with everyone updating together (the data file gains `choreLevels`).

## Files touched
- `src/VikingsForHire/Plugin.cs`, `src/VikingsForHire/VikingsForHire.csproj`, `package/manifest.json`: version 0.4.0.
- `README.md`: the Jobs section's Steward entry rewritten:
  - each chore and its level table;
  - the Shift+E chore list;
  - the mods it steps aside for;
  - the new settings;
  - a note that existing data files keep their `stations` and `Gear`: add `windmill`, `piece_spinningwheel` and `VFH_Broom` by hand, or delete the file for fresh defaults.
- `CHANGELOG.md`: `## 0.4.0`, including the level 1 Steward change ("no longer smelts: promote to level 2").
- `docs/next-release.md`: the 0.4.0 rows and the batch list.
- `docs/test-checklist.md`: VFH-CHORE-1…12 and VFH-BROOM-1 with results.
- `plan/steward-chores/*.md`: "As built" notes where the build differed.

## Steps
1. **Single player** (`vikingsforhire-dev`, which has PetPantry, Torches Eternal and AzuAreaRepair since phase 01):
   - every `vfh_t_chore_*`, `vfh_t_fires`, `vfh_t_beehive`, `vfh_t_sap`, `vfh_t_animals`, `vfh_t_repairs`, `vfh_t_mills`, `vfh_t_fermenter`, `vfh_t_shield` and `vfh_t_tidy`;
   - the regression set `vfh_t_work5`, `vfh_t_azu3`, `vfh_t_keep1`, `vfh_t_keep2`, `vfh_t_door1`, `vfh_t_nav1` and `vfh_t_nav5`.
2. **Local dedicated server:** `vfh_t_fires`, `vfh_t_animals` and `vfh_t_repairs` from a client. Chore RPCs go to objects owned by other games.
3. **Live mod set by hand,** on a copy of the live world with `1dotohsupermodded`:
   - a Steward at a real base, for 15 minutes;
   - the panel shows "Fires: handled by TorchesEternal" and "Animals: handled by PetPantry";
   - smelters, beehives and fermenters are serviced, damaged walls repaired, and items left on the ground tidied away;
   - no `lvl=E` lines, and `perf.minute` about as in 0.3.0.
4. **Docs** as listed.
5. **Release,** only with Tim's go-ahead in chat:
   - package with `dotnet build -c Release -t:Package`;
   - push, tag `v0.4.0` (annotated);
   - `gh release create v0.4.0 dist/Spronglehump-HiredHands-0.4.0.zip --title "Hired Hands 0.4.0" --notes-file <0.4.0 section>`, ending with "update everyone together".
6. **After release:** Tim's live play session. Read `steward.job`, `steward.survey` and `nav.stuck` from his client log and the AMP server log.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- `dotnet build -c Release -t:Package` produces the 0.4.0 zip.
- Every macro in steps 1–2 passes with no `lvl=E`.

## Test plan
The overview's success criteria, one by one, through steps 1–3 and the live session.

## Commit
`chore: release 0.4.0 (Steward chores)`

## Rollback
- **Quick:** server settings `Steward*` off per chore.
- **Full downgrade to 0.3.0:** everyone together, after deleting the `choreLevels:` block from `Spronglehump.HiredHands.yml`. Stewards' `chore:` skip entries are ignored by 0.3.

## As built
- **Testing:** every chore macro passed in single player during phases 02–08 (VFH-CHORE-1…12, VFH-BROOM-1 by hand). Tim skipped the full single-player and local dedicated server runs and tests 0.4.0 on the live dedicated server after publishing it.
- **Older data files** also get the broom: a pre-0.4 file whose Steward has the old default (a plain club at every level) is switched to `VFH_Broom`; any other gear is kept.
