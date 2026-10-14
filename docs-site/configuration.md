# Configuration

Two files in `BepInEx/config`, both synced from the server and only editable there in multiplayer (settings marked "your
own setting" stay on each player's game).

## Spronglehump.HiredHands.cfg

Rules and behaviour, in sections:

- *General*: raw food, death mode, respawn, graves.
- *Base*: what counts as a base, board spacing.
- *Hiring*: arrival delay, unpaid days, low funds warnings and map pins, combat hirelings on or off.
- *Work*: tree and rock safety distances, terrain protection and field digging, gathering noise, chest minimums, doors,
  chest modes.
- *Steward*: chores on or off, refill and repair thresholds, board food days.
- *Farmer and Cook*, *Rancher*: chores on or off.
- *Followers*: portal and ship radius, catch-up, when lost followers go home, trip times, retreat key.
- *Combat*: combat skill preset, damage, healing, wizard settings.
- *Balance log* and *Debug*.

## Spronglehump.HiredHands.yml

The data tables: board levels and their upgrade costs, hireling levels (health, armour, cargo slots and weight, hire fee
and upkeep in food and coins, combat chances and damage), per-job settings (cost multiplier, pickup list, what gatherers
can be told to gather, gear by level, hat, minimum board level, work-radius multiplier, steward stations and reserves), raw
foods, names. Edits are picked up live. An invalid file is rejected with a log message and the defaults are used.

!!! warning "Updating doesn't change values already in this file"
    New settings are added with their defaults, but changed defaults only reach a server when you edit the file or
    delete it to get a fresh one. The changelog says when a default changed and what to set.

```yaml
hirelingLevels:
- level: 2
  hireFood: 0          # hiring from level 2 is coins only
  hireCoins: 50
  upkeepFood: 60       # upkeep is food only
  upkeepCoins: 0
  cargoWeight: 325     # 0 = no weight limit
jobs:
  Miner:
    minBoardLevel: 2          # no pickaxe before Eikthyr
    workRadiusMultiplier: 2   # gatherers cover more ground
    pickupItems: [Stone, CopperOre, TinOre, ...]
enhancements:                 # every enhancement's numbers, per job (see Enhancements)
  GuardMelee:
    Toughened.health: 0.1
    ShieldBash.cooldown: 12
enhancementSettings:
  respecCostFraction: 0.5     # a respec costs this share of the hire fee
```

The `Enhancements` setting in the .cfg (section *3 - Hiring*) turns [enhancements](enhancements.md) off for everyone.

## Balance log (for server owners)

`BalanceLog` (off by default) records how hirelings fight and work, to help tune the mod: one line per fight (job, level,
stance, weapon, enemy and its stars, biome, length, damage each way, outcome), death, delivery, upkeep day and hire. Every
player's game sends its summaries to the server once a minute; the server writes them to
`BepInEx/HiredHands/balance/YYYY-MM-DD.jsonl`, keeping at most `BalanceLogMaxMB` (20). Players appear only as scrambled
ids. `scripts/balance-report.py` in the source repository turns the files into tables (kill and death rates by job and
enemy, damage per second, resources per day, upkeep).
