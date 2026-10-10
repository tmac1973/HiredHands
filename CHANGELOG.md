# Changelog

## 0.7.2

- **Command Stone on a chest.** Point the stone at a chest (left click): followers near you carrying something that chest already holds walk over and put it in, then carry on (following you, or back to their spot).
- **Combat hirelings' damage is a setting.** `GuardMeleeDamage` and `GuardRangedDamage` (Combat) scale melee guards' and archers' damage on top of the level table (default 1; 0.5 halves it), for servers where a guard makes things too easy.

## 0.7.1

- **Orders tab: only food and farm produce.** The Cook's list offered gold weapons, Deep North armor, shields and staffs: the game's frost foundry is built like a cooking station, and the food preparation table's fishing bait came along too. Now a cooking station only counts when it makes food, the cauldron, prep table and mead ketill when they make food, meads, potions or ingredients, and the Farmer never lists armor, weapons, tools or trophies, whatever a mod adds. What's left out is logged (`kitchen.rejected`, `crops.rejected`).
- **Raw fish from any fish.** The food preparation table's raw fish takes any one fish; the Cook treated it as needing all twelve at once, so it never made it.

## 0.7.0

- **Combat and worker caps.** A hiring board now has two caps instead of one:
  - combat hirelings (guards, any mix): 1, 1, 2, 2, 3, 3, 4, 4 at board levels 1-8;
  - workers: 2, 4, 6, 8, then 8.

  Inside the workers there are at most 2 woodcutters, 2 miners, 1 Steward, 1 Farmer and 1 Cook per board. A base can
  be fully worked from a level 4 board, while the fighting force stays small: before, the one cap went from 2 to 10
  hirelings of any kind.
- **Refusals say which limit is full,** with the numbers: "Combat hirelings: 1/1 at this board level",
  "Woodcutter: 2/2 per board".
- **The Contracts tab** shows "Combat 1/2 · Workers 3/6 · Woodcutter 1/2", and the Upgrade tab what the next level
  adds.
- **Existing boards keep their hirelings.** A board over the new caps (say 5 guards at level 6) keeps them; only new
  contracts of that kind wait. The Steward is now one per board too: boards with two keep both.
- **Data file:** `combatCap` and `workerCap` per board level and `maxPerBoard` per job are added to existing files
  automatically (`hirelingCap` stays but is no longer used). 0.6 builds reject a file with them, so everyone moves to
  0.7 together.

- **Hirelings can come back on their own.** A new death setting, `DeathMode` (General):
  - **ReturnAfterDays** (the default for new installs): a dead hireling returns to the board by itself, free, after
    `ReturnAfterDays` in-game days (default 3; sleeping skips the night);
  - **PayToRespawn:** returns after a cooldown for a fee, as with permadeath off before;
  - **Permadeath.**

  It replaces `PermadeathEnabled`; existing configs keep their behaviour (on → Permadeath, off → PayToRespawn). The
  Roster tab counts down in days for a long wait.

**Update everyone together** (players and the server).

## 0.6.0

