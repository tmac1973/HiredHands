# Combat AI: blocking and dodging — Project Overview

## Problem
Hirelings fight like monsters: they walk up, swing (or shoot), and soak whatever comes back.
- Melee guards already raise their shield for 0.8 s when an enemy within 4 m is mid-attack (`CombatBehaviour.Melee`).
  - This is a reaction to an attack already under way, not a read of one coming.
  - There's no parry timing.
  - It doesn't improve with level.
  - It ignores anything thrown or shot.
- No hireling ever gets out of the way. A troll's slam, an abomination's sweep or a goblin shaman's fireball lands on
  guards, archers and workers alike.

This affects everyone with hirelings that fight: guards on posts, followers out in the field, and workers caught by a
raid. Hirelings take damage a player would avoid, and fights look mindless.

## Goals
- **Guards block.** Melee guards, who carry shields at every level, raise their shield against attacks they see
  coming. This covers both close attacks and projectiles flying at them.
  - Raised in time (within vanilla's 0.25 s window before the hit), the block is a **parry**: the enemy is staggered
    and the block has extra force, as for players.
  - Level decides how often they read an attack and how often they time it as a parry: about half of attacks and
    rarely a parry at level 1; nearly all and mostly parries at level 8.
- **Everyone dodges.** Any hireling in a fight can roll out of the way of an attack that would hurt a lot. This covers
  guards, archers, and workers defending themselves.
  - "A lot" means the attack's damage, after armor (and after the block, if blocking), would take more than about a
    quarter of its current health, or it's an area attack.
  - The roll is the player's: its animation, a quick move away from the attack, and a short window where hits miss.
  - The chance to read the attack rises with level. After a dodge there's a cooldown before the next one (about 6 s at
    level 1, 2 s at level 8).
- **Projectiles count.** Rocks, arrows, spears and fireballs heading at a hireling are blocked by guards with a shield,
  and dodged by the same "would hurt a lot" rule.
- **It's visible.** You can see the shield go up and the hireling roll, and parries show floating text the way they do
  for players.
- **It's measured.** The balance log and the test harness record blocks, parries and dodges, so the effect can be
  measured and tuned.
- **It's tunable.** All chances and cooldowns live in the levels table of the data file.
- **It can be turned off.** One server-synced setting turns blocking and dodging off and restores today's behaviour.
- **It ships in 0.6.0**, together with the tree patches and hirelings passing each other, which are already done.

## Non-goals
- **Tactics,** which stay in `plan/vikings-for-hire/futures.md` as a later project:
  - Tank, DPS and Hit and run;
  - Kite and Shoot from cover;
  - drawing aggro, power attacks, keeping distance.
- **Stamina** for hirelings.
- **Per-hireling switches.** There's no Shift+E toggle and nothing new in the board UI.
- **Blocking for anyone without a shield.** Workers and archers don't block, whether with tools or weapons.
- **Stat changes made up front.** Health, damage and armor stay as they are. If hirelings end up too tough, the
  chances and cooldowns get tuned instead.
- **Changes to how enemies behave.**
- **A list of attacks to maintain.** What to dodge is worked out from each attack, which also covers modded creatures.

## Users & primary flow
**Players** with guards, followers or workers that end up in fights:
1. An enemy starts an attack on a hireling, or a projectile is flying at it.
2. The hireling reads it, with a chance that depends on its level.
3. If the hit would be big, the hireling rolls away, provided its dodge cooldown is ready. The hit misses.
4. Otherwise a melee guard raises its shield and blocks. If it timed it well, it parries: the enemy staggers and
   "Parry!" pops up.
5. The fight carries on as today. A guard holds its swing for a moment while a block is due, then swings back at
   once; archers still keep their distance.

**Server owners** can turn the whole thing off with one setting.

**Tim and the developers** run the repeatable test fights through the harness and `mtb`, before and after the change,
and read the balance log from the live server.

