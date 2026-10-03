# Phase 10 — Smelter job & AzuAutoStore-aware output handling

**Depends on:** 02, 03, 05, 06, 07, 08 · **Enables:** 11 (v1 release)

## Goal
Add the Smelter. It keeps processing stations in the work radius stocked with ore/input and fuel taken from chests. It collects their finished output into chests only when AzuAutoStore isn't installed. With AzuAutoStore present it never collects, so it can't get confused when finished items disappear. Its decisions come from each station's live state on every cycle, never from remembered expectations.

## Files touched
- `src/VikingsForHire/Hirelings/Work/SmelterBehaviour.cs`: priority 200 for `JobType.Smelter`. Cycle: `Survey → Fetch → Load → (Collect) → repeat`.
- `src/VikingsForHire/Hirelings/Work/StationSurvey.cs`: finds `Smelter` components within the radius whose prefab name is in `DataStore.Current.Jobs.Smelter.Stations` (default `smelter`, `blastfurnace`, `charcoal_kiln`, `eitrrefinery`) and reads their state from the ZDO: `queued` ore count (`Smelter.GetQueueSize`), `fuel` (`GetFuel`), `m_maxOre`, `m_maxFuel`, accepted inputs (`m_conversion[].m_from`), fuel item (`m_fuelItem`), and processed-waiting count (`Smelter.GetProcessedQueueSize`, from the vanilla `SpawnProcessed`/accumulation queue).
- `src/VikingsForHire/Core/SmelterPlanner.cs`: pure planner. Given station states, the chest stock (prefab → count, minus `KeepMinimumInChest`), the cargo, and `SmelterRefillThreshold`, it outputs an ordered task list: `Fetch(chest, prefab, n)`, `Load(station, prefab, n, isFuel)`. Stations below the threshold come first (lowest fill ratio first). Fuel and input are fetched in proportion.
- `src/VikingsForHire/Hirelings/Work/StationLoader.cs`: walks to the station's ore/fuel switch (`Smelter.m_addOreSwitch`/`m_addWoodSwitch` positions) and adds items one at a time through the vanilla RPCs `RPC_AddOre(name)` / `RPC_AddFuel` via `m_nview.InvokeRPC`. Removes from cargo only after each RPC is sent, and re-reads the station's ZDO counts 0.5 s later to confirm. Unconsumed items stay in cargo for the next cycle.
- `src/VikingsForHire/Hirelings/Work/OutputCollector.cs`: only when `!AzuAutoStoreCompat.IsLoaded`. It (a) triggers the station's empty-processed switch (`Smelter.m_emptyOreSwitch` interaction via `RPC_EmptyProcessed`) when the processed queue is above 0, and (b) picks up `ItemDrop`s within 4 m of `m_outputPoint` whose prefab is a conversion output (`m_conversion[].m_to`) of any surveyed station. Delivery reuses `CargoDelivery` from phase 08.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: registers `SmelterBehaviour`.
- `tests/VikingsForHire.Tests/SmelterPlannerTests.cs`.