- **Combat: blocking and dodging.** Hirelings now see attacks coming. Guards with shields raise them just before a swing lands and turn into arrows, rocks and spears; a well-timed block is a parry ("Parry!", the attacker staggers). Any hireling in a fight (guards, archers, workers) rolls out of the way of a hit that would take more than a quarter of its health, or of an explosion, with the player's roll (the hit misses), only towards safe ground. Higher levels see more, parry and dodge more and roll again sooner (`readChance`, `parryChance`, `dodgeChance`, `dodgeCooldown` in the levels table; older data files get them automatically). Server setting `CombatSkill` (Combat) picks how good they are: Off (the 0.5.0 fighting), Green, Trained (default) or Veteran.
- **`HealthRegen`** (Combat): how fast hirelings heal once they're out of a fight, as a multiple of today's rate (1% of max health every 2 s after 10 s unhurt). 0 to 3; default 1. Health, damage and armor are unchanged; with the balance log on (`BalanceLog`), fight records now include blocks, parries and dodges: send them over if hirelings feel too tough or too weak.
- **Hirelings pass each other.** Two meeting head-on (along a fence, in a doorway) each keep to their right and slide past; two still wedged together after a couple of seconds pass through each other briefly. Server setting `HirelingsPassEachOther` (Work).
- **Tree patches: woodcutters replant.** A new **Tree patch** sign (hammer, Misc, 2 Wood), placed inside a hiring board's area, marks a woodlot; Shift+E sets its radius and what to plant. Woodcutters plant its free spots from the seeds in your chests, only kinds their axe can fell, where they'll grow and can later be felled, and fell the grown trees as usual. Server setting `WoodcuttersPlantTrees` (Work).
- **No reaching through floors and walls.** A chest (or station) within reach counts only with no other building piece in the way, so a chest upstairs is no longer filled from the ground below, nor a chest in a room through its wall: hirelings walk up the stairs or in through the door to it.
- **Stairs any way round.** A stair or stepladder is now recognised whichever way it faces (a turned stepladder could read as a gap and hirelings wouldn't use it).
- **Data file:** the new level fields (`readChance`, `parryChance`, `dodgeChance`, `dodgeCooldown`) are added to existing files automatically. 0.5 builds reject a file with them, so everyone moves to 0.6 together.

**Update everyone together** (players and the server).

## 0.5.0

- **Farmer.** A new job (board level 2, one per board) that works the ground you cultivated in its radius, holding a cultivator. It harvests every ripe crop and puts the harvest away, picks berry bushes and other regrowing plants while an order wants them (never planting bushes), and plants to the board's orders in tidy rows lined up with your own, only where the plant can grow. Crops unlock with their biome (`cropLevels`). Works with PlantEverything (its bushes and plants) and PlantEasily (its spacing and harvest radius).
- **Cook.** A new job (board level 2, one per board) that uses the kitchen you built, holding a homemade ladle: it cooks on spits and in the oven (fuelling it, staying near so nothing burns) and crafts at the cauldron, food preparation table and mead ketill, like a player (station level, fire, roof). It makes an intermediate when it needs one (dough, then bread). Recipes unlock by station (`stationLevels`, `recipeLevels`).
- **A wider board panel** (four tabs: Contracts, Roster, Orders, Upgrade).
- **Production orders.** The board's new **Orders** tab: "keep at least X in stock" orders for the Farmer and the Cook, worked top to bottom with seed orders first. A seed order is also a reserve: those seeds are never planted for produce and never used by the Cook (nor the produce the Farmer needs to grow them).
- **Server settings** *11 - Farmer and Cook*: `FarmerHarvest`, `FarmerPlant`, `CookStoves`, `CookCraft`. Debug: `vfh_crops`, `vfh_recipes`.
- **Looting Stewards reach further:** a Steward in Gather Here picks up loot within `LootRadius` (Followers, 30 m) of its spot, instead of the gatherers' 15 m.
- **Chests under raised floors:** hirelings deliver to a chest from a spot within reach of it (as a player would), instead of trying to walk to the chest itself and giving up.
- **Pausing when chests are full** now waits until there's no room at all (10 more coal fitting no longer pauses the kiln).
- **Repairs** leave pieces standing in water above half health (water wears them like rain), and skip for an hour a piece that's worn again soon after a repair with no enemy about (weak support), instead of going back to it over and over.
- **Data file:** the Farmer and Cook jobs and their level tables are added to existing files automatically. 0.4 builds reject a file with them, so everyone moves to 0.5 together.

## 0.4.3

- **Stewards stock the board with food.** A new level 1 chore, "Board food" (its own toggle in Shift+E; server setting `StewardBoard`): when a Steward's board has less than 3 days of upkeep left in food, it brings food from the chests up to 7 days (`StewardBoardRefillDays`, `StewardBoardFillDays`). Cheapest food first, as the board pays upkeep, so your best food stays in the chests; only food the board accepts (raw food only with `AllowRawFood`); never the last of a food in a chest.
- **Stewards loot in the field.** Take a Steward along with the Command Stone and park it in **Gather here**: it picks up loot lying around its spot (monster drops, trophies, coins…), nearest first, until its cargo is full. It leaves what a player has only just dropped, the board's pile and anything under a ward you can't open. Back home it puts the loot away like other leftovers.
- **Pausing when chests are full, more fixes:** room is counted in every chest in the area (a chest skipped for a minute after a failed fetch was left out, so a Steward could pause the kiln with room in a coal chest); fermenters keep a ready mead until its chests have room, and stations that hold their output (spinning wheel, eitr refinery) aren't emptied when there's nowhere to put it.
- **Logs:** `storage.full` names the chests a pause counted and the room in each; `steward.tidy_left` names items left because their chests are full; `loot.unreachable` when a looting Steward gives up on an item.

## 0.4.2

- **Workers pause when the chests are full** (server setting `PauseWhenStorageFull`, on). At home, when the chests that hold an item (the ones they deliver to) have less than a stack of room left, work that only makes more of it stops until there's room again: woodcutters leave trees for that wood, miners rocks for that ore, and Stewards stop loading a station whose product has no room (a charcoal kiln when the coal chests are full) and skip beehives, sap and tidying for full items. The status says why ("Paused: no room left for Wood", "Charcoal kiln: paused, no room left for Coal"). Orders and work out in the field aren't paused, and items no chest holds still go to the board's pile. Turn it off to keep working regardless.
- **Command Stone key hints.** With the stone in hand, the hints at the bottom right show its controls: Recruit / order (left click), Recall / back to work (right click), Retreat (middle click), Hireling panel (Shift+E).

## 0.4.1

- **Repairs leave rain wear alone.** Rain wears wood without a roof down to half health and no further, so a Steward no longer runs round topping up weathered walls: on such pieces it only repairs damage that takes them below half. Roofed pieces, pieces under a shield and pieces rain doesn't wear are repaired from 95% as before.
- **Repairs within reach.** A Steward repairs from where a player could (5 m from its eye, nothing in the way), checks it can get there before starting, and skips a piece it can't reach for 30 minutes instead of walking at it again and again. Its status says so ("Repairs: can't get to Wood beam").
- **Tidying explained in the log:** `steward.tidy_left` (once a minute) lists items lying in a Steward's radius that it leaves and why (no chest holds that item, too new, next to a player, warded, cargo full).

## 0.4.0

- **The Steward looks after the whole base.** On top of smelting stations it now keeps fires, torches, braziers and hot tubs fuelled, empties beehives and sap collectors, loads windmills and spinning wheels, feeds hungry tamed animals, repairs damaged building pieces, loads and taps fermenters, feeds shield generators, and puts items lying about away in chests that already hold them. Each chore unlocks at a Steward level matching its biome (fires, beehives and tidying at 1; smelters, kilns and animals at 2; repairs at 3; fermenters at 4; blast furnaces, windmills and spinning wheels at 5; eitr refineries and sap at 6; shield generators at 7). It always does the most urgent job first, and tidying last. See *The Steward* in the README.
- **Level 1 Stewards no longer smelt:** smelters and charcoal kilns need level 2 (blast furnaces 5, eitr refineries 6). Promote a level 1 Steward that runs your smelter.
- **Per-Steward chore list.** Shift+E on a Steward lists every chore with its state (on, off, locked until level N, handled by another mod, off on this server) and switches each one on or off. The hover and the Roster tab say what it's doing or what's missing ("Smelter: no Coal in any chest").
- **Steps aside for other mods:** with PetPantry it leaves animals alone, with Torches Eternal it leaves fires alone. Food it drops for an animal is kept out of AzuAutoStore's reach. With AzuAutoStore, stations that hold their output inside (the spinning wheel) are still emptied.
- **The Steward carries a broom** (club stats). New server settings section *10 - Steward*: each chore on or off for everyone, fire refill fraction, repair threshold, repair quiet time, and how long items lie on the ground before they're tidied.
- **Data file:** new `choreLevels` under the Steward (`Smelter`) job. An older file gains it automatically, with the windmill and spinning wheel added to its `stations` and the Steward's old club changed to the broom (gear you chose yourself is kept). 0.3 builds reject a file with it, so everyone moves to 0.4 together.
- **Fix:** items from this mod or other Jotunn mods in the data file's `gear` were thrown out as unknown at startup.

## 0.3.0

- **Hirelings find their way through your buildings.** Inside a hiring board's area, the board scans for doors and for anything that climbs from one floor to another (stairs, stepladders, modded stairs and ladders, recognised by their shape). When the game's walking map can't get a hireling somewhere (a chest upstairs, a closed room), it routes through those: it opens the door, steps through and closes it behind, walks up the stairs, and hops to the top of a ladder it can't climb. It replans around a locked door or a removed stair. Followers follow you through the house and upstairs the same way while you're both at a base. Server settings `BaseNavLinks` (on; off is the 0.2 behaviour) and `HirelingsCloseDoors` (on).
- **Stairs that run straight into each other** (two flights with no landing between) join into one climb. Small landings, and the last step onto a chest's platform, count as walkable even where the game's map has none.
- **`vfh_navlinks show|hide|scan|list|why`** draws and lists the doors, stairs and ladders found and each hireling's route; `why` plans from the nearest hireling to what you're looking at and prints every step it considered.
- **`vfh_deliver`** (cheat): the nearest hireling takes what it carries to the chests now.
- **Fixes:** the Hiring Charter no longer logs an error each time you go back to the main menu; in single player, Command Stone requests (recruit, release, posts) work again.
- **Data file:** new `navLinks` block (`include`/`exclude` prefab names) for the rare piece the shape test gets wrong. 0.2 builds reject a file with it, so everyone moves to 0.3 together.

## 0.2.4

- **Call a hireling to the board.** The Roster tab has **Call to board** for a working hireling you can't find: it drops what it's doing, comes to the board and waits beside it ("Waiting at the board"), facing you, until you press **Back to work** on the Roster tab or in its Shift+E panel. With no way there, or stuck for 20 s, it's moved there. Recruiting it as a follower also ends the wait.
- **A hireling holds still while you give it orders.** With its Shift+E panel open it stops and faces you, as it already did with its cargo open, so it doesn't walk out of range and close the panel.
- **Low funds map pin matches the board.** The pin could say a board had food for 0 days while its hover said 20. For a board loaded in your game the pin now goes by what your game sees in it, as the hover does; the server logs `funds.pin_low` with what it read whenever it calls a board low.
- **Idle hirelings stay put.** A hireling with nothing to do at home ("Works at home" off and no post, say) wandered at random round the board, into nearby buildings and up and down their stairs. It now stands at an open-air spot a few metres from the board that it has a full route to (on the board's floor if the board is indoors), and moves to another one now and then.

## 0.2.3

- **Board upgrades pull from nearby chests (AzuCraftyBoxes).** With AzuCraftyBoxes installed, the Upgrade tab counts the materials in the chests near the board that CraftyBoxes would let you pull from (its range, pull toggle and chest rules), and upgrading takes them from your inventory first, then from those chests. Per-player setting `UpgradeFromNearbyChests` (Hiring section, on by default).
- **Move a board without losing its level.** Deconstructing a board of level 2 or more with the hammer gives you a **Hiring Charter** (into your inventory, or at your feet) that remembers its level. Build a new Hiring Board while carrying it and the new board starts at that level; the charter is used up. A board destroyed by monsters or damage gives none. Its contracts still end when it comes down, as before.
- **Hirelings reach chests upstairs.** Walking counted only the distance along the ground, so a hireling could "arrive" on the stairs under a chest on the floor above and stay there for good, too far to put anything away. It now keeps climbing until the height matches too, and if it still can't get to a chest after 25 s it uses other chests (or the pile) for 5 minutes.
- **Stuck hirelings are logged** (`nav.stuck`, always on, at most every 30 s per hireling): where it is and where it's going, the height gap, what the pathfinder offered and what it was doing; a stuck hireling also tries a jump or two. `deliver.chest_unreachable` says when it gives up on a chest.
- **Followers use doorways.** A follower with a wall between it and you and no route (the game's walkable map lags behind a door you just opened) walks to the doorway, opens the door if it's closed, and goes straight through, instead of pushing against the wall beside the door until it jumped over it.
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
