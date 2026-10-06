# Phase 07 — The Farmer: planting and the seed cycle

**Depends on:** 02 (`FarmPlanner`), 03 (`ChoreLoop`, `WorkContext.ExtraReserve`), 04 (crop catalog, `StockCounter`, `PlantMods.CropSpacing`), 05 (orders), 06 (Farmer, fixtures) · **Enables:** 09 (the Cook respects the seed reserve), 10

## Goal
The Farmer plants: it asks the `FarmPlanner` what the orders need, fetches seeds (or produce, for seed crops) from the chests without touching the seed reserve, and plants them in tidy rows on empty cultivated ground in its radius, under the same rules a player's cultivator has, so nothing it plants dies for being in the wrong place.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Farm/PlantChore.cs` (new, `IChore`, kind `Plant`).
- `src/VikingsForHire/Hirelings/Work/Farm/FieldGrid.cs` (new): free planting spots in rows.
- `src/VikingsForHire/Hirelings/Work/Farm/FarmState.cs` (from phase 06): `freeSpots` now comes from `FieldGrid` (free spots per sapling prefab, at that crop's spacing), so the plan includes planting; it's also what the Cook reads for protected items (phases 08–09).
- `src/VikingsForHire/Hirelings/Work/Chores/WorkContext.cs`: the Farmer's context sets `ExtraReserve` to the plan's `SeedReserve` for `Crop`-order planting.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: the Farmer's loop gets `PlantChore` after `HarvestChore`.
- `src/VikingsForHire/Localization/English.json`: `vfh_farmer_plant` ("Planting $1"), `vfh_need_seed` ("$1: no $2 in any chest"), `vfh_need_seed_reserve` ("$1: no $2 above the reserve"), `vfh_need_field` ("No free cultivated ground").
- `src/VikingsForHire/Testing/FixturesFarm.cs`: checks `plant_spacing_ok <sapling>` (true when no two such plants in the radius are closer than the spacing the Farmer uses, `PlantMods.CropSpacing`, minus 0.05 m) and `grid_aligned <sapling>` (true when every such plant is within 0.1 m of the grid the Farmer would use), `plants_under_roof` (plants with something solid above them); fixture `piece <prefab> <x> <z> <height>` (a piece at an offset from the field's centre).
- `test/alias_vfh.yaml`: `vfh_t_farm_seeds` (VFH-FARM-3), `vfh_t_farm_rows` (VFH-FARM-4).
- `docs/test-checklist.md`: rows VFH-FARM-3 and 4.

## Steps
1. **The plan:** `FarmState.For(board)` (phase 06) runs `FarmPlanner.Plan` with the catalog crops, the board's orders, `StockCounter`'s `Chests` and `Growing`, the Farmer's level, and `freeSpots` from `FieldGrid`. Its `Missing` becomes the chore's `Missing`.
2. **Free spots (`FieldGrid`):** in the work radius, on a grid at the crop's spacing (`PlantMods.CropSpacing(growRadius)`: PlantEasily's spacing when loaded, else `growRadius*2`):
   - **Grid origin and direction:** lined up with the nearest existing plant or crop in the radius (its position and the direction to its nearest neighbour), else with the board's axes, so rows continue the player's rows.
   - **A spot is free when:** `Heightmap.IsCultivated(p)` (the cultivate paint), no plant, pickable or piece collider within the crop's `m_growRadius` (overlap sphere on the piece/item layers), open sky (raycast up 100 m hits nothing that isn't tagged `leaky`, as `Plant`'s own roof check), the biome at `p` is in the crop's `Plant.m_biome`, and not inside a shield dome when the crop doesn't tolerate cold/heat (vanilla's `ShieldGenerator.IsInsideShield`), and a ward the board's owner may use.
   - Spots are taken row by row, nearest row first; checked spots are cached for 30 s.
3. **Job (one trip per crop in the plan, first in plan order):**
   - Fetch `count × ConsumesAmount` of the consumed item from the nearest chests (`WorkSteps.TakeFromChest` with the reserve: the usual reserve plus, for Crop-order planting, `SeedReserve`), up to cargo room.
   - Walk the free spots in row order; at each, place the sapling as the cultivator does: `TerrainModifier.SetTriggerOnPlaced(true)`, instantiate the sapling prefab at the spot facing a random yaw, `SetTriggerOnPlaced(false)`, `Piece.SetCreator(board owner's player id)`, play the piece's `m_placeEffect`, remove `ConsumesAmount` from cargo. 0.6 s between plantings.
   - Re-check the spot just before planting (another player may have used it); a spot that fails is skipped.
   - When the cargo's seeds run out, the trip ends; leftovers go back to a chest by the leftover rule.
4. **Status:** "Planting Carrot seeds" while working; when idle, the plan's `Missing` or "No free cultivated ground"; with nothing to do, "Field tended".
5. **Never** plants regrowing pickables (catalog entries with `Regrowing`).

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.

## Test plan
- `vfh_t_farm_seeds` (VFH-FARM-3), the seed cycle: board level 2, `field 10 10`, chest with 3 Carrot and 0 CarrotSeeds, orders `CarrotSeeds 6 seed`, `Carrot 10 crop`; post Farmer 2.
  - Assert `plants sapling_seedcarrot >= 2` within 120 s and `plants sapling_carrot == 0` (no seeds yet).
  - `skip_time 5000` (crops ripen), assert seeds harvested: `deposited … CarrotSeeds >= 6` within 240 s.
  - Then `plants sapling_carrot > 0` within 120 s, and the chest still holds at least 6 CarrotSeeds (`chest … CarrotSeeds >= 6`).
- `vfh_t_farm_rows` (VFH-FARM-4): an existing row of 3 carrots planted by the fixture (`crop sapling_carrot 3`); chest with CarrotSeeds 20; order `Carrot 15 crop`; after planting (`plants sapling_carrot >= 10` within 180 s), `plant_spacing_ok sapling_carrot == true` and `grid_aligned sapling_carrot == true`.
- Roof safety: `vfh_t_farm_rows` also puts a `wood_roof` (fixture `piece wood_roof <x> <z> <height 2>` over one corner cell of the field); assert no plant under it (`plants_under_roof == 0`).

## Commit
`feat(farmer): planting in rows to production orders, with the seed cycle and seed reserve`

## Rollback
Revert the commit; the Farmer harvests only (phase 06).

## As built
- Crops take 4000–5000 s to grow, more than `skip_time` allows (it stays under a day so no upkeep is charged), so the tests use a new fixture, `grow_all`, which replaces every growing crop near the board with its ripe pickable.
- No separate shield/temperature check: the crop's biome check covers it (no vanilla crop grows in the cold biomes).
- `vfh_t_farm_seeds` uses a seed order of 4 (not 6) so the first seed harvest leaves seeds above the reserve for carrots (two seed carrots give 6 seeds; with the per-chest minimum of 1, 2 are spare).
