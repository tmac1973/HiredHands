# Phase 06 — Archer behaviours

**Depends on:** 04 (effects framework, shared boosts) · **Enables:** 12 (balance pass)

## Goal
The archer's behaviour enhancements: Eagle eye, Fire arrows, Frost arrows, Hunter, Volley, Headshot, Marksman,
Skirmisher and Rain of arrows.

## Files touched
- `src/VikingsForHire/Hirelings/Combat/ArcherTechniques.cs` — new: timers and the techniques below, ticked from
  `CombatBehaviour.Ranged`; `enh.<technique>` Debug lines and counters (for `enh_events`).
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs` — `RangedMax` and sight + `EagleEye.range`; Skirmisher's
  back-off rules; volley and rain shots through `ArcherTechniques`.
- `src/VikingsForHire/Hirelings/Combat/ThreatScanner.cs` — sight + `EagleEye.range` for archers with it.
- `src/VikingsForHire/Hirelings/DamagePatches.cs` — on a hit from an archer's arrow: Fire arrows (adds the game's
  `Burning` status: fire damage over time), Frost arrows (the game's `Frost` status: slow), Hunter (+25% vs creatures with
  `AnimalAI`), Headshot (chance × 2), Marksman (+20% beyond 15 m from the shooter).
- `src/VikingsForHire/Hirelings/Work/FieldLoot.cs` — Hunter: an archer in the field picks up drops of creatures it killed
  (as Gather Here loot, but anywhere within 20 m while following).
- `src/VikingsForHire/Testing/FixturesCombat.cs` — check `shots_fired <sel>` (arrows the hireling launched since
  `enh_events_reset`). (`enemy_status`, `damage_mult`, `enh_events` come from phase 04.)
- `test/alias_vfh.yaml` — rows VFH-ENH-A1 … A9.

## Steps
1. **Eagle eye** (L3 A): +5 m (`EagleEye.range`) to its sight, its shooting range and its threat scan.
2. **Fire arrows** (L4 A): each hit adds fire damage `FireArrows.damage` (10 per hit) and applies `Burning` for
   `FireArrows.seconds` (4 s), as the game's fire arrows do. **Frost arrows** (L4 B): applies the game's `Frost` status
   (slow `FrostArrows.slow` 0.30 for 3 s) and frost damage `FrostArrows.damage` (5).
3. **Hunter** (L5 B): damage × 1.25 vs `AnimalAI` creatures; picks up its kills' drops when following (step above).
4. **Volley** (L6 A): every `Volley.cooldown` (15 s), the next shot is `Volley.arrows` (3) arrows spread ±6° (the game's
   projectile spawned three times with the bow's attack, ammo used once).
5. **Headshot** (L6 B): `Headshot.chance` (0.10) of × 2 damage; a "Headshot!" damage text colour (critical yellow).
6. **Marksman** (L7 A): × (1 + `Marksman.damage`, 0.20) when the target is more than `Marksman.range` (15 m) away.
   **Skirmisher** (L7 B): never swaps to its sidearm; backs off whenever an enemy is within 10 m (from 8 m), for up to
   6 s (from 3 s), then shoots.
7. **Rain of arrows** (L8 A): every `RainOfArrows.cooldown` (30 s) in a fight, `RainOfArrows.arrows` (8) arrows fall from
   above over a 4 m circle at the target, each at the bow's damage × 0.5.
8. Rows (level 8 archer, one pick each): A1 eagle eye (`enemies Greydwarf 1 33`: `hireling last target == $enemy_greydwarf`
   within 10 s; for a plain archer in the same row it stays `none` for 10 s); A2 fire arrows
   (`enemy_status nearest Burning > 0` after a hit); A3 frost arrows (`enemy_status nearest Frost > 0`); A4 hunter (an archer
   following you: `post GuardRanged 8 20 free`, `stone 1`, `recruit_posted`; `damage_mult B` against a tagged boar is × 1.25 of `damage_mult G` against a tagged greydwarf; the boar's drops end up in
   its cargo); A5 volley (`shots_fired` jumps by 3 at once); A6
   headshot (over 200 shots on an immune target, 8-35 headshots: `enh_events Headshot`); A7 skirmisher (never `gear.right == Club` with a
   greydwarf at 3 m); A8 rain (`enh_events RainOfArrows >= 1` against three greydwarfs 2 m apart, then
   `enemies_damaged 8 >= 2`); A9 marksman (`damage_mult T` for a
   tagged target 20 m away is × 1.20 of the same at 10 m).

## Build gate
- Compile check; unit tests.
- In game: rows A1-A9 pass; regression `stance5 proj1 proj2 combat10 noeffect catchup1 retreat1` passes.

## Test plan
Rows above; the ranged regression; by hand: fire arrows visibly burn, the volley's spread looks right.

## Commit
`feat(enhancements): archer behaviours (eagle eye, fire and frost arrows, hunter, volley, headshot, marksman, skirmisher, rain of arrows); rows VFH-ENH-A1..A9`

## Rollback
Gated by `Enh.Has`; reverting removes them with no saved state.
