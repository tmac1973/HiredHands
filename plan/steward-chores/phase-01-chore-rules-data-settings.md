# Phase 01 — Chore rules, data, settings and mod detection

**Depends on:** nothing (0.3.0 code) · **Enables:** 02 (the chore loop uses all of this), 03–06

## Goal
The ground work every chore uses, with no change in game yet:
- the list of chores and how each one's urgency is scored, as pure logic in `Core/` with unit tests;
- each chore's (and each station's) minimum Steward level, in the data file;
- the windmill and spinning wheel added to the Steward's stations;
- per-Steward chore toggles, stored in the contract's existing skip list;
- server settings for each chore, the refill and repair thresholds, and a test-only override;
- detection of the mods that already do a chore.

## Files touched
- `src/VikingsForHire/Core/Chores/ChoreKind.cs` (new): `enum ChoreKind { Fires, Beehives, Stations, Mills, Sap, Animals, Repairs }`, plus `ChoreKeys`: the data and toggle key for each (`fires`, `beehives`, `stations`, `mills`, `sap`, `animals`, `repairs`) and `TryParse`.
- `src/VikingsForHire/Core/Chores/ChoreRules.cs` (new):
  - `MinLevel(JobData steward, string key)`, where the key is a chore key or a station prefab; a key with no entry counts as level 1.
  - `Unlocked(level, key)`.
  - `KindOfStation(prefab)`: `windmill` and `piece_spinningwheel` are `Mills`, every other station is `Stations`.
  - `ChoresOff(string skipItems)` and `WithChore(string skipItems, ChoreKind, bool on)`: the `chore:<key>` entries in the skip list.
- `src/VikingsForHire/Core/Chores/ChoreUrgency.cs` (new): the scoring functions (step 3).
- `src/VikingsForHire/Core/Data/VfhData.cs`: `JobData.ChoreLevels` (`Dictionary<string, int>`), documented as "Steward only: lowest Steward level for each chore (fires, beehives, stations, mills, sap, animals, repairs) and each station prefab".
- `src/VikingsForHire/Core/Data/DefaultData.cs`:
  - the Steward's `Stations` gain `windmill` and `piece_spinningwheel`;
  - `ChoreLevels` defaults to: `fires 1, beehives 1, smelter 2, charcoal_kiln 2, animals 2, repairs 3, blastfurnace 5, windmill 5, piece_spinningwheel 5, eitrrefinery 6, sap 6`.
