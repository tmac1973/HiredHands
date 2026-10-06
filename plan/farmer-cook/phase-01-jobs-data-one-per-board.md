# Phase 01 — Jobs, data and one per board

**Depends on:** nothing · **Enables:** 02 (planners read the level tables), 03 (chore sets per job), 06 and 08 (the jobs exist)

## Goal
The Farmer and Cook exist as jobs that can be posted and hired from board level 2, at most one of each per board, with their level tables in the data file. They don't work yet (they idle at the board like a woodcutter with nothing to do); later phases give them work. Everything here is data and pure rules, unit tested.

## Files touched
- `src/VikingsForHire/Core/JobType.cs`: appends `Farmer`, `Cook` (after `GuardRanged`, so saved contracts keep their numbers); `JobTypeExtensions.OnePerBoard(this JobType)` true for Farmer and Cook.
- `src/VikingsForHire/Core/Roster.cs`: `OpOutcome.JobTaken`; `Post` refuses a Farmer or Cook when the board already has a Pending or Active contract of that job (a Leaving one doesn't count).
- `src/VikingsForHire/Core/Data/VfhData.cs`: `JobData.CropLevels` and `JobData.RecipeLevels` (`Dictionary<string,int>`, item prefab → hireling level), `JobData.StationLevels` (`Dictionary<string,int>`, station prefab → Cook level for its level-1 recipes).
- `src/VikingsForHire/Core/Data/DefaultData.cs`: the two jobs (below).
- `src/VikingsForHire/Core/Data/DataValidator.cs`: the three tables' levels must be 1–8 (an error); unknown item or station names only warn in `Sanitize`, like `choreLevels`.
- `src/VikingsForHire/Core/Data/DataDefaults.cs`: nothing new to migrate by hand: a file without `Farmer`/`Cook` gets them from the defaults through the existing fill; a test proves it.
- `src/VikingsForHire/UI/ContractsTab.cs`: the job picker shows Farmer and Cook from board level 2; posting a second one shows "$vfh_op_job_taken" ("This board already has a $1").
- `src/VikingsForHire/Localization/English.json`: `vfh_job_farmer` ("Farmer"), `vfh_job_cook` ("Cook"), `vfh_op_job_taken`.
- `tests/VikingsForHire.Tests/RosterTests.cs`, `DataTests.cs`, `DataDefaultsTests.cs`: new cases.

## Steps
1. **Jobs in the data file** (`jobs.Farmer`, `jobs.Cook`), both:
   - `costMult 0.9`, `workerCombatFactor 0.3` (like the Steward), `minBoardLevel 2`, `workRadiusMultiplier 1`;
   - `gear`: `Club` at every level for now (phases 06 and 08 switch them to their own items);
   - no `pickupItems`, `gatherToggles`, `stations` or `keepInStorage` (empty).
2. **`jobs.Farmer.cropLevels`** keyed by the item a crop yields (vanilla defaults, by biome, matching the board levels):
   - 1: Raspberry, Mushroom, Dandelion (regrowing plants; PlantEverything or wild)
   - 2: Carrot, CarrotSeeds, Blueberries, Thistle, MushroomYellow
   - 3: Turnip, TurnipSeeds
   - 4: Onion, OnionSeeds
   - 5: Barley, Flax, Cloudberry
   - 6: MushroomJotunPuffs, MushroomMagecap
   - 7: Vineberry, VineberrySeeds, Fiddleheadfern, MushroomSmokePuff
   - 8: OatSeeds, Kale, KaleSeeds, Poteitr, PoteitrSeeds (Oat itself is milled from OatSeeds at the windmill: a Steward station, not a crop)
   - Any crop item not listed is level 1.
3. **`jobs.Cook.stationLevels`** (the Cook level for a station's level-1 recipes and conversions): `piece_cookingstation 1`, `piece_cauldron 2`, `piece_cookingstation_iron 3`, `piece_oven 5`, `piece_preptable 6`, `piece_MeadCauldron 7`. A cauldron recipe needing cauldron level N is Cook level `2 + (N - 1)`, capped at 8.
4. **`jobs.Cook.recipeLevels`**: empty by default; an entry (item prefab → level) overrides the station rule for that item.
5. **One per board:** `Roster.Post` checks `entry.Job.OnePerBoard()` before the cap: an existing Pending or Active entry of that job → `JobTaken`. The board's contract op path (`BoardRosterOps` / `MutationService`) already returns the outcome to the UI; the Contracts tab maps `JobTaken` to the message.
6. **Hireling setup:** `HirelingAI.Init`'s `switch` sends Farmer and Cook to the existing `default` branch for now (on-request delivery only), so a hired Farmer or Cook stands at the board and can be recruited, posted and dismissed like any hireling.

## Build gate
- `dotnet build -c Release` with 0 warnings.
- `dotnet test`: new cases pass (a second Farmer is `JobTaken`, a Leaving Farmer doesn't block; a Farmer and a Cook together are fine; defaults validate; a 0.4 data file gains both jobs with defaults; a level 9 in `cropLevels` is an error; an unknown crop item only warns).

## Test plan
- Unit tests above.
- In game (single player): post a Farmer and a Cook on a level 2 board; both arrive; a second Farmer is refused with "This board already has a Farmer"; on a level 1 board neither is offered.

## Commit
`feat(jobs): Farmer and Cook jobs (one per board), crop/recipe/station level tables`

## Rollback
Revert the commit. Contracts for Farmer/Cook saved while it was in would load with unknown job numbers on an older build, so revert only before release (nothing ships until phase 10).
