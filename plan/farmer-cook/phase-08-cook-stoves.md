# Phase 08 — The Cook: ladle and stoves

**Depends on:** 02 (`KitchenPlanner`), 03 (`ChoreLoop`, Cook chore kinds), 04 (kitchen catalog, `StockCounter`), 05 (orders), 07 (`FarmState`'s protected items) · **Enables:** 09 (crafting joins the same kitchen state), 10

## Goal
A hired Cook holds a homemade ladle and runs the stoves: spits over fires (cooking station, iron cooking station) and the oven. For short kitchen orders it loads raw food from the chests, keeps the oven fuelled, stays near the stoves while anything is cooking, and takes food off when it's done so nothing burns, then puts it away.

## Files touched
- `src/VikingsForHire/Hirelings/Gear/MeshBuilder.cs` (new): the broom's tube/cap mesh helpers, moved out of `BroomItem` (plus a hemisphere "bowl").
- `src/VikingsForHire/Hirelings/BroomItem.cs`: uses `MeshBuilder` (no visible change).
- `src/VikingsForHire/Hirelings/Gear/LadleItem.cs` (new): `VFH_Ladle`, club stats, a built ladle: a 1.0 m wooden handle (wood_pole material) and a 0.12 m bowl at the far end (an iron material from `piece_cookingstation_iron`, falling back to the wood), pointed like the club; `DataStore.ItemExists` accepts it.
- `src/VikingsForHire/Core/Data/DefaultData.cs`: Cook `gear` → `VFH_Ladle` at every level.
- `src/VikingsForHire/Hirelings/Work/Kitchen/KitchenState.cs` (new): per board, the current `KitchenTask` (recomputed at most every 5 s), with `FarmState`'s protected items (seed reserve plus the produce planned for seed orders) passed to `KitchenPlanner`.
- `src/VikingsForHire/Hirelings/Work/Kitchen/StovesChore.cs` (new, `IChore`, kind `Stoves`).
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: the Cook's loop gets `StovesChore`.
- `src/VikingsForHire/Localization/English.json`: `vfh_cook_load` ("Cooking $1"), `vfh_cook_takeoff` ("Taking $1 off"), `vfh_cook_fuel` ("Fuelling $1"), `vfh_need_fire` ("$1: no fire"), `vfh_cook_idle` ("Kitchen tidy").
- `src/VikingsForHire/Testing/FixturesKitchen.cs` (new): fixture `stove <prefab> [lit] [fuel <n>] [tag]` (a cooking station over a campfire, lit with `lit`; an oven with `n` wood, default 5), checks `stove_slots <tag> <empty|cooking|done|burnt>` (how many slots are in that state) and `stove_fuel <tag>` (an oven's fuel).
- `test/alias_vfh.yaml`: `vfh_t_cook_spit` (VFH-COOK-1), `vfh_t_cook_oven` (VFH-COOK-2).
- `docs/test-checklist.md`: rows VFH-COOK-1 and 2.

## Steps
1. **Stoves in reach:** `CookingStation` pieces in the work radius, ward-usable, whose prefab's Cook level ≤ the Cook's level. Slot state from the ZDO: `"slot"+i` (item name; empty = free), `"slotstatus"+i` (0 cooking, 1 done, 2 burnt).
2. **Jobs, by urgency:**
   - **Take off** (1.0): a slot is done (status 1) or burnt (2): walk there, `InvokeRPC("RPC_RemoveDoneItem", userPos, 1)` per slot (it drops the item at the station), then pick the drops up with `WorkSteps.PickUpDrops` (burnt items are Coal: picked up and delivered like anything else); then `DeliverPending`.
   - **Fuel the oven** (0.7): an oven below half fuel with Wood in the chests: fetch, `RPC_AddFuel` once per wood up to `m_maxFuel - 1` (the RPC doesn't cap).
   - **Load** (0.6): the `KitchenTask` is a stove conversion and a station of that prefab has free slots and heat (spits: `IsFireLit()`; oven: fuel > 0): carried raw items count first; fetch only the rest, up to the free-slot count of the raw item (usual reserves plus the protected items: the Cook's `WorkContext.ExtraReserve` is set from `KitchenState`'s protected items), `InvokeRPC("RPC_AddItem", rawPrefab, false)` per slot. A spit with no fire sets `Missing = vfh_need_fire`.
3. **Stay near the stoves:** while any stove in the radius has a cooking slot that this Cook loaded (tracked per station), the Cook only takes jobs whose target is within 10 m of the nearest such stove, and a Take-off is always taken first. (Its own loads only: food a player put on isn't its responsibility to babysit, but done or burnt items there are still taken off.)
4. **Outputs:** cooked items are chore outputs (`ChoreOutputs`), delivered to chests holding them; `PauseWhenStorageFull` stops loading a conversion whose output has no room.
5. **Fires:** the spit's fire is the Steward's (Fires chore) or the player's; the Cook doesn't fuel campfires, it reports "no fire".

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.

## Test plan
- `vfh_t_cook_spit` (VFH-COOK-1): board level 2, `stove piece_cookingstation lit S`, chest with RawMeat 10 and CookedMeat 1, order `CookedMeat 4 kitchen`, post Cook 2. Assert `deposited … CookedMeat >= 4` within 300 s and `ground_items Coal 30 == 0` and `stove_slots S burnt == 0`.
- `vfh_t_cook_oven` (VFH-COOK-2): `post Cook 5 30 free`, `stove piece_oven fuel 1 O`, chest with BreadDough 4, Bread 1, Wood 20; order `Bread 3 kitchen`. Assert `stove_fuel O > 1` within 120 s and `deposited … Bread >= 3` within 300 s.
- By hand: the Cook holds a ladle; while meat is on the spit it doesn't walk off to the far side of the base.

## Commit
`feat(cook): Cook with a ladle runs spits and the oven to kitchen orders without burning anything`

## Rollback
Revert the commit; the Cook idles (phase 03).
