# Phase 05 — Guard behaviours

**Depends on:** 04 (effects framework, shared boosts) · **Enables:** 12 (balance pass over all combat trees)

## Goal
The melee guard's behaviour enhancements, each one a visible change in a fight: Shield training, Shield bash, Second
wind, Bodyguard, Taunt, Bloodthirst, Cleave, Last stand and Rallying cry.

## Files touched
- `src/VikingsForHire/Hirelings/Combat/GuardTechniques.cs` — new: per-guard timers and the techniques below, ticked from
  `CombatBehaviour.Melee` while in a fight; logs `enh.<technique>` at Debug (Combat) each time one fires.
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs` — calls into `GuardTechniques` (bash before a swing when ready,
  cleave after a landed swing, taunt/bodyguard target changes).
- `src/VikingsForHire/Hirelings/Combat/BlockController.cs` — Shield training: block power × (1 + `ShieldTraining.block`).
- `src/VikingsForHire/Hirelings/DamagePatches.cs` — Bloodthirst (heal a share of damage dealt), Last stand (a killing
  blow on a guard with it ready leaves 1 health and 3 s of no damage; cooldown kept on the hireling's ZDO
  `vfh_enh_laststand` so it survives a reload), Second wind (below the threshold once per fight: heal), Rallying cry
  (outgoing damage × 1.10 for hirelings within 15 m of a fighting Rallying guard).
- `src/VikingsForHire/Hirelings/Combat/AllyHits.cs` — Bodyguard reads recent hits on its owner or nearby hirelings.
- `src/VikingsForHire/Testing/FixturesHireling.cs` — check field `last_block` (damage absorbed by its last block). (Test
  checks `enh_events`, `enemy_staggered`, `enemy_target`, `enemies_damaged`, `damage_mult` and the `strike_hireling`
  fixture come from phase 04.)
- `test/alias_vfh.yaml` — rows VFH-ENH-G0 … G8.

## Steps
1. **Shield training** (L2 B): block power × 1.15 (`ShieldTraining.block`).
2. **Shield bash** (L4 A): every `ShieldBash.cooldown` (12 s), when in reach, a bash: stagger the target
   (`Character.Stagger`) and small blunt damage (`ShieldBash.damage`, 10). Only with a shield equipped.
3. **Second wind** (L4 B): once per fight (a fight ends after 10 s quiet), when health < `SecondWind.below` (0.30), heal
   `SecondWind.heal` (0.25) of max health, with the game's heal effect.
4. **Bodyguard** (L5 B): every `Bodyguard.cooldown` (15 s), the most recent enemy that hit its owner (when following) or a
   hireling within `Bodyguard.range` (6 m) is made to target this guard (the enemy's `BaseAI.SetTarget`).
5. **Taunt** (L6 A): every `Taunt.cooldown` (20 s) during a fight, every enemy within `Taunt.range` (8 m) targets it for
   `Taunt.seconds` (5 s), with a shout chatter line.
6. **Bloodthirst** (L6 B): heals `Bloodthirst.share` (0.10) of the damage its hits deal.
7. **Cleave** (L7 A): a landed swing also hits up to `Cleave.extra` (2) other enemies within 2.5 m in a 120° arc in front,
   for `Cleave.share` (0.5) of the swing's damage.
8. **Last stand** (L7 B): as above; cooldown `LastStand.cooldown` (600 s).
9. **Rallying cry** (L8 B): while it's in a fight, hirelings within `RallyingCry.range` (15 m) deal × (1 + `RallyingCry.damage`, 0.10).
10. Rows (each a level 8 Guard with the one pick, ghosted tester, enemies from fixtures):
    - G0 shield training: a level 8 guard with it and a plain one each block the same greydwarf hit (`hireling_health 1`,
      ghosted, facing the greydwarf): `last_block` of the trained one ≥ 1.14 × the plain one's.
    - G1 bash: vs a troll, `enh_events ShieldBash >= 1` within 30 s and the troll staggered (`enemy_staggered`).
    - G2 second wind: `hireling_health 0.25` during a fight → health rises by about 25% once, not twice in the same
      fight.
    - G3 bodyguard: a greydwarf attacking a second hireling switches to the guard (`enemy_target`).
    - G4 taunt: three greydwarfs on an archer switch to the guard.
    - G5 bloodthirst: health rises while it lands hits (regen off: `cfg_set HealthRegen 0`).
    - G6 cleave: three greydwarfs 1.5 m in front (`enemies Greydwarf 3 1.5`, immune off): after its first landed swing,
      `enemies_damaged 4 == 3`.
    - G7 last stand: `strike_hireling 100000`: alive at 1 health; a second `strike_hireling 100000` 5 s later kills it.
    - G8 rally: an archer 10 m from the guard: `damage_mult` × 1.10 while the guard fights a greydwarf, × 1.00 after the
      fight; 20 m away: × 1.00.

## Build gate
- Compile check; unit tests.
- In game: rows G0-G8 pass; regression `combat1 combat5 combat6 read1 block1 block2 block3 dodge1 stance4 post1
  retreat1 noeffect` passes.

## Test plan
The nine rows above; the combat regression.

## Commit
`feat(enhancements): guard behaviours (shield training, bash, second wind, bodyguard, taunt, bloodthirst, cleave, last stand, rallying cry); rows VFH-ENH-G0..G8`

## Rollback
Every technique is gated by `Enh.Has(...)`; reverting removes them. No saved state except the Last stand cooldown key,
which an older build ignores.
