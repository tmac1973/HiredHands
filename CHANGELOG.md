# Changelog

## 0.12.4

Works alongside 0.12.x, but update the server and players together before building Pen signs: a game without 0.12.4
doesn't know the sign.

- **The Pen sign:** build one in each animal pen (hammer, Misc, 2 Wood; E wider, Shift+E narrower). The Rancher's animals
  inside its circle are in the pen, anywhere else they're out, so a pen on a terrace dug below the base, an odd-shaped
  pen, or a walled basement nearby is no longer guessed wrong. Set the circle to reach the middle of the walls: an animal
  a little past it that's walled in all round counts too (a square pen's corners). Without a sign, a steep bank of earth
  now counts as a pen wall.
- **Herder fetches escaped animals:** a Rancher with Herder walks to an animal that's out of its pen and leads it back
  in, waiting when it lags and going back for it if it wanders off; doors wait for it.
- **Farmers:** a posted Farmer plans its planting round its post (its planting jobs kept failing); crops growing outside
  its area no longer count as on their way; spare seeds come in one batch of at least half the reserve; and a worker
  leaves a target behind a door marked against it at once (mushrooms in a Rancher-only pen) instead of trying again and
  again.

## 0.12.3

Works alongside 0.12.x; update the server and players when it suits you (until then, a game on an older version sees a
"Rancher only" chest as a normal one).

- **Pens stay shut to everyone but the Rancher:** stairs right behind a door marked Rancher only (or leave shut) take
  its mark, and a hireling that can't reach a flight of stairs no longer hops to the far end anyway. A Farmer went down
  the stairs into a pen behind Rancher-only doors, then couldn't get out.
- **A "Rancher only" chest mode:** only the Rancher puts in and takes out; everyone else leaves it alone, and it doesn't
  count toward orders. For a pen's feed chest (PetPantry's, say), so nobody else goes into the pen for what's in it.
- **Farmers get going with no crop left to grow seeds from:** with the seeds at their reserve and none of the crop
  anywhere (the Cook used it), the Farmer now plants half the seeds on hand; the harvest is then planted for seeds and
  fills the reserve again.
