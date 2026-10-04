# Hired Hands

Build a **Hiring Board** at your base and post contracts to hire wandering vikings. They chop wood, mine, keep your smelters and kilns fed, and guard your base. Pay them in food, and in coins once they're more skilled. Upgrade the board with boss trophies to hire better vikings and more of them. Craft a **Command Stone** to take them out with you: they follow you through portals and dungeons and aboard ships, gather and fight on your orders, and find their own way home.

Works in single-player and on dedicated servers. Every client and the server need the mod.

## Requirements

- BepInExPack Valheim
- Jotunn 2.30+
- YamlDotNet (ValheimModding)
- Optional: AzuAutoStore. Smelters then leave finished bars for AzuAutoStore to store (see below).

## Getting started

1. **Build a base.** The board can only be placed with a workbench, a bed and 40 built pieces within 20 m (all configurable). Boards must be 100 m apart.
2. **Build the Hiring Board** with the hammer (Wood 45, Stone 20, Deer hide 10, Leather scraps 10, Resin 10).
3. **Stock it.** Use the board's *Funds (food & coins)* to put in cooked food and coins. Only food and coins fit. Food is counted in *food points* (a food's health + stamina + eitr), and the cheapest food is used first so your best food is left alone.
4. **Post a contract** under *Manage → Contracts*: choose the job, the level, the work radius and, for guards, a stance. The hire fee is taken from the board's funds, and a viking walks in a few minutes later.
5. **Upkeep** is paid from the board every in-game day. If the board can't pay for 2 days, that hireling quits. Dead hirelings come back after a cooldown for half their hire fee (permadeath can be turned on instead).

Hover a hireling to see its name, job, level, health, what it's doing and its cargo. You can open its cargo like a chest.

## Jobs

| Job | Does | Notes |
|---|---|---|
| Woodcutter | Fells trees in its radius, clears the fallen logs and stumps, picks up wood, resin and seeds | Fells trees away from your buildings and leaves trees right next to them alone. Axe tier rises with level (stone, flint, bronze…), which decides what it can cut |
| Miner | Mines copper, tin, rocks and boulders, ore before stone | Needs a level 2 board. Leaves rocks touching your builds alone and never digs near your buildings; out in the field it digs down to ore buried up to 1.5 m deep (but never digs up a deposit nobody has found, like a hidden silver vein). Pickaxe tier rises with level |
| Smelter | Keeps smelters, kilns, blast furnaces and eitr refineries in its radius stocked from your chests | Tops a station up when it's below half full. Never takes the last of an item from a chest, and leaves a reserve of chosen items in storage (`keepInStorage`, default 50 Wood). Without AzuAutoStore it also collects the bars |
| Guard (melee) | Patrols the radius and fights | Stances: Passive, Defensive, Aggressive |
| Guard (ranged) | Same, with a bow | |

Woodcutters and miners work twice the board's radius. Everyone defends themselves; non-guards fight weakly and flee or defend by stance. **Chopping and mining make noise**, as yours does, so nearby monsters come for a worker (`GatheringMakesNoise` turns it off).

**What to gather.** Shift+E on a woodcutter or miner switches items on and off (Wood, Fine wood, Core wood…; Stone, Copper, Tin…). Each tree or rock counts as its best drop, so with Wood off a woodcutter still fells birches for their fine wood, and with Stone off a miner takes only the copper from a deposit and leaves the stone on the ground. **Works at home: off** keeps a gatherer from touching anything inside its board's area (for bases with decorative trees and rocks); it still works in the field with you.

**Deliveries.** Gatherers deliver each item type only to a chest that **already holds** that item (they never mix items into other chests), nearest first. Whatever doesn't fit is left in front of the board. Put one of an item in a chest to make it the home for that item.

