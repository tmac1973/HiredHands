# Changelog

## 0.1.1

- Repairing a hiring board with the hammer no longer brings up the "remove this board?" dialog.
- **Doors:** hirelings open doors in their way, head for a door when there's no other route, and close each door behind them (not while a player is in it). Only doors the board's owner may use under wards; never locked doors; idle wandering never goes through doors. `HirelingsOpenDoors` (Work) turns it off.
- Investigating a frame-rate drop reported on a heavily modded server: `vfh_perf` now also reports the time spent in each of the mod's game hooks, a `perf.minute` line logs frame rate and hook cost every minute, and `vfh_patches off|on` removes and restores the hooks for an A/B comparison.
- Hirelings fight back much sooner: they swing whenever their swing is ready and only raise the shield in between (guards used to block for as long as the enemy kept attacking), stop at a big enemy's edge instead of pushing into it, and look around for enemies every 0.5 s (`ThreatScanIntervalSeconds`).
- Tames (pets, summons, Defend Your Base guardians) can no longer hurt hirelings, matching hirelings not hurting tames, and a hireling hit by a tame doesn't turn on it. A hireling's death log now names what last hit it.
- The cargo-slot hook on inventory grids no longer loops over every slot when the grid isn't a hireling's cargo.
- Fixes since 0.1.0: miners stand at the rock's real surface and keep at copper they can nearly reach; drops they can't reach are skipped; the "other boards nearby" check retries when an answer is lost; settings missing from an older data file use the shipped defaults.

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
