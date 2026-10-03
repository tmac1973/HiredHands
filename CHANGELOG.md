# Changelog

## 0.1.0

First release: base workers (milestone 1).

- **Hiring Board** build piece, placeable only at a base (workbench, bed, built pieces nearby; boards kept apart). Funds storage for food and coins; Manage panel with Contracts, Roster and Upgrade tabs.
- **Contracts and payment**: hire fee in food points (level 1) or food and coins (level 2+), daily upkeep, quitting when unpaid, respawn after death for part of the fee (or permadeath), dismissing and cancelling with refunds.
- **Board upgrades** to level 8, each gated by a boss trophy and that biome's materials, raising the hireling cap, max level and work radius.
- **Hirelings**: randomised viking looks and names, gear and armour by level, persistent identity and cargo, shown status on hover.
- **Jobs**:
  - Woodcutter: directional felling away from buildings, clears logs and stumps, collects wood, resin and seeds.
  - Miner (board level 2+): copper, tin, rocks and boulders by pickaxe tier; never digs, skips buried chunks and rocks touching builds.
  - Smelter: keeps smelters, kilns, blast furnaces and eitr refineries stocked from chests; never takes the last item; collects bars unless AzuAutoStore is installed.
  - Guards (melee and ranged) with Passive / Defensive / Aggressive stances; workers flee or defend.
- **Deliveries** per item type, only to chests that already hold the item, with overflow left at the board.
- Gatherers work twice the board radius.
- **Compatibility**: AzuAutoStore, AzuCraftyBoxes, PullMats, PetPantry, CreatureLevelControl.
- **Configuration**: server-synced `.cfg` and a hot-reloaded YAML data file for all tables.
- **Diagnostics**: one-line structured logs, `vfh_debug`, `vfh_dump_state`, `vfh_perf`, and a ServerDevcommands test harness.
