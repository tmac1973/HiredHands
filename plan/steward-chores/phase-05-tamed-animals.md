# Phase 05 — Feeding tamed animals

**Depends on:** 01 (`ChoreUrgency.Animal`, level `animals` 2, `StewardCompat` for PetPantry), 02 (`IChore`, `StewardSteps`) · **Enables:** 09

## Goal
From level 2, a Steward feeds hungry tamed animals inside its work radius. It takes one item each animal eats from a chest and drops it right in front of the animal, which eats it the vanilla way. Tamed animals only eat items lying on the ground, which they look for themselves. AzuAutoStore must not sweep that food into a chest first. With PetPantry installed (animals eat straight from chests), the chore steps aside.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Steward/AnimalsChore.cs` (new).
- `src/VikingsForHire/Hirelings/Work/Steward/StewardBehaviour.cs`: adds it.
- `src/VikingsForHire/Compat/CompatPatcher.cs`: when AzuAutoStore is loaded, a Harmony prefix on `AzuAutoStore.Util.Functions.CheckItemDropInstanceAndStore(ItemDrop)`, found by reflection like the existing Azu patches. It returns false (skips) for an item whose ZDO has `vfh_feed` set. It's reported in `CompatPatcher.Status` like the others; if the method isn't found, a warning is logged and feeding still works without it, but Azu may take the food.
- `src/VikingsForHire/Localization/English.json`: `vfh_steward_feed` ("Feeding $1"), `vfh_need_food` ("$1: no food it eats in any chest").
- `src/VikingsForHire/Testing/FixturesWork.cs`:
  - fixture `tame <prefab> [tag]`: spawns the creature 6 m from the board, tames it (`Character.SetTamed(true)`), and makes it hungry by setting its ZDO `ZDOVars.s_tameLastFeeding` to 0;
  - check `animal_hungry <tag>` (`Tameable.IsHungry()`);
  - check `fed_by_steward <tag>`: items Stewards have dropped for that animal since `vfh_test_begin` (counted in `AnimalsChore`).
- `test/alias_vfh.yaml`: `vfh_t_animals`.
- `docs/test-checklist.md`: row VFH-CHORE-7.

## Steps
1. **Candidates:** every `Character` in the radius (`Character.GetAllCharacters()`, filtered by distance from home) that:
   - is tamed (`IsTamed()`);
   - isn't a hireling or a player;
   - has a `Tameable` and a `MonsterAI` with non-empty `m_consumeItems`;
   - is hungry (`Tameable.IsHungry()`);
   - is usable by the board's owner under wards (checked at the animal's position).

   Urgency is `ChoreUrgency.Animal(true)`. Its food is the first entry of `m_consumeItems` that chests hold above reserves; with none, it isn't a candidate and sets `Missing = vfh_need_food(animal)`.
2. **Job:**
   1. Fetch one of each needed food for every hungry animal it will visit this trip (nearest-first, up to cargo).
   2. For each animal: approach to within 2 m.
   3. Drop one food item 0.8 m in front of the animal (`ItemDrop.DropItem` at the animal's forward), set `vfh_feed = true` on the drop's ZDO, and take it out of cargo.
   4. Wait up to 15 s for the animal to stop being hungry, which means it ate.
   5. If it hasn't eaten, pick the item back up into cargo. Log Debug `steward.feed_refused` (an animal that can't reach it, or isn't actually hungry).
   6. Next animal, then done. Leftover food stays in cargo, and the leftover rule returns it to a chest after 5 min.
3. **Not fed:**
   - wild animals, other players' summons and anything outside the radius;
   - animals in a fight or following a player: Valheim's `MonsterAI` won't eat then anyway, so the 15 s wait just times out once, and that animal is skipped for 5 min (`Reservations.Skip`).
4. **PetPantry:** `StewardCompat.HandledBy(Animals)` returns `PetPantry`. The panel row shows it and the chore never runs.
5. **Logging:** Debug `steward.feed` (animal, item, eaten), plus `steward.job`.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- `vfh_t_animals` passes in single player with `cfg_set StewardIgnoreOtherMods true` (the dev profile has PetPantry).

## Test plan
- **`vfh_t_animals` (VFH-CHORE-7):**
  - Setup:
    - `cfg_set StewardIgnoreOtherMods true`;
    - a board at level 2 and a level 2 Steward;
    - `tame Boar B` and a chest with Raspberry 10 (a boar food), with AzuAutoStore on (the default `chest` fixture registers with Azu, which checks the `vfh_feed` guard).
  - Assert `animal_hungry B == false` within 180 s.
  - Then `cfg_set StewardIgnoreOtherMods false`, `tame Boar B2`, wait 60 s, and assert `fed_by_steward B2 == 0`: with PetPantry installed, the Steward steps aside (PetPantry may or may not feed B2 itself). The log has `steward.compat animals=PetPantry`.
- **By hand:** a pen of tamed boars and wolves with a food chest. The Steward feeds them as they get hungry, and no food ends up in Azu's chests.

## Commit
`feat(steward): feed hungry tamed animals from chests (steps aside for PetPantry)`

## Rollback
Revert the commit, or set `StewardAnimals = false` on the server. The Azu prefix only affects items marked `vfh_feed`, so reverting it is safe.
