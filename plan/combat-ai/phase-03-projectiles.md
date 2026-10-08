# Phase 03 — Reading and blocking projectiles

**Depends on:** 01 (`IncomingAttack`, `AttackReader`, `AttackEstimate`, counters, fixtures), 02 (`BlockController`
blocks whatever is in `Incoming`) · **Enables:** 04 (dodging projectiles), 05 (projectile numbers in the balance log)

## Goal
Hirelings also read projectiles flying at them:
- rocks, arrows, spears, fireballs, and the bigger thrown things;
- they work out when each one will arrive, how much it would hurt, and whether it explodes (area);
- the reads go into the same `Incoming` list as close attacks;
- melee guards block and parry them with the phase 02 controller.

Hirelings without a shield only read them in this phase. They dodge them in phase 04.

## Files touched
- `src/VikingsForHire/Hirelings/Combat/ProjectileWatch.cs` (new):
  - a static registry of live projectiles, filled by a Harmony postfix on `Projectile.Awake`;
  - per-projectile tracking: first-seen position, last position, velocity, and the shooter it's guessed to come from.
- `src/VikingsForHire/Hirelings/Combat/AttackReader.cs`: also scans `ProjectileWatch` for projectiles heading at its
  hireling, and adds them to `Incoming`.
- `src/VikingsForHire/Hirelings/Combat/AttackEstimate.cs`: `ForProjectile(...)`, which returns damage, area and arrival
  time.
- `src/VikingsForHire/Hirelings/Combat/IncomingAttack.cs`:
  - `Projectile` is now set for projectile reads;
  - `From` (from phase 01) is set from the flight direction for projectile entries.
- `src/VikingsForHire/Hirelings/Combat/BlockController.cs`: faces `From` rather than the attacker for projectile
  entries. The late-block rule also covers unread projectiles (below).
- `src/VikingsForHire/Hirelings/Combat/DefenseStats.cs`: new counters `ProjReads` and `ProjBlocks`.
- `src/VikingsForHire/Hirelings/Combat/BlockPatches.cs`: counts `ProjBlocks` when `hit.m_ranged` is set.
- `src/VikingsForHire/Testing/FixturesCombat.cs`: the `defense` check also accepts `proj_reads` and `proj_blocks`.
- `src/VikingsForHire/Core/DefenseRules.cs`: `ClosestApproach(pos, vel, target)`, returning the distance and the time.
- `tests/VikingsForHire.Tests/DefenseRulesTests.cs`: tests for `ClosestApproach`.
- `test/alias_vfh.yaml`: rows `VFH-PROJ-1` and `VFH-PROJ-2`.

## Steps
1. **`ProjectileWatch` registry.**
   - A postfix on `Projectile.Awake` adds the projectile to a static `HashSet<Projectile>`. Destroyed ones (Unity
     null) are pruned on each scan.
   - **Tracking.** For each projectile the first time any reader scans it, record `firstPos`, then `lastPos` and
     `lastTime` on every scan.
     - **Velocity** comes from the change in position between scans. This works for the owner's projectile and for
       synced copies on other machines, which get their position from `ZSyncTransform`.
     - On the owner it uses `m_vel` once that's non-zero.
   - **Shooter.**
     - On the projectile's owner, it's `m_owner`.
     - Elsewhere, it's the nearest `Character` within 3 m of `firstPos` when the projectile is first seen, if there is
       one.
   - **Friendly projectiles** are skipped. A projectile is friendly when its shooter is a `Player`, a tame, or a
     hireling (has `HirelingAI`). A projectile with no shooter found is treated as hostile.
2. **Heading at us?** `AttackReader` checks every 0.1 s while it's active (same rule as phase 01). For each hostile
   projectile within 30 m:
   - **Closest approach** to the hireling's chest (`GetCenterPoint()`) is worked out from position and velocity, as a
     straight line. Projectiles that arc mostly drop over longer distances, and this is re-checked each scan, so the
     estimate improves as it nears.
   - **It's heading at us** when the closest approach is under `1.0 m + hireling radius`, the time to it is between
     0.05 s and 1.5 s, and the projectile is approaching (velocity · direction to the hireling > 0).
   - **Each projectile is read once** per hireling, so the read roll and the parry roll are made once. Its instance id
     is the `AttackId`.
   - **Hit time keeps updating.** On every later scan while it's still approaching, the entry's `HitTime` is updated
     from the new closest-approach time. Arcing projectiles get more accurate as they near.
   - **Raise time follows it.** `BlockController.Act()` recomputes `RaiseAt = HitTime - lead` each tick for entries
     whose shield isn't up yet. The lead is `ParryLeadSeconds` when `ParryWon`, otherwise `EarlyBlockLeadSeconds`.
     This adds one line to `Act()` in this phase. Close attacks never change their `HitTime`, so it makes no
     difference for them.
