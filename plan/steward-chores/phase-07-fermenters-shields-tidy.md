# Phase 07 — Fermenters, shield generators and tidying up

**Depends on:** 01 (`ChoreKind`, `ChoreKeys`, `ChoreRules`, `ChoreUrgency`, settings section, data `choreLevels`), 02 (`IChore`, `StewardContext`, `StewardSteps`, `StewardBehaviour.ServerAllows`, the chore loop, and the panel built from `ChoreKeys.All`), 03 (`vfh_need_fuel`, `fire` fixture), 05 (`vfh_feed` marker) · **Enables:** 09

## Goal
Three more chores, added the same way as phases 03–06:
- **Fermenters, level 4 (Mountains):** load an empty fermenter with a mead base from chests; tap it when ready; deliver the meads to chests.
- **Shield generators, level 7 (Ashlands):** keep them fed with their fuel (bones) from chests.
- **Tidying up, level 1:** pick up items lying on the ground in the work radius and put them away in chests that already hold them.
  - It's the lowest-priority chore: any other job comes first.
  - It's not stepped aside for AzuAutoStore, because the Steward's work radius is usually larger than Azu's chest range, and covers bases without Azu.

## Files touched
- `src/VikingsForHire/Core/Chores/ChoreKind.cs`: appends `Fermenters`, `Shields` and `Tidy` to `ChoreKind` (after `Repairs`, so earlier kinds keep their order), with keys `fermenters`, `shields` and `tidy`.
- `src/VikingsForHire/Core/Chores/ChoreUrgency.cs`:
  - `Fermenter(ready, empty)`: ready to tap 0.6; empty with a base available 0.45; else 0.
  - `Shield(fuel, max)`: as `Fire` with the refill fraction.
  - `Tidy()`: a fixed 0.15, below every other chore's lowest non-zero urgency (0.3).
- `src/VikingsForHire/Core/Data/DefaultData.cs`: `choreLevels` gains `tidy 1`, `fermenters 4` and `shields 7`.
- `src/VikingsForHire/Config/VfhConfig.cs`, *Steward* section:
  - `StewardFermenters`, `StewardShields`, `StewardTidy` (bool, true);
  - `StewardTidyMinSeconds` (float, 60): "Leave items alone until they've been on the ground this long (so it doesn't grab what a player just dropped)".
- `src/VikingsForHire/Hirelings/Work/Steward/FermentersChore.cs`, `ShieldsChore.cs`, `TidyChore.cs` (new).
- `src/VikingsForHire/Hirelings/Work/Steward/StewardBehaviour.cs`:
  - adds the three chores;
  - `ServerAllows` maps the three settings.
- `src/VikingsForHire/Hirelings/Work/SmelterDeliveryPolicy.cs`:
  - `StewardOutputs` gains the fermenters' outputs (each `m_conversion` `m_to`);
  - tidied items are delivered too: anything the tidy job picked up counts as an output.
- `src/VikingsForHire/Localization/English.json`:
  - `vfh_chore_fermenters`, `vfh_chore_shields`, `vfh_chore_tidy`;
  - `vfh_steward_ferment` ("Tending $1"), `vfh_steward_shield` ("Feeding $1"), `vfh_steward_tidy` ("Tidying up");
  - `vfh_need_base` ("$1: no mead base in any chest").
- `src/VikingsForHire/Testing/FixturesWork.cs`:
  - fixture `fermenter <state> [tag]` (`empty` | `ready`): places a `fermenter` 6 m from the board under the `roof_over` fixture's thatch roof (wood floors are "leaky" and don't count) (an uncovered fermenter is Exposed and never ferments); for `ready`, loads it with a mead base and sets its start time back past `m_fermentationDuration`;
  - fixture `shieldgen <fuel> [tag]`: places a `piece_shieldgenerator` with that fuel;
  - fixture `litter <item> <count> [tag]`: drops items 10 m from the board, with their spawn time set back past `StewardTidyMinSeconds`;
  - checks `fermenter_status <tag>`, `shield_fuel <tag>` and `ground_items <item> [radius]`.
- `test/alias_vfh.yaml`: `vfh_t_fermenter`, `vfh_t_shield`, `vfh_t_tidy`.
- `docs/test-checklist.md`: rows VFH-CHORE-10 (fermenter), VFH-CHORE-11 (shield), VFH-CHORE-12 (tidy).

