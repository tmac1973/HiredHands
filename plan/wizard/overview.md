# Wizard hireling — Project Overview

## Problem
Hired Hands has two combat hirelings, the melee guard and the archer. Magic is a big part of later Valheim, and of
Therzie's Wizardry mod, which Tim's server runs: Wizardry lets players cast from the Black Forest onwards. There is no
hireling that fights with magic, so a base's defence never changes character as the game goes on, and Wizardry players
have nothing to hire that matches how they play.

## Goals
- **A new combat job, the Wizard:** a ranged caster that fights with a staff's main spell. It counts against the
  board's combat cap like guards and archers (`JobType.IsCombat`); any mix of combat jobs is allowed within the cap.
- **Unlocks by what the world allows:**
  - **With Therzie's Wizardry installed** (BepInEx GUID `Therzie.Wizardry`): from **board level 2** (Eikthyr's trophy,
    the Black Forest), when Wizardry's first staff, Evergrowth, can be crafted.
  - **Without it:** from **board level 6** (Yagluth's trophy), when the Mistlands and the vanilla Galdr table staffs
    open up.
- **A staff per hireling level, biome by biome** (a gear list in the data file, like other jobs):
  - with Wizardry: L2 Evergrowth (`StaffBlackforest_TW`), L3 `StaffSwamp_TW`, L4 `StaffMountain_TW`, L5
    `StaffPlains_TW`, L6 `StaffMistlands_TW`, L7–8 Dundr (`StaffLightning`, vanilla Ashlands);
  - without: L6 Staff of Embers (`StaffFireball`), L7 Staff of Frost (`StaffIceShards`), L8 Dundr (`StaffLightning`).
- **Fights as a caster that keeps its distance:** casts from range; when an enemy closes in it steps back to casting
  range rather than fighting hand to hand. No sidearm, no shield (it dodges like everyone, never blocks).
- **Main spell only:** no second attacks (Evergrowth's roots, meteors, Mountain staff healing, summons).
- **Endless eitr, paced by a cooldown:** like the archer's endless arrows; a setting sets the time between casts.
- **Its own damage setting,** `WizardDamage` (Combat, default 1), like `GuardMeleeDamage` / `GuardRangedDamage`.
- **What spells may hit is a setting,** `WizardSpellsHit` (Combat): `EnemiesOnly` (default) or `EnemiesAndTerrain`
  (destructible props in a blast, such as greydwarf nests, can be damaged). Never players, hirelings, tames or buildings, whatever
  the PvP or friendly-fire settings.
- **Everything an archer can do:** stances (Passive, Defensive, Aggressive), guard posts, following with the Command
  Stone, attack and retreat orders.
- **Dressed as a mage:** its own outfit list per level in the data file: Wizardry's Spellslinger robes by biome when
  Wizardry is installed; without it, vanilla Eitr-weave robes from level 6 and Embla robes at 7–8. Any item that
  doesn't exist falls back to the shared armour list. Looks only: the armour value still comes from the level table.
- **Priced at 1.5x** (`costMult`), against 1.3x for guards and archers.
- **Removing Wizardry doesn't break a base:** existing wizards stay and carry the nearest vanilla staff (Staff of
  Embers below level 6); only new wizard contracts below board level 6 are refused. Nobody is sent away.
- **Ships as 0.8.0.** (The playtest fixes once planned to ride along shipped on their own as 0.7.6.)

## Non-goals
- **The Healer.** A separate later job (Wizardry's healing areas spawned directly, plus a vanilla fallback).
- **Second attacks,** summons and conjured creatures, for any staff.
- **An eitr pool** that runs out and refills.
- **Blood magic summoning staffs** (`StaffSurtling_TW`, `StaffGolem_TW`).
- **Wizardry's other content:** its foods, meads, crafting stations and armour sets aren't used by hirelings beyond
  the robes as looks.
- **Support for other magic mods** (Magic Overhaul, Rune Magic and the like).
- **Changes to the combat cap, board levels or other jobs' prices.**

## Users & primary flow
**Players:**
1. With Wizardry installed, a player with a level 2 board posts a Wizard contract (Contracts tab, "Wizard"): it counts
   as combat ("Combat 1/1"), costs 1.5x, and the wizard walks in wearing robes with Evergrowth in hand.
2. Greydwarves attack: the wizard casts Evergrowth's bolt at them from range, steps back from one that closes in, and
   keeps casting every few seconds. Nothing but the greydwarves is hurt.
3. Promoted to level 3, it carries the Swamp staff; at 6 the Mistlands one; at 7 Dundr.
4. The player posts it on a wall with the Command Stone, or takes it along as a follower; it obeys stances and orders
   like an archer.
5. On a server without Wizardry, the Wizard job shows as locked until board level 6 ("needs a level 6 board"); then it
   casts the Staff of Embers.

**Server owners:**
- set `WizardDamage` to tone magic down, `WizardSpellsHit` to choose what blasts may damage, and the wizard's staffs,
  robes, price and unlock levels in the data file.

## Constraints
- **Where it fits in the code:**
  - `Core/JobType.cs` (`JobType`, `IsGuard`, `IsCombat`);
  - `Core/Data/DefaultData.cs` / `VfhData.cs` (per-job `Gear` sets, `MinBoardLevel`, `CostMult`, shared `ArmorSets`);
  - `Core/LevelRules.cs` (`JobUnlocked`), `Board/BoardRosterOps.cs` (contract checks);
  - `Hirelings/Combat/CombatBehaviour.cs` (melee and ranged fighting, `ArrowAim`, cooldowns, `Attacked`);
  - `Hirelings/GearApplier.cs`, `Hirelings/DamagePatches.cs` (`VfhConfig.JobDamage`), `Config/VfhConfig.cs`;
  - `UI/ContractsTab.cs`, `UI/HirelingPanel.cs`, localization in `Localization/English.json`.
- **Wizardry is optional:** detected through BepInEx `Chainloader.PluginInfos` (`Therzie.Wizardry`), like the other
  compat checks in `Compat/`. No compile-time reference to it; its staffs are found by prefab name at run time.
- **Multiplayer:** the unlock check runs where the roster is changed (the board owner's machine), as for every
  contract. Every machine on a server has the same mods, so they agree on whether Wizardry is there.
- **Staffs are player items:** casting from a hireling has to go through vanilla `Humanoid` attacks (as the archer's bow
  does); a hireling has no eitr bar, so eitr costs must not stop it. Wizardry's second attacks swap in player-only
  animations; main attacks use vanilla ones (`staff_fireball`, `staff_rapidfire`…).
- **Data file compatibility:** a new job adds entries to the data file. Older data files get them filled in from the
  defaults (`DataDefaults.FillMissing`). Everyone updates together, as the release notes say.
- **Testing:** Tim adds Therzie-Wizardry and its dependencies to the `vikingsforhire-dev` profile in Gale (before phase
  02's in-game checks); a test switch makes the mod act as if Wizardry were missing, for the without-Wizardry rows.
- **A wizard moving to another board** (a follower joining it) is checked like a new contract there: without Wizardry a
  level 2–5 board refuses it, and it stays a follower on its own board. Existing contracts are never re-checked.

## Success criteria
- **Unit tests** cover:
  - the Wizard job's kind (combat), price and unlock level with and without Wizardry;
  - the staff and outfit chosen per level with and without Wizardry, and the fallbacks when an item is missing;
  - existing wizards' staff after Wizardry is removed;
  - `WizardSpellsHit` deciding what a blast may damage.
- **In-game test rows** (ModTestBridge, with Wizardry in the dev profile):
  - with Wizardry, a level 2 board takes a Wizard contract; without (test switch), it's refused until level 6;
  - a wizard kills greydwarves with Evergrowth from range, and backs off from one that closes in;
  - a level 6 wizard without Wizardry casts the Staff of Embers;
  - a wizard's blast next to a player, a hireling, a tame, a building piece and a prop (a greydwarf nest) hurts none
    of them;
  - with `WizardSpellsHit = EnemiesAndTerrain` a blast can damage that prop; with `EnemiesOnly` it can't (trees and
    rocks only take chop and pickaxe damage, so spells never hurt them either way);
  - a Passive wizard leaves an enemy alone; `WizardDamage 0` makes a wizard harmless;
  - a wizard moving to a level 2–5 board without Wizardry is refused and stays yours;
  - a posted wizard holds its post and casts from it; a following wizard follows and fights.
- **By hand (Tim):** wizards look right (robes, staff in hand, casting animation) on the live server with Wizardry.
- **Existing combat rows still pass.**
- **The README** describes the Wizard (unlock levels, staffs, settings), and the CHANGELOG has 0.8.0.

## Decisions
- **Unlock with Wizardry** → board level 2 (Black Forest), when Evergrowth can be crafted.
- **Unlock without Wizardry** → board level 6 (Mistlands open).
- **Fighting style** → a caster that keeps its distance: steps back from enemies that close in; no sidearm.
- **Staffs** → biome by biome: Wizardry staffs L2–L6, then Dundr at L7–8; without Wizardry, Embers L6, Frost L7,
  Dundr L8.
- **Second attacks** → main attack only.
- **Healing** → not the wizard's: a separate Healer job later.
- **Eitr** → endless, paced by a cooldown setting.
- **Wizardry removed** → existing wizards keep working with the nearest vanilla staff; only new low-level contracts
  are refused.
- **Price** → 1.5x.
- **Damage setting** → its own, `WizardDamage`.
- **Outfit** → mage robes: Wizardry's Spellslinger robes, vanilla Eitr-weave and Embla robes without it; falls back to
  the shared armour list.
- **What spells hit** → a setting, `WizardSpellsHit`: enemies only (default) or enemies and terrain/props; never
  people, tames or buildings.
- **Abilities** → everything an archer can do (stances, posts, following, orders).
- **Testing** → Wizardry added to the dev profile; a test switch for the without-Wizardry case.
- **Release** → 0.8.0 (the 0.7.6 fixes were released on their own first, at Tim's request).