3. **`AttackEstimate.ForProjectile`:**
   - **Damage:**
     - On the owner, `projectile.m_damage` when its total is above 0. That's already scaled for stars at launch.
       Scale it by `Game.m_enemyDamageRate` and apply the hireling's armor (`ApplyArmor`), as in phase 01, then sum.
     - Otherwise, from the shooter's prefab weapons: the one whose `m_attack.m_attackProjectile` prefab name matches
       the projectile's prefab name, with stars and the world damage rate applied as in phase 01.
     - With no match, `0.2 ×` the hireling's max health. That's a mid-size hit: below the "hurts a lot" line unless
       its health is already low.
   - **Area:** `projectile.m_aoe > 0`.
   - **Arrival:** `HitTime = now + time to closest approach`. `Timed = true`.
   - **From:** `-velocity.normalized` (flattened), pointing back towards where the projectile came from.
4. **The read roll.** It uses `ReadChance`, as for close attacks. The entry goes into `Incoming`, with `ProjReads` and
   `Reads`/`Misses` counted. It's logged as `defense.read` with `proj=<prefab>`.
5. **Blocking projectiles** (`BlockController`). Projectile entries are planned exactly as melee ones: a parry roll,
   then `raiseAt` at the parry or early-block lead. The guard turns to face `From`.
   - **Turning in time.** Vanilla needs the hit to come from in front. If facing `From` would take more than 120° of
     turning, there isn't time, so the raise is skipped (logged `defense.block_skipped why=turn`) and the attack is left to the late-block
     rule. Hirelings turn
     at their `m_turnSpeed`; a full turn takes too long.
   - **The late-block rule for projectiles:** an unread hostile projectile under 0.4 s away, and roughly in front
     (within 60° of forward), gets the shield up for 0.6 s.
6. **Counting.** `BlockPatches` counts `ProjBlocks` when the blocked hit has `m_ranged == true`. Parries are counted as
   in phase 02. Vanilla gives no stagger for ranged parries.
7. **Cost.**
   - The registry is shared, and only readers that are active scan it.
   - A scan is cheap: a squared-distance check (30 m) before any maths.
   - The number of projectiles alive at once is small (tens).
8. **Batch log.** Add this phase's commit row, and the steps for checking projectiles (rows PROJ-1/2, and the by-hand Draugr archer check), to the 0.6.0 tab of
   `docs/next-release.md`, as the batch-testing routine requires.

## Build gate
- `dotnet build -c Release` builds and deploys, with no new warnings.
- `dotnet test` passes. The closest-approach maths goes in `DefenseRules.ClosestApproach(pos, vel, target)` (pure),
  with unit tests: a head-on shot, a miss to one side, and a projectile moving away.

## Test plan
Run in game through `mtb`.
- **VFH-PROJ-1:**
  1. A level 8 GuardMelee posted at the test spot (`post`, so it holds position).
  2. `enemies Draugr_Ranged 1 18`.
  3. Eventually (90 s): `defense last proj_reads >= 2` and `defense last proj_blocks >= 1`.
  4. Then clean up with `kill_enemies` and check `log_errors == 0`.
- **VFH-PROJ-2** (an archer without a shield only reads):
  1. A level 4 GuardRanged.
  2. A Greydwarf at 20 m, which throws rocks.
  3. Eventually (120 s): `defense last proj_reads >= 1`, then `defense last proj_blocks == 0`.
  4. Then, eventually (a further 120 s): `enemies_alive 40 == 0`.
- **Regression:** the phase 02 rows (BLOCK-1 to BLOCK-3), the combat chain, STN-5 (the archer against a Neck) and
  POST-1.
- **By eye** (Tim): a posted guard facing Draugr archers turns into the arrows and blocks most of them. Arrows fired at
  someone else don't make it react.

## Commit
`feat(combat): hirelings read projectiles flying at them (arrival, damage, explosive); guards turn into them and block or parry`

## Rollback
Revert the commit. Phases 01–02 keep working for close attacks. The `Projectile.Awake` postfix is the only new patch,
and reverting removes it. With `BlockAndDodge = false`, readers are idle and projectiles are only registered, which
costs nothing noticeable.

## Implementation notes (as built)
- **Projectiles are scanned every tick while the setting is on,** not only during a fight.
  - Arrows from close by arrive within about 0.25 s, and an idle guard often hasn't noticed the archer.
  - The tracker uses the launcher's own velocity on the first look (`Projectile.GetVelocity()`), with no need for two
    looks.
- **Swing windups whose weapon fires a projectile are skipped** (a Greydwarf's throw, a Draugr's bow). The projectile is
  read in flight instead, so it isn't counted twice.
- **Damage estimates follow vanilla's order for a hireling:**
  1. our armor (`DamagePatches`);
  2. `m_enemyDamageRate × m_playerDamageRate ×` the difficulty scales;
  3. chop and pickaxe damage dropped, since characters ignore them. A Troll's slap is mostly chop damage.

  Estimates now land within about 20% of the hits taken. Before this they were 4× too high for Trolls and about 2.5×
  too low for 2-star arrows.
