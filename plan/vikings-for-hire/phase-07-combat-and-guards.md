# Phase 07 — Combat stances, self-defence & the Guard jobs

**Depends on:** 02, 05, 06 · **Enables:** 08–10 (workers keep their combat behaviour while working), 11, 13 (follower combat uses the same stances)

## Goal
All hirelings handle threats on their own according to their stance, with no micromanagement. Workers **Flee** (run towards the board or owner and away from the threat) or **Defend** (fight back when attacked or when an enemy is within 4 m, weakly because of `workerCombatFactor`). Guards are the real fighters: **Passive** (never start a fight, only retaliate), **Defensive** (engage enemies that enter the work radius or attack anyone friendly nearby) and **Aggressive** (also hunt enemies they can see out to radius + 10 m). Melee guards close in with sword and shield and block. Ranged guards keep their distance and shoot. With no threat, guards patrol the work radius. Combat preempts job work and hands control back afterwards.

## Files touched
- `src/VikingsForHire/Hirelings/Combat/ThreatScanner.cs`: throttled (every `AiScanIntervalSeconds`, or immediately when damaged) search for hostile `Character`s using `BaseAI.FindClosestEnemy`-style logic restricted to enemies (`BaseAI.IsEnemy`). It skips tamed creatures, players and hirelings. It keeps a "last attacker" from `Character.m_onDamaged`.
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs`: priority 900, the stance decision table (below). Uses vanilla `MonsterAI` helpers: `SetTarget`, `MonsterAI.DoAttack(target, false)` (selects the equipped weapon and calls `Humanoid.StartAttack`), `MoveTo`, `LookAt`, and `Humanoid.m_blocking` for melee guards.
- `src/VikingsForHire/Hirelings/Combat/FleeBehaviour.cs`: priority 950 for workers in Flee. Moves to a point 15 m away from the threat, biased towards the home board (or the owner when following). Returns to work after no threat within 20 m for 5 s.
- `src/VikingsForHire/Hirelings/Jobs/GuardPatrolBehaviour.cs`: priority 100. Picks random reachable patrol points inside the work radius every 20–40 s and walks them, pausing 5–10 s at each.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: registers the behaviours by job. `HirelingAI.Context` exposes the home position, work radius, stance and job.
- `src/VikingsForHire/Core/StanceRules.cs` (created in phase 02): adds the pure `Decide(stance, isGuard, threatDistance, threatInRadius, wasAttacked, allyAttacked)` → `CombatAction { None, Flee, Engage }`, unit-tested.
- `tests/VikingsForHire.Tests/StanceRulesTests.cs`.

- `src/VikingsForHire/Testing/FixturesCombat.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Decision table** (`StanceRules.Decide`):
   - Flee (worker): any threat within 12 m, or attacked → Flee.
   - Defend (worker): attacked, or a threat within 4 m → Engage. Otherwise None.
   - Passive (guard): attacked → Engage. Otherwise None.
   - Defensive (guard): attacked, an ally within 15 m attacked, or a threat inside the work radius → Engage.
   - Aggressive (guard): any threat seen within work radius + 10 m → Engage, plus everything Defensive does.
   - "Ally" = player, hireling, or tamed creature. Ally-attacked comes from a static recent-damage registry (a postfix on `Character.Damage` records the victim, the attacker and time, kept for 5 s).
2. **Engage:**
   - Melee guard: if the target is more than 2.5 m away, `MoveTo` it (run). In range, `StartAttack` with the equipped sword. When the target is attacking (`Character.InAttack`) and within 4 m, raise the block (`Humanoid.m_blocking = true` for 0.8 s), using vanilla shield blocking so the parry/stagger rules apply.
   - Ranged guard: keep 8–20 m. If the target is closer than 6 m, switch to the sidearm club. Otherwise stay on the bow. Aim with `LookAt` plus `StartAttack`. Vanilla `Attack` handles the projectile. AI humanoids skip the player hold-to-draw, so the shot fires at the attack animation's release, the same way Draugr archers shoot. Ammo comes from phase 05's infinite quiver.
   - Workers in Defend swing their tool (axe/pickaxe/club). Damage is scaled by phase 05's `DamagePatches`.
   - Leash: guards stop chasing past work radius + 15 m (Defensive/Passive) or radius + 25 m (Aggressive) and go back. Followers leash to their owner at 30 m instead. `CombatBehaviour` reads `HirelingAI.Context.LeashCenter`, which is the home board here, and phase 12 switches it to the owner while following.
3. **Disengage:** the target dies, goes out of range past the leash, or there's no damage taken or dealt for 10 s → clear the target and hand control back to the lower-priority behaviour.
4. **Health retreat:** any hireling below 25% health that isn't a guard in Aggressive flees (Flee behaviour) until it's above 40%. Hirelings regenerate 1% max health per 2 s when nothing has hurt them for 10 s (applied by the owner in `Hireling.Update`).
5. **Fire and water:** reuse `BaseAI.m_avoidFire`/`m_avoidWater` set in phase 05. Never path into deep water (vanilla handles this).
6. **Performance:** threat scans use `Character.GetAllCharacters()` with squared-distance filtering, at most once per scan interval per hireling, staggered by `hid` hash so all hirelings don't scan on the same frame.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.

## Test plan
- **Test macros** (phase 02 harness): `FixturesCombat.cs`. Fixtures: `enemies <prefab> <n> <distance>` (spawned at the given distance from the nearest hireling), `stance <short-hid|all> <stance>`. Checks: `hireling <h> behaviour` (current top behaviour name), `hireling <h> target`, `hireling <h> alive`, `enemies_alive <radius>`. Aliases added to `test/alias_vfh.yaml`: `vfh_t_combat1` (raid at a 2-guard base, `vfh_assert_eventually 120 enemies_alive 40 == 0`), `vfh_t_combat2` (dedicated, both clients), plus single-player `vfh_t_stance1`…`vfh_t_stance5`, one per stance. Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Single-player at an L4 board with `vfh_spawn` / contracts:
  - Spawn 3 Greydwarfs near a Flee woodcutter: it runs towards the board and returns after they're gone.
  - Defend miner: walk a Greydwarf up to it. It fights back only when attacked or when the enemy is very close, and loses to a Troll.
  - Defensive melee guard (L4): enemies entering the radius get engaged. It blocks the Greydwarf brute's swings (block VFX visible). It doesn't chase past the leash.
  - Aggressive ranged guard ignores Deer (not `IsEnemy`), shoots a Neck at 25 m, keeps its distance, and switches to the club when rushed.
  - Passive guard ignores a Greyling until it's hit.
  - Raid test: `event army_eikthyr` near the base with 2 guards L2: guards engage and workers flee or defend per their stance. No player input needed (row **VFH-COMBAT-1**).
- Dedicated server: two clients see the same combat animations and the hit numbers are consistent (row **VFH-COMBAT-2**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(combat): stance-driven autonomous combat and melee/ranged guard jobs`

## Rollback
Revert the commit. Hirelings fall back to phase 06 behaviour (idle, no combat). No data migration.
