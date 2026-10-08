# Phase 01 — Reading attacks (foundations)

**Depends on:** nothing · **Enables:** 02 (blocking uses the reads), 03 (projectiles feed the same stream), 04 (dodging
uses the reads and damage estimates), 05 (counters and logs)

## Goal
Hirelings notice melee and area attacks coming at them, before the hit. For each one they work out:
- **when it will land** (the hit time);
- **how much it would hurt**, after armor;
- **whether it's an area attack**;
- **whether they "read" it in time**, a roll against their level's read chance.

Nothing reacts yet. Every read is logged and counted, so this phase can be checked on its own. The phase also adds:
- the on/off setting;
- the new numbers in the levels table, overridable from the data file;
- a pure rules class with unit tests;
- the test fixtures and checks that later phases build on.

## Files touched
- `src/VikingsForHire/Core/Data/VfhData.cs`: four new `HirelingLevelData` fields, `ReadChance`, `ParryChance`,
  `DodgeChance` and `DodgeCooldown`, each with a `YamlMember` description.
- `src/VikingsForHire/Core/Data/DefaultData.cs`: `Hireling(...)` fills the new fields from the agreed curve (table
  below).
- `src/VikingsForHire/Config/DataStore.cs`: after a data file loads, fill the levels whose four new values are all 0
  (step 1).
- `src/VikingsForHire/Core/DefenseRules.cs` (new, pure, no Unity): the per-level numbers, `WouldHurtALot`, the dodge
  roll and cooldown rules, and `ParryLeadSeconds`.
- `src/VikingsForHire/Config/VfhConfig.cs`: the `BlockAndDodge` setting.
- `src/VikingsForHire/Hirelings/Combat/AttackReader.cs` (new): watches nearby enemies' animators and produces
  `IncomingAttack` records.
- `src/VikingsForHire/Hirelings/Combat/IncomingAttack.cs` (new): the record type.
- `src/VikingsForHire/Hirelings/Combat/AttackEstimate.cs` (new): estimates the damage, area flag and hit time from the
  enemy's prefab weapons and its current animation clip.
- `src/VikingsForHire/Hirelings/Combat/DefenseStats.cs` (new): per-hireling counters for reads, misses, blocks,
  parries, dodges and dodged hits. Used for checks and, from phase 05, the balance log.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: create the reader, tick it from `UpdateAI` when there's something to
  read, and expose `Incoming` (the current reads) and `Defense` (the stats).
- `src/VikingsForHire/Testing/FixturesCombat.cs`:
  - fixtures `set_cfg <key> <value>` and `enemy_level <prefab> <stars>`;
  - checks `defense <last|all> <reads|misses|blocks|parries|dodges|dodged_hits>`, which sum the `DefenseStats`
    counters.
- `tests/VikingsForHire.Tests/DefenseRulesTests.cs` (new): unit tests for `DefenseRules`.
- `tests/VikingsForHire.Tests/DataDefaultsTests.cs`: assert the new fields are set for levels 1–8 and that each one
  rises (or, for the cooldown, falls) with level.
- `test/alias_vfh.yaml`: new rows `VFH-READ-1` and `VFH-READ-2`.

## Steps
1. **Levels table.** Add `ReadChance`, `ParryChance`, `DodgeChance` (all 0–1) and `DodgeCooldown` (seconds) to
   `HirelingLevelData`. `Hireling(...)` in `DefaultData` fills them from this table:

   | Lvl | read | parry | dodge | cooldown |
   |---|---|---|---|---|
   | 1 | 0.50 | 0.15 | 0.30 | 6.0 |
   | 2 | 0.57 | 0.23 | 0.37 | 5.4 |
   | 3 | 0.63 | 0.31 | 0.44 | 4.9 |
   | 4 | 0.70 | 0.39 | 0.51 | 4.3 |
   | 5 | 0.76 | 0.46 | 0.59 | 3.7 |
   | 6 | 0.83 | 0.54 | 0.66 | 3.1 |
   | 7 | 0.89 | 0.62 | 0.73 | 2.6 |
   | 8 | 0.95 | 0.70 | 0.80 | 2.0 |

   - **Older data files:** a file without these keys loads the defaults. A missing key deserialises as 0, so
     `DataStore`, after deserializing, fills any level whose four values are all 0 from the defaults for the same level. Log this as
     `data.defaults_filled`, at Info level.
2. **The setting.** Add `BlockAndDodge` with `Synced("8 - Combat", "BlockAndDodge", true, ...)`. The description says
   that turning it off restores the 0.5.0 behaviour: a shield raised when a swing is already coming, and no dodging.
3. **`DefenseRules`**, a static, pure class:
   - `bool WouldHurtALot(float damageAfterArmor, float currentHealth, bool area)`: true when `area` is set, or when
     the damage is more than 25% of current health. The 25% is the constant `BigHitFraction = 0.25f`.
   - `bool Roll(float chance, float random01)`: `random01 < chance`. The random value is passed in, so tests can be
     deterministic.
   - `float ParryLeadSeconds = 0.15f`: how long before the hit a parrying guard raises its shield. That is inside
     vanilla's 0.25 s window, with margin for the frame rate.
   - `float EarlyBlockLeadSeconds = 0.45f`: how long before the hit a guard that read the attack but lost the parry
     roll raises its shield. That's outside the parry window, so it blocks without parrying.
   - `float DodgeLeadSeconds = 0.30f`: how long before the hit a dodge starts.
   - `bool DodgeReady(float now, float lastDodge, float cooldown)`.