**Doors.** Hirelings open doors in their way and close them behind themselves (never in a player's face). They only use doors the board's owner may use under wards, never locked doors, and don't wander through doors when idle. `HirelingsOpenDoors` turns this off.

**Workers only work while their area is loaded**, i.e. while a player is nearby, as with everything else in Valheim.

## Followers: the Command Stone

Craft the **Command Stone** at a workbench with a hiring board of a high enough level within 30 m of it. Each quality takes more followers:

| Quality | Board level | Followers | Materials |
|---|---|---|---|
| 1 | 2 | 1 | Surtling core 3, Greydwarf eye 20, Resin 20, Deer hide 10 |
| 2 | 4 | 2 | Iron 15, Guck 10, Ooze 10, Withered bone 10 |
| 3 | 6 | 3 | Black metal 15, Silver 10, Needle 10, Linen thread 10 |
| 4 | 8 | 4 | Flametal 15, Eitr 10, Black core 2, Molten core 2 |

With the stone in hand (the list of your followers shows on the left of the screen):

| Input | On | Does |
|---|---|---|
| Left click | A board's hireling | Recruit it (needs the ward, if any) |
| Left click | Your follower at home | A guard is posted on that spot; a worker goes back to work |
| Left click | Your follower in the field | Follow ↔ Stay |
| Left click | A tree or log / a rock | Your woodcutters / miners harvest it (and what it leaves behind), then follow again |
| Left click | An enemy | Your guards attack it |
| Left click | The ground | Your followers go there and hold; at home, guards are posted there |
| Right click | Your follower at home / a posted guard | Back to work / clear the post |
| Right click | Anything else | Recall: everyone within 50 m follows you |
| Middle click | Anywhere | **Retreat**: everyone drops the fight and runs with you, ignoring enemies, until 20 s pass without a hit |
| Shift+E | A hireling | Orders panel: follow mode (Follow, Stay, Gather Here), stance, what to gather, work at home, rename, send home, apply to all |
| E | Your follower | Its cargo, to load your own loot onto it |

- **Gather Here**: a woodcutter or miner works around the spot you leave it until its cargo is full.
- **Keeping up**: followers sprint when you do, catch up faster when well behind, get themselves unstuck, and as a last resort reappear just behind you when you can't see them.
- **Portals and dungeons**: followers within 20 m go through with you. One carrying what the portal won't take (ore, metal) stays behind in Stay, as you would (`AllowNonTeleportableThroughPortals`). The world modifier that lets portals take everything applies to them too.
- **Ships**: take the helm (or stand on a ship that's under way) and your followers nearby board as passengers; they step off beside you when you're ashore. If the ship sinks they're left in the water to swim after you.
- **Going home**: *Send home* in the Shift+E panel walks a follower back to its board on a timer (25 s per 100 m, 1 to 20 minutes) with its cargo; it delivers it and goes back to work. A follower left far behind, left in Stay while you're far away, or whose owner has been offline for 5 minutes goes home the same way. With `ReturnHomeWithNonTeleportable` off, it first drops what a portal wouldn't take, so going home isn't a free ore portal.
- **Posts**: a guard can stand watch on a spot at home (a tower, a gate). It watches all round, fights by its stance and walks back to its post. Archers hold fire when the shot is blocked.

## Board levels

| Level | Upgrade needs | Hirelings | Max radius |
|---|---|---|---|
| 1 | (build cost) | 2 | 20 m |
| 2 | Eikthyr trophy, Hard antler 3, Deer hide 20, Flint 20, Wood 50 | 3 | 25 m |
| 3 | The Elder trophy, Bronze 10, Core wood 40, Troll hide 5, Greydwarf eye 20 | 4 | 30 m |
| 4 | Bonemass trophy, Iron 20, Ancient bark 40, Guck 10, Withered bone 10 | 5 | 35 m |
| 5 | Moder trophy, Silver 20, Dragon tear 5, Wolf pelt 10, Obsidian 20 | 6 | 40 m |
| 6 | Yagluth trophy, Black metal 20, Linen thread 20, Needle 20, Lox pelt 5 | 7 | 45 m |
| 7 | The Queen trophy, Black core 3, Eitr 15, Yggdrasil wood 40, Carapace 20 | 8 | 50 m |
| 8 | Fader trophy, Flametal 20, Blackwood 40, Asksvin hide 10, Molten core 3 | 10 | 60 m |

A board hires up to its own level. Higher-level hirelings have more health and armour, better gear, work faster and carry more, and cost more (coins from level 2).

## Configuration

Two files in `BepInEx/config`, both synced from the server and only editable there in multiplayer:

- **`Spronglehump.HiredHands.cfg`**: rules and behaviour, in sections *General* (raw food, permadeath, respawn), *Base* (what counts as a base, board spacing), *Hiring* (arrival delay, unpaid days), *Work* (tree and rock safety distances, terrain protection and field digging, gathering noise, smelter refill threshold, chest minimums), *Followers* (portal and ship radius, catch-up, when lost followers go home, trip times), *Combat*, *Balance log* and *Debug*.
- **`Spronglehump.HiredHands.yml`**: the data tables: board levels and their upgrade costs, hireling levels (health, armour, gear, cargo, prices), per-job settings (cost multiplier, pickup list, gear by level, minimum board level, work-radius multiplier, smelter stations), raw foods, names. Edits are picked up live. An invalid file is rejected with a log message and the defaults are used.

```yaml
jobs:
  Miner:
    minBoardLevel: 2          # no pickaxe before Eikthyr
    workRadiusMultiplier: 2   # gatherers cover more ground
    pickupItems: [Stone, CopperOre, TinOre, ...]
```

## AzuAutoStore, AzuCraftyBoxes, PullMats

- The board's funds and hirelings' cargo are excluded from AzuAutoStore and AzuCraftyBoxes (and so from PullMats), so they never take your hireling money or cargo.
- With AzuAutoStore, smelters never collect bars: Azu stores them into a chest that already holds that bar. Seed one bar into a chest.

## Balance log (for server owners)

`BalanceLog` (off by default) records how hirelings fight and work, to help tune the mod: one line per fight (job, level, stance, weapon, enemy and its stars, biome, length, damage each way, outcome), death, delivery, upkeep day and hire. Every player's game sends its summaries to the server once a minute; the server writes them to `BepInEx/HiredHands/balance/YYYY-MM-DD.jsonl`, keeping at most `BalanceLogMaxMB` (20). Players appear only as scrambled ids. `scripts/balance-report.py` in the source repository turns the files into tables (kill and death rates by job and enemy, damage per second, resources per day, upkeep).

## Bug reports

Logs go to `BepInEx/LogOutput.log` and `BepInEx/HiredHands.log`, one line per event (`[VFH] … evt=…`). `vfh_debug All on` turns on detailed logging; `vfh_log_mark <text>` adds a marker; `vfh_dump_state` logs every board and hireling. None of these need cheats.

## Coming next

Ideas being considered: woodcutters replanting, a farmer, repairs, meal plans (more food for stronger hirelings), a scout. Balance tuning from the balance logs.
