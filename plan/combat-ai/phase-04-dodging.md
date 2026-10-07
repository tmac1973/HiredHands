# Phase 04 — Dodging (everyone who fights)

**Depends on:** 01 (reads, estimates, `DefenseRules`, levels table, counters), 02 (`BlockController.Reset`, and the
planned block a dodge replaces), 03 (projectile reads, and `From` set for them) · **Enables:** 05 (dodge rates in the
balance log and the before/after fights)

## Goal
Any hireling in a fight rolls out of the way of a read attack, close or projectile, that would hurt a lot. That means
one of:
- after armor, and after its shield if it was going to block, the attack would take more than 25% of its current
  health;
- or it's an area attack.

The roll:
- is the player's: the same animation, moving the body by root motion;
- makes hits miss while it lasts, through vanilla's own `IsDodgeInvincible` checks, on every machine;
- is limited by the level's dodge chance and dodge cooldown;
- only goes towards safe ground.

## Files touched
- `src/VikingsForHire/Hirelings/Combat/DodgePatches.cs` (new): Harmony postfixes on `Character.InDodge` and
  `Character.IsDodgeInvincible`, for hirelings only.
- `src/VikingsForHire/Hirelings/Combat/DodgeController.cs` (new): decides whether, when and which way to roll, starts
  the roll, and ends it.
- `src/VikingsForHire/Hirelings/Combat/BlockController.cs`: a new `Lower()` method (step 5).
- `src/VikingsForHire/Hirelings/Combat/IncomingAttack.cs`: new fields `Vector3 DodgeDir` (the direction chosen at
  decision time), `HitData.DamageTypes Types` (after armor) and `bool Dodgeable`.
- `src/VikingsForHire/Hirelings/Combat/AttackEstimate.cs`:
  - fills `Types`;
  - fills `Dodgeable` from the matched weapon's `m_shared.m_dodgeable`, or the projectile's `m_dodgeable`. When there's
    no match it's true, since most attacks are dodgeable.
