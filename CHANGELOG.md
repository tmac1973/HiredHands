# Changelog

## 0.2.3 (unreleased)

- **A chop or mine order keeps the worker at the job.** It switches to Stay at that tree or rock, so it doesn't drop the job and follow you as soon as you walk on (or get sent home as left behind); it finishes and waits there for you. Orders last 10 minutes instead of 2, long enough for a big tree's logs or a whole deposit.
- **Hirelings go through the doors they open.** A hireling with no route to its goal (for example inside a fence) heads for a door and opens it, but the game's walkable map only catches up with an open door a few seconds later, so it walked away again, the door closed behind it, and it came back to open it: over and over. It now walks straight through to the far side first, and the door isn't closed until it's through.

## 0.2.2

- **Posts for gatherers.** A woodcutter or miner with "Works at home" off can be posted on a spot at home like a guard (with the stone: click it, or the ground, at home while it's following you), so it stands there instead of wandering about the base and getting in the way. Gatherers that do work at home go back to their post when they run out of work. Clear a post with a right click or the Shift+E panel.
- **Followers sneak when you do.** Crouch and your followers within 15 m crouch too: they move at crouch speed, make no footstep noise and are harder for monsters to see. A follower further behind runs to catch up first; a fight or a retreat stands them up.
- Prices show their real numbers: the board's hire fee, upkeep and promotion costs read "[1] food pts + [2] coins" instead of the amounts. They now show only what's charged ("50 coins", "60 food pts").

## 0.2.1

- **Hire with coins, pay upkeep in food.** From level 2 the hire fee is coins only (level 1 is still hired for food: there are hardly any coins in the Meadows), and the daily upkeep is food only. Promotions and respawn fees follow the hire fee. Coins are scarce, and the old daily coin upkeep came to hundreds of coins an hour of play at higher levels. **Existing servers:** the data file keeps its old values, so in `Spronglehump.HiredHands.yml` set `hireFood` to 0 for levels 2–8 and `upkeepCoins` to 0 for every level (or delete the file to get the new defaults).
- **Cargo weight limits.** Hirelings carry about what a player can: 300 at level 1, 25 more per level (475 at level 8), on top of the slot limit (`cargoWeight` per level in the data file, 0 for no limit). A gatherer that's full by weight goes to deliver, and picks up only part of a pile when that's all it can take. Handing a follower more than it can carry is refused with a message. The hover shows the weight.
- **Low funds warning.** The board's hover shows the daily upkeep and how many days its food and coins last, orange when low and red on the last day. When it's low (`LowFundsWarnDays`, default 2), the players at the base are told once a day, and the board's builder gets a map pin on it (`LowFundsMapPins`, your own setting) until it's topped up.
- **Graves for hirelings' cargo.** A dead hireling's cargo goes into a grave named after it, which floats and lasts like a player's, instead of a pile that vanilla clears after a while. A follower's grave opens only for its owner (with a map pin for them), a base worker's for anyone with ward access there. Nobody gets the corpse-run boost from it. `HirelingTombstones` off keeps the loose pile.
- The Smelter is now called the **Steward** (in time it'll do more around the base). Only the shown name changed: data files, commands (`vfh_spawn Smelter`) and saved worlds use `Smelter` as before.
- **A follower's death far from its board is no longer lost.** The board's update was sent to the game that last owned the board, which no longer had it loaded (its player had gone through a portal), and was dropped, so the board kept a dead hireling as "away", using a slot and charging upkeep. Changes now go to the owner's game as a plain message and come back to the server to apply when that game doesn't have the board loaded.
- **Boards repair themselves:** every minute the server checks each board's contracts, and one whose hireling has been missing from the world for 10 minutes is settled (an active contract counts as a death, with the board's permadeath or respawn rule; a leaving one is removed). Existing "away" contracts from 0.2.0 clear this way after the update.

## 0.2.0

Milestone 2: followers.

- **Command Stone** (4 qualities, crafted at a workbench near a board of level 2/4/6/8, 1 to 4 followers). With it in hand: recruit a hireling, give orders by clicking (harvest a tree or rock, attack an enemy, go to a spot and hold, post a guard at home, Follow/Stay), right click to recall or send back to work, middle click to **retreat**. The **Shift+E** panel on any hireling sets follow mode (Follow, Stay, Gather Here), stance, what to gather, work at home, a new name, send home, and can apply to all your followers. A list of your followers shows on screen while the stone is in hand.
- **Followers keep up**: they sprint with you, catch up faster when well behind, unstick themselves, and reappear just behind you (out of sight) when they're far behind or stuck.
- **Portals, dungeons and ships**: followers go through portals and dungeon doors with you (portal item rules apply to their cargo, including the world modifier), and board your ship as passengers.
- **Going home**: send a follower home from the field, and lost followers (left far behind, left in Stay while you're far away, owner offline for 5 minutes) walk home on a distance-based timer with their cargo. `ReturnHomeWithNonTeleportable` off makes them drop what a portal wouldn't take first.
- **Guard posts**: guards stand watch on a chosen spot at home, watching all round. Archers aim from the bow, allow for arrow drop, hold fire when the shot is blocked and shoot flyers overhead.
- **Gatherers**: choose what a woodcutter or miner gathers (switched-off drops stay on the ground) and whether it works at home at all. Miners dig down to buried ore out in the field (never near your buildings, never into an undiscovered deposit such as a hidden silver vein), give up on an unreachable chunk rather than the whole deposit, and work chunks from a little further away. A stone order covers only what you clicked and what it leaves behind.
- **Noise**: chopping and mining make the noise a player's would, so monsters come for workers (`GatheringMakesNoise`).
- **Rename** any hireling (Shift+E); the name stays on its contract.
- **Balance log** for server owners (`BalanceLog`, off by default): fight, death, delivery, upkeep and hire summaries written on the server for tuning.
- A new contract's work radius starts at the most the job allows on that board (it was always 20 m), and still goes down in 5 m steps.
- Hirelings ignore harmless wildlife (deer, hares and other passive animals) instead of chasing them; anything that attacks is still fought.
- Combat fixes: enemies can no longer sneak-attack hirelings for 4x damage, staggered hirelings don't take double damage (players don't), hirelings no longer lose their weapon or tool (showing it but punching), aiming at creatures without a head no longer errors.
- A hireling's death is now announced to its follower owner and players near where it died (on dedicated servers nobody was told).
- The dedicated-server script starts even when the client log has no Steam ID to make admin.
- Board rosters are saved in a new format (3); older boards still load, but a board saved by 0.2.0 can't be read by 0.1.x.

## 0.1.2

An early 0.1.1 build was briefly published; 0.1.2 is the full set of changes below.

- **Woodcutters chop fallen logs.** Logs weren't found by their search at all, so they cut trees and stumps and left the logs (most of the wood) lying. They now find every log in range and work it from the nearest point of its surface.
- **Hireling armor now works.** Vanilla only applies body armor to players, so hirelings took every hit unarmored. They now get the real armor of the set they wear (bronze at level 3, iron at level 4…) plus a small per-level `armorBonus`; the old `armor` value in the data file is no longer used.
- Smelters leave a reserve of chosen items in storage, counted across all chests in their radius: `jobs.Smelter.keepInStorage` in the data file, default Wood 50, so the charcoal kiln doesn't burn all your wood.
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
