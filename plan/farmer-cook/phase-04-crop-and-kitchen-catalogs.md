# Phase 04 — Crop and kitchen catalogs, stock counting

**Depends on:** 01 (level tables), 02 (`CropInfo`, `KitchenInfo`) · **Enables:** 05 (the order picker lists them), 06–09 (workers act on them)

## Goal
At runtime, learn every crop and every kitchen recipe/conversion from the game's own prefabs (vanilla, PlantEverything, Deep North and any other mod), turn them into the `CropInfo`/`KitchenInfo` the planners take, and count stock the way orders count it (chests in the radius above reserves, plus what's growing). Debug commands list what was found.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Farm/CropCatalog.cs` (new).
- `src/VikingsForHire/Hirelings/Work/Farm/PickableYield.cs` (new): a pickable's yield without a player.
- `src/VikingsForHire/Hirelings/Work/Kitchen/KitchenCatalog.cs` (new).
- `src/VikingsForHire/Hirelings/Work/StockCounter.cs` (new).
- `src/VikingsForHire/Compat/PlantMods.cs` (new): PlantEverything / PlantEasily detection and settings.
- `src/VikingsForHire/Commands/DebugCommands.cs`: `vfh_crops`, `vfh_recipes` (no cheat): print the catalogs with levels.
- `src/VikingsForHire/Testing/FixturesWork.cs`: checks `crop_known <item>`, `recipe_known <item>`, `stock <item>`.
- `src/VikingsForHire/Plugin.cs`: register the catalogs.
- `test/alias_vfh.yaml`: `vfh_t_catalog` (VFH-FARM-0).
- `docs/test-checklist.md`: row VFH-FARM-0.

## Steps
1. **When:** both catalogs build on `PrefabManager.OnPrefabsRegistered` (in a world: `ZNetScene` and `ObjectDB` are complete, PlantEverything has edited its prefabs) and rebuild on `DataStore.Changed` (levels).
2. **Crops** (`CropCatalog`):
   - Every piece in the Cultivator's piece table (`ObjectDB` item `Cultivator` → `m_shared.m_buildPieces.m_pieces`) with a `Plant` component: `Plant` = sapling prefab, `Consumes` = `Piece.m_resources[0]` item and amount, grown prefab = `Plant.m_grownPrefabs[0]` → its `Pickable`: `Yields` = `m_itemPrefab`, `YieldPerPlant` = `PickableYield.Main`, `ExtraYields` from `m_extraDrops` (expected value). Also keep `Plant.m_biome`, `m_growRadius`, `m_needCultivatedGround`, the grown prefab name (to recognise ripe crops).
   - Every piece in that table that *is* a `Pickable` with `m_respawnTimeMinutes > 0` (PlantEverything's bushes, mushrooms…): `Regrowing = true`, `Plant` = the pickable prefab, `Yields` = its item, no `Consumes`.
   - Wild regrowing pickables not in the piece table (raspberry and blueberry bushes, mushrooms, thistle, cloudberries… without PlantEverything): every `ZNetScene` prefab with a `Pickable` whose `m_respawnTimeMinutes > 0` and whose `m_itemPrefab` is set becomes a `Regrowing` entry too (never planted, so no piece needed). Stones, flint and branches are left out (items `Stone`, `Flint`, `Wood`).
   - `Level` from `jobs.Farmer.cropLevels[Yields]` (default 1).
3. **`PickableYield.Main(p)`** = `p.m_dontScale ? p.m_amount : Max(p.m_minAmountScaled, Game.instance.ScaleDrops(p.m_itemPrefab, p.m_amount))`; extras from `m_extraDrops.GetDropListItems()` when picking (expected value `(min+max)/2 × chance` for planning). Never calls `RPC_Pick` (it needs a local player; null on a dedicated server).
4. **Kitchen** (`KitchenCatalog`):
   - **Stoves:** every piece prefab with a `CookingStation` (piece_cookingstation, piece_cookingstation_iron, piece_oven, modded ones): one `KitchenInfo` per `m_conversion` (from → to, `m_cookTime`), `StationKind.Stove`, `Level = stationLevels[prefab]` (default 1), `recipeLevels[to]` overrides.
   - **Crafting:** every `Recipe` in `ObjectDB.instance.m_recipes` whose `m_craftingStation` is `piece_cauldron`, `piece_preptable` or `piece_MeadCauldron` (matched by the station's `m_name`), enabled, with an `m_item`: `Output`, `OutputAmount = m_amount`, `Inputs` from `m_resources` (`m_amount` for quality 1), `StationLevelNeeded = m_minStationLevel`, `Level = stationLevels[station] + (m_minStationLevel - 1)` capped at 8, `recipeLevels[output]` overrides.
5. **`StockCounter.Count(home, radius)`** → `Stock { Dictionary<string,int> Chests; Dictionary<string,int> Growing }` (the planner takes them separately; the Orders tab shows their sum):
   - Chests: `ChestFinder.Find(home, radius)` contents, minus the usual reserves (`VfhConfig.ChestReserve` per chest, `keepInStorage`).
   - Growing: every `Plant` in the radius whose sapling is a catalog crop: `+YieldPerPlant` of its item (and extras rounded down); every unpicked ripe crop pickable counts too.
   - Cached per board for 5 s.
6. **`PlantMods`:** `IsPlantEverything` (`Chainloader.PluginInfos` has `advize.PlantEverything`), `IsPlantEasily` (`advize.PlantEasily`); `CropSpacing(growRadius)`: with PlantEasily loaded, `growRadius*2 + [Grid]ExtraCropSpacing` (read via `Instance.Config.TryGetEntry<float>("Grid","ExtraCropSpacing")`), else `growRadius*2`; `HarvestRadius`: PlantEasily's `[Harvesting]HarvestRadius` if loaded, else 3 m. Missing entries (dedicated servers don't bind them) fall back to the defaults.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.

## Test plan
- In game: `vfh_crops` lists carrot/seed carrot/turnip/…/barley/flax/oat/kale/poteitr with yields and levels, and (with PlantEverything) raspberry and blueberry bushes as regrowing; `vfh_recipes` lists cooked meat (spit), bread (oven), carrot soup (cauldron level 1) etc. with levels.
- Macro `vfh_t_catalog` (VFH-FARM-0): `crop_known Carrot == true`, `crop_known CarrotSeeds == true`, `recipe_known CookedMeat == true`, `recipe_known CarrotSoup == true`.

## Commit
`feat(farm,kitchen): crop and kitchen catalogs from the game's prefabs, stock counting`

## Rollback
Revert the commit; only debug commands use it until phase 05.

## As built
- Wild regrowing plants are only crops when their item is food or listed in `cropLevels` (so surtling core stands and the like aren't).
- Crops and pickables near a board are found through the loaded ZDOs by prefab hash (`FarmScan`), not a physics query.