- `src/VikingsForHire/Core/DefenseRules.cs`: `DodgeDirections(Vector3 from, bool area, bool projectile, bool
  leftFirst)`, which returns the candidate directions in order of preference (pure, tested). `DodgeController` keeps
  `_leftFirst` and flips it after each melee dodge, so the side alternates.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`:
  - creates the `DodgeController` for every hireling. Like the block controller it checks the setting each tick and
    does nothing while it's off, so it can be switched mid-session;
  - ticks it between `BlockController.Plan()` and `BlockController.Act()`. That way the dodge decision sees the
    planned raise and the parry roll (step 2), and a dodge it starts is in place before the shield would go up;
  - calls `BlockController.Lower()` when a dodge starts (step 5);
  - skips the behaviour tick while `InDodge()`, so no behaviour moves the hireling mid-roll;
  - its existing `m_onDamaged` handler tells the controller about hits (for counting dodged hits).
- `src/VikingsForHire/Hirelings/Combat/DefenseStats.cs`: `Dodges` and `DodgedHits` are now counted, plus new counters
  `DodgeRolls` and `DodgeWins` (the roll itself, for checking the rate against `dodgeChance`).
- `src/VikingsForHire/Testing/FixturesCombat.cs`: the `defense` check also accepts `dodge_rolls` and `dodge_wins`.
- `test/alias_vfh.yaml`: rows `VFH-DODGE-1` to `VFH-DODGE-4`.

## Steps
1. **`DodgePatches`.** These apply only when `__instance` has `HirelingAI`; everything else is left as vanilla.
   - **`InDodge` postfix:** true when the animator's current or next state carries the `dodge` tag, the same test as
     `Player.UpdateDodge`:
     `m_animator.GetBool(ZSyncAnimation.GetHash("dodge")) || GetNextOrCurrentAnimHash() == dodgeTagHash`. Hashes are
     cached.
     - Because of this, vanilla `Character.AddRootMotion` applies the roll's root motion. The body travels the
       player's roll distance, with no scripted push.
     - `IsBlocking()` and `StartAttack()` already refuse during a dodge.
   - **`IsDodgeInvincible` postfix:**
     - On the owner, it returns the controller's `Invincible` flag.
     - Elsewhere, it reads the ZDO bool `ZDOVars.s_dodgeinv`. That's the same key players use, and a hireling never
       has a `Player` component, so it can't clash.
     - Vanilla checks this in `Attack` (melee, on the attacker's machine), in `Projectile` (on the projectile owner's
       machine) and in `Character.RPC_Damage` (on ours), each time only for hits with `m_dodgeable`. Syncing it
       through the ZDO is what makes all three work.
2. **Choosing to dodge** (`DodgeController.Tick`, setting on). Each entry in `Incoming` is decided once: on the first
   tick after it appears, entries with `DodgeDecided` false are taken in `HitTime` order, and `DodgeDecided` is set on
   each. Because this runs between `BlockController.Plan()` and `Act()`, the block plan (`ParryWon`, `RaiseAt`) already
   exists, and no shield has been raised for the entry yet. It dodges only when every condition below holds:
   - It has `Read`, `Timed` and `Dodgeable` set.
   - **Damage check.** If the hireling has a shield and the `BlockController` has a raise planned for this attack,
     take the damage after the block: copy `Types`, apply `ApplyArmor(shield.GetBlockPower(0f))`, then sum. Otherwise
     sum `Types` as it is.
     - It dodges when `DefenseRules.WouldHurtALot(that, character.GetHealth(), Area)` is true. A planned parry
       multiplies block power by `m_timedBlockBonus` first, so the guard trusts a likely parry.
   - **Rolls.** `DefenseRules.DodgeReady(now, lastDodge, LevelData.DodgeCooldown)`, then the `DodgeChance` roll, made
     once per attack. `DodgeRolls` is counted when the roll is made, and `DodgeWins` when it's won. A won roll can
     still end without a dodge when no direction is safe (step 4); that's logged as `defense.dodge_blocked_by_ground`.
   - **State.** The hireling is on the ground (`IsOnGround()`), not attached (`!IsAttached()`), not swimming, not
     staggered, not mid-swing (`!InAttack()`) and not already rolling.
   - **Time.** `HitTime - now` is at least 0.15 s.
   - **Logging.** If it decides not to dodge a read, timed attack, it logs `VfhLog.D(LogCat.Combat,
     "defense.no_dodge", ("hid"), ("attacker"), ("why", ...))`. The reason is one of `small`, `cooldown`, `roll`,
     `state`, `late` or `path`. The entry is then left to the `BlockController` as planned.
   - **If it dodges:** it sets `Dodging = true` and cancels the entry's block plan (`RaiseAt = LowerAt = NaN`), so
     `Act()` never raises the shield for it. This happens at decision time, before the earliest possible raise. Only
     one dodge can be pending at a time; later entries decided while one is pending get `why=cooldown`.
3. **When.** It starts the roll at `HitTime - DefenseRules.DodgeLeadSeconds` (0.30 s), or at once if that moment has
   passed but 0.15 s or more are left.
4. **Which way.** This is decided at the same moment as the dodge itself (step 2), before `Dodging` is set. A won roll
   with no safe direction is a `dodge_blocked_by_ground`, and the entry keeps its block plan.
   `DefenseRules.DodgeDirections(from, area, projectile, _leftFirst)` returns the candidates in order:
   - **Area attacks:** straight away from the attacker; then 45° back-left and back-right; then left and right.
   - **Projectiles:** left and right, at right angles to the flight; then 45° back-left and back-right.
   - **Melee:** 45° back-left and back-right (the side to try first comes from `leftFirst`, which alternates, so it isn't predictable); then left
     and right; then straight back.

   Each candidate is checked 3 m out, the player's roll distance. The first one that passes all of these wins:
   - **Ground:** a ray down from 2 m above the end point hits terrain or static ground within ±1.2 m of the hireling's
     current ground height. That rules out cliffs and drops.
   - **Water:** the water level at the end point (`Floating.GetLiquidLevel`) is at most 0.4 m above the ground. No
     rolling into deep water.
   - **Clear path:** a capsule cast from the hireling's position along the direction for 3 m, at the hireling's radius
     against `static_solid`, `Default`, `piece`, `terrain` and `vehicle`, hits nothing.

   If none passes, there's no dodge, and the attack goes back to the block controller.
5. **Starting the roll** (at the time from step 3). First the chosen direction's clear-path cast is repeated; things
   may have moved since the decision. If it's now blocked, the roll is abandoned: `Dodging = false`, logged
   `defense.no_dodge why=path`, and the attack is left to the late-block rule. Otherwise:
   - `BlockController.Lower()`, a small new method that sets `m_blocking = false` and leaves every entry's plan alone,
     so attacks after this one are still blocked once the roll is over;
   - `StopMoving()`;
   - set `transform.rotation` and `m_body.rotation` to `Quaternion.LookRotation(dir)`;
   - `m_zanim.SetTrigger("dodge")`;
   - `Invincible = true`, and set the ZDO `s_dodgeinv` to true;
   - play the player's dodge effects: `m_dodgeEffects`, taken once from the `Player` prefab in `ZNetScene` and cached,
     created at the hireling's position;
   - `AddNoise(5f)`, as for players;
   - set `lastDodge = now` and `Defense.Dodges++`;
   - log `VfhLog.D(LogCat.Combat, "defense.dodge", ("hid"), ("dir"), ("attacker"), ("dmg"), ("area"), ("proj"))`.
6. **Ending the roll.**
   - Once `InDodge()` has gone true and then false again (the animation is over), set `Invincible = false` and the ZDO
     to false.
   - **Safety:** if it's still invincible 1.2 s after the start (the animation never played: a missing trigger, or a
     hireling disabled mid-roll), end it anyway and log `defense.dodge_timeout` at Warning level.
   - **Dodged hits.** If no damage reached the hireling between the start of the roll and the attack's `HitTime +
     0.3 s`, add one to `DodgedHits`. The `m_onDamaged` handler records the time of the last hit.
7. **Behaviours wait.** While `InDodge()` is true, `HirelingAI.UpdateAI` skips the behaviour tick. Timers, the reader
   and the controllers still run. The swing cooldown and the archer's draw carry on, so fighting resumes straight after
   the roll.
8. **Setting off.** The controller does nothing, and `Invincible` stays false, so the patches give vanilla's answers.
   If the setting is turned off mid-roll, the roll finishes normally (step 6) and no new one starts.
9. **Batch log.** Add this phase's commit row, and the steps for checking dodging (rows DODGE-1–4, the by-hand rolls check and the dedicated-server check), to the 0.6.0 tab of
   `docs/next-release.md`, as the batch-testing routine requires.

## Build gate
- `dotnet build -c Release` builds and deploys, with no new warnings.
- `dotnet test` passes, including new `DefenseRulesTests` cases:
  - the order of `DodgeDirections` for area, projectile and melee attacks;
  - `leftFirst` true or false swapping the two sides for melee dodges;
  - the damage-after-block decision: a block that brings the hit under 25% means no dodge.

## Test plan
Run in game through `mtb`.
- **VFH-DODGE-1:**
  1. One level 4 GuardMelee against a 2-star Troll (`enemies Troll 1 12`, then `enemy_level Troll 2`).
  2. Eventually (120 s): `defense last dodges >= 1` and `defense last dodged_hits >= 1`.
  3. Then `kill_enemies`, `kill_hirelings` and `log_errors == 0`.
- **VFH-DODGE-2** (a worker dodges):
  1. One level 5 Woodcutter, Defend stance, against a 2-star Troll at 8 m (`enemy_level Troll 2`).
  2. Eventually (120 s): `defense last dodges >= 1`.
- **VFH-DODGE-3** (projectile and area):
  1. One level 4 GuardRanged against `GoblinShaman` at 18 m. Its fireball explodes.
  2. Eventually (150 s): `defense last dodges >= 1`.
- **VFH-DODGE-4** (setting off):
  1. `set_cfg BlockAndDodge false`, then the same fight as DODGE-1 for 60 s.
  2. Check `defense last dodges == 0`.
  3. Run `set_cfg BlockAndDodge true`.
- **Regression:** the phase 01–03 rows, the combat chain, STN-1 to STN-5, RETREAT-1, TAME-1, POST-1, the follow rows
  (followers dodge too, and must keep up), and PASS-1 (rolling mustn't upset passing).
- **Dedicated server** (by hand, Tim): two players near a fight. A hireling owned by one client dodges a troll owned
  by the other, and the hit misses. Both players see the roll.
- **By eye** (Tim): rolls look deliberate. They go sideways or back, never off ledges or into water, and are not
  constant: the cooldown visibly spaces them out at low levels.

## Commit
`feat(combat): hirelings roll out of the way of hits that would hurt a lot (player's roll, i-frames synced via ZDO, safe-ground directions, per-level chance and cooldown)`

## Rollback
Revert the commit. The two patches and the controller go, and blocking (phases 02–03) is unaffected. Watch out for one
case: a hireling caught mid-roll by a hot update could keep `s_dodgeinv = true` on its ZDO. That's harmless after a
revert, because nothing reads it for a non-Player any more. As a quick fix without a revert, set
`BlockAndDodge = false`.
