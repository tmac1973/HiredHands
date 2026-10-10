# Hired Hands

Build a **Hiring Board** at your base and post contracts to hire wandering vikings. They chop wood, mine, farm, cook, look after your base (fires, smelters, beehives, animals, repairs and more) and guard it. Hire them for coins (level 1 for food: there are hardly any coins in the Meadows) and feed them every day. Upgrade the board with boss trophies to hire better vikings and more of them. Craft a **Command Stone** to take them out with you: they follow you through portals and dungeons and aboard ships, gather and fight on your orders, and find their own way home.

Works in single-player and on dedicated servers. Every client and the server need the mod.

## Requirements

- BepInExPack Valheim
- Jotunn 2.30+
- YamlDotNet (ValheimModding)
- Optional: AzuAutoStore, AzuCraftyBoxes, PetPantry, Torches Eternal (see below).

## Getting started

1. **Build a base.** The board can only be placed with a workbench, a bed and 40 built pieces within 20 m (all configurable). Boards must be 100 m apart.
2. **Build the Hiring Board** with the hammer (Wood 45, Stone 20, Deer hide 10, Leather scraps 10, Resin 10).
3. **Stock it.** Use the board's *Funds (food & coins)* to put in cooked food and coins. Only food and coins fit. Food is counted in *food points* (a food's health + stamina + eitr: a cooked meat is 40), and the cheapest food is used first so your best food is left alone.
4. **Post a contract** under *Manage → Contracts*: choose the job, the level, the work radius (it starts at the most the job allows) and, for guards, a stance. The hire fee is taken from the board's funds, and a viking walks in a few minutes later.
5. **Feed them.** Upkeep is paid in food from the board every in-game day, only while the base is loaded (days away are free). If the board can't pay for 2 days, that hireling quits.

**What it costs.** Hiring is paid in **coins** (level 1 in food, since there are hardly any coins in the Meadows); upkeep is always **food**. Promoting a hireling costs the difference in hire fees, and bringing a dead one back costs half its hire fee (or turn on permadeath). Each job scales the prices: Steward ×0.9, Woodcutter ×1, Miner ×1.1, guards ×1.3.

| Level | Hire fee | Upkeep a day | Health | Cargo slots | Carries (weight) |
|---|---|---|---|---|---|
| 1 | 150 food | 40 food | 80 | 8 | 300 |
| 2 | 50 coins | 60 food | 120 | 10 | 325 |
| 3 | 150 coins | 90 food | 180 | 12 | 350 |
| 4 | 300 coins | 120 food | 250 | 16 | 375 |
| 5 | 500 coins | 160 food | 330 | 20 | 400 |
| 6 | 800 coins | 200 food | 420 | 24 | 425 |
| 7 | 1200 coins | 250 food | 520 | 28 | 450 |
| 8 | 1800 coins | 300 food | 650 | 32 | 475 |

**Running low.** The board's hover shows the daily upkeep and how many days its food and coins last, orange when it's low and red on the last day. When it's down to 2 days (`LowFundsWarnDays`), everyone at the base is told once a day, and the board's builder gets a map pin on it until it's topped up.

**Hirelings.** Hover one to see its name, job, level, stance, health, what it's doing and its cargo (slots and weight). Open its cargo with E like a chest; Shift+E opens its orders panel (stance, rename, and more for gatherers and followers). A hireling carries about what you can: it's full when its slots or its weight limit run out, and a gatherer then goes to deliver. Can't find one? **Call to board** on the board's Roster tab brings it to the board to wait there (off work) until you press **Back to work** (there or in its Shift+E panel); a hireling with no way there is moved there. It holds still while its Shift+E panel is open.