4. **`IncomingAttack`**, a record with these fields:
   - `Character Attacker`, `Projectile? Projectile` (null in this phase);
   - `float HitTime` (Unity `Time.time` when it lands), `float Damage` (estimated, after the hireling's armor), and
     `bool Area`;
   - `bool Read` (the read roll), and `bool Timed` (the hit time came from the clip's attack event, step 5);
   - `Vector3 From`: a flat unit vector from the hireling towards where the hit comes from. For close attacks that's
     `(attacker.position - hireling.position)` with `y = 0`, normalised;
   - `bool DodgeDecided` and `bool Dodging`, both false until phase 04's dodge controller sets them. The block
     controller skips entries with `Dodging` set. Entries are never otherwise marked as handled: each controller
     tracks its own per-entry state (phase 02's plan fields, phase 04's `DodgeDecided`), so several attacks in flight
     are all handled, not just the first;
   - `int AttackId`: the attacker's instance id combined with the animator state's start time, so a single swing is
     one record.
5. **`AttackEstimate`** works from data every machine has. It must not read `Humanoid.m_currentAttack`, which only
   exists on the attacker's owner.
   - **Weapons** come from the attacker's prefab (`ZNetScene.GetPrefab(Utils.GetPrefabName(attacker.gameObject))`). It
     takes the `Humanoid.m_defaultItems`, `m_randomWeapon` and `m_randomSets` items. Results are cached per prefab
     name.
   - **The current attack** is matched by the animator: compare `animator.GetCurrentAnimatorStateInfo(0)` (or the next
     state, during a transition) with each weapon's `m_attack.m_attackAnimation`, using
     `Animator.StringToHash`/`IsName`.
     - With no match, assume the strongest of the prefab's weapons, which is the cautious choice.
     - For non-Humanoid attackers (wolves, deathsquitos and the like, which use `Character` with an `Attack` on
       `ItemDrop`), the same applies to whatever default items they have.
   - **Damage** is the matched weapon's `m_shared.m_damages`, then:
     - scaled for stars by vanilla's formula, `1 + max(0, attacker.GetLevel() - 1) * 0.5`. This copies
       `Attack.GetLevelDamageFactor`, which lives on `Attack`, not on `Character`;
     - scaled by the world's enemy-damage modifier, `Game.m_enemyDamageRate`, which `Character.RPC_Damage` applies;
     - reduced by the hireling's armor with `HitData.DamageTypes.ApplyArmor(armor)`, using `Hireling.Armor`, the
       same total armor `DamagePatches` applies;
     - then summed with `GetTotalDamage()`.
   - **Area** is true when `m_attack.m_attackType == Attack.AttackType.Area`, or when the attack spawns an area effect
     on trigger (`m_attack.m_spawnOnTrigger` is set).
   - **Hit time**:
     - From the current clip's `AnimationEvent`s, find the first one whose `functionName` is `"OnAttackTrigger"` or
       `"AttackTrigger"`.
     - Hit time = state start time + `event.time / (clip.length) * stateLength / animator.speed`, worked out from
       `AnimatorStateInfo.normalizedTime` at the moment the state was first seen.
     - With no event found, assume `Time.time + 0.5f`, and mark the estimate `Timed = false`. An untimed attack can be
       blocked late but never parried or dodged on purpose.
6. **`AttackReader`**, one per hireling, ticked from `HirelingAI.UpdateAI` before the behaviours run:
   - **When it runs:** when `BlockAndDodge` is on, and the hireling either has a combat target or took damage in the
     last 10 s (`_combat.CombatTarget != null || Time.time - LastHitTime < 10`). Otherwise it clears its state and
     returns.
     - `LastHitTime` is a new `HirelingAI` field, set to `Time.time` in the existing `m_onDamaged` handler
       (`HirelingAI.cs:178-186`).
     - The setting is checked on every tick, so turning it on or off mid-session (with `set_cfg`, or by the server
       owner) takes effect at once.
   - **What it watches:** enemies from `ThreatScanner`'s list that meet all of these:
     - within 8 m;
     - facing the hireling: the angle between the enemy's forward and the direction to the hireling is under 60°.
       Facing is the test, rather than "is targeting this hireling", because an enemy's target is only known on the
       enemy owner's machine;
     - whose animator has just entered an attack state (`attacker.InAttack()`, which reads the animator and so works
       for remote enemies), and that isn't already recorded under this `AttackId`.

     It checks every 0.1 s, not every frame.
   - **New attacks:** each one is estimated (step 5), rolls `ReadChance` for the hireling's level (`Hireling.LevelData`)
     and goes into `Incoming`. Records are dropped 0.5 s after their `HitTime`.
   - **Counting:** each read adds to `Defense.Reads` or `Defense.Misses`.
   - **Logging:** `VfhLog.D(LogCat.Combat, "defense.read", ("hid"), ("attacker"), ("dmg"), ("area"), ("in", hitTime -
     now), ("read"), ("timed"))`. These are Debug lines, which go to `HiredHands.log`. Summary counts reach
     `LogOutput.log` through the `defense` check.
7. **Fixtures and checks** in `FixturesCombat.cs`:
   - `set_cfg <key> <value>` sets a config entry through `VfhConfig.Find` and `BoxedValue`, then logs it. Synced
     settings set on the server reach clients through Jotunn's normal sync.
   - `enemy_level <prefab> <stars>` sets the stars of the nearest spawned enemy of that prefab, for repeatable hard
     hits. Vanilla counts levels from 1 (no stars), so it calls `Character.SetLevel(stars + 1)`: `enemy_level Troll 2`
     gives a 2-star Troll at level 3, with ×2 damage.
   - `defense <last|all> <counter>` returns the summed counter.
8. **Unit tests** in `DefenseRulesTests`, covering:
   - `WouldHurtALot` at the 25% boundary, and the area override;
   - `Roll` at 0 and at 1;
   - `DodgeReady` either side of the cooldown;
   - the default curve: monotonic for every field, with the exact values at levels 1 and 8.
9. **Test rows** in `test/alias_vfh.yaml`:
   - `VFH-READ-1` (macro `vfh_t_read1`):
     1. one level 8 GuardMelee, Passive stance (so it doesn't attack first);
     2. spawn 2 Greydwarfs at 6 m;
     3. eventually (120 s) `defense last reads >= 20`. A Passive level 8 guard (650 health) survives two Greydwarfs
        for that long;
     4. check that `log_errors == 0`.
   - `VFH-READ-2` (macro `vfh_t_read2`): the same with `set_cfg BlockAndDodge false`; after 30 s, `defense last reads == 0` and
     `defense last misses == 0`. It finishes with `set_cfg BlockAndDodge true`.
10. **Batch log.** Add this phase's commit row, and the steps for checking the reader (rows READ-1/2), to the 0.6.0 tab of
   `docs/next-release.md`, as the batch-testing routine requires.

## Build gate
- `dotnet build -c Release` (repo root) builds and deploys `HiredHands.dll` with no new warnings.
- `dotnet test` passes, including `DefenseRulesTests` and the updated `DataDefaultsTests`.

## Test plan
- **Unit tests:** as above.
- **In game**, through `mtb restart` and then `mtb run "vfh_test_chain read1 read2" "evt=test.result row=VFH-READ-2" 300`. `vfh_test_chain` takes macro names,
  with the `vfh_t_` prefix optional, so no chain alias is needed. The rows' macros are `vfh_t_read1`
  and `vfh_t_read2`:
  - both rows pass;
  - `HiredHands.log` shows the `defense.read` lines from READ-1: at least 20 of them, with between 85% and 100% having
    `read=true`; every Greydwarf read has `timed=true` and `in=` between 0.2 and 1.0 s.
- **Regression:** `vfh_test_chain combat` (VFH-COMBAT-1, -5 and -6) and the stance rows STN-1 to STN-5 pass unchanged.
  Nothing reacts yet, so fights are as before.

## Commit
`feat(combat): read incoming attacks (hit time, damage after armor, area) with a per-level read chance; BlockAndDodge setting, defense counters and test fixtures`

## Rollback
Nothing outside the reader acts on its output, so a partial state is harmless. With the reader off
(`BlockAndDodge = false`), behaviour is exactly 0.5.0, which is also the quick fix without a revert.

To roll back, revert the commit **except** the four new `HirelingLevelData` fields in `VfhData.cs`, which stay, unused.
The reason:
- `DataYaml.Deserialize` rejects unknown keys.
- `DataStore` writes the default data file on first run whenever there isn't one, so any install that ran this build
  has `readChance`, `parryChance`, `dodgeChance` and `dodgeCooldown` in its file.
- Keeping the fields lets those files go on loading.

## Implementation notes (as built, f95f9f2)
- **The test fixture** is the existing `cfg_set <key> <value>` rather than a new `set_cfg`. Later phases' rows use
  `cfg_set` too.
- **The weapon** comes from the attacker's synced right-hand item (`ZDOVars.s_rightItem`, then
  `ObjectDB.GetItemPrefab`), not by matching animator state names. Monsters equip the weapon of the attack they're
  making, and the ZDO value is the same on every machine. The fallback is `GetCurrentWeapon()`, then a damage of 0.
- **The hit event** is `Hit` or `OnAttackTrigger`: `CharacterAnimEvent` has both.
- **Older data files:** `DataYaml`'s `FillMissing` already fills keys missing from list entries (levels) from the
  shipped defaults, so no extra loader code was needed. A unit test covers a 0.5.0-style file.
- **VFH-READ-1** uses three waves of 2 Greydwarf Brutes and asserts `reads >= 6`. A level 8 guard kills plain
  Greydwarfs too fast to collect 20 reads in one row. The 85–100% read rate is checked in `HiredHands.log` across the
  run instead.
