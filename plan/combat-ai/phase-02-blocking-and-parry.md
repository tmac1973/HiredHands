# Phase 02 — Blocking and parrying (melee guards, close attacks)

**Depends on:** 01 (reads, hit times, `DefenseRules`, the setting, counters and fixtures) · **Enables:** 03 (blocking
projectiles uses the same controller), 04 (a dodge takes over an attack that would otherwise be blocked), 05
(block/parry rates)

## Goal
Melee guards, the hirelings with a shield, use the reads to block on time.
- **A read attack** gets the shield raised just before the hit. That's a parry if the guard wins its parry roll, and a
  plain block otherwise.
- **A missed read** gets a late block, exactly as in 0.5.0.
- **A parry** staggers the enemy (vanilla handles that) and shows "Parry!" over the guard for everyone nearby.
- **Every block and parry** is counted.
- **With `BlockAndDodge` off,** the 0.5.0 code runs unchanged.

## Files touched
- `src/VikingsForHire/Hirelings/Combat/BlockController.cs` (new): decides when to raise and lower the shield, from
  `HirelingAI.Incoming`. It has two methods: `Plan()` makes the parry roll and the `raiseAt`/`lowerAt` times, and
  `Act()` raises and lowers the shield.
- `src/VikingsForHire/Hirelings/Combat/IncomingAttack.cs`: new fields `bool? ParryWon` (null until rolled), `float
  RaiseAt` and `float LowerAt` (`float.NaN` when no raise is planned).