**Deaths.** When a hireling dies, its cargo goes into a **grave** named after it, which floats and lasts like a player's (it doesn't despawn). A follower's grave opens only for its owner, who gets a map pin on it; a base worker's opens for anyone with ward access there. Its weapon and armour aren't dropped. You're told when one of yours dies. What happens next is the server's choice (`DeathMode`, General):
- **ReturnAfterDays** (default for new installs): it comes back to the board by itself, free, after `ReturnAfterDays`
  in-game days (3; a day is 30 minutes of play, and sleeping skips the night).
- **PayToRespawn:** it comes back after `RespawnCooldownSeconds` once the board pays half its hire fee
  (`RespawnCostFraction`).
- **Permadeath:** it's gone, and the contract ends.

Either way its slot on the board stays taken while it's away. Configs from older versions keep what they had
(`PermadeathEnabled` on becomes Permadeath, off becomes PayToRespawn).

## Jobs

| Job | Does | Notes |
|---|---|---|
| Woodcutter | Fells trees in its radius, clears the fallen logs and stumps, picks up wood, resin and seeds | Fells trees away from your buildings and leaves trees right next to them alone. Axe tier rises with level (stone, flint, bronze…), which decides what it can cut |
| Miner | Mines copper, tin, rocks and boulders, ore before stone | Needs a level 2 board. Leaves rocks touching your builds alone and never digs near your buildings; out in the field it digs down to ore buried up to 1.5 m deep (but never digs up a deposit nobody has found, like a hidden silver vein). Pickaxe tier rises with level |
| Steward (was the Smelter) | Looks after the base: fires, beehives, smelting stations, mills, animals, repairs and more (see *The Steward* below) | One per board. Armed with a broom. Still `Smelter` in the data file and commands |
| Farmer | Plants and harvests your cultivated field to the board's orders (see *The Farmer and the Cook* below) | Needs a level 2 board; one per board. Carries a cultivator |
| Cook | Cooks on your spits and in your oven, crafts at the cauldron, prep table and mead ketill, to the board's orders | Needs a level 2 board; one per board. Carries a ladle |
| Guard (melee) | Patrols the radius and fights | Stances: Passive, Defensive, Aggressive |
| Guard (ranged) | Same, with a bow | |

Woodcutters and miners work twice the board's radius. Everyone defends themselves; non-guards fight weakly and flee or defend by stance. **Chopping and mining make noise**, as yours does, so nearby monsters come for a worker (`GatheringMakesNoise` turns it off).

**Tree patches.** Build a **Tree patch** sign (hammer, Misc, 2 Wood) inside a hiring board's area to mark a woodlot. Shift+E on it sets its radius (3–20 m) and what to plant (any common tree that grows there, from beech seeds, birch seeds, acorns, fir or pine cones; or one kind, including PlantEverything's ancient or Ygga trees, which take Ancient seeds or Sap and are only planted when chosen); looking at it shows the patch's circle. Woodcutters whose work radius covers it plant free spots from the seeds in your chests (beech seeds, birch seeds, acorns, fir and pine cones, modded tree saplings), now and then between felling, only kinds their axe can fell and only where they'll grow and can be felled later (clear of buildings). Grown trees in a patch are felled like any other. `WoodcuttersPlantTrees` (Work) turns it off.

**What to gather.** Shift+E on a woodcutter or miner switches items on and off (Wood, Fine wood, Core wood…; Stone, Copper, Tin…). Each tree or rock counts as its best drop, so with Wood off a woodcutter still fells birches for their fine wood, and with Stone off a miner takes only the copper from a deposit and leaves the stone on the ground. **Works at home: off** keeps a gatherer from touching anything inside its board's area (for bases with decorative trees and rocks); it still works in the field with you.

**Deliveries.** Gatherers deliver each item type only to a chest that **already holds** that item (they never mix items into other chests), nearest first. Whatever doesn't fit is left in front of the board. Put one of an item in a chest to make it the home for that item. **When those chests have no room left, work on that item pauses** (`PauseWhenStorageFull`, on): a woodcutter leaves trees for that wood, a miner that ore, a Steward stops loading a kiln whose coal has no room; the status says "Paused: no room left for Wood", and work picks up again once there's room.

