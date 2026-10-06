# Phase 06 — The Farmer: gear and harvesting

**Depends on:** 02 (`FarmPlanner`), 03 (`ChoreLoop`, Farmer chore kinds), 04 (crop catalog, `PickableYield`, `PlantMods.HarvestRadius`), 05 (the board's orders, for regrowing pickables) · **Enables:** 07 (planting fills the ground this phase empties), 10

## Goal
A hired Farmer holds a cultivator and harvests: every ripe crop in its work radius, plus regrowing pickables (bushes, mushrooms…) whose item has a short order. It harvests everything ripe within reach in one go, and puts the harvest away in chests that hold it (the pause-when-full rule applies). Test fixtures make fields and crops without waiting a day.

## Files touched
- `src/VikingsForHire/Hirelings/Gear/HeldModel.cs` (new): the visual swap shared by built and borrowed models (the club's in-hand visual found under `attach`, re-pointed along the club's axis, real size), moved out of `BroomItem`.
- `src/VikingsForHire/Hirelings/BroomItem.cs`: uses `HeldModel`.
- `src/VikingsForHire/Hirelings/Gear/CultivatorItem.cs` (new): `VFH_Cultivator`, a `CustomItem` cloned from `Club` (club stats, never crafted) wearing the vanilla Cultivator's mesh and materials, 1.4 m long; registered like the broom; `DataStore.ItemExists` accepts it.
- `src/VikingsForHire/Core/Data/DefaultData.cs`: Farmer `gear` → `VFH_Cultivator` at every level.
- `src/VikingsForHire/Hirelings/Work/Farm/FarmState.cs` (new): per board, the current `FarmPlan` from `FarmPlanner.Plan` (catalog crops, the board's orders, `StockCounter` stock and growing yields, the Farmer's level), recomputed at most every 10 s; in this phase `freeSpots` is empty (nothing is planted yet), so only `PickRegrowing` and `SeedReserve` are used.
- `src/VikingsForHire/Hirelings/Work/Farm/HarvestChore.cs` (new, `IChore`, kind `Harvest`).
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: the Farmer's `ChoreLoop` gets `HarvestChore`.
- `src/VikingsForHire/Localization/English.json`: `vfh_farmer_harvest` ("Harvesting $1"), `vfh_farmer_idle` ("Field tended").
- `src/VikingsForHire/Testing/FixturesFarm.cs` (new): fixtures and checks below.
- `test/alias_vfh.yaml`: `vfh_t_farm_harvest` (VFH-FARM-1), `vfh_t_farm_bush` (VFH-FARM-2).
- `docs/test-checklist.md`: rows VFH-FARM-1 and 2.

## Steps
1. **Gear:** `CultivatorItem` registers on `OnVanillaPrefabsAvailable` (Cultivator prefab from `PrefabManager.Cache`), swapping the club visual for the cultivator's mesh with `HeldModel`. The broom moves onto `HeldModel` with no visible change.
2. **Candidates** (each survey, within the work radius, ward-usable by the board's owner):
   - **Ripe crops:** a `Pickable` whose prefab is a catalog crop's grown prefab (`m_respawnTimeMinutes == 0`), not picked (`m_picked == false`). Always candidates. Urgency 0.5.
   - **Regrowing:** a `Pickable` with `m_respawnTimeMinutes > 0`, ripe (`CanBePicked()`), whose item is in `FarmState.For(board).PickRegrowing` (an order is short). Urgency 0.4.
   - `PauseWhenStorageFull`: an item with no room in its chests is skipped (and named in the status), like the Steward's.
   - Level: a crop above the Farmer's level is still harvested (it's already grown); level only gates planting.
3. **Job (one trip):** walk to the nearest candidate (`WorkSteps.Approach`), then harvest every candidate within `PlantMods.HarvestRadius` (3 m, or PlantEasily's setting) of where it stands, then the next nearest within 15 m, up to 8 stops or until cargo is full; then `DeliverPending`.
4. **Harvesting one pickable without a player:** claim its ZDO, add `PickableYield.Main` of `m_itemPrefab` plus `m_extraDrops.GetDropListItems()` to cargo (`AddItem(…, dropIfFullInv: false)`; what doesn't fit is dropped at its feet), play `m_pickEffector`, then `InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true)` (crops are destroyed by their owner, bushes start regrowing). Count per item for `farmer.harvest` (Debug).
5. **Delivery:** harvested items are chore outputs (`ChoreDeliveryPolicy` with `DeliverPending` at the trip's end), so they go to chests already holding them; items with no home go to the board's pile as usual. Seeds and planting stock the Farmer will need (phase 07) are delivered too; phase 07 fetches them back from chests, so cargo never hoards.
6. **Fixtures** (`FixturesFarm.cs`):
   - `field <w> <h> [tag]`: cultivates a w×h m patch 8 m in front of the board (flattened first), the way the cultivator paints (`TerrainComp` cultivate op), and tags its centre.
   - `crop <sapling> <n> [ripe] [tag]`: places n saplings in a row on the field (spaced by the catalog's spacing); `ripe` places the grown pickable instead (as if fully grown).
   - `bush <pickable> [tag]`: places a regrowing pickable (e.g. `RaspberryBush`) at the field's edge, ripe.
   - Checks `ripe_crops [radius]`, `plants <sapling> [radius]`, `picked <tag>` (a bush's picked state).

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.

## Test plan
Existing fixtures and checks used here and in later phases: `post <job> <level> [radius] [free]`, `chest`, `deposited`, `ground_items`, `skip_time`, `roof_over`, `order` (phase 05).
- `vfh_t_farm_harvest` (VFH-FARM-1): board level 2, `field 8 8`, `crop sapling_carrot 6 ripe`, chest with 1 Carrot, post Farmer 2. Assert `ripe_crops == 0` within 120 s and `deposited carrots Carrot >= 6`.
- `vfh_t_farm_bush` (VFH-FARM-2): `bush RaspberryBush B`, chest with 1 Raspberry; no order → `picked B == false` after 60 s; `order add Raspberry 20 crop` → `picked B == true` within 90 s and raspberries deposited.
- By hand: the Farmer holds a cultivator; it swats a Greyling with it.

## Commit
`feat(farmer): Farmer holds a cultivator and harvests ripe crops and short-ordered bushes`

## Rollback
Revert the commit; the Farmer goes back to idling (phase 03).