## Steps
1. **Fermenters** (`Fermenter` component, prefab `fermenter`, built by a player, with ward access).
   - **State:** from `GetStatus()` (Empty, Fermenting, Ready, Exposed).
   - **Ready:** urgency 0.6.
     - Approach, then `RPC_Tap` (as `Fermenter.Interact` does when ready).
     - After 0.5 s, pick up the meads from its output point with `StewardSteps.PickUpDrops` (`m_conversion` outputs), then set `DeliverPending`.
   - **Empty:** urgency 0.45, if any mead base listed in its `m_conversion` (`m_from`) is available in the chests above reserves.
     - Pick the base with the most stock, fetch one, approach, and add it the way `Fermenter.AddItem` does: `m_nview.InvokeRPC("RPC_AddItem", basePrefabName.GetStableHashCode(), false)` (the prefab name's stable hash, and "not cheated"). Remove the base from cargo.
     - With no base available, `Missing = vfh_need_base(fermenter)`.
   - **Exposed** (not under a roof): skip it; the player has to fix the placement.
2. **Shield generators** (`ShieldGenerator` component, prefab `piece_shieldgenerator`).
   - **Need:** fuel from `GetFuel()` against `m_maxFuel`, scored with `ChoreUrgency.Shield`.
   - **Fuel item:** any of its `m_fuelItems` the chests hold above reserves. Bone fragments first, by the list's order.
   - **Job:** fetch enough to fill it (cargo permitting), approach, then `RPC_AddFuel` once per item at 0.3 s intervals, removing each from cargo and playing `m_fuelAddedEffects` at the generator.
   - With no fuel in any chest, `Missing = vfh_need_fuel(generator, bone fragments)`, reusing phase 03's string.
3. **Tidying up:**
   - **Candidates:** every `ItemDrop` in the work radius that:
     - has a valid ZDO and is on the ground (not in a container);
     - has been on the ground at least `StewardTidyMinSeconds` (`GetTimeSinceSpawned()`);
     - is not the board's drop pile (`DropPile.Tag`);
     - is not marked `vfh_feed` (phase 05's animal food);
     - is not within 3 m of any player;
     - is an item some chest in the radius already holds (the delivery rule): it is only picked up if it has somewhere to go, so it never just carries things to the pile.
   - **Job:** one job is one trip, with urgency `ChoreUrgency.Tidy()`.
     - It visits up to 5 drop spots, nearest-first, each within 15 m of the last, while cargo has room.
     - At each spot it walks there and runs `StewardSteps.PickUpDrops` for every candidate kind within 3 m.
     - Then the job is Done. The tidied items are Steward outputs (`SmelterDeliveryPolicy`), so `DeliverBehaviour` (priority 300) takes them to their chests straight away, before the next survey.
     - Every other chore outranks tidying at each survey, so a trip never delays real work by more than one trip.
   - **Not tidied:** anything outside the radius, or an item no chest holds. Each pick-up is logged at Debug (`steward.tidy`).
4. **Panel:** the three new rows appear automatically (rows come from `ChoreKeys.All`).

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes, with `ChoreRulesTests` and `ChoreUrgencyTests` extended for the new keys and scores, plus `Tidy` below every other chore's lowest score.
- `vfh_t_fermenter`, `vfh_t_shield` and `vfh_t_tidy` pass in single player.

## Test plan
- **`vfh_t_fermenter` (VFH-CHORE-10):** a board at level 4, a level 4 Steward, `fermenter ready F`, and `chest meads MeadHealthMinor 1 noazu`.
  - Assert `fermenter_status F == Empty` within 120 s, then `deposited meads MeadHealthMinor > 0` within 180 s.
  - Then add a chest with `MeadBaseHealthMinor 2`; assert `fermenter_status F == Fermenting` within 120 s.
- **`vfh_t_shield` (VFH-CHORE-11):** a board at level 7, a level 7 Steward, `shieldgen 0 G`, and a chest with `BoneFragments 40`.
  - Assert `shield_fuel G > 0` within 180 s.
- **`vfh_t_tidy` (VFH-CHORE-12):** a board at level 1, a level 1 Steward, `chest tidy Wood 1 noazu`, and `litter Wood 20`. Assert `ground_items Wood 30 == 0` within 180 s and `deposited tidy Wood > 0`.
  - **Priority check:** a second run adds `fire hearth 0 H` with `cfg_set StewardIgnoreOtherMods true`. Assert `steward_chore == Fires` first, before any tidying, and end with `cfg_set StewardIgnoreOtherMods false`.
- **By hand:** at the live-copy base, items dropped by a player stay put for a minute, then end up in the matching chests. With AzuAutoStore, items inside Azu's range are usually gone before the Steward gets there, which is fine.

## Commit
`feat(steward): fermenters, shield generators, and tidying up items on the ground`

## Rollback
Revert the commit, or set `StewardFermenters`, `StewardShields` or `StewardTidy` to false on the server.

## As built
- **Older files:** a data file with `choreLevels` but without a chore added later (fermenters, shields, tidy) gets that chore's default level from `DataDefaults.Migrate`, not level 1.
- **Shield generators:** their `RPC_AddFuel` doesn't stop at full, so the Steward stops once fuel is within 1 of the maximum. The RPC plays its own effect.
- **Fermenter loading:** `RPC_AddItem(hash of the base's prefab name, false)`, as `Fermenter.AddItem` does. The `fermenter ready` fixture uses the minor healing mead base when the fermenter takes it, so the test can seed a chest for `MeadHealthMinor`.
- **Tidied items:** delivered by setting `DeliverPending` at the end of the trip, not by adding them to `StewardOutputs`. Otherwise tidying up wood would make the Steward hand back the wood it fetched for fires.
- **Test setup:** `litter` sets the items' spawn time 10 minutes back, so they count as long on the ground. The tidy test's priority check uses a resin torch, because the wood reserve (`keepInStorage` Wood 50) would leave no wood for a hearth.
