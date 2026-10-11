# Phase 03 — Staff and robes

**Depends on:** 02 · **Enables:** 04 (it needs the staff in hand), 06

## Goal
A wizard is dressed and armed from its effective data: the staff for its level in its right hand, and mage robes with a
cape. Anything missing falls back, so no wizard is ever left empty-handed: a staff that doesn't exist becomes the
no-mod data's staff for that level; an outfit piece that doesn't exist becomes the shared armour for that slot (cape:
none). With Wizardry removed, existing wizards get the vanilla staff and stay on the roster.

## Files touched
- `src/VikingsForHire/Core/GearRules.cs` (new, Unity-free): which items to equip, given the level, the effective and
  no-mod data and an "item exists" test.
- `src/VikingsForHire/Hirelings/GearApplier.cs`: weapons and armour through `LevelRules.Gear/Outfit` and `GearRules`;
  equips the cape slot; logs `gear.fallback` when one was used.
- `src/VikingsForHire/Hirelings/Hireling.cs`: no change (the ammo and sidearm code is behind `Job ==
  JobType.GuardRanged`, which a wizard isn't).
- `src/VikingsForHire/Testing/FixturesHireling.cs`: field `gear.cape` on the `hireling` check; fixture `regear` (re-apply
  the posted hireling's gear now, as on load).
- `test/alias_vfh.yaml`: row VFH-WIZ-2.
- `tests/VikingsForHire.Tests/WizardRulesTests.cs`: gear choice tests.

## Steps
1. `GearRules.Weapon(level, effectiveGear, noModGear, exists)`: the effective set's main for the level; if that item
   doesn't exist, the no-mod set's main for the level; if that doesn't exist either, `StaffFireball`; and if even that
   is missing (a broken game), `Club`, so the hireling always holds something. (The data file covers every level; if it
   has no set at all for a level, the nearest lower level's set is used, as `GearApplier` already does for other jobs.)
2. `GearRules.Armor(level, effectiveOutfit, sharedSets, exists)`: per slot (helmet, chest, legs, cape), the outfit's
   item if set and existing, else the shared set's item for that slot (cape: none).
3. `GearApplier` uses `GearRules.Armor` for every job (jobs without an outfit get the shared sets, as today) and
   `GearRules.Weapon` for wizards only; every other job keeps today's weapon code and its fallbacks unchanged.
4. Gear is applied when a hireling loads (existing behaviour), so a wizard whose Wizardry staff is gone gets the vanilla
   staff the next time it loads.
5. Unit tests: with Wizardry, L2 gets `StaffBlackforest_TW` and the BlackForest robes and cape; without it, a level 3
   wizard gets `StaffFireball` and the shared armour; L7 gets `StaffLightning` either way; a missing outfit item falls
   back to the shared slot; a staff that doesn't exist falls back to the no-mod staff, then `StaffFireball`, then `Club`.
6. Row VFH-WIZ-2: `base; board_here; board_level 2; stock_board 2000 500; post Wizard 2 20; roster Active == 1` → `hireling posted gear.right == StaffBlackforest_TW`, `gear.chest ==
   ArmorSpellSlingerChest_BlackForest_TW`, `gear.cape == CapeSpellslinger_Blackforest_TW`; `mods Therzie.Wizardry off;
   regear` → `gear.right == StaffFireball`, `gear.chest` is the shared level 2 chest (`ArmorLeatherChest`); `roster
   Active == 1` (still on the roster); `mods Therzie.Wizardry real`; cleanup.

## Build gate
- `dotnet build -c Release`; `dotnet test tests/VikingsForHire.Tests`.

## Test plan
- Unit tests above; row VFH-WIZ-2; regression `hire4 hire5 recruit1` (gear and snapshots).

## Commit
`feat(wizard): staff and mage robes per level from the effective job data; fallbacks to the no-mod staff and shared armour`

## Rollback
Revert; jobs without outfits or overrides are unaffected either way.
