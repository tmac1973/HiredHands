# Phase 04 — Casting: range, pace and keeping distance

**Depends on:** 01 (blasts can't hurt players), 02, 03 (staff in hand) · **Enables:** 05, 06

## Goal
Wizards fight: they cast their staff's main spell at a target from 10–22 m, one cast every `WizardAttackCooldown`
seconds, step back when an enemy is inside 8 m, and never use a second attack. Endless eitr: vanilla's
`Character.HaveEitr` returns true for anything that isn't a player, so casting costs a hireling nothing. Damage is
scaled by `WizardDamage`. Stances, posts, following, attack and retreat orders work as for archers.

## Files touched
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs`: `Wants` stops excluding wizards (phase 02); `Tick` sends
  `Wizard` to a new `Cast(...)`; constants
  `CastMin = 10`, `CastMax = 22`, `CastBackOff = 8`; aiming reuses `ArrowAim` (it reads the weapon's
  `m_attackProjectile`, speed and gravity, and checks a clear line).
- `src/VikingsForHire/Config/VfhConfig.cs`: `JobDamage(Wizard) = Max(0, WizardDamage)` (the setting itself is from
  phase 02).
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: no change: `Attack(target)` already calls `DoAttack(target, false)`,
  the primary attack only.
- `src/VikingsForHire/Testing/FixturesCombat.cs`: `enemies` gains an optional last argument `still` (its AI switched
  off for as long as it lives: it stands where it spawned and doesn't fight); new check `enemy_dist <last|posted>` (distance from that hireling to
  the nearest living enemy, `none` without one); new fixture `order_attack_nearest` (the stone's attack order, given by
  you, on the enemy nearest your followers). Existing and reused: checks `hireling … post_dist|order|retreat|behaviour|
  target|gear.right`, a new field `cast_dist` (the distance to the target at the wizard's last cast, kept on
  `CombatBehaviour`), `followers`, `enemies_alive`; fixtures `enemies`, `enemies_immune`, `stance`, `stone`,
  `recruit_posted`, `post_posted`, `retreat`, `cfg_set`.
- `test/alias_vfh.yaml`: rows VFH-WIZ-3, -4, -5, -8, -9, -10, -11.

## Steps
1. `Cast(ai, me, target, dist, dt)`:
   - inside `CastBackOff` and not posted: walk away from the target to `CastMin` (as the archer backs off from inside
     `RangedMin`);
   - posted: hold the post, cast at whatever is in range, never move;
   - beyond `CastMax` and not posted: close in to `CastMax * 0.8`, except through water (the 0.7.5 water rule, as
     for archers);
   - in range: `ArrowAim`; no clear line → the archer's blocked-shot handling (a free wizard moves in, a posted one
     waits);
   - when `Time.time >= _nextAttack`, not holding for a dodge, and looking at the aim point within 15°:
     `ai.Attack(target)`; on success `Attacked(WizardAttackCooldown)`.
2. Rapid-fire staffs (`staff_rapidfire`: the Mountain and Frost staffs) fire one projectile per `Attack` for an NPC;
   paced by the cooldown like the others.
3. The no-effect (25 s), underwater, in-water and blocked rules apply to wizards as to archers (they share the code).
4. Dodging as everyone (DodgeController); never blocks (no shield).
5. Rows (each self-contained: it sets up and cleans up after itself):
   - VFH-WIZ-3: `hirelings Wizard 2; stance last Aggressive` → `hireling last gear.right == StaffBlackforest_TW`;
     `enemies Greydwarf 3 15` → `hireling last behaviour == Combat` within 20 s, then `enemies_alive 40 == 0` within
     90 s, and `hireling last cast_dist >= 8` (it cast from range);
   - VFH-WIZ-4 (backing off): `hirelings Wizard 2; stance last Aggressive; enemies Greydwarf 1 4 still; enemies_immune
     20` (it neither follows nor dies) → `enemy_dist last >= 8` within 6 s; `kill_enemies`;
   - VFH-WIZ-5: `mods Therzie.Wizardry off; hirelings Wizard 6` → `hireling last gear.right == StaffFireball`; `enemies
     Goblin 1 15` → `enemies_alive 40 == 0` within 90 s; `mods Therzie.Wizardry real`;
   - VFH-WIZ-8 (on a post): `base; board_here; board_level 2; stock_board 2000 500; post Wizard 2 20; roster Active ==
     1; stone 1; recruit_posted; post_posted right 8; hireling posted posted == true`; `enemies Greydwarf 1 15` (the
     `enemies` fixture measures from the last hireling the `hirelings` fixture spawned if it's still there, else from
     your last contract's hireling: here no `hirelings` were spawned in the row, so 15 m from the posted wizard) → `enemies_alive 40 == 0` within 90 s; then `hireling posted post_dist < 1.5` (an end check: it
     never left the post to chase); cleanup;
   - VFH-WIZ-9 (following): the same setup to `recruit_posted`; `hireling posted behaviour == Follow` within 10 s
     (it follows you); `enemies Greydwarf 1 15; order_attack_nearest` → `hireling posted order == Attack`, then
     `enemies_alive 40 == 0` within 90 s; `enemies Greydwarf 1 15; retreat` → `hireling posted retreat == true` and
     `hireling posted behaviour == Follow` within 3 s; `kill_enemies`; cleanup;
   - VFH-WIZ-10 (Passive): `hirelings Wizard 2; stance last Passive; enemies Greydwarf 1 15; wait 10` → `hireling last
     behaviour != Combat`; `kill_enemies`;
   - VFH-WIZ-11 (`WizardDamage`): `cfg_set WizardDamage 0; hirelings Wizard 2; stance last Aggressive; enemies
     Greydwarf 1 15` → `hireling last behaviour == Combat` within 20 s, then `hireling last target == none` within 40 s
     (it did nothing, so the no-effect rule let go) and `enemies_alive 40 >= 1`; `cfg_set WizardDamage 1;
     kill_enemies`.

## Build gate
- `dotnet build -c Release`; `dotnet test tests/VikingsForHire.Tests`.

## Test plan
- Rows VFH-WIZ-3, -4, -5, -8, -9, -10, -11 pass; regression `combat1 stance4 stance5 post1 retreat1 noeffect block2 dodge1`.

## Commit
`feat(wizard): wizards cast their staff's main spell from 10-22 m every WizardAttackCooldown, step back inside 8 m; WizardDamage`

## Rollback
Revert; wizards would then stand idle (no combat branch). Ships together with 02–06.
