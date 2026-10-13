# Phase 11 — Rancher behaviours

**Depends on:** 04 (effects framework, shared boosts: Pack mule, Swift, Strong back) · **Enables:** 12

## Goal
The Rancher's enhancements: Gentle hand, Butcher, Hearty feed, Herder, Skinner, Breeder, Herd guard, Star breeder, Big
herd, Master rancher and Wrangler.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Steward/AnimalsChore.cs` (the shared feeding chore, RanchFeed kind) — Gentle hand
  (feeding reach + 2 m, so more feeding over the fence); Hearty feed (after an animal eats food it dropped, the animal's
  `ZDOVars.s_tameLastFeeding` is moved forward by `HeartyFeed.share` 0.50 of its fed duration); Breeder (animals it feeds
  get ZDO `vfh_breeder` until their next feeding).
- `src/VikingsForHire/Hirelings/Work/Ranch/RanchPatches.cs` — new: a `Procreation` patch: animals flagged `vfh_breeder`
  reach their pregnancy check `Breeder.share` (0.15) sooner; Star breeder: an offspring of a mother in a Star-breeder
  Rancher's herd gets one more level with chance `StarBreeder.chance` (0.10), up to level 3 (2 stars).
- `src/VikingsForHire/Hirelings/Work/Ranch/SlaughterChore.cs` — Butcher (0.33 chance of one more meat), Skinner (0.33
  chance of one more hide or leather: the animal's drops other than meat), Master rancher (chores × 0.85 time, and its own
  0.10 chance of each, added to Butcher's and Skinner's when those are picked).
- `src/VikingsForHire/Hirelings/Work/Ranch/HerderChore.cs` — new: Herder: for an escaped herd animal (HerdScan's
  `Escaped`), walks to it, makes it follow (the game's tame follow command, `Tameable.Command`), leads it back through the
  pen's gate (careful-gate rules apply) to a spot inside, then stops the follow; gives up after 3 minutes and leaves it in
  Problems.
- `src/VikingsForHire/Core/Orders/RanchPlanner.cs` — Big herd: pools + `BigHerd.extra` (2) for a Rancher with it.
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs` and `AllyHits.cs` — Herd guard: a Rancher with it fights
  anything that hit one of its herd's animals within its area in the last 5 s, at × (1 + `HerdGuard.damage`, 0.25), then
  returns to work.
- `src/VikingsForHire/Hirelings/Work/Ranch/WranglerChore.cs` — new: Wrangler: a wild animal of a kind its board keeps
  (a kind with a breeding pool) inside its area is fed by it and tamed at once (`Tameable.Tame`), joining the herd; one
  every `Wrangler.cooldown` (300 s); never a boss or a creature the game can't tame.
- `src/VikingsForHire/Testing/FixturesWork.cs` — check `animal_pregnant <tag>`.
- `test/alias_vfh.yaml` — rows VFH-ENH-R1 … R10.

## Steps
1. Implement each as listed, reading numbers through `Enh`.
2. Rows (level 8 Rancher, pen fixture, `StewardIgnoreOtherMods true`, one pick each, except R4 which takes Butcher and Skinner): R1 gentle hand (feeds a boar at the
   far fence of an 8 m pen from outside, no gate opened); R2 hearty feed (the boar's next hunger comes ~1.5× later); R3
   herder (`move_animal B 0 9`: back inside within 3 minutes, `outside_pen Boar == 0`); R4 butcher and skinner (20
   slaughters with fixtures: meat and hides above plain); R5 breeder (time from feeding to `animal_pregnant B == true` ≤ 0.87 × the same
   for a plain Rancher's pair in the same row); R6 herd
   guard (a wolf attacks a tame boar: the Rancher fights it); R7 big herd (pool 2 → keeps 4 before slaughtering); R8
   wrangler (a wild boar in the area becomes tame within 60 s); R9 master rancher (20 slaughters: meat and hides above plain,
   without Butcher or Skinner picked). Star breeder: R10 with `StarBreeder.chance` set to 1.0 through a data-file test
   override fixture (`data_set Rancher StarBreeder.chance 1`, from phase 04, then `data_reset`), a newborn has one more star than its mother.

## Build gate
- Compile check; unit tests (RanchPlanner big herd).
- In game: rows R1-R10 pass; `vfh_t_ranch_all` and `door_mark` pass.

## Test plan
Rows above and the ranch regression.

## Commit
`feat(enhancements): rancher behaviours (gentle hand, butcher, hearty feed, herder, skinner, breeder, herd guard, star breeder, big herd, wrangler, master rancher); rows VFH-ENH-R1..R10`

## Rollback
Gated by `Enh.Has`. Animals tamed by Wrangler stay tame after a rollback (as if a player tamed them).