- **Your own marking keys:** `ChestMarkKey`, `DoorMarkKey` and `FireMarkKey` (*4 - Work*, your own settings, Shift by
  default; `None` turns one off) set the key held with E to mark chests, doors and fires, for mods that also use Shift+E
  (MissingPieces' drawers, for one).

## 0.12.2

Works alongside 0.12.x; update the server and players when it suits you (the new setting takes effect on the games
running 0.12.2).

- **Farmers don't get stuck at the seed reserve:** with seeds exactly at their seed order (the reserve) and a produce
  order short, the Farmer stood idle: the seed order was met, and produce may only be planted from seeds above the
  reserve. Now he plants a few for spare seeds above the reserve (no more than the free field can use), then plants the
  produce from those. The reserve itself is never spent.
- **Bigger boards for big bases:** `BoardRadiusMultiplier` (*Base*, server setting, 0.5 to 3, default 1) scales every
  board level's area: 1.5 makes a level 5 board's 40 m into 60 m (woodcutters and miners twice that). Workers keep the
  radius they were hired with: raise each one with + in the Roster tab.

## 0.12.1

Works alongside 0.12.0; update the server and players when it suits you (until then, a game on 0.12.0 treats a
"Rancher only" door as "leave shut", for the Rancher too).

- **Rancher-only doors:** Shift+E on a door near a board now goes round three marks: hirelings may use it, **only the
  Rancher may use it**, or leave it shut. Mark a pen's gates Rancher only: he goes in to feed and slaughter, and nobody
  else walks through the pen and lets the animals out.
- **Double doors:** at two doors side by side, a hireling opened both, and the second could stay open while it worked
  by the doorway. Now it opens only the one in its way, and never one beside a door that's already open.
- **No false "can't pay tomorrow's wages":** arriving at your base (through a portal, say), you could be told the board
  couldn't pay with days of food in it, because the board was checked before its contents had loaded. Now it waits
  for the board to settle and checks twice before saying so.
- 0.12.0's notes said it worked alongside 0.11.x: it doesn't, everyone needed 0.12.0.

## 0.12.0

**Everyone updates together:** the server and every player need 0.12.0 (a 0.11.x game can't join a 0.12 server).

- **Post any worker at home:** with the Command Stone, recruit a worker and left click a spot in its area (or the worker
  where it stands). It goes back to work with its area centred on the post (the same radius) and waits there between
  jobs, instead of everyone bunching at the board: the Cook in the kitchen, the Rancher by the pen, the Farmer by the
  fields. What it puts in the chests round its post counts toward the board's orders. Right click it (or *Clear post*
  in its Shift+E panel) to have it work round the board again. Before, the stone just kept workers on hold as followers.
- **Search the order list:** a search box at the top of every worker's *Add an order* list. Type part of a name
  ("barley", "fire") to find it among the Cook's hundreds of recipes.
- **Bigger pens:** an enclosure up to about 30 m across counts as a pen (it was 15 m from the animal to each wall), so a
  boar near one wall of a big stone-walled yard isn't reported as out of its pen.
- **Fleeing followers keep up:** a worker following you that runs from an enemy now catches up (and appears behind you
  when far back), as it does when following; before, a miner fleeing a skeleton across the fields was left behind and
  sent home.

## 0.11.1

Works alongside 0.11.0; update the server and players when it suits you.

- **Guards give up on what they can't reach:** a guard with a sword under an enemy it can't get to (a blob up on a swamp
  ruin) stood there until it went quiet, while other mobs chased you. Now it gives up after a few seconds and fights the
  next enemy, and that enemy hitting it again doesn't pull it back.
- **Guards fight back straight away:** a guard that let an enemy go for keeping away (a Fenring mage) took its hits for
  ten seconds before answering. Now anything it only lost interest in is fought again as soon as it hits.

## 0.11.0

**Everyone updates together:** a new board operation for the Steward's starting order; the server and every player need
0.11.0.

- **The Steward works to orders**, like the Farmer, Cook and Rancher: Shift+E, then **Orders** (it replaces Limits; any
  limits you set are kept as its orders). It makes only what it has an order for. A Steward's board starts with one
  **Everything else: ∞** order, so it carries on smelting, baking and collecting everything as before; an order of its
  own for an item (say Coal: 200) takes over for that item, and deleting Everything else means it makes only what's
  ordered.
- **∞ orders for every worker:** the **∞** button on an order means keep making it, however many there are (a Steward
  smelting all the ore in its chests, a Rancher keeping every animal it breeds).
- **Miners no longer dig deep holes:** a miner after a buried vein (silver, say) could dig on down well past the usual
  depth, a little at a time. It now stops at most 1.5 m (2.5 m with Deep digger) below the ground as it was when miners
  first came to that deposit.
- The Shift+E panel's **Close** button stays in the top right corner on its wider pages (Orders, Enhancements,
  Problems).

## 0.10.1

Works alongside 0.10.0; update the server and players when it suits you.

- **Each worker has its own orders:** Shift+E on the Farmer, Cook or Rancher, then **Orders**: its list of "keep at least
  this many" orders, with a picker of what it can make, just as the Steward has its Limits. The board's Orders tab is now
  a read-only overview of every worker's orders. Orders are still kept on the board, so nothing is lost on updating and a
  new worker takes them over.
- **Problems for woodcutters and miners:** a **Problems** button lists what they left alone and why: an item switched off
  in their panel (a woodcutter with only fine wood on, by trees that give wood), chests with no room, too close to
  buildings, a tool too weak, another hireling already on it, or nothing within reach.

## 0.10.0

**Everyone updates together:** the board's roster gains each hireling's picks (a new roster format), new board operations
and a new data section; the server and every player need 0.10.0. Rosters saved by 0.10.0 can't be read by 0.9.x.

