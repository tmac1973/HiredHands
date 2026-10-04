# Hired Hands

Build a **Hiring Board** at your base and post contracts to hire wandering vikings. They chop wood, mine, keep your smelters and kilns fed, and guard your base. Pay them in food, and in coins once they're more skilled. Upgrade the board with boss trophies to hire better vikings and more of them.

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
| Miner | Mines copper, tin, rocks and boulders, ore before stone | Needs a level 2 board. Never digs the ground, leaves buried chunks and rocks touching your builds alone. Pickaxe tier rises with level |
| Smelter | Keeps smelters, kilns, blast furnaces and eitr refineries in its radius stocked from your chests | Tops a station up when it's below half full. Never takes the last of an item from a chest. Without AzuAutoStore it also collects the bars |
| Guard (melee) | Patrols the radius and fights | Stances: Passive, Defensive, Aggressive |
| Guard (ranged) | Same, with a bow | |

Woodcutters and miners work twice the board's radius. Everyone defends themselves; non-guards fight weakly and flee or defend by stance.

**Deliveries.** Gatherers deliver each item type only to a chest that **already holds** that item (they never mix items into other chests), nearest first. Whatever doesn't fit is left in front of the board. Put one of an item in a chest to make it the home for that item.

**Doors.** Hirelings open doors in their way and close them behind themselves (never in a player's face). They only use doors the board's owner may use under wards, never locked doors, and don't wander through doors when idle. `HirelingsOpenDoors` turns this off.

**Workers only work while their area is loaded**, i.e. while a player is nearby, as with everything else in Valheim.

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

- **`Spronglehump.HiredHands.cfg`**: rules and behaviour, in sections *General* (raw food, permadeath, respawn), *Base* (what counts as a base, board spacing), *Hiring* (arrival delay, unpaid days), *Work* (tree and rock safety distances, terrain protection, smelter refill threshold, chest minimums), *Combat* and *Debug*.
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

## Bug reports

Logs go to `BepInEx/LogOutput.log` and `BepInEx/HiredHands.log`, one line per event (`[VFH] … evt=…`). `vfh_debug All on` turns on detailed logging; `vfh_log_mark <text>` adds a marker; `vfh_dump_state` logs every board and hireling. None of these need cheats.

## Coming next

Milestone 2: a Command Stone to take hirelings with you as followers (through portals and on ships), field orders and stances on the go. The follower settings in the config already exist but do nothing yet.
