# Phase 01 — Hirelings never hurt players

**Depends on:** nothing · **Enables:** phase 04 (a wizard's blasts can't hit the players around its target)

## Goal
No hireling attack damages a player, whatever PvP or `FriendlyFireOnHirelings` say. Today `DamagePatches`
stops hirelings hurting each other, tames and buildings, but a stray hit (an arrow's splash, and soon a spell's
blast) lands on a player standing by the target. This is a fix for all hirelings, shipped first because it stands
alone.

## Files touched
- `src/VikingsForHire/Hirelings/DamagePatches.cs`: in `CharacterDamagePatch.Prefix`, when the attacker is a hireling
  and the target is a `Player`, block the hit (`Blocked("hireling_on_player", …)`).
- `src/VikingsForHire/Testing/FixturesCombat.cs`: fixture `hit_me_by_hireling <damage>` (a hit on you from the last
  spawned hireling, through `Character.Damage` with the hireling as attacker); fixture `protect <on|off>`
  (`TestProtection.Set`: off lets hits reach you for the rest of the row; the next row's `vfh_test_begin` turns it on
  again); fixture `heal_me` (back to full health).
- `test/alias_vfh.yaml`: row VFH-COMBAT-10.

## Steps
1. In `CharacterDamagePatch.Prefix`, after `if (target != null) { … }` and before the tame check, add: when `from !=
   null` and `__instance is Player`, return `Blocked("hireling_on_player", from, __instance)`. `Blocked` already logs
   at trace level and returns false.
2. Leave player-on-hireling rules as they are (`FriendlyFireOnHirelings`).
3. Add the fixture: `HitData` with `m_damage.m_blunt = <damage>`, `SetAttacker(last hireling's Humanoid)`, then
   `Player.m_localPlayer.Damage(hit)`.
4. Row VFH-COMBAT-10: `hirelings GuardMelee 2; stance last Passive; protect off`; control: `hit_me 0.2` (an existing
   fixture, a hit from no one) → `player_health < 0.99`, so hits do land; `heal_me`; then `hit_me_by_hireling 30` →
   `player_health >= 0.99`; `protect on; kill_hirelings`.

## Build gate
- `dotnet build -c Release` (no warnings).
- `dotnet test tests/VikingsForHire.Tests`.

## Test plan
- Row VFH-COMBAT-10 passes in the dev game.
- Regression: `vfh_test_chain combat1 stance4 stance5 block2 dodge1 noeffect` still pass.

## Commit
`fix(combat): no hireling attack ever damages a player (stray splash and blasts landed on players by the target)`

## Rollback
Revert the commit (the `if` in `DamagePatches`, the fixture and row VFH-COMBAT-10 together). Nothing else depends on
it until phase 04.
