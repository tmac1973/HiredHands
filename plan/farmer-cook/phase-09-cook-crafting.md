# Phase 09 — The Cook: cauldron, prep table and mead ketill

**Depends on:** 02 (`KitchenPlanner`), 03, 04 (kitchen catalog), 05 (orders), 07 (`FarmState` protected items: seed reserve and seed-order planting stock), 08 (Cook, `KitchenState`, stay-near rule) · **Enables:** 10

## Goal
The Cook crafts kitchen recipes at the cauldron, the food preparation table and the mead ketill, like a player at the station: only what the station offers at its upgrade level, only when the station is usable (fire under the cauldron and ketill, a roof over the prep table), with ingredients from the chests (never the seed reserve or what the Farmer needs to plant for seed orders), one step of chaining (dough for bread), and the results put away.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Kitchen/CraftChore.cs` (new, `IChore`, kind `Craft`).
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: the Cook's loop gets `CraftChore` after `StovesChore`.
- `src/VikingsForHire/Localization/English.json`: `vfh_cook_craft` ("Making $1 at the $2"), `vfh_need_ingredient` ("$1: needs $2 more $3"), `vfh_need_station_level` ("$1: needs a level $2 $3"), `vfh_need_roof` ("$1: needs a roof").
- `src/VikingsForHire/Testing/FixturesKitchen.cs`: fixture `craftstation <prefab> [level] [tag]` (cauldron/ketill over a lit fire, prep table under a roof; `level` adds that many vanilla extensions within 5 m), checks `crafted_by_cook <item>` and `status_has <posted|hid> <text>` (true when the hireling's status line, in English, contains the text).
- `test/alias_vfh.yaml`: `vfh_t_cook_cauldron` (VFH-COOK-3), `vfh_t_cook_chain` (VFH-COOK-4), `vfh_t_cook_protect` (VFH-COOK-5).
- `docs/test-checklist.md`: rows VFH-COOK-3 to 5.

## Steps
1. **Usable station:** a `CraftingStation` piece in the radius whose `m_name` matches the recipe's station, ward-usable; `GetLevel()` ≥ the recipe's `m_minStationLevel` (else `Missing = vfh_need_station_level`); fire stations need `m_haveFire` (else `vfh_need_fire`); the prep table needs cover ≥ 0.7 (`Cover.GetCoverForPoint` at the station; else `vfh_need_roof`). Never `CheckUsable` (it needs a local player).
2. **Job:** for the `KitchenTask` (a craft): batches = the planner's count, limited by cargo room for the inputs and the output.
   - Fetch every input (`TakeFromChest`, reserves: usual + protected items).
   - Approach the station; per batch: 2.5 s at the station (facing it), then remove each input's `m_amount` from cargo by shared name and `AddItem(output, OutputAmount, quality 1, …, dropIfFullInv: false)` (as `InventoryGui.DoCrafting` does, without a player), play the station's `m_craftItemEffects`.
   - At the end, `DeliverPending`; leftovers go back by the leftover rule.
   - **Chained step:** when the task has a `Then` (it made dough for bread), `KitchenState` pins `Then` for this Cook (recomputing every 5 s doesn't replace a pinned task) until it's done, fails, or 120 s pass; the output stays in cargo instead of being delivered, and the Cook goes straight on to `Then`. Phase 08's Load counts carried raw items first, so it loads the carried dough into the oven without fetching. If the pin expires, the dough is a leftover and goes back to a chest.
3. **Stay near the stoves (phase 08)** still applies: a craft job at a station more than 10 m from a stove it's cooking at waits until the stove's food is off.
4. **Urgency:** crafting 0.5 (below Take off, Fuel and Load).
5. **Logging:** `cook.craft` (Info): item, batches, station, level.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.

## Test plan
- `vfh_t_cook_cauldron` (VFH-COOK-3): `post Cook 3 30 free`, `craftstation piece_cauldron 1 C`, chest with Carrot 10, Mushroom 10, CarrotSoup 1; order `CarrotSoup 3 kitchen`. Assert `deposited … CarrotSoup >= 3` within 240 s; ingredients used match the recipe.
- `vfh_t_cook_chain` (VFH-COOK-4): `post Cook 6 30 free`, `craftstation piece_preptable 1 P` (under a roof; bread dough is a level 1 prep table recipe, BarleyFlour 10 → BreadDough 2), `stove piece_oven fuel 5 O`, chest with BarleyFlour 20, Bread 1; order `Bread 2 kitchen`. Assert dough is made then baked: `deposited … Bread >= 2` within 400 s.
- `vfh_t_cook_protect` (VFH-COOK-5): orders `CarrotSeeds 10 seed` (0 seeds in stock) and `CarrotSoup 5 kitchen`; chest with Carrot 3, Mushroom 10. `field 8 8`, `post Farmer 2 30 free`, then `craftstation piece_cauldron 1 C` and `post Cook 2 30 free` (posted last, so `posted` is the Cook). The Farmer needs the 3 carrots for seed carrots; assert the Cook makes no carrot soup while the seed order is short (`crafted_by_cook CarrotSoup == 0` after 120 s) and `status_has posted Carrot == true` (its status names the missing carrots).
- A cauldron without fire: `Missing` says "no fire"; a prep table without a roof: "needs a roof".

## Commit
`feat(cook): crafting at the cauldron, prep table and mead ketill to kitchen orders`

## Rollback
Revert the commit; the Cook runs stoves only (phase 08).