- **Enhancements:** from level 2 to 8 every hireling picks one of two enhancements at each level: 126 in all, seven pairs
  per job, each a real choice (often a plain number against something it does). Choose on the new Enhancements page of
  the hireling's Shift+E panel (with ward access, click twice to confirm); a choice waiting shows on its hover and in the
  Roster tab. A **Respec** button in the Roster tab clears the picks for half the hire fee. Guards bash, taunt, cleave and
  rally; archers shoot volleys, fire and frost arrows and rain arrows down; wizards blink, heal, shield you and raise the
  dead; workers gather more, carry more and pick up new tricks. All of them are on the
  [Enhancements](https://tmac1973.github.io/HiredHands/enhancements/) page.
- **For server owners:** every number is in the data file's new `enhancements` section (added to your file with its
  defaults), with `enhancementSettings.respecCostFraction`; the `Enhancements` setting (*3 - Hiring*) turns it all off.
- **The balance log** notes each hireling's picks (`enh`) on fight and death records, and the report compares each pick
  with its pair.
- **Workers wear a hat for their job** instead of a helmet: the woodcutter a red cap, the miner a dverger circlet (with
  its lamp), the Steward a yellow headscarf, the farmer a straw hat, the rancher a fur cap, and the cook a chef's hat.
  Guards, archers and wizards keep their helmets. Looks only: a hat counts as the level's helmet for armour. Each job's
  hat is in the data file (`hat`).
- **A hireling's cargo grid has a fifth row**, used only when its slots go past 32 (Pack mule at level 8).

## 0.9.3

Works alongside 0.9.1 and 0.9.2; update the server and players when it suits you.

- **Miners take veins you've dug out:** a silver vein dug right out of the ground counted as buried (with HearthBelow the
  game's ground height stays at the old surface), so "None of your miners can mine that". A chunk open to the sky now
  counts as above ground.
- **Ore stays behind at map-pick portals:** with TargetPortal (pick the destination on the map), a follower carrying ore or
  metal went through, as the trip wasn't seen as a portal trip. It now stays behind in Stay, as you would.
- **Loading a hireling with heavy stacks:** a stack heavier than its whole weight limit (a full stack of silver ore) was
  refused, so an empty hireling took nothing ("can't carry that much (0/375)"). Now as many go in as it can carry, and
  you're told how many.
- **The cargo window shows the weight limit:** "308/375", red when full, as your inventory does.

## 0.9.2

No changes to the game: works alongside 0.9.1, no need to update the server or other players together.