**Doors.** Hirelings open doors in their way and close them behind themselves (never in a player's face). They only use doors the board's owner may use under wards, never locked doors, and don't wander through doors when idle. `HirelingsOpenDoors` turns this off.

## How hirelings fight

Guards patrol and fight by stance; everyone else defends themselves, weakly, and flees or defends by stance. Since
0.6.0 they also **see attacks coming**:

- **Blocking.** A guard with a shield raises it just before a swing it saw coming lands, and turns into arrows,
  thrown rocks and spears flying at it. A well-timed block is a **parry** ("Parry!" pops up): it blocks with extra force
  and staggers the attacker, as yours does. A swing the guard didn't see coming lands, or gets a late block at best.
- **Dodging.** Any hireling in a fight (guards, archers, workers) rolls out of the way of a hit that would take more
  than a quarter of its health, or of an explosion: the player's roll, and the hit misses while it lasts. Only towards
  safe ground (no ledges, no deep water, nothing in the way), and not again straight away.
- **Levels.** Higher levels see more attacks coming, parry more often, dodge more often and roll again sooner. Level 1
  sees about half of the attacks, parries a few and rolls again after 6 s; level 8 sees nearly all, parries most and
  rolls again after 2 s. The numbers are in the data file's levels table (`readChance`, `parryChance`, `dodgeChance`,
  `dodgeCooldown`).
- **How good they are** is the server's choice: `CombatSkill` (Combat, server-synced) is one of four presets.
  - **Off:** the 0.5.0 fighting.
  - **Green:** they block and roll now and then; you still have to look after them.
  - **Trained** (default): the levels table as tested.
  - **Veteran:** they parry most swings and roll often.

  The preset scales the whole levels table, so higher levels stay better than lower ones.
- **How hard they hit.** `GuardMeleeDamage` and `GuardRangedDamage` (Combat, server-synced) scale melee guards' and
  archers' damage on top of the levels table (`guardDamageMult`, 1 at level 1 up to 5.2 at level 8). The default is 1;
  0.5 halves it.