- `src/VikingsForHire/Core/Data/DataValidator.cs`: rejects `ChoreLevels` values outside 1–8 and unknown chore keys (a key is valid if it's a chore key or listed in `Stations`). A station with no level gets a warning (`data.station_no_level`), not a rejection: it counts as level 1.
- `src/VikingsForHire/Config/VfhConfig.cs`, a new *Steward* section, all synced:
  - `StewardFires`, `StewardBeehives`, `StewardStations`, `StewardMills`, `StewardSap`, `StewardAnimals`, `StewardRepairs` (bool, true): "Stewards may do this chore at all".
  - `StewardFireRefillFraction` (float, 0.5): "Refuel a fire when its fuel is below this fraction of full; it's filled up to full."
  - `StewardRepairBelow` (float, 0.95): "Repair pieces below this fraction of full health."
  - `StewardRepairQuietSeconds` (float, 20): "No repairs until this long after the last enemy was seen within 30 m of the Steward or the piece."
  - `StewardIgnoreOtherMods` (bool, false): "Testing: behave as if PetPantry and Torches Eternal weren't installed."
- `src/VikingsForHire/Compat/StewardCompat.cs` (new):
  - `HandledBy(ChoreKind)` returns the covering mod's name, or null: `Animals` when `Azumatt.PetPantry` is loaded, `Fires` when `Xenofell.TorchesEternal` is loaded. It always returns null when `StewardIgnoreOtherMods` is on.
  - On first use it logs Info `steward.compat` with each chore and the mod covering it.
  - AzuAreaRepair and RepairStation aren't treated as covering anything.
- `src/VikingsForHire/Localization/English.json`:
  - chore names `vfh_chore_<key>`;
  - states `vfh_chore_state_on`, `_off`, `_locked` ("locked until level $1"), `_handled` ("handled by $1"), `_disabled` ("off on this server").
- `tests/VikingsForHire.Tests/ChoreRulesTests.cs`, `ChoreUrgencyTests.cs` (new).
- `tests/VikingsForHire.Tests/DataTests.cs` and `DataDefaultsTests.cs`: `ChoreLevels` defaults, validation, and filling into an older file.

## Steps
0. **Test profile (Tim, in Gale, before this phase's tests):** add PetPantry, Torches Eternal and AzuAreaRepair to the `vikingsforhire-dev` profile, so the step-aside tests in phases 03 and 05 have them. The local dedicated server picks them up through `scripts/run-dedicated-server.sh`.
1. **Keys.** Chore keys are lower-case and stable: they appear in data files and in contracts' skip lists. A station prefab is gated by its own `ChoreLevels` entry, and toggled by its kind's toggle (`stations` or `mills`).
2. **Toggles are stored in the contract's `SkipItems`** (the gatherers' item toggles), as `chore:<key>` entries. That needs no roster format change; `RosterOp.SetGather` and `HirelingOp.SkipItems` already carry it.
   - `GatherRules.ParseSkip`/`FormatSkip` keep unknown entries, so `chore:` entries survive.
   - A Steward has no gather toggles, so nothing collides.
   - Every chore is on unless its `chore:<key>` entry is present.
3. **Urgency** (`ChoreUrgency`), each 0..1, where 0 means nothing to do:
   - `Fire(fuel, max, refill)`: 0 if `fuel >= refill*max`, else `0.5 + 0.5*(1 - fuel/(refill*max))`. An empty fire scores 1.
   - `Station(oreFrac, fuelFrac, outputWaiting, threshold)`: below the threshold, `0.4 + 0.5*(1 - min(oreFrac, fuelFrac))`. With output waiting, at least 0.6. Otherwise 0.
   - `Producer(level, max)` (beehive, sap collector): 0 below half full, else `0.3 + 0.4*(level/max)`. A full producer scores 0.7.
   - `Animal(hungry)`: 0.7 if hungry, else 0.
   - `Repair(health, below)`: 0 if `health >= below`, else `0.3 + 0.6*(1 - health)`.
   - `Score(urgency, distance, radius)` = `urgency - 0.1*min(distance/radius, 1)`. The highest score above 0 wins; ties go to the nearer job, then the earlier kind in enum order.
4. **Data.** `DataDefaults.FillMissing` already fills new properties into older files, including the whole `choreLevels` dictionary. It doesn't add list entries, though, so in an existing file `stations` stays as it is: an existing server's data file gets no windmill or spinning wheel unless the owner adds them. Phase 08 documents this in the README, like the 0.2.1 price note.
5. **Compat** reads `Chainloader.PluginInfos` once, lazily, after all plugins have loaded.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes, including the new tests and `LocalizationCoverageTests`.

## Test plan
- **`ChoreRulesTests`:**
  - every default level as listed;
  - `Unlocked(1, "smelter")` is false, `Unlocked(2, "smelter")` is true;
  - an unknown station counts as level 1;
  - `KindOfStation`;
  - toggles round-trip through `WithChore`/`ChoresOff` and leave other skip entries alone.
- **`ChoreUrgencyTests`:**
  - a fire at 0.6 with refill 0.5 scores 0; at 0.1, about 0.9; empty, 1;
  - producers: 0 below half, 0.7 when full;
  - repair: 0 at 0.96, about 0.84 at 0.1;
  - the distance bias orders two equal jobs by distance;
  - a far, urgent job beats a near job with nothing to do.
- **Data tests:**
  - defaults validate;
  - a `choreLevels` value of 9 is rejected;
  - an unknown key is rejected;
  - a station without a level only warns;
  - `choreLevels` is filled into a file without it.

## Commit
`feat(steward): chore list, urgency scoring, chore levels in the data file, steward settings and mod detection`

## Rollback
Revert the commit. Nothing in game uses these yet. An existing data file keeps its filled-in `choreLevels`, which this phase's build added and older builds reject: delete the `choreLevels:` block after reverting. 0.4.0 needs everyone on 0.4 together (phase 08).