## Constraints
- **Stack:**
  - BepInEx, Harmony, Jotunn, a publicized `assembly_valheim`, net472.
  - Hirelings are a Humanoid built from a clone of the Player prefab, so they have the player's animator, including
    its roll, and its equipment visuals.
  - The AI is `HirelingAI : MonsterAI`, with its own behaviour list. Fighting happens in
    `Hirelings/Combat/CombatBehaviour.cs`.
- **Multiplayer:**
  - A hireling's AI runs on whichever machine owns it, and damage to it is applied there, in `Character.RPC_Damage`.
  - Reading an enemy's attack has to work when that enemy is owned by another machine. That means using synced state
    such as the animator, not fields that exist only on the owner.
  - Projectiles are spawned by their owner, and seen elsewhere through their synced copies.
- **Performance:**
  - Checks run only for hirelings that are in a fight or have been recently attacked.
  - They only look at enemies within 8 m that are facing the hireling (whom an enemy is targeting is only known on the
    enemy owner's machine), and at nearby projectiles, a few times a second, not every frame for every creature.
- **Compatibility:**
  - Work with modded creatures and weapons, with no lists.
  - Don't break the existing combat rules: stances, retreat, the protections in `DamagePatches`, `AllyHits`, the
    sidearm switch.
- **Data:**
  - The new per-level numbers go in the `HirelingLevels` table: read chance, parry chance, dodge chance and dodge
    cooldown.
  - The data file can override them, as for the other level fields.
- **Telemetry:** blocks, parries and dodges are added to the balance log's `fight` record.

## Success criteria
- **Test fights cover every kind of fighter.** Repeatable fights in the test harness:
  - a level 1 melee guard against 3 Greydwarfs;
  - a level 3 melee guard against a Troll;
  - an archer against Draugr archers;
  - a worker defending itself against a Troll.

  Each is run several times before and after, through `mtb`.
- **Damage taken drops.** Hirelings take less damage per fight after the change (mean % of max health) in the melee
  guard and worker fights. In the archer fight it is no more than 2 points higher, since archers can't block and only
  projectile dodges help them.
- **Kill rates hold.** Guards win and kill about as often as before, allowing for the noise of 5 runs per matchup:
  kill % no more than 10 points lower, and fights no more than 25% longer.
- **The chances hold.** The log shows blocks, parries and dodges happening at roughly the chances in the levels table,
  for each level tested.
- **Projectiles are handled.** A guard blocks a Greydwarf's rock or a Draugr's arrow, and a hireling dodges a big
  projectile.
- **The switch works.** With the setting off, fights behave as they do in 0.5.0: the existing combat tests pass, no
  reads, parry rolls or dodges are logged, and the only blocks are 0.5.0's late blocks.
- **The existing tests still pass:** the combat, stance, retreat, tame and post tests.
- **It looks right to Tim.** Shield raises, rolls and parries read as deliberate, not twitchy, both in single player
  and on the dedicated server.

## Decisions
- **Scope** → Block and dodge only. Tactics (Tank/DPS/Hit and run, Kite, Shoot from cover) stay in futures.
- **Who blocks and who dodges** → Guards block (melee guards, the only ones with shields). Everyone who fights dodges,
  guards, archers and workers.
- **How blocking scales with level** → Reads more attacks and parries more as level rises. Both are chances per attack,
  set in the levels table.
- **What to dodge** → Attacks that would hurt a lot: after armor and block, more than about a quarter of current
  health, or an area attack. Worked out per attack, with no list.
- **What limits dodging** → A cooldown that shortens with level (about 6 s at level 1, 2 s at level 8), on top of the
  chance to read the attack. No stamina.
- **Balance** → Measure first, adjust after. No stat changes now; tune chances and cooldowns if needed.
- **Controls** → One server-synced setting to turn it off (default on), and it shows in fights. No per-hireling toggle.
- **Release** → Together with tree patches and passing, in 0.6.0.
- **Projectiles** → Both close attacks and projectiles. Guards block projectiles, and big ones get dodged.
- **Success measure** → Visible in fights, and less damage taken: repeatable before/after test fights, logged
  block/parry/dodge rates near the set chances, no regression in wins, and Tim's eye.