- `src/VikingsForHire/Hirelings/Combat/DefenseStats.cs`: new counters `ParryRolls` and `ParryWins`, counted when the roll
  is made. These measure the roll itself; `Parries` counts what actually happened.
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs`:
  - `Melee()` hands blocking to the controller when the setting is on, and keeps today's block code (lines 113–125) as
    the branch for when it's off;
  - it doesn't start a swing while a block is planned or held.
- `src/VikingsForHire/Hirelings/Combat/BlockPatches.cs` (new): a Harmony prefix and postfix on `Humanoid.BlockAttack`
  for hirelings. They count blocks and parries, and show the parry text.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: create the controller for hirelings whose left-hand item is a shield.
  Each tick it runs, in order: the reader, then `BlockController.Plan()`, then `BlockController.Act()`. Phase 04 slots
  the dodge decision in between the last two.
- `src/VikingsForHire/Localization/English.json`: `vfh_parry` = "Parry!".
- `src/VikingsForHire/Testing/FixturesCombat.cs`: the `defense` check also accepts `parry_rolls` and `parry_wins`.
- `test/alias_vfh.yaml`: rows `VFH-BLOCK-1`, `VFH-BLOCK-2`, `VFH-BLOCK-3`.

## Steps
1. **Who has a `BlockController`.** A hireling gets one when `humanoid.GetLeftItem()?.m_shared.m_itemType ==
   ItemDrop.ItemData.ItemType.Shield`. This is checked again after `Regear` and `EnsureArmed`, because job and level
   changes can add or remove the shield. Today that means GuardMelee at every level.
2. **`Plan()`.** Each tick (setting on), it plans every entry in `Incoming` that it hasn't planned yet (`ParryWon ==
   null` and not `Dodging`). The entries are melee or area attacks, ordered by `HitTime`.
   - **Read and timed:** it rolls `ParryChance` once per attack. The result goes in `ParryWon`, and `ParryRolls` and
     (on a win) `ParryWins` are counted.
     - **Parry roll won:** `raiseAt = HitTime - DefenseRules.ParryLeadSeconds` (0.15 s).
     - **Parry roll lost:** `raiseAt = HitTime - DefenseRules.EarlyBlockLeadSeconds` (0.45 s).
   - **Not read, or untimed:** no planned raise (`RaiseAt` stays NaN). The late-block rule below covers it.
   - **`LowerAt = HitTime + 0.3 s`** for planned raises.
   - **The setting is checked each tick.** When it's off, `Plan()` and `Act()` lower a shield they raised, clear the
     plan, and from then on hand over to the 0.5.0 block lines in `Melee()`. The controller object itself stays, so
     turning the setting on again mid-session needs nothing re-created.
3. **Facing.** From the moment a raise is planned, the guard turns to face the attacker, through the existing
   `HirelingAI` look-at used by `Melee()`. Vanilla's `BlockAttack` only blocks hits from in front, where
   `Vector3.Dot(hit.m_dir, forward) <= 0`.
4. **Raising and lowering (`Act()`).** It works from the soonest planned entry that isn't `Dodging`.
   - At `raiseAt`, if the guard isn't mid-swing (`!InAttack()`) and isn't staggered, set `m_blocking = true`.
     - Vanilla's `UpdateBlock` starts `m_blockTimer` at 0 then. A hit landing 0.15 s later finds it under 0.25 and is a
       parry, provided the shield's `m_timedBlockBonus > 1`. A hit landing 0.45 s later finds 0.45, which is a block.
       Tower shields (`m_timedBlockBonus == 1`) never parry, as for players.
     - If it's mid-swing at `raiseAt`, the raise is skipped and logged as
       `VfhLog.D(LogCat.Combat, "defense.block_skipped", ("hid"), ("why", "mid_swing"))`. The attack is then left to the
       late-block rule.
   - At `lowerAt`, set `m_blocking = false`, unless another planned raise falls within the next 0.3 s. In that case it
     stays up, which also means a second hit soon after can't be a parry. That's fine, and it's how vanilla works.
5. **Late block (the 0.5.0 rule).** When the target is `InAttack()` within 4 m and no raise is planned for that
   attack, raise the shield for 0.8 s. This is the existing `BlockSeconds` code, moved into the controller unchanged so
   the fallback stays identical.
6. **Swings.** `Melee()` doesn't start a swing when a planned `raiseAt` is less than 0.6 s away, or while the shield is
   up for a planned block. Its cooldown (`MeleeAttackCooldown`) carries on counting, so once the block is over it
   swings at once. Starting a swing would cancel the block, because vanilla's `IsBlocking()` is false during an
   attack.
7. **Dropping a fight.** `CombatBehaviour.Drop` and the start of a retreat call `BlockController.Reset()`, which lowers the shield and clears the plan. This replaces today's `m_blocking = false`
   lines at 70–71 and 284.
8. **`BlockPatches`**, which applies to hirelings only (`__instance` has `HirelingAI`):
   - **Prefix** on `Humanoid.BlockAttack(HitData hit, Character attacker)`: work out `wouldParry` exactly as vanilla
     does (`blocker.m_shared.m_timedBlockBonus > 1f && m_blockTimer != -1f && m_blockTimer < 0.25f`) and pass it on
     with `__state`.
   - **Postfix**, when `__result` is true:
     - `Defense.Blocks++`;
     - for a parry, `Defense.Parries++`, then
       `DamageText.instance.ShowText(DamageText.TextType.Bonus, hit.m_point + Vector3.up, "$vfh_parry")`. Vanilla
       broadcasts damage text to every client and localises it on each one, in orange at 1.5× size.
     - Log `VfhLog.D(LogCat.Combat, "defense.block", ("hid"), ("parry", wouldParry), ("attacker"), ("blocked",
       damage blocked))`.
   - **Where it runs.** `BlockAttack` runs inside `Character.RPC_Damage` on the hireling's owner, the same machine that
     runs its AI, so the counters are local.
   - **Ranged parries.** Vanilla doesn't stagger a ranged attacker on a parry (`hit.m_ranged` skips the push-back, and
     the stagger needs `attacker.m_staggerWhenBlocked`). Leave that as it is.
9. **Setting off.** As in step 2: the controller does nothing, and `Melee()` runs its existing block lines.
   `BlockPatches` still counts blocks and parries in both modes, so phase 05's before/after fights can compare them.
   These are the 0.5.0 late blocks; the overview's "off" check is about reads and dodges, not blocks.
10. **Batch log.** Add this phase's commit row, and the steps for checking blocking and parrying (rows BLOCK-1–3, and the by-hand Brute check), to the 0.6.0 tab of
   `docs/next-release.md`, as the batch-testing routine requires.

## Build gate
- `dotnet build -c Release` builds and deploys, with no new warnings.
- `dotnet test` passes. The `LocalizationCoverageTests` cover `vfh_parry`.

## Test plan
Run in game through `mtb`.
- **VFH-BLOCK-1:**
  1. One level 8 GuardMelee, Defensive stance.
  2. Three Greydwarfs at 6 m.
  3. Eventually (90 s): `defense last blocks >= 3` and `defense last parries >= 1`.
  4. Then, eventually (a further 120 s): `enemies_alive 40 == 0`, and `log_errors == 0`.
- **VFH-BLOCK-2** (parries are rare at level 1):
  1. One level 1 GuardMelee against a Greydwarf at 6 m.
  2. Eventually (90 s): `defense last blocks >= 2`.
  3. Then, eventually (a further 120 s): `enemies_alive 40 == 0`.
- **VFH-BLOCK-3** (setting off):
  1. `set_cfg BlockAndDodge false`.
  2. One level 4 GuardMelee against 3 Greydwarfs.
  3. Eventually (90 s): `defense last blocks >= 2` (the late blocks), with `defense last reads == 0` and
     `defense last parry_rolls == 0`.
  4. Then, eventually (a further 120 s): `enemies_alive 40 == 0`. Then `set_cfg BlockAndDodge true`.
- **Regression:** `vfh_test_chain combat` (COMBAT-1, -5, -6), STN-1 to STN-5, RETREAT-1, TAME-1 and POST-1 pass.
- **By eye** (Tim, `docs/next-release.md`): watch a level 5+ guard fight a Greydwarf Brute. The shield comes up just
  before each swing lands, parries show "Parry!" and stagger the Brute, and the guard swings back right after.

## Commit
`feat(combat): guards raise their shield in time for attacks they read (parry roll per attack), late block as before otherwise; Parry! text; block/parry counters`

## Rollback
Revert the commit. `CombatBehaviour` goes back to its 0.5.0 block code, and the phase 01 reader still runs harmlessly.
For a quick fix without a revert, set `BlockAndDodge = false` on the server, which brings back the 0.5.0 block code
path and stops the reads.
