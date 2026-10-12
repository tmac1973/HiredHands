# Phase 07 — Pen safety: over the fence first, careful gates, escapes reported

**Depends on:** 03 (feeding), 05 (slaughter, drop collecting), 06 (eggs), 02 (herd scan) · **Enables:** 08

## Goal
Animals are kept in pens, and a boar that gets out can't be led back. The Rancher works from outside the fence whenever
he can reach, goes in only when he can't, and then handles the gate so nothing slips out. Every hireling's door handling
gets the same care near tames. An animal of the herd found outside its pen is reported.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Chores/WorkSteps.cs` — reaching a creature (an animal), a building piece whose top
  is below the eye (a fence, a low wall) doesn't block the line: `PieceBetween(..., overLowPieces)` for creature
  targets in `WithinReach` and `UsableFrom`.
- `src/VikingsForHire/Hirelings/DoorHelper.cs` — before opening a door, wait (up to 20 s, then go ahead) while a tamed
  animal is within 3 m of it; once through a door with a tame within 6 m of it, close it as soon as the hireling is
  1.5 m past it (instead of 3 m and 2.5 s). Logged `door.waited_for_animals`, `door.closed_quick`.
- `src/VikingsForHire/Hirelings/Work/Ranch/HerdScan.cs` — `Enclosed(position)`: 8 horizontal rays at 0.5 m height
  hit a building piece within 15 m on every side. Each herd animal's last enclosed state is remembered by its ZDO id;
  one that was enclosed and now isn't is `Escaped`.
- `src/VikingsForHire/Core/Orders/RanchModel.cs`, `RanchPlanner.cs` — `HerdAnimal.Escaped`; reason
  `$vfh_ranch_escaped|<kind>|<count>` on that kind's Herd order (first, before breeding reasons); escaped animals are
  still counted and may still be slaughtered.
- `src/VikingsForHire/Localization/English.json` — `vfh_ranch_escaped`.
- `src/VikingsForHire/Testing/FixturesWork.cs` — fixture `pen <gate tag> [size] [<prefab> <tag> <edge|mid|gate>]...`: a
  square pen of half walls (`wood_wall_half`, below eye height) with a `wood_gate` on the board's side, 14 m out, and
  tamed hungry animals placed in it; fixture `move_animal <tag> <dx> <dz>`; checks `door_opened <tag>` (times
  hirelings opened it, counted in `DoorHelper.OpenedCount`) and `outside_pen <kind>` (herd animals not walled in).
- `tests/VikingsForHire.Tests/RanchPlannerTests.cs` — escaped reason.
- `test/alias_vfh.yaml` — rows VFH-RANCH-12, -13, -14.

## Steps
1. **Over the fence.** In `WithinReach`/`UsableFrom`, when the target is a `Character`, a blocking hit whose piece's
   collider top (`bounds.max.y`) is below the eye's height minus 0.2 m is ignored. Approach spots are already only
   those the hireling can walk to, so with the gate shut it stands outside and reaches over.
2. **Gates.** In `DoorHelper`, opening a door: if a tamed, non-hireling character is within 3 m of the door, halt and
   wait (re-check every 0.25 s, at most 20 s, then open anyway and log it). Closing: if a tame is within 6 m of the
   door, close once 1.5 m past it, with no delay.
3. **Escapes.** `HerdScan` marks `Escaped` for an animal remembered as enclosed and not enclosed now (an animal never
   seen enclosed is never escaped: free-range herds are fine). The planner reports it on the kind's Herd order.

## Verification
- `dotnet test` passes (escaped reason).
- Row VFH-RANCH-12 (over the fence): `pen G 6 Boar B edge`, food in a chest, a Rancher: `animal_hungry B == false`,
  `door_opened G == 0`, `outside_pen Boar == 0`.
- Row VFH-RANCH-13 (gates): `pen G 10 Boar M mid Boar D gate`: M is fed (he had to go in), then `outside_pen Boar == 0`
  and the gate is shut.
- Row VFH-RANCH-14 (escape): `pen G 6 Boar B mid Boar C mid`, a pool of 2, then `move_animal B 0 14`:
  `why has vfh_ranch_escaped|Boar == true`.
- Existing door rows (`vfh_t_doors`, nav links) still pass.

## Done when
- [ ] The Rancher feeds and slaughters over the fence when he can reach.
- [ ] Any hireling waits while a tame is near a door and closes it at once when tames are around.
- [ ] An escaped herd animal shows in Problems.
- [ ] Rows VFH-RANCH-12, -13, -14 pass.
