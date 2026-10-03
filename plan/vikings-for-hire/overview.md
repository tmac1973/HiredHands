# Vikings for Hire — Project Overview

## Problem
Valheim is a game of heavy, repetitive resource work: chopping wood, mining ore, and keeping smelters and kilns fed. Bases also sit undefended while players are away. Solo players and small groups have no way to hand off that busywork, and on raids or long trips they carry everything themselves. Existing NPC/companion mods tend either to be overpowered (free and unlimited labor) or to need constant micromanagement.

Vikings for Hire adds a hiring economy. Players pay gold to bring in wandering vikings, who do base jobs automatically or follow the player into the field. Progression goes through Valheim's boss ladder, so the labor is earned, costs ongoing gold, and never replaces the core gameplay loop.

## Goals
- Add a **Hiring Board** build piece (Hammer) that can only be placed at a qualifying base.
- Let players post **contracts** for these jobs: **Woodcutter, Miner, Smelter, Guard (Melee), Guard (Ranged)**. Each contract picks a hireling level up to the board's max level, and a work radius.
- Charge for hirelings with a **hire fee plus daily upkeep**, both scaling with job and level and paid from the board's own storage. Level 1 hirelings cost **food only**. Level 2+ cost **food + Coins**, since gold isn't really available in the Meadows. Food is measured in **food points** from each item's vanilla health + stamina values, so better food goes further. By default only cooked or prepared food counts.
- Give the board **8 levels** (L1 base build, then L2–L8 upgrades with each boss trophy in order plus mats from that boss's biome). Board level raises the max hireling level and the hireling cap.
- Scale hirelings by level: health, damage/combat efficiency, gathering speed, carry capacity, roam range, and cosmetic tier-appropriate vanilla gear.
- Generate each viking's look randomly from the player customization options (gender, hair, beard, skin and hair color).
- Make gatherers carry their own loads and deliver each item type separately to a chest that already holds that item. If there's no room, they drop it next to the board. They should work cleanly alongside **AzuAutoStore**.
- Give every hireling stance-based autonomous combat (workers: Flee / Defend, guards: Passive / Defensive / Aggressive), so fights never need micromanaging.
- Add a **Command Stone** (milestone 2): a 4-level upgradeable item that lets players take hirelings with them as followers. It gives context orders (look-and-use), a stance hotkey, portal travel, and a follower cap by stone level.
- Add an **orphan "return home"** system: followers who get stuck, separated, or left behind despawn and reappear at their home board after a distance-based timer, still holding their cargo.
- Make every number, cost, recipe, level table, and toggle configurable, with sensible server-synced defaults.

## Non-goals
- No Porter job in this plan (AzuAutoStore covers sorting and hauling). The job framework must still let one be added later.
- No Farmer or Forager jobs in this plan.
- No XP or levelling over time. Level is fixed at hire, and promotion is a paid upgrade.
- No player-supplied gear. Hireling gear is cosmetic and can't be looted.
- No bypassing vanilla portal restrictions: followers carrying non-teleportable items can't use portals by default.
- No server-optional or client-only mode. Every client and the server must run the mod.
- No explicit compatibility work for mods other than AzuAutoStore, AzuCraftyBoxes and PullMats (the player's own mod set). Others are best-effort.
- No custom 3D art pipeline beyond what's needed for the board and stone. Reuse vanilla assets and prefabs wherever possible.

## Users & primary flow
**Users:** Valheim players on single-player or dedicated/hosted multiplayer servers running BepInEx + Jotunn.

**Milestone 1 — Base workers**
1. The player builds a base: at least one workbench, at least one bed, and at least N player-built pieces within a set radius.
2. The player places a **Hiring Board** with the Hammer. If the base doesn't qualify, or another board is too close, placement is blocked and a message lists what's missing.
3. The player stocks the board's storage with food (and Coins for L2+ hirelings). They open the board UI and post a contract by choosing job, level (up to the board max), work radius, and combat stance. The hire fee (food points, plus Coins at L2+) is taken from the board's storage.
4. After a configurable delay, a randomly generated viking spawns at the edge of the base and walks to the board. The contract is now active.
5. The hireling works on its own within the radius:
   - **Woodcutter / Miner:** harvest, carry, and deliver each item type separately to a chest that already holds that item. If none has room, drop it next to the board.
   - **Smelter:** load ore and fuel from chests into smelters, kilns, charcoal kilns, and blast furnaces in range. Collect the output only when AzuAutoStore isn't installed. Never get confused when AzuAutoStore takes the output first.
   - **Guards:** patrol the radius and fight threats based on their stance.
6. Each in-game day, upkeep (food points, plus Coins at L2+) is taken from the board's storage. If upkeep goes unpaid for a set number of days, the hireling leaves.
7. The player upgrades the board using a boss trophy plus biome mats. This unlocks higher hireling levels and a bigger cap. Existing hirelings can be promoted for the fee difference.
8. When a hireling dies, it's gone for good by default and drops what it carried (configurable: respawn at the board after a cooldown).

**Milestone 2 — Followers**
1. With a board at level 3 or higher near the workbench, the player crafts a **Command Stone** (upgradeable to L4 with board L4/L6/L8).
2. With the stone in hand, the player aims at one of the board's hirelings and presses the primary attack to recruit it as a follower, up to the stone's cap. Its base work is suspended, but the contract stays active: it still counts against the board cap and still pays upkeep.
3. Followers walk with the player and go through portals with them, unless they carry non-teleportable items, in which case they stay behind with a message. When the player takes a ship's helm, nearby followers are stowed aboard as passengers and reappear beside the player on disembarking.
4. With the stone in hand, the player aims at a target and presses the primary attack: rock/ore = mine, tree = chop, enemy = attack, ground = move/hold there. A hotkey cycles Follow / Stay / Gather Nearby, and another sets combat stance.
5. Back inside the home board's radius, "Release to work" (Command Stone) (or logging out at base) makes them deposit their cargo and go back to their contract.
6. If a follower is orphaned (stuck, too far for too long, owner dead or logged out elsewhere), it enters **Returning Home**: it despawns and reappears at its board after a distance-based timer (about walking speed, with a minimum), still holding its cargo.

## Constraints
- **Stack:** Valheim (current public release), BepInEx 5, **Jotunn** (2.30.x), **ValheimModding-YamlDotNet** for pieces, items, recipes, localization, config sync, and the mod version check. Use Harmony patches only where Jotunn has no API. Use vanilla systems (ZNetView/ZDO, MonsterAI/BaseAI, Humanoid, VisEquipment, Container) instead of rewriting them.
- **Multiplayer:** All hireling and board state lives in ZDOs, so it survives zone unload, owner handoff, and server restart. AI runs on the ZDO owner. Board and hireling changes (contracts, payments, upgrades, caps, mode changes) are applied by the object's ZDO owner through RPCs, which is the same pattern vanilla containers use, so two players can't race each other. When no client has the object loaded, the server takes ZDO ownership and applies the same operation itself, so changes work in unloaded zones too. World-wide checks (global board cap, orphaned followers whose owner is offline) run on the server. Server config syncs to clients and locks admin-only values.
- **Project conventions:** Mirror the `vh_pull_mats` setup: GUID prefix `Spronglehump`, net472 SDK project, `Directory.Build.props` with a Gale dev profile, deploy-to-Gale and Package targets, an xUnit test project linking the pure `Core/` code, a local dedicated-server script, and a docs/test-checklist.md matrix.
- **Required everywhere:** Jotunn's NetworkCompatibility check enforces that the mod is on both server and clients.
- **Soft dependencies:** AzuAutoStore, AzuCraftyBoxes and PullMats, detected at runtime with no hard references. None of them may ever store into, or pull from, the hiring board's storage or a hireling's cargo. AzuAutoStore also changes the Smelter's output handling. The dev profile runs all three, so they're part of normal testing, not just special compat rows.
- **Balance:** Achievable but not cheap. Recipes use biome-appropriate mats in meaningful quantities. Ongoing upkeep is a permanent gold sink. Defaults favor vanilla balance (portal rules, no free teleports).
- **Performance:** Hireling AI must be throttled (scan intervals, cached target searches) so a full base of hirelings at max cap doesn't noticeably hurt server or client frame time.
- **Diagnostics:** Structured, greppable debug logging from the start (phase 02). One line per event with a fixed prefix, timestamp, network role, category and the ids involved (board id, hid, contract id, ZDOID). It's written both to the BepInEx log and to a dedicated `VikingsForHire.log`. Categories can be toggled in config, and console commands dump the live state. Testing goes single-player first, then the local dedicated server, so every log line records the network role.
- **Configuration:** Every tunable (costs, recipes, level tables, radii, timers, caps, toggles) is in BepInEx config with sensible defaults and server sync.

## Success criteria
- The board can only be placed at a qualifying base and at least the minimum distance from other boards, with clear failure messages.
- Each of the five v1 jobs works on its own for at least one full in-game day with no player input, at every board level, in single-player and on a dedicated server with two clients.
- Gatherers never put mixed item types into a single chest, and fall back to the drop pile next to the board correctly.
- With AzuAutoStore installed, smelters stay fed and the Smelter hireling doesn't stall or loop because output went missing. Without it, the Smelter collects output into chests.
- Food-point and Coin fees, upkeep, unpaid-wage departure, upgrades (trophy + mats), promotion, and caps all behave as configured.
- Hirelings, their contracts, and carried inventories survive zone unload/reload, logout/login, and server restart.
- (Milestone 2) Followers go through portals with the player, respect the vanilla teleport restrictions, obey context orders and stances, and orphaned followers reliably return home with their cargo after the expected timer.
- Changing any config value on the server is reflected on connected clients.
- With the maximum number of hirelings active at one base, frame time doesn't regress noticeably compared with the same base without hirelings.

## Decisions
- **v1 scope** → Base workers first (board, contracts, five jobs, upgrades). Command stone, following, and portals are milestone 2 of the same plan.
- **How vikings arrive** → Delayed arrival: they spawn at the base edge after a configurable timer and walk to the board.
- **Payment model** → Hire fee + daily upkeep, both paid from the board's storage. They leave after N unpaid days.
- **Currency** → L1 hirelings cost food only. L2+ cost food + Coins. Food is a pool of 'food points' from each item's vanilla health + stamina. Only cooked or prepared food by default (raw food is a config toggle).
- **Death** → Configurable. Default is permadeath with a drop of carried items. The alternative is respawning at the board after a cooldown.
- **Porter / logistics** → Porter removed (AzuAutoStore covers it). Gatherers carry and deliver per item type to chests already holding that item, falling back to a drop pile next to the board. Followers returning to base deposit their cargo the same way.
- **v1 jobs** → Woodcutter, Miner, Smelter, Guard (Melee), Guard (Ranged). The Smelter mainly loads ore and fuel, collects output only when AzuAutoStore is absent, and must tolerate items vanishing because of AzuAutoStore.
- **Base definition** → Workbench + bed + N player pieces in a radius, checked when placing the board.
- **Ownership** → Boards and base workers are shared (ward/access rules apply). Hire caps are per board. Followers belong to the player holding the command stone, and the follower cap is per player.
- **Board tiers** → 8 levels: L1 base, then the Eikthyr, Elder, Bonemass, Moder, Yagluth, Queen, and Fader trophies, each with mats from that biome.
- **Hireling progression** → Level fixed at hire. A paid promotion raises a hireling to the board max for the fee difference.
- **Gear** → Cosmetic vanilla gear chosen by level and job. Stats come from config tables. Gear can't be looted.
- **Field orders** → Command stone held in hand: aim + primary attack gives context orders (and recruits hirelings or releases them back to work). Hotkeys cycle follow mode and stance. Guards: Passive / Defensive / Aggressive. Workers: Flee / Defend. Combat is autonomous, with no micromanagement.
- **Going home** → Followers walk with the player and resume work with "Release to work" (Command Stone) at base. Orphaned followers (stuck, separated, owner dead or offline) despawn and respawn at their board after a distance-based timer, keeping their cargo.
- **Portal ore** → Respect vanilla teleport restrictions by default (configurable).
- **Ore via return-home** → Allowed, but the timer matches roughly walking pace with a minimum, so it's no faster than walking.
- **Command stone** → Upgradeable item, 4 levels: needs board L3 / L4 / L6 / L8 nearby, follower cap 1 / 2 / 3 / 4.
- **Install & compat** → Required on the server and all clients. Server config is synced. AzuAutoStore, AzuCraftyBoxes, PullMats and PetPantry are explicitly supported soft dependencies. Multiplayer testing happens on Tim's live server (Gale profile `1dotohsupermodded`, about 55 mods); the phase 05 file lists the mods there that affect hirelings. ValheimPlus isn't supported and stays disabled in the dev profile.
- **Board stacking** → One board per base area (minimum distance between boards, default 100m). Optional world-wide cap, default unlimited.
- **Plugin identity** → `Spronglehump.VikingsForHire` (same author prefix as PullMats). Thunderstore package `Spronglehump-VikingsForHire`.
- **Board destroyed** → All of its contracts are void. Its hirelings (including followers) drop their cargo and leave. Its stored food and coins drop like a chest's. Deconstructing a board that still has hirelings asks for confirmation first.
- **Offline work** → Hirelings only work while their zone is loaded. Upkeep is charged only for days the base was loaded.
- **Ships** → Followers within 20m are stowed as passengers on the ship's ZDO when you take the helm, and reappear when you disembark. If the ship is destroyed, they Return Home.
- **Debug logging** → Robust structured logging is built in phase 02 and required in every phase: per-category toggles, a dedicated log file, id-tagged single-line events and state-dump commands, so bug reports can be traced from the logs. Testing goes single-player first, then the local dedicated server.
- **Test macros** → JereKuusela Server_devcommands aliases (`test/alias_vfh.yaml`), one macro per checklist row. They chain the mod's cheat fixture commands (build a base, stock chests, spawn trees, ore, enemies, portals) with `vfh_assert` checks that write PASS/FAIL `cat=Test` lines to the log, and a `fast_timers` mode shortens waits.