- `src/VikingsForHire/Testing/FixturesSmelter.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Survey every cycle** (scan interval). Nothing is cached between cycles except the reservation of which station this smelter is servicing (so 2 smelters split the stations: reservation map as in phase 08).
2. **Fetch:** `ChestFinder` (phase 08 exclusions) for chests holding the needed prefabs. Take items with `ContainerAccess` (claim ownership, remove, save) into cargo, up to cargo capacity, never leaving fewer than `KeepMinimumInChest` of a prefab in that chest. Kiln wood: only `Wood` is taken by default (the charcoal kiln's conversion list defines the accepted inputs, read at runtime). FineWood/RoundLog are never burned unless the station's conversions list them.
3. **Load:** add input until the queue reaches `m_maxOre` and fuel until `m_maxFuel`. A station counts as "needing service" when `queue/maxOre < threshold` or `fuel/maxFuel < threshold` (default 0.5) **and** stock exists.
4. **AzuAutoStore present:** never collect output, and never treat an unexpected drop in processed items or output drops as an error. Since all decisions read live state, outputs vanishing has no effect on the plan. Azu's auto-pickup may also grab ore the smelter dropped (it never drops ore: unconsumed items stay in cargo). Row **VFH-AZU-3** confirms there are no loops.
5. **AzuAutoStore absent:** after loading, run `OutputCollector` on every station within the radius, then deliver bars and coal per item type with `CargoDelivery` (bars to chests already holding that bar, else the drop pile).
6. **Idle:** when no station needs service and nothing waits for collection, return to the board and re-survey every 30 s (`$vfh_status_no_work`). Status tokens: `$vfh_status_fetching`, `$vfh_status_loading`, `$vfh_status_collecting`, `$vfh_status_no_input` (stations need ore or fuel but the chests have none).
7. **`NeedsDelivery` override** (`src/VikingsForHire/Hirelings/Work/SmelterDeliveryPolicy.cs`, an `IDeliveryPolicy`): for the Smelter it's true only when `vfh_deliver_pending` is set, when cargo holds collected output (prefabs that are station conversion outputs), or under the leftover rule below. Fetched inputs and fuel never trigger delivery while a station needs them.
8. **Cargo leftovers:** if the hireling holds input or fuel with no station needing it for 5 minutes, deliver it back with `CargoDelivery` (to chests already holding that prefab).

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.

## Test plan
- **Test macros** (phase 02 harness): `FixturesSmelter.cs`. Fixtures: `smelter_base` (2 smelters, a charcoal kiln and tagged chests stocked with CopperOre, TinOre, Coal and Wood), `blast_furnace`. Checks: `station <prefab> ore_ratio|fuel_ratio`, `station <prefab> processed_waiting`, `azu_loaded`, plus `chest <tag> <item>`. Aliases added to `test/alias_vfh.yaml`: `vfh_t_work5` (AzuAutoStore off: ratios ≥ 0.5 over `skip_days 2`, bars in chests), `vfh_t_azu3` (AzuAutoStore on: ratios ≥ 0.5, no loop: `log_errors == 0` and `hireling <h> behaviour != Deliver` sampled 10×), `vfh_t_work6` (dedicated). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Base with 2 smelters, 1 charcoal kiln, chests with CopperOre/TinOre, Coal and Wood. L3 smelter hireling, AzuAutoStore **disabled**:
  - Keeps both smelters ≥ half full of ore and coal, and the kiln stocked with Wood. Collects Copper/Tin bars and Coal output into chests that already hold them (or the drop pile). Run 2 in-game days with no player input (row **VFH-WORK-5**).
  - Add a blast furnace (L4 base): it stays fed once ingredients are present.
  - Empty the ore chests: the status reads "no input" and it idles at the board.
- AzuAutoStore **enabled** (row **VFH-AZU-3**): smelters still fed. Azu picks up the bars. The smelter hireling never tries to collect output, never loops and never stalls over 2 in-game days. Check the log for zero warnings from VikingsForHire.
- Two smelter hirelings: the stations are split, with no double-loading past max (vanilla rejects overfill, and the cargo keeps the remainder).
- Dedicated server: a station owned by client B is loaded correctly by a hireling owned by client A (vanilla RPC routing) (row **VFH-WORK-6**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(work): Smelter job feeding stations from chests with AzuAutoStore-aware output collection`

## Rollback
Revert the commit. Smelter hirelings fall back to idle + combat. Station contents stay as they are.

## As built
- One file, `SmelterBehaviour.cs`, does the survey → plan → one step (fetch / load / collect) loop; `StationSurvey.cs` reads stations; `SmelterPlanner` (Core, unit-tested) plans. There is no separate `StationLoader`/`OutputCollector`.
- Loading adds items one at a time through the vanilla `RPC_AddOre`/`RPC_AddFuel`, never more than the station's live free space, then waits 2 s before the next survey so a remote owner's count catches up (vanilla doesn't refuse ore past max).
- Deliveries are filtered per item (`IDeliveryPolicy.Delivers`): a smelter only hands in finished output (any conversion output of a configured station), unless its ore/fuel has been unneeded for 5 minutes or it's told to deliver.
- A chest it couldn't take from is skipped for 60 s; a step that takes over 45 s is given up and the next survey starts after 60 s.
- The `chest` test fixture now places chests in creation order (6 per ring) instead of by tag hash, which could put two chests in one spot.