- **Healing.** A hireling that hasn't been hit for 10 s heals 1% of its health every 2 s, a full heal in about 3.5
  minutes. `HealthRegen` (Combat, server-synced) scales that: 0.5 is about 7 minutes, 0 means no healing of their own
  (only the game's slow trickle), and the maximum is 3.

## The Steward

A Steward works inside its board's radius from the chests there, taking on more chores as it levels up. Each chore unlocks with the biome whose boss makes its resources available:

| Level | Chores it gains |
|---|---|
| 1 | **Fires and lights** (fires, torches, braziers, hot tubs: fuelled when below half), **beehives** (emptied at half full), **tidying up**, **board food** (stocks its board from the chests) |
| 2 | **Smelters and charcoal kilns** (loaded and fuelled, bars collected), **tamed animals** (fed when hungry) |
| 3 | **Repairs** of damaged building pieces |
| 4 | **Fermenters** (loaded with a mead base, tapped when ready) |
| 5 | **Blast furnaces, windmills and spinning wheels** |
| 6 | **Eitr refineries, sap collectors** |
| 7 | **Shield generators** (fed bones) |
| 8 | nothing new; more health and cargo |

- **Most urgent first.** Every few seconds it looks over everything it may do and takes the most urgent job (an empty fire before a half-full smelter, anything before tidying), nearer jobs first when it's close.
- **From your chests only.** It never takes the last of an item from a chest, and leaves a reserve of chosen items in storage (`keepInStorage`, default 50 Wood). What it collects (bars, honey, sap, meads, flour, thread, tidied items) goes into chests that already hold that item.
- **Repairs** follow your rules: a crafting station for the piece in range, ward access, and free as for you. They wait until no enemy has been within 30 m of the Steward or the piece for 20 s. Wood out in the rain without a roof, or standing in water, wears down to half health and no further, so the Steward leaves such pieces alone until something takes them below half; a piece that keeps wearing soon after a repair with no enemy about (weak support) is left for an hour. It repairs from where a player could (5 m, in sight), and skips a piece it can't get to for 30 minutes ("Repairs: can't get to …").
- **Tamed animals** get one item they eat, dropped in front of them (AzuAutoStore leaves it alone).
- **Fermenters** need cover as for you (a roof and mostly walled in); the Steward says when one doesn't have enough instead of loading it.
- **Board food:** when its board has less than 3 days of upkeep left in food, it brings food from the chests up to 7 days (`StewardBoardRefillDays`, `StewardBoardFillDays`), cheapest food first so your best food stays put, and only food the board accepts (raw food only with `AllowRawFood`).
- **In the field:** a Steward you take along with the Command Stone and park in **Gather here** picks up loot lying within `LootRadius` (30 m) of its spot (monster drops, trophies, coins), until its cargo is full. Back home it puts it away like other leftovers.
- **Tidying up** puts items that have lain on the ground for a minute away in chests that already hold them. It's the last thing it does.
- **Shift+E** on a Steward lists every chore with its state (on, off, locked until level N, handled by another mod) and switches each one on or off for that Steward. Its hover and the Roster tab say what it's doing ("Fuelling Hearth") or what's missing ("Smelter: no Coal in any chest").
- **Other mods:** with **PetPantry** (animals eat from chests) the Steward leaves animals alone, and with **Torches Eternal** (fires never burn out) it leaves fires alone.
- **Server settings** (*10 - Steward*): each chore can be turned off for everyone (`StewardFires`, `StewardBeehives`, `StewardStations`, `StewardMills`, `StewardSap`, `StewardAnimals`, `StewardRepairs`, `StewardFermenters`, `StewardShields`, `StewardTidy`, `StewardBoard`), plus `StewardFireRefillFraction` (0.5), `StewardRepairBelow` (0.95), `StewardRepairQuietSeconds` (20) and `StewardTidyMinSeconds` (60). The levels are `choreLevels` in the data file.

**Workers only work while their area is loaded**, i.e. while a player is nearby, as with everything else in Valheim.

## The Farmer and the Cook

Both work to the board's **Orders** tab: a list of "keep at least X of this in the chests" orders. Add one with *Add farm order* or *Add kitchen order* (the list shows what your Farmer or Cook can make at its level), set the target with - / + (Shift for steps of 1), move orders up or down, pause or remove them. Orders are worked top to bottom; **seed orders always come first**. "Have" counts the chests in the board's area (above their reserves) plus what's growing in the field.

**The Farmer** works the ground **you** cultivated inside its radius (bring your own cultivator; it never tills new ground):
- It **harvests every ripe crop** and puts the harvest in chests that hold it, and picks **berry bushes, mushrooms and other regrowing plants** (wild, or planted with PlantEverything) only while an order for them is short. It never plants bushes. Stones, flint and branches are never picked.
- It **plants** for the orders: a seed order ("keep 20 carrot seeds") plants carrots to grow seeds; a produce order ("keep 50 carrots") plants the seeds **above** the seed order's target, so your seed stock is never planted away. Barley, flax and oats replant from their own harvest.
- Rows: it plants on a grid at the crop's spacing, lined up with the plants already there (with **PlantEasily**, at PlantEasily's spacing, and it harvests within PlantEasily's harvest radius). It only plants where the game would let the plant grow (cultivated, the right biome, open sky, room around it), so nothing it plants withers.
- Crops unlock by level with their biome (`cropLevels` in the data file): carrots and Black Forest plants at 2, turnips 3, onions 4, barley, flax and cloudberries 5, Mistlands mushrooms 6, Ashlands plants 7, Deep North crops 8. A wild plant the table doesn't list counts as level 8. `vfh_crops` lists what it knows.

**The Cook** uses the kitchen you built in its radius:
- **Spits and the oven:** it loads raw food for short orders, keeps the oven fuelled with wood, and takes food off as soon as it's done. While its food is cooking it stays near the stoves, so nothing burns.
- **Cauldron, food preparation table, mead ketill:** it crafts what the station offers at its upgrade level, like you: fire under the cauldron and ketill, a roof over the prep table. When an order needs something it can make first (bread needs dough), it makes that and carries it straight on.
- Ingredients come from the chests above their reserves; it never uses what a seed order keeps, nor the produce the Farmer needs to grow seeds. Its status says what's missing ("Carrot soup: needs 2 more Carrot").
- Recipes unlock by the station: spits 1, cauldron 2 (+1 per cauldron upgrade level), iron spit 3, oven 5, prep table 6, mead ketill 7 (`stationLevels`, with per-item `recipeLevels`). `vfh_recipes` lists them.

Both have Shift+E chore toggles (Harvesting / Planting, Spits and ovens / Cauldron…) and server settings in *11 - Farmer and Cook* (`FarmerHarvest`, `FarmerPlant`, `CookStoves`, `CookCraft`). The Steward's Board food chore puts the Cook's food on the board, so a base can feed its own hirelings.

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
| Left click | Your follower at home | A guard (or a gatherer with "Works at home" off) is posted on that spot; any other worker goes back to work |
| Left click | Your follower in the field | Follow ↔ Stay |
| Left click | A tree or log / a rock | Your woodcutters / miners harvest it (and what it leaves behind), staying there (Stay) until you call them |
| Left click | An enemy | Your guards attack it |
| Left click | The ground | Your followers go there and hold; at home, guards (and gatherers with "Works at home" off) are posted there |
| Right click | Your follower at home / a posted guard | Back to work / clear the post |
| Right click | Anything else | Recall: everyone within 50 m follows you |
| Middle click | Anywhere | **Retreat**: everyone drops the fight and runs with you, ignoring enemies, until 20 s pass without a hit |
| Shift+E | A hireling | Orders panel: follow mode (Follow, Stay, Gather Here), stance, what to gather, work at home, rename, send home, apply to all |
| E | Your follower | Its cargo, to load your own loot onto it |

- **Gather Here**: a woodcutter or miner works around the spot you leave it until its cargo is full.
- **Sneaking**: crouch and your followers nearby crouch with you: slower, silent and harder to spot.
- **Keeping up**: followers sprint when you do, catch up faster when well behind, get themselves unstuck, and as a last resort reappear just behind you when you can't see them.
- **Portals and dungeons**: followers within 20 m go through with you. One carrying what the portal won't take (ore, metal) stays behind in Stay, as you would (`AllowNonTeleportableThroughPortals`). The world modifier that lets portals take everything applies to them too.
- **Ships**: take the helm (or stand on a ship that's under way) and your followers nearby board as passengers; they step off beside you when you're ashore. If the ship sinks they're left in the water to swim after you.
- **Going home**: *Send home* in the Shift+E panel walks a follower back to its board on a timer (25 s per 100 m, 1 to 20 minutes) with its cargo; it delivers it and goes back to work. A follower left far behind, left in Stay while you're far away, or whose owner has been offline for 5 minutes goes home the same way. With `ReturnHomeWithNonTeleportable` off, it first drops what a portal wouldn't take, so going home isn't a free ore portal.
- **Posts**: a guard can stand watch on a spot at home (a tower, a gate). It watches all round, fights by its stance and walks back to its post. Archers hold fire when the shot is blocked. A gatherer with "Works at home" off can be posted the same way, so it waits on that spot instead of wandering about the base.

## Doors, stairs and ladders

Inside a hiring board's area, hirelings find their way through your buildings: through closed doors and up and down stairs to any floor you can walk to. The game's own walking map treats a closed door as a wall and often doesn't join the floors of a house, so the board scans its area for doors and for anything that climbs from one floor to another, and hirelings route through those when the game's map can't get them there.

- **Automatic, modded pieces included.** Stairs and ladders are recognised by their shape (a surface that climbs steadily from one floor to another), so build pieces from other mods work without any setup. The scan is redone a couple of seconds after anything near the board is built or removed.
- **Doors** are opened, walked through and closed behind (never in a player's face). Locked doors and doors under a ward the board's owner can't use stay closed.
- **Ladders**: a hireling that can't physically climb one (or any stair it can't manage) hops to the top after a few seconds.
- **Followers** follow you through the house the same way while you're both inside a board's area. Elsewhere they follow as before.
- **Settings** (*Work* section, server): `BaseNavLinks` (on) turns all of this off, back to the 0.2 handling; `HirelingsCloseDoors` (on) off leaves doors open behind them.
- **See what it found:** `vfh_navlinks show` draws every link (doors green, stairs yellow, ladders cyan, blocked red) and each hireling's route; `hide`, `scan` (rescan the nearest board now) and `list` (print its links).
- **A piece it gets wrong** (rare): name its prefab in the data file.

```yaml
navLinks:
  include: [MyModLadder]     # always try this piece as a stair or ladder
  exclude: [MyModFancyRamp]  # never use this piece as one
```

0.3.0 adds `navLinks` to the data file, which 0.2 builds don't accept: the server and every player need 0.3.

## Board levels

| Level | Upgrade needs | Combat | Workers | Max radius |
|---|---|---|---|---|
| 1 | (build cost) | 1 | 2 | 20 m |
| 2 | Eikthyr trophy, Hard antler 3, Deer hide 20, Flint 20, Wood 50 | 1 | 4 | 25 m |
| 3 | The Elder trophy, Bronze 10, Core wood 40, Troll hide 5, Greydwarf eye 20 | 2 | 6 | 30 m |
| 4 | Bonemass trophy, Iron 20, Ancient bark 40, Guck 10, Withered bone 10 | 2 | 8 | 35 m |
| 5 | Moder trophy, Silver 20, Dragon tear 5, Wolf pelt 10, Obsidian 20 | 3 | 8 | 40 m |
| 6 | Yagluth trophy, Black metal 20, Linen thread 20, Needle 20, Lox pelt 5 | 3 | 8 | 45 m |
| 7 | The Queen trophy, Black core 3, Eitr 15, Yggdrasil wood 40, Carapace 20 | 4 | 8 | 50 m |
| 8 | Fader trophy, Flametal 20, Blackwood 40, Asksvin hide 10, Molten core 3 | 4 | 8 | 60 m |

**How many hirelings.** A board has two caps (since 0.7.0):
- **Combat:** guards, melee or archers in any mix.
- **Workers:** everyone else, with at most 2 woodcutters, 2 miners, 1 Steward, 1 Farmer and 1 Cook per board.

So a base can be fully worked from a level 4 board, while the fighting force stays small. A contract on its way out
still counts against its cap until the hireling has gone, though a replacement for its job can already be hired.
- **Upgrading from an older version:** boards that already have more than the new caps allow keep their hirelings;
  only new contracts of that kind wait.
- **Changing the numbers:** server owners can change them in the data file (`combatCap` and `workerCap` per board
  level, `maxPerBoard` per job, where 0 means no limit).

**Moving a board.** Deconstruct it with the hammer and you keep a **Hiring Charter**. It remembers the board's level
(from level 2) and carries its hirelings, packed away with their gear, cargo, names and contracts, followers included.
Build the new board while carrying the charter:
- it starts at that level, so you don't have to fight the bosses again;
- the hirelings walk back in, like new hires.

A level 1 board gives a charter too when it has hirelings. The board's stored food and coins drop like any chest's. A
board destroyed by monsters or damage gives no charter, and its hirelings leave.

A board hires up to its own level. Higher-level hirelings have more health and armour, better gear, work faster and carry more, and cost more (see *What it costs* above).

## Configuration

Two files in `BepInEx/config`, both synced from the server and only editable there in multiplayer:

- **`Spronglehump.HiredHands.cfg`**: rules and behaviour, in sections *General* (raw food, permadeath, respawn, graves), *Base* (what counts as a base, board spacing), *Hiring* (arrival delay, unpaid days, low funds warnings and map pins), *Work* (tree and rock safety distances, terrain protection and field digging, gathering noise, smelter refill threshold, chest minimums), *Steward* (chores on or off, refill and repair thresholds), *Farmer and Cook* (chores on or off), *Followers* (portal and ship radius, catch-up, when lost followers go home, trip times), *Combat*, *Balance log* and *Debug*.
- **`Spronglehump.HiredHands.yml`**: the data tables: board levels and their upgrade costs, hireling levels (health, armour, cargo slots and weight, hire fee and upkeep in food and coins), per-job settings (cost multiplier, pickup list, what gatherers can be told to gather, gear by level, minimum board level, work-radius multiplier, steward stations and reserves), raw foods, names. Edits are picked up live. An invalid file is rejected with a log message and the defaults are used. **Updating the mod doesn't change values already in this file**: new settings are added with their defaults, but changed defaults (like 0.2.1's coin-only hiring and food-only upkeep) only reach a server when you edit the file or delete it to get a fresh one.

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
```

## AzuAutoStore, AzuCraftyBoxes, PullMats, PetPantry, Torches Eternal, PlantEverything, PlantEasily

- The board's funds and hirelings' cargo are excluded from AzuAutoStore and AzuCraftyBoxes (and so from PullMats), so they never take your hireling money or cargo.
- With AzuCraftyBoxes, **board upgrades take their materials from nearby chests** too (your inventory first, then the chests CraftyBoxes lets you pull from within its own range of the board). The Upgrade tab counts them ("Requires (counting nearby chests)"). Turn it off for yourself with `UpgradeFromNearbyChests` in the *Hiring* section of the cfg.
- With AzuAutoStore, stewards leave bars and other station output for Azu to store into a chest that already holds it (seed one bar into a chest). Stations that keep their output inside, like the spinning wheel, are still emptied by the Steward.
- With PetPantry the Steward doesn't feed animals; with Torches Eternal it doesn't fuel fires (the Shift+E chore list says "handled by PetPantry/TorchesEternal").
- With PlantEverything, the Farmer picks the bushes and other plants you planted with it (for short orders); with PlantEasily, its rows use PlantEasily's spacing and it harvests within PlantEasily's harvest radius.

## Balance log (for server owners)

`BalanceLog` (off by default) records how hirelings fight and work, to help tune the mod: one line per fight (job, level, stance, weapon, enemy and its stars, biome, length, damage each way, outcome), death, delivery, upkeep day and hire. Every player's game sends its summaries to the server once a minute; the server writes them to `BepInEx/HiredHands/balance/YYYY-MM-DD.jsonl`, keeping at most `BalanceLogMaxMB` (20). Players appear only as scrambled ids. `scripts/balance-report.py` in the source repository turns the files into tables (kill and death rates by job and enemy, damage per second, resources per day, upkeep).

## Bug reports

Logs go to `BepInEx/LogOutput.log` and `BepInEx/HiredHands.log`, one line per event (`[VFH] … evt=…`). `vfh_debug All on` turns on detailed logging; `vfh_log_mark <text>` adds a marker; `vfh_dump_state` logs every board and hireling. None of these need cheats.

## Coming next

Ideas being considered: smarter fighting (blocking, dodging, and tactics such as tank, hit and run, or shooting from cover), woodcutters replanting, meal plans (more food for stronger hirelings), a scout. Balance tuning from the balance logs.

## License

MIT; see [LICENSE](LICENSE).
