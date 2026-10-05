# Phase 03 — Fires and lights

**Depends on:** 01 (`ChoreUrgency.Fire`, `StewardFireRefillFraction`, `StewardCompat`), 02 (`IChore`, `StewardContext`, `StewardSteps`) · **Enables:** 08

## Goal
A Steward from level 1 keeps fires fuelled: every lit or unlit vanilla `Fireplace` with a fuel item in its work radius. That covers fire pits, hearths, braziers, standing and wall torches, sconces and hot tubs. When a fire's fuel drops below the refill fraction, it fetches that fire's fuel item from chests (respecting reserves) and adds fuel until the fire is full, through the fire's own RPC, as a player would.

Fires are skipped:
- when Torches Eternal is installed (it keeps every fire full);
- for any single fire with `m_infiniteFuel`.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Steward/FiresChore.cs` (new).
- `src/VikingsForHire/Hirelings/Work/Steward/StewardBehaviour.cs`: adds `FiresChore` to its chores.
- `src/VikingsForHire/Localization/English.json`: `vfh_steward_fuel` ("Fuelling $1"), `vfh_need_fuel` ("$1: no $2 in any chest").
- `src/VikingsForHire/Testing/FixturesWork.cs`:
  - fixture `fire <prefab> <fuel> [tag]`: places a fireplace prefab 6 m from the board and sets its fuel (`RPC_SetFuelAmount`);
  - check `fire_fuel <tag>`: its fuel now, as a fraction of its maximum (0..1);
  - check `fuel_added <tag>`: fuel items Stewards have put into that fire since `vfh_test_begin` (counted in `FiresChore`).
- `test/alias_vfh.yaml`: `vfh_t_fires`, `vfh_t_chore_urgency`.
- `docs/test-checklist.md`: rows VFH-CHORE-4 and VFH-CHORE-9 (urgency).

## Steps
1. **Candidates.**
   - Every `Fireplace` in `Piece.GetAllPiecesInRadius(home, radius)` (its piece's component, or a child's) qualifies if it:
     - has a `ZNetView` with a valid ZDO;
     - has `m_fuelItem` set;
     - doesn't have `m_infiniteFuel`;
     - was built by a player (`GetCreator() != 0`);
     - is usable under wards by the board's owner (`DoorRules`-style check with `PrivateArea`).
   - Fuel is the ZDO's `fuel` float against `m_maxFuel`, and urgency is `ChoreUrgency.Fire(fuel, max, StewardFireRefillFraction)`.
   - A fire whose fuel item isn't in any chest above reserves (`StewardContext` stock) isn't a candidate. Instead it sets `Missing = vfh_need_fuel(fire, item)` when it would otherwise have scored.
2. **Job:**
   1. Work out the need: `ceil(max - fuel)` items, capped by what the chests hold above reserves and by free cargo slots.
   2. Fetch them with `StewardSteps.FetchFromChests`, unless cargo already holds them.
   3. Walk to the fire's side with `StewardSteps.Approach`.
   4. Add fuel one item at a time: remove one from cargo, `m_nview.InvokeRPC("RPC_AddFuel")`. Repeat every 0.3 s (the vanilla `m_holdRepeatInterval` feel) until the fire is full or cargo has none left. Play the fire's `m_fuelAddedEffects` at the fire.
   5. Done.
   - **Multiple fires:** while fetching for one fire, it fetches enough for every other fire with the same fuel item scoring above 0, up to cargo, then visits them nearest-first before finishing. One trip tops up a whole hall of torches.
   - **Leftovers:** leftover fuel stays in cargo for the next fire. `SmelterDeliveryPolicy`'s 5-minute leftover rule takes it back to a chest if nothing uses it.
3. **Reservation:** `Reservations.Claim(fire, hid)`, so two Stewards don't fuel the same fire.
4. **Logging:** Debug `steward.fire` (fire, before/after fuel, items used); Info `steward.job` as for every chore.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- `vfh_t_fires` passes in single player with `cfg_set StewardIgnoreOtherMods true` (and `false` at the end), since the dev profile has Torches Eternal.

## Test plan
- **`vfh_t_fires` (VFH-CHORE-4):**
  - Setup:
    - a board at level 1 and a level 1 Steward;
    - `fire hearth 1 H` and `fire piece_groundtorch_wood 0 T`;
    - a chest with Wood 100 and Resin 20 (`keepInStorage` Wood 50 leaves 50);
    - `cfg_set StewardIgnoreOtherMods true`.
  - Assert `fire_fuel H >= 0.95` and `fire_fuel T >= 0.95` within 180 s.
  - Then `cfg_set StewardIgnoreOtherMods false`, place `fire hearth 1 H2`, wait 60 s, and assert `fuel_added H2 == 0`. Torches Eternal is installed, so the Steward steps aside (Torches Eternal fills that hearth itself). The log has `steward.compat fires=TorchesEternal`.
- **`vfh_t_chore_urgency` (VFH-CHORE-9), two chores waiting:**
  - Setup:
    - `cfg_set StewardIgnoreOtherMods true`;
    - a board at level 2 and a level 2 Steward;
    - `fire hearth 0 H`: empty, urgency 1;
    - `stations smelter` with a chest of copper ore and coal: an empty smelter, urgency 0.9;
    - the smelter is placed nearer the board than the hearth, so the distance bias favours it.
  - Assert `steward_chore == Fires` within 20 s of the Steward arriving (the first job), then `station smelter queue > 0` within 180 s (it does the smelter next).
  - End with `cfg_set StewardIgnoreOtherMods false`.
- **By hand, with Torches Eternal removed:** a hot tub and wall torches in a house upstairs are fuelled, through the door and up the stairs. The panel shows "Fires: on", and with Torches Eternal back, "handled by TorchesEternal".

## Commit
`feat(steward): fuel fires, torches and hot tubs from chests`

## Rollback
Revert the commit: the chore is gone and the panel row shows nothing to do. Or set `StewardFires = false` on the server.
