# Phase 10 — Check, docs and release 0.5.0

**Depends on:** 01–09 · **Enables:** the next round (combat AI, from `plan/vikings-for-hire/futures.md`)

## Goal
Check the Farmer and Cook against the overview's success criteria in single player, on the local dedicated server and with the live server's mod set (PlantEverything, PlantEasily), document them, and ship 0.5.0 together with the unreleased 0.4.4 fixes, everyone updating together.

## Files touched
- `src/VikingsForHire/Plugin.cs`, `src/VikingsForHire/VikingsForHire.csproj`, `package/manifest.json`: version 0.5.0; manifest description mentions farming and cooking.
- `README.md`: Jobs table rows for Farmer and Cook; new sections *The Farmer* (field = cultivated ground in the radius, harvest rules, rows, crop levels table, PlantEverything/PlantEasily notes), *The Cook* (stations, stay-near rule, recipe levels), *Orders* (the board tab, seed orders first and as reserve, have = chests + growing); configuration lines for section 11 and the new data tables.
- `CHANGELOG.md`: `## 0.5.0` with the Farmer, the Cook, the Orders tab, one-per-board, and the 0.4.4 fixes (LootRadius; deliveries to chests under raised floors; pause only when there's no room; repairs skip water wear and pieces that keep wearing); data file notes (new jobs and tables are added automatically).
- `docs/next-release.md`: the 0.4.4 tab becomes the 0.5.0 tab with these phases' commits.
- `docs/test-checklist.md`: rows VFH-ORDER-1, VFH-FARM-0…4, VFH-COOK-1…5 with results.
- `plan/farmer-cook/*.md`: "As built" notes where the build differed.

## Steps
1. **Single player** (`vikingsforhire-dev`, with PlantEverything and PlantEasily added to the profile for this run):
   - `vfh_test_chain catalog orders farm_harvest farm_bush farm_seeds farm_rows cook_spit cook_oven cook_cauldron cook_chain cook_protect`;
   - regression: phase 03's Steward set (`chore_gate chore_toggle fires beehive animals repairs repairs2 fermenter shield tidy board_food loot pause1 pause2`) plus `deliver1 deliver2 nav1 nav5 keep1 azu3`.
2. **Local dedicated server**, from a client: `vfh_t_orders`, `vfh_t_farm_harvest`, `vfh_t_cook_spit` (orders through the server; picking and RPCs on objects owned by other games; no local-player calls on the server).
3. **Live mod set by hand** (a single-player copy of the live world with `1dotohsupermodded`): a Farmer on a real field with a seed order and a produce order for 30 minutes of play; PlantEasily rows match the player's; raspberry bushes picked only while their order is short; a Cook on spits and a cauldron; no `lvl=E`, `perf.minute` about as in 0.4.3.
4. **Docs** as listed.
5. **Release**, only with Tim's go-ahead in chat: `dotnet build -c Release -t:Package`, push, annotated tag `v0.5.0`, `gh release create v0.5.0 dist/Spronglehump-HiredHands-0.5.0.zip --title "Hired Hands 0.5.0" --notes-file <0.5.0 section>` ending with "update everyone together"; Tim uploads to Thunderstore/Hexium.
6. **After release:** read `farmer.*`, `cook.*`, `storage.full` and `nav.stuck` lines from Tim's client log after his live session.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes; `-t:Package` produces the 0.5.0 zip.
- Every macro in steps 1–2 passes with no `lvl=E`.

## Test plan
The overview's success criteria, through steps 1–3.

## Commit
`chore: release 0.5.0 (Farmer, Cook, production orders)`

## Rollback
- **Quick:** server settings `FarmerHarvest`, `FarmerPlant`, `CookStoves`, `CookCraft` off; dismiss the Farmer/Cook.
- **Full downgrade to 0.4.3:** everyone together, after dismissing Farmers and Cooks (their contracts' job numbers are unknown to 0.4) and removing the `Farmer`/`Cook` jobs from `Spronglehump.HiredHands.yml`.