- **Documentation moved to a site:** [tmac1973.github.io/HiredHands](https://tmac1973.github.io/HiredHands/), with a page
  per job and topic, search and dark mode, brought up to date. This page is now a short overview that links to it.

## 0.9.1

**Everyone updates together:** the hiring board's storage is bigger, and a game on an older version would see the old
size; the server and every player need 0.9.1.

- **The board holds 24 slots** (as an iron chest does), up from 8. Eight slots of cooked meat came to about 7,000 food:
  not enough for a crew of level 3-4 hirelings, who eat about 700 a day. Boards already built get the new size; their
  food stays where it was.
- **The Steward stocks the board with food that fits:** still the cheapest food first (the best stays for you), but when
  the board's slots couldn't hold a full stock of it, he brings richer food instead. If even the richest food in the
  chests can't fill it, his Problems list says how many days the board holds.
- **Warnings before your crew walks off:**
  - when a day's wages go unpaid, everyone at the base is told how many went unpaid, what the board needs and has, and
    after how many days they quit;
  - coming home to a board that can't pay tomorrow's wages tells you so (again after 10 minutes);
  - a full board that's running low says richer food pays more wages per slot.
- **The low-food map pin is right:** far from the board it always said 0 food (it read the board's items the wrong way).
- **Hirelings stuck in the water are lifted out:** after 30 seconds swimming in place (against a bank, under a dock) a
  hireling is put on the nearest dry ground. Brand swam below the base for four days and the board ran dry.
  Guards no longer patrol to spots in the water.
- **Archers in crypts:** an archer at a wall corner judged every shot blocked (the shot was checked from the way it was
  facing, not the way it turns to shoot) and stayed put fighting the wall instead of following you. Shots are checked
  facing the target, and an archer or wizard with no clear shot for 10 seconds drops that target and carries on.
- **Guards keep watch all round:** every guard notices enemies within its full sight (30 m) whichever way it faces, and
  other hirelings anything within 20 m. Guards also join in on an enemy a player has just hit near them. A bear walked up
  behind Thorvald while his owner shot it dead.
- **Hirelings on the map are coloured dots** (workers blue, guards red, your followers gold) instead of hammer pins that
  piled up into a heap round a base.
- **Feeding animals:** the feed is dropped on a side of the animal with no wall between (a boar facing the pen's fence got
  its berry outside), and feed taken from a chest is no longer put straight back by the delivery rule.
- **Gather Here loot:** a fight in between no longer makes a hireling give up on the loot at its feet, and loot it gave up
  on is tried again after 2 minutes.
- **Under roofs and in caves (HearthBelow):** hirelings find the floor under a roof or a dug-out cave's rock, not the roof
  or the hill above it: patrol points, returning home, arrival spots, repairs. No rain chatter in a cave.

## 0.9.0

**Everyone updates together:** a new job, new order kinds and new data-file entries; the server and every player need 0.9.0.

- **The Rancher,** a new worker (level 2 board, one per board, with a butcher knife) who keeps your tamed animals:
  - **Ranch orders** on the board's Orders tab: meat ("keep 40 raw meat"), eggs, and breeding pools ("keep 4 boars
    breeding", never below 2).
  - **Slaughters** only while a meat order is short and only above a kind's pool, the **lowest-star adult first**, so the
    pool is always your best animals and the herd breeds upward. Never young animals, never a pet a player has named,
    never a kind with no pool. Picks up everything dropped and stores it.
  - **Eggs** are collected up to the egg order; the rest are left to hatch.
  - **Feeds** the herd; a Steward on the same board leaves feeding to him (with PetPantry, both leave it to PetPantry).
  - Works with any tameable animal that breeds and drops meat, modded ones included.
  - **Keeps pens shut:** works over the fence when he can reach; otherwise waits for the gate to clear and shuts it behind
    him. An animal that gets out is reported.
  - His Shift+E **Problems** list says why an order isn't met or a pen isn't breeding (crowded, no partner, hungry, no feed).
- **Hirelings hit like a player with the same weapon, not twice as hard.** Their weapons already get better with level;
  the level table's `guardDamageMult` multiplied that again (2.2 at level 4, 5.2 at level 8), so a level 4 archer one-shot
  Black Forest monsters and took the Elder down in 44 s. The default is now 1.0 at level 1 to 1.3 at levels 7-8.
  **Existing servers keep their data file's numbers:** to take the new ones, set `guardDamageMult` under each of the
  `hirelingLevels` in `Spronglehump.HiredHands.yml` (1, 1.05, 1.1, 1.15, 1.2, 1.25, 1.3, 1.3), or turn
  `GuardMeleeDamage` / `GuardRangedDamage` / `WizardDamage` down instead.
- **Guards' Gather Here does something:** a guard, archer or wizard parked with Gather Here picks up loot lying round its
  spot between fights, like a Steward, and brings it to the chests when released or sent home.
- **Board upgrades need one boss item each, its trophy:** level 5 took 5 dragon tears (a second Moder kill) and now takes
  10 freeze glands from drakes; level 2's 3 hard antlers (all of Eikthyr's) are now 10 more deer hide. Existing servers
  keep their data file's costs: change `DragonTear: 5` to `FreezeGland: 10` under level 5, and `HardAntler: 3` /
  `DeerHide: 20` to `DeerHide: 30` under level 2, in `Spronglehump.HiredHands.yml`.
- **Torches up high:** a torch the Steward can't reach (placed high, for looks) is remembered after two tries and left
  alone for the session, and his Problems list names it so you fuel it. Shift+E on a fire or torch near a board marks it
  for workers to leave alone altogether.
- **Seed orders no longer lock up their crop:** with a few seeds under a big seed order and none of the produce to plant
  for seeds (7 onion seeds, an order for 100, no onions), the crop order may now plant those seeds; the crop then feeds
  the seed order. Before, neither moved.
- **Doors hirelings leave shut:** Shift+E on a door near a board marks it (a pen's gates); hirelings never open it or route
  through it, even when it's open. Shift+E again clears it.
- **Chest modes:** Shift+E on a chest near a board sets what workers may do with it: put in and take out (default), put
  in only (not counted as stock: a pen's feed chest the Cook leaves alone), take out only, or leave alone. Shown in the
  chest's hover text; follows the wards.
- **No more stuck under an overhang:** a hireling on a low terrace under a stone floor ran into the wall instead of taking
  the stairs back up (the game put it on the floor above). It takes the stairs now.
- **Every hireling minds the gate:** with tame animals near a door, hirelings wait for them to move off before opening it
  and shut it as soon as they're through.
  - Settings in *12 - Rancher*.

## 0.8.3

Fixes from playtesting. Everyone should update (the server and the players: each game runs some hirelings).

- **Arrows and spells go where hirelings aim.** An archer's arrows could fly off up to 90 degrees to the side: the game
  turned every shot toward whatever last hit the hireling, not what it was shooting at.
- **Orders count what's waiting at the board.** The first of a new item (mead base, say) goes to the board's pile, since a
  chest only takes what it already holds, and orders counted only the chests, so a Cook made it without end. The pile now
  counts toward farm and kitchen orders and the Steward's limits, and the Problems list says to put one in a chest.
- **Workers reach chests again:** a drawer stacked high on other drawers is reached from the floor by pointing up at it,
  and a chest just below the edge of a raised floor is reached over the edge (0.8.2's "no working through floors" fix
  had stopped both, so a Farmer piled carrots at the board). Cooking through a floor stays out.
- **A Steward gives up on a torch it can't reach** for a while, instead of trying it every half minute for hours (and
  never getting to the fermenters).

## 0.8.2

**The server needs 0.8.2;** players on 0.8.1 can still play (the fixes run on the server and on the game running each hireling), but should update too.

- **No ghost followers at portals.** A follower taken through a portal could leave a frozen copy standing at the portal it left, seen by players there (the same vanilla glitch that leaves player "ghosts"). The server now tells everyone when a hireling jumps far.
- **No working through the floor.** Workers could use things on the floor below them (a Cook tending a cooking station from the room above). Standing above something now needs a clear way to it.

## 0.8.1

**Everyone updates together:** new network messages (players' logs, follower orders); a player on 0.8.0 would miss them.

- **Followers stay with the player they follow.** A follower recruited at someone else's base kept being run by that player's game: it didn't go through portals with its new owner, and once left behind it never went home (its "go home" order went to a game far away and was dropped), stuck "following" someone who couldn't even see it. Now your game takes over your followers near you, portals take all of yours in range, and a go-home order that can't be carried out goes back to the server, which sees it done.
- **Problems list in Shift+E** for Farmers, Cooks and Stewards: every order the job works, with have / target and whether it's done, being worked on, or why not (no carrots, no station, Cook level too low…), then every other chore problem (no fuel, no room, paused stations). The hover still shows one line; this shows them all.
- **Players' logs on the server, for troubleshooting.** `ClientLogsToServer` (Debug, off by default) has every player's game send its Hired Hands log lines to the server, into `BepInEx/HiredHands/clients/<player>.log`; `ClientLogLevel` sets how much (default Info). Any player can send their recent lines once with the console command `vfh_send_log`, whatever the setting.
- Farm reasons in the hover now name items properly ("Carrots", not "Carrot").

## 0.8.0

**Everyone updates together:** a new job and new data-file entries; the server and every player need 0.8.0.

- **The Wizard,** a new combat hireling that fights with a staff's main spell. It casts from 10-22 m, one spell every 3 s, and steps back from anything that gets close: strong at range, fragile up close. It takes stances, posts, orders and following like guards and archers, and costs 1.5x.
  - **Without other mods:** from a level 6 board (the Mistlands), with the Staff of Embers, then Frost, then Dundr, in Eitr-weave and Embla robes.
  - **With Therzie's Wizardry:** from a level 2 board (the Black Forest), with Wizardry's staffs biome by biome (Evergrowth first) and its Spellslinger robes and capes.
  - **If Wizardry is removed,** wizards already hired stay and carry the vanilla staff for their level.
  - **Settings (Combat):** `WizardDamage` (default 1), `WizardAttackCooldown` (3 s), and `WizardSpellsHit`: `EnemiesOnly` (default) or `EnemiesAndTerrain` to let a blast damage breakable props like bushes and barrels (greydwarf nests shrug off spells either way).
  - **Data file:** a job can now have its own `outfit` (with a `cape`) and `withMods` entries that apply while another mod is loaded.
- **Followers board your ship when you're a passenger.** Riding someone else's ship, your followers stayed on the dock (and were sent home as too far behind): whether the ship was moving was read in a way that only works for the player steering.
- **New hirelings no longer leave a brand-new board as "gone".** A hireling checked its board the moment it appeared, before the server had registered a board placed a moment before; now it checks after 30 s and leaves only after two misses in a row.
- **Hirelings never hurt players.** A stray hit from any hireling (an arrow's splash, a spell's blast) no longer lands on a player by its target, whatever PvP is set to.

## 0.7.7

**Everyone updates together:** chest puffs use a new network message; a player on an older version would log an error for each one.

- **Hirelings talk.** Now and then a hireling near you says something snarky in a speech bubble: about its job, about what it's doing (following you, idle, full up, fighting, out in the rain), or about you standing there watching. `HirelingChatter` turns it off and `ChatterMinutes` (default 5) sets how often; both are your own settings.
- **Puffs of dust at chests** when a hireling puts something in or takes something out, so you can see who's at your chests. `ChestPuffs` (your own setting) turns them off.
- **Hirelings walk instead of swimming.** They path with the game's water-avoiding agent, so they walk out of a seaside house over land instead of jumping in and swimming round. With no way over land they still head straight there.
- **The Cook watches the food.** With something on the spit or in the oven and nothing more urgent, the Cook stays by the stove until it's done, instead of walking back to the board and running back before it burns.
- **Followers left at the base go back to work.** A follower left in Stay at its base while its owner portals away (or logs off) is released back to work after the usual wait, so anyone can manage it again. Before, it stayed that owner's follower for good.
- **Lost hirelings come back.** A hireling whose world object vanishes without dying (one disappeared entering a crypt) now comes back to its board after the usual walk-in time, free, instead of being treated as dead. Any such deletion is logged with what caused it (`hireling.zdo_destroyed`), to track it down.

## 0.7.6

- **Turnips are produce.** The farm order picker listed turnips as a seed ("Turnip (seed)"): raw turnips aren't food, and planting one grows turnip seeds. Turnips and onions are now plain produce orders. (A turnip order added through the old "(seed)" entry is still a seed order: remove it and add Turnip again.)
- **No planting among picked plants.** The Farmer planted carrots between picked mushroom stalks (a picked mushroom, berry bush or dandelion hides the part that takes up space). Now no crop goes beside a regrowing plant, picked or not.
- **Only edible food on the board.** Balrond Shipyard's CoalPaste carries food values but can't be eaten; the Steward stocked a board with it. Food now has to be a consumable. CoalPaste already on a board no longer counts: take it out.
- **Mead and potion bases from Cook level 2,** not 7: a mead ketill is built in the Black Forest era.
- **"End contract (no refund)"** replaces "Cancel (refund)" for a hireling waiting to come back from death (it never refunded).
- **The Upgrade button** sits below the materials instead of covering the last of them.

## 0.7.5

- **Hirelings keep to dry land.** By a base on the shore they used to stand about in the sea and swim after things:
  - idle hirelings, and ones called to the board, stand on land;
  - woodcutters and miners leave rocks and trees standing in the water alone, and give up a target as soon as getting to it means swimming;
  - followers wait on the shore while you swim ("Waiting on the shore") and catch up once you're back on land;
  - melee guards don't wade out after an enemy in the water, and archers don't walk in to get closer (they shoot from the shore if in range); after a few seconds they let it go.
- **Gather Here stays put.** A follower in Gather Here mines and chops only within its circle (15 m, `GatherNearbyRadius`): a big rock whose middle is inside but whose far side is 30 m away no longer sends it off to the far side.

## 0.7.4

- **A higher-level board's base can be spread wider.** Placing a board from a Hiring Charter, the workbench, bed and pieces are looked for within the board's own area (the circle the hammer shows: 35 m for a level 4 board) when that's bigger than `BaseCheckRadius` (20 m). A new board still uses 20 m.

## 0.7.3

**Everyone updates together:** this version adds network messages, so the server and every player need 0.7.3.

- **Move a follower to another board.** Take a follower to another of your bases and right-click it with the Command Stone: it joins that board if the board has room for it (its level, job and caps are checked as for a new contract; no fee), and works there from then on.
- **See a board's area.** Looking at a hiring board, or having its panel open, draws its area on the ground as a gold circle, and the hover says how big it is (with the woodcutters' and miners' double range). While placing a board with the hammer, the circle follows the ghost, at the level the new board will have (a carried Hiring Charter's level).
- **Steward limits.** In a Steward's Shift+E panel, *Limits* caps what it makes: coal, bars, flour, linen, honey, sap, each mead, and anything a modded station makes. At the limit a kiln or smelter isn't loaded, a beehive or sap collector is left full, and a fermenter isn't loaded with that mead's base. Anything without a limit is made as before. Limits are kept on the board, so a new Steward follows them.
- **Charcoal kilns are their own chore,** apart from smelters, furnaces and refineries, so you can switch them off on their own.
- **Followers on the map.** With the Command Stone in hand, the map shows your followers and the hirelings of the boards you built, wherever they are.
- **A retreat key.** `RetreatKey` (Followers, your own setting, unset by default): any key or mouse button for the retreat order, stone in hand or not.
- **A bigger board panel,** with larger order rows (10 a page) and a roomier order picker.
- **Guards stop swinging at walls.** A melee guard no longer swings forever at a monster just the other side of a wall: it walks round to it, or gives up after a few seconds.
- **Guards give up on what they can't hurt.** A guard whose target loses no health for 25 s (a creature stuck in the ground, say) leaves it alone for a minute and takes on others; creatures below the water's surface aren't targeted. A creature stuck at 0 health that never died is finished off.
- **Posts.** A hireling that dies comes back patrolling, without its old post. A posted guard that can't walk to its post tries again, says "Can't reach their post", and after a few tries goes straight there when no one is looking.
- **Raw food from mods doesn't pay upkeep.** Any food nothing makes (no recipe, cooking station, oven or fermenter) now counts as raw, so a mod's raw mushroom (Witch Eye) or the Ashlands Smoke Puff no longer fills a board. `AllowRawFood` still lets it all in.
- **The board hover** says "Food lasts N days"; coins are mentioned only when a data file charges coin upkeep.

## 0.7.2

- **Command Stone on a chest.** Point the stone at a chest (left click): followers near you carrying something that chest already holds walk over and put it in, then carry on (following you, or back to their spot).
- **Move a board without losing your people.** Deconstructing a board now packs its hirelings (gear, cargo, names, contracts, followers too) into the Hiring Charter you keep, even at level 1; build a new board while carrying it and they walk back in. Before, every contract ended.
- **Workers only, if you like.** `CombatHirelings` (Hiring) off: no guards or archers can be hired, and the board offers workers only; ones already hired stay.
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
