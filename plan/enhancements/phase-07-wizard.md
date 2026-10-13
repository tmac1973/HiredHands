# Phase 07 — Wizard behaviours

**Depends on:** 04 (effects framework, shared boosts) · **Enables:** 12 (balance pass)

## Goal
The wizard's behaviour enhancements: Blink, Secondary spell, Mender, Elemental mastery, Protection, Entangling roots,
Long reach, Chain spell and Raise dead, built on the game's own staffs and effects.

## Files touched
- `src/VikingsForHire/Hirelings/Combat/WizardTechniques.cs` — new: timers and techniques, ticked from
  `CombatBehaviour.Cast`; `enh.<technique>` lines and counters.
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs` — `CastMin`/`CastMax` + `LongReach.range`; Blink in the back-off
  branch; secondary spell, roots and chain through `WizardTechniques`.
- `src/VikingsForHire/Hirelings/DamagePatches.cs` — Elemental mastery: the wizard's status effects (burning, frost,
  lightning's shock) last and hurt × 1.30; Chain spell's second hit at × 0.5.
- `src/VikingsForHire/Hirelings/Combat/SummonedAllies.cs` — new: Raise dead's skeleton (the game's `Skeleton_Friendly`,
  as the Dead Raiser staff summons), tamed, following the wizard, not counted against any cap, removed after
  `RaiseDead.seconds` (60 s) or when the wizard leaves the area; logged `enh.raise_dead`.
- `src/VikingsForHire/Testing/FixturesCombat.cs` — checks `summons_near <radius>`, `player_status <status>` (seconds left).
  (`enh_events`, `enemy_status`, `damage_mult` come from phase 04.)
- `test/alias_vfh.yaml` — rows VFH-ENH-W1 … W9.

## Steps
1. **Blink** (L3 B): when an enemy is within `Blink.trigger` (4 m) and Blink is ready (`Blink.cooldown` 10 s), teleport
   `Blink.distance` (8 m) away from it, to a standable point with line of sight back (the game's teleport effect).
2. **Secondary spell** (L4 A): every `SecondarySpell.cooldown` (20 s) in a fight, use the staff's secondary attack
   (`m_secondaryAttack`) when the staff has one; otherwise a frost nova: frost damage `SecondarySpell.nova` (20) and `Frost` on
   enemies within 5 m.
3. **Mender** (L4 B): every `Mender.cooldown` (15 s), heal the most hurt player or hireling within `Mender.range` (15 m)
   by `Mender.heal` (0.10) of its max health, with the game's heal effect; never itself.
4. **Elemental mastery** (L5 B): as above (`ElementalMastery.mult` 0.30).
5. **Protection** (L6 A): every `Protection.cooldown` (60 s), the game's Staff of Protection bubble (`Staff_shield` status)
   on its owner when following, else on itself, absorbing `Protection.absorb` (100) damage.
   **Entangling roots** (L6 B): every `EntanglingRoots.cooldown` (30 s), roots (the Staff of the Wild's attack) at its target.
6. **Long reach** (L7 A): casts from 4 m further (`LongReach.range`). **Chain spell** (L7 B): each spell hit also strikes
   the nearest other enemy within `ChainSpell.range` (6 m) for half damage, with a lightning line effect.
7. **Raise dead** (L8 B): every `RaiseDead.cooldown` (120 s) in a fight, one skeleton for 60 s.
8. Rows (level 8 wizard, one pick each; the dev profile has Therzie's Wizardry, whose staffs have the secondary attacks W2
   uses): W1 blink (`enemies Greydwarf 1 2 G`: `hireling last dist_to G >= 6`
   within 2 s); W2 secondary (`enh_events SecondarySpell >= 1`); W3 mender (a hurt guard at 50% gains ~10%); W4 mastery
   (right after a hit, `enemy_status nearest Burning` is ≥ 1.25 × the same reading for a plain wizard in the same row); W5 protection (`player_status Staff_shield > 0` while following); W6 roots
   (`enh_events EntanglingRoots >= 1`, target held); W7 chain (two greydwarfs 4 m apart, `enemies Greydwarf 2 18`: after its first cast,
   `enemies_damaged 25 == 2`); W8 raise dead
   (`summons_near 20 == 1` in a fight, `== 0` 60 s later); W9 long reach (`enemies Greydwarf 1 25`: `hireling last target == $enemy_greydwarf`
   within 10 s; a plain wizard's stays `none`).

## Build gate
- Compile check; unit tests.
- In game: rows W1-W9 pass; `vfh_t_wiz_all` passes.

## Test plan
Rows above and the wizard set; by hand: the bubble, roots and skeleton look and behave like the player's own staffs.

## Commit
`feat(enhancements): wizard behaviours (blink, secondary spell, mender, elemental mastery, protection, roots, long reach, chain, raise dead); rows VFH-ENH-W1..W9`

## Rollback
Gated by `Enh.Has`; summons are temporary and removed on timeout, so reverting leaves nothing behind.
