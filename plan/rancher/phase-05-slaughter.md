# Phase 05 — Slaughter, collecting the drops, delivering them

**Depends on:** 03 (the job and its chore loop), 04 (`RanchState` plan) · **Enables:** 06 (shares the drop collecting)

## Goal
The Rancher slaughters the animal the plan picks with his butcher knife, collects everything it drops and delivers it
to the chests. Only his hits can hurt a tame; every other hireling's stay blocked.

## Files touched
- `src/VikingsForHire/Hirelings/DamagePatches.cs` — the `hireling_on_tame` block lets a hit through when the attacker is
  a Rancher (`Hireling.Of(attacker)?.Job == JobType.Rancher`); logged `ranch.slaughter_hit`.
- `src/VikingsForHire/Hirelings/Work/Ranch/SlaughterChore.cs` (new) — `IChore` for `ChoreKind.Slaughter`.
- `src/VikingsForHire/Hirelings/Work/Ranch/DropCollect.cs` (new) — pick up `ItemDrop`s within 4 m of a spot that
  appeared after a given time, into cargo (as `FieldLoot` does), reserving them.
- `src/VikingsForHire/Hirelings/HirelingAI.cs` — the Rancher's chore loop gains `SlaughterChore`.
- `src/VikingsForHire/Localization/English.json` — `$vfh_ranch_slaughter` label, `$vfh_ranch_collect`.
- `src/VikingsForHire/Core/Chores/ChoreKind.cs`, `ChoreRules.cs`, `Config/VfhConfig.cs`/`ChoreLoop.ServerAllows` —
  `Slaughter` kind (key `slaughter`) added to `ChoresFor(Rancher)`, mapped to `RancherSlaughter`.
- `src/VikingsForHire/Testing/FixturesWork.cs` — fixture `ranch_stats_reset` and check `slaughtered_by_rancher` (kills
  since the reset).
- `src/VikingsForHire/Testing/FixturesCombat.cs` — fixture `hit_tame_by_hireling <tag> <job>`: the nearest hireling of
  that job hits the tagged tame once (slash 50) as its attack would.
- `test/alias_vfh.yaml` — rows VFH-RANCH-6, -7, -8, -9.

## Steps
1. **Candidates.** With `RanchState.For(board).Slaughter` set and the animal loaded, reserved by nobody: one job,
   urgency 0.55 (below feeding, `ChoreUrgency.Animal(true)` = 0.7 in
   `Core/Chores/ChoreUrgency.cs`, above tidying), label `$vfh_ranch_slaughter|<animal name>`. Cargo full: no job
   (deliver first). `Missing` is not set here; the order reasons come from the plan.
2. **Begin/Tick.** Walk to the animal (`WorkSteps.Approach` with the player reach). Face it, play the knife's attack
   (`HirelingAI.Attack` with the animal as target), then deal one hit from the Rancher equal to the animal's current
   health plus 1 (slash damage, attacker = the Rancher), through `Character.Damage`, so vanilla drops its loot. Record
   the time and the spot. If the animal moved more than 3 m before the swing, follow it (up to 20 s, then fail).
3. **Collect.** For 3 s after the kill, `DropCollect` picks up every item that appeared within 4 m of the spot into
   cargo; what doesn't fit stays on the ground and is logged (`ranch.left_on_ground`). Then `RanchState.Invalidate`.
4. **Deliver.** The usual `DeliverBehaviour` empties cargo to the chests (drop pile by the board when there's no room).
5. **Guards still can't.** `ThreatScanner` keeps skipping tames, so only the Rancher's deliberate hit reaches one.

## Verification
- Row VFH-RANCH-6 (selective): Rancher; tame boars at 0, 1, 1 and 2 stars; `order add Boar 2 herd`,
  `order add RawMeat 100 meat`: eventually `herd Boar adults == 2`, `herd Boar stars 2 == 1`, `herd Boar stars 1 == 1`,
  and a chest holds RawMeat and LeatherScraps.
- Row VFH-RANCH-7 (met): same herd, `order add RawMeat 1 meat` with 5 RawMeat in a chest: after 60 s
  `slaughtered_by_rancher == 0`.
- Row VFH-RANCH-7b (no pool): three boars, `order add RawMeat 100 meat`, no herd order: after 60 s
  `slaughtered_by_rancher == 0` and `why has vfh_ranch_need_pool == true`.
- Row VFH-RANCH-8 (named): two boars plus a named one, pool 2, meat short: the named boar is never slaughtered and
  `herd Boar named == 1` at the end.
- Row VFH-RANCH-9 (others can't): a guard and a tame boar T: `hit_tame_by_hireling T GuardMelee`: `object_health T == 1`;
  then a Rancher: `hit_tame_by_hireling T Rancher`: `object_health T < 1`.
- Existing combat rows (`combat1 stance4 noeffect`) still pass.

## Done when
- [ ] The Rancher slaughters exactly the planned animal and delivers its drops.
- [ ] Only the Rancher's hits hurt tames.
- [ ] Rows VFH-RANCH-6, -7, -7b, -8, -9 pass.
