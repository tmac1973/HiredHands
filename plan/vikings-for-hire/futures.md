# Hired Hands — Future ideas

Ideas for after the 16-phase plan. None of these are scheduled. Each needs its own planning pass before it's built.

## Woodcutters replant (done in 0.6.0: tree patch signs, plan/tree-patches/)
Woodcutters already pick up the seeds and cones trees drop (BeechSeeds, FirCone, PineCone, BirchSeeds, Acorn are in the default pickup list). The idea: higher-level woodcutters also **plant** them, so a managed forest regrows inside the work radius.
- Level-gated in the job table (e.g. a `plantFromLevel` value; planting from level 3 is a reasonable default).
- Plant the matching sapling with the vanilla cultivator rules (biome, spacing, open ground), keeping clear of buildings and of other saplings.
- Only plant where a tree was felled, or inside a configurable "forestry zone", so they don't fill the whole base with trees.
- Keep a configurable number of seeds of each type in cargo for replanting, and deliver the rest to chests as usual.

## Doors (done in 0.1.1)
Hirelings use creature pathfinding, which treats a closed door as a wall, so chests and stations behind a door are unreachable (the smelter skips the chest; gatherers drop at the board). Planned for 0.1.1: when a hireling's route is blocked and a door piece is within a couple of metres on the way, it opens it (Door.Interact as a Humanoid), walks through and closes it behind itself. Respect wards (only doors the board's owner could open) and never leave a door open. Test row VFH-NAV-2.


## Farmer (done in 0.5.0, plan/farmer-cook/)
Works to quotas set with the production orders below ("keep 50 carrots", "keep 100 barley"): it replants what's needed to meet them and leaves the rest of the field to you.
A farmer role is a priority once the basics work, but how it should behave isn't settled. Questions to answer in its planning pass, with possible answers:
- **What does it farm?** Only fields the player has already cultivated (simplest; the player stays in charge of the layout), or does it also cultivate new ground inside a marked area?
- **Harvest and replant:** harvest ripe crops (`Pickable`) and immediately replant the same crop from its own harvest or from seed chests. That keeps a field cycling without the player micromanaging which crop goes where.
- **Where do seeds come from?** Kept back from the harvest (like the woodcutter's seed reserve), or taken from chests that hold seeds (Carrot seeds, Turnip seeds, Onion seeds, Barley, Flax…).
- **Marking the field:** reuse the contract's work radius, or a separate "field" marker piece so farms and woodlots don't overlap.
- **Biome and growth rules:** respect the vanilla rules (cultivated ground, biome, spacing, sunlight/roof), so a farmer can't grow anything a player couldn't.
- **Level gating:** higher levels could handle later crops (Barley and Flax are Plains crops), farm a bigger field, or tend more plants per trip.
- **Animals:** out of scope at first (tending chickens or lox could be its own later role).

## Steward (done: renamed in 0.2.1, base chores in 0.4.0, plan/steward-chores/)
The Smelter becomes the **Steward**: the hireling that keeps the base running.
- **Keeps stations fed** (what the Smelter does today): smelters, kilns, blast furnaces, eitr refineries, with the same reserves and chest rules.
- **Repairs** (see Repairs below for the rules): damaged pieces near a workbench or forge of the right type, worst first, after fights rather than during them.
- **Odds and ends**, one at a time:
  - fuel the fires, braziers, torches and hot tubs (vanilla `Fireplace` fuel, from chests that hold it);
  - empty beehives, sap collectors and windmill/spinning wheel output into chests, and feed the windmill and spinning wheel from chests;
  - feed tamed animals in pens from a chest of their food (PetPantry users may not want this; check compat).

  Each one could be a per-hireling toggle in the Shift+E panel, like the gatherers' item toggles, so a base owner can keep it to the chores they want.
- **Rename only on the surface:** `JobType.Smelter` stays as the enum value and data key (it's saved in worlds and in everyone's YAML), and only the shown name, description and hover change. If the data key ever has to change, `DataDefaults` would need to accept the old key.
- **Level gating:** higher levels unlock more chores or a bigger radius, the way gatherers get better tools.

## Production orders (done in 0.5.0: the board's Orders tab, keep-in-stock only)
A RimWorld-style bill list on crafting stations, so the Cook and the Farmer have quotas to keep filled instead of the player micromanaging them.
- **Where:** a small "Orders" panel on a station (cauldron, cooking station, oven, fermenter, and for the Farmer the cultivated field or a seed chest), opened from the station's hover with a key, as the Shift+E panel is for hirelings.
- **Two kinds of order** per recipe:
  - **Make X:** craft this many, then the order is done (for example "Make 20 Queen's jam").
  - **Keep X in stock:** craft whenever the chests in the work radius hold fewer than X of it (for example "Keep 40 cooked meat", "Keep 10 bread"). This is the one that runs a base on its own.
- **Ingredients:** only from chests in the work radius, never the player's inventory, and respecting the same reserve rules as the Steward (never the last of an item, per-item reserves). An order with missing ingredients waits and says so ("needs 3 more Barley flour").
- **Priority:** orders run top to bottom; drag to reorder. A paused order is skipped.
- **Vanilla rules:** only recipes the base's owner (or the board's owner) has discovered, and only at a station of the level the recipe needs, so a hireling can't make anything a player couldn't.
- **Saved on the station's ZDO**, so the orders survive restarts and anyone with ward access sees and edits the same list.
- **Feeds the board:** "Keep X cooked meat" pairs naturally with the board's food: a Cook can keep the board's funds stocked from the base's own food chain (as a later option, since it changes the economy).

## Cook (done in 0.5.0)
Runs the kitchen against the production orders above.
- Cooking station and iron cooking station: puts raw meat and fish on, takes the cooked food off before it burns.
- Cauldron, oven, fermenter: crafts the recipes the orders ask for, fetches the ingredients, delivers the results to chests that hold that item (the usual delivery rule).
- Level gating: higher levels cook faster and handle more stations at once; later stations (oven, fermenter) could need a higher board level.

## Repairs (done in 0.4.0 as a Steward chore)
One hireling job (or a perk of a higher-level job) repairs damaged building pieces inside its work radius.
- **Who:** a dedicated Builder/Carpenter role, or a perk for higher-level Guards, who'd naturally patrol the base and patch it up after raids.
- **Rules to match vanilla:** vanilla repair is free but needs a hammer and a crafting station in range. The hireling should follow the same rule (only pieces within range of a workbench/forge/etc. of the right type), so it can't repair anything a player couldn't.
- **Pace and priority:** repair a piece every few seconds, worst-damaged first, and not while enemies are near (repairs after the fight, not during).
- **Compat:** Tim's server runs Azumatt AzuAreaRepair; check how it repairs so the two don't fight over the same pieces.

## Forager (mostly covered: the Farmer picks bushes and mushrooms for orders, a Steward in Gather Here loots)
Picks berries, mushrooms, thistle and the like in the work radius.

## Cargo weight limit (done in 0.2.1: 300 + 25 per level)
Hirelings are currently limited only by usable cargo slots (8 at level 1 up to 32 at level 8), whatever the items weigh. Dense items like metal bars are therefore cheap to haul compared with what a player can carry.
- A per-level weight cap in the hireling level table (e.g. `cargoWeight`), checked by the cargo gate alongside slots, with 0 meaning no limit so it can stay off by default.
- What happens at the limit: a gatherer stops picking up and goes to deliver; a follower refuses items you try to hand it, with a message.
- Decide after watching gatherers and followers haul for real (phases 08–13), so the numbers come from play.

## Jester / dancer (comfort)
A morale role that raises the base's comfort level while it's on duty near players.
- How: a hireling within range of a player counts like a comfort piece (vanilla `SE_Rested` comfort comes from pieces in range; this would add a bonus on top, via a patch on the comfort calculation).
- Level gating: higher levels give more comfort, or work in a larger radius.
- Flavour: performs emotes (dance, cheer, flex, toast) on a loop, and maybe plays music near the fire in the evening.
- Balance: cap the bonus (e.g. +1 to +3 comfort) so it can't stack past what a well-built base offers; one jester per board.

## Wizards and healers (higher levels)
Support roles unlocked at higher board levels, since magic belongs to the later biomes.
- **Healer:** heals hurt players, hirelings and tames near it, out of combat or during it from range; higher levels heal more and faster. Could use vanilla staff effects (e.g. the Staff of Protection / healing visuals) for the look.
- **Wizard:** a ranged magic guard using the Mistlands staves (fire/ice/lightning) as its gear, with eitr standing in for ammo in the same way archers have endless arrows. Strong against groups but fragile up close.
- Both need gear and animations that suit NPCs; staves are player items like bows, so the same "fully drawn" trick may be needed.
- Gate them by board level (e.g. healer from L5, wizard from L7) and price them high.
- I use therzie mods quite often which includes earlier magic options and at least one healing option I think.. we could offer healers/wizards only if the therzie magic mod is installed and use it's assets/spells along with the vanilla ones to offer earlier level hirelings. Maybe still offer late game wizards without the therzie mod too. Healers would be really cool!

## Scout
A hireling sent out to find things and report back, instead of the player wandering the map.
- **Ordering it:** from the board, pick what to look for and roughly where (a direction or a map pin), e.g. ore deposits (copper, tin, silver, iron in crypts), a biome, a boss altar or location type (crypts, villages, dungeons), a trader, or resources like berries and mushrooms.
- **What it does:** leaves the base, travels out (it's away from the board, like a follower on a trip), and comes back after a while, timed like return-home (distance-based, about walking pace).
- **What you get:** map pins for what it found (vanilla minimap pins, maybe a "Scouted" icon), and possibly the explored map area revealed along its route.
- **Risk and level:** it can be hurt or killed on the way (a chance based on biome danger vs. its level), so higher levels scout further and more dangerous biomes and survive more often. Its fee and upkeep would reflect that.
- **Multiplayer:** whose map gets the pins (the player who sent it, or everyone sharing the board).
- **Balance:** long cooldowns and costs so it doesn't replace exploring, and no finding things a player couldn't (e.g. respect the vanilla "location must be discovered" rules for things like the trader, or only reveal coarse areas).
- Not sure how this would work given the server doesn't process areas that don't have a player in it. would have to look into that. 
- 
## Guard posts (done in 0.2.0: posts set with the stone or Shift+E)
Let guards and archers hold a chosen spot instead of walking the work radius.
- **Setting the spot:** a small "guard post" build piece (or a marker placed from the board) that a guard is assigned to in the Roster tab; or, with the Command Stone, aim at a spot and order "stand watch here".
- **Behaviour:** stand at the post facing outward (or the way the post faces), engage by stance as usual, then walk back to the post after a fight. Archers would suit walls and towers: a post on a rampart gives them height and cover.
- **Options per guard:** Patrol (current behaviour) or Watch post, chosen in the Roster tab.
- **Several posts per board** so a wall can be manned at the gate and the corners.
- we already put something like this in place....
- 
## Meal plans (pay more food for stronger hirelings)
A per-contract setting for how well a hireling is fed: more food points per day buys a modest buff, the way better food buffs the player.
- **Three plans:** Normal meal (today's upkeep, no buff), Hearty meal, Gourmet meal. Chosen per contract (Contracts/Roster tab, and the Shift+E panel), with an "apply to all" option like stances.
- **Cost:** a multiplier on the level's `UpkeepFood`, e.g. Normal ×1, Hearty ×1.5, Gourmet ×2.5. The hire fee is unchanged. It's in the data file per plan, so servers can tune it.
- **Buff:** mainly max health, e.g. Hearty +15%, Gourmet +30%. Candidates for a second stat, to pick during balancing:
  - faster health regeneration out of combat (today 1% every 2 s);
  - a little more damage for guards;
  - a little faster work (gather speed) for workers.
  One bonus besides health is plenty: the point is a small reason to spend food, not a second levelling system.
- **Quality instead of quantity (option):** let the plan require better food rather than just more of it, e.g. Gourmet only counts prepared dishes (not cooked meat), so it pulls on the player's cooking. That's closer to how the player's own food works, but harder to keep stocked.
- **Running short:** if the board can't pay the chosen plan, drop to the best plan it can pay that day (with a message and a roster note), and only count the day unpaid when even Normal can't be paid. A hireling never leaves because Gourmet ran out.
- **Show it:** hover and roster show the plan (and the buff), and the HUD follower lines could show it for followers, since that's where the extra health matters most.
- **Balance check:** compare a level 1 hireling on Gourmet with a level 2 on Normal. The meal plan shouldn't make levelling pointless, so the buff should stay well under the gap between two levels (about +50% health per level today).

## Low funds warning (done in 0.2.1: hover, daily message, builder's map pin)
Tell players before a board runs dry, not after hirelings start going unpaid (today the first sign is "unpaid (1/2)" on a hireling, a day before it quits).
- **How long the funds last:** the board already knows its daily upkeep (the sum over its active contracts) and its funds, so it can work out "days left", separately for food points and for coins, since either can run out first.
- **Where it shows:**
  - The board's hover: "Funds: food for 3 days, coins for 1 day", in orange below a threshold and red at the last day.
  - The Manage panel (Roster or a Funds line at the top), with the same numbers and the daily cost.
  - A message when it crosses the threshold, once per in-game day at most: to players near the board, and to the players who posted its contracts wherever they are (they may be out exploring with followers). For example: "The hiring board at Home has food for 1 more day."
- **Settings:** `LowFundsWarnDays` (default 2) for when the warning starts, and an on/off switch for the messages (the hover and panel always show the days left).
- **Multiplayer:** the server runs upkeep for unloaded boards, so the check sits beside the upkeep code and the message goes out the way follower messages do (`FollowerServer.Tell`). Which player to tell comes from each contract's poster, which would need recording on the contract (it isn't stored today).
- **Map pin:** while a board is low, a pin on the map at the board for everyone who hired from it ("Hiring board: food for 1 day"), updated each in-game day and removed once it's topped up above the threshold. It helps when you're far away and have forgotten which base needs feeding. It's a local pin each player's game adds and removes from the board's state, not one saved into the shared map, so it never lingers.
- **Nice to have:** a marker on the board itself while it's low, and a "fill from my inventory" button in the Funds panel that tops it up with your cheapest food.

## Tombstone for a dead hireling's cargo (done in 0.2.1)
Today a hireling's cargo falls to the ground where it dies, as loose items that vanilla clears after about an hour outside a base. A tombstone would keep it safe until someone comes for it, as a player's does.
- **How:** spawn the vanilla `Player_tombstone` where it died, name it after the hireling ("Bjorn's grave"), and move the cargo into its container instead of dropping it. A tombstone floats and is found as a player's is, and it doesn't despawn. Gear still isn't dropped.
- **Who can open it:** the follower's owner, or for a base worker anyone with ward access at its board, so a passer-by on a public server can't loot it. Check how vanilla `Tombstone` decides who may open it and whether that ties to a player profile, and work around it (an owner id written on the ZDO and a check on `Interact`).
- **Finding it:** a map pin for the owner ("Bjorn's cargo"), removed when the tombstone is emptied, plus the death message saying where it is.
- **Empty is deleted:** like a player's, the tombstone goes away once it's emptied, and the pin with it.
- **Setting:** `HirelingTombstones` (default on); off keeps today's loose pile.
- **Watch out for:** the tombstone's own "pick up all" and its "you died" status effect, which mustn't touch the player who opens it. Test both, and test that AzuAutoStore and AzuCraftyBoxes don't pull from it (as they don't from cargo today).

## Moving a board without losing its level (done in 0.2.3: the Hiring Charter, level only)
Today deconstructing a hiring board gives back only its build cost (vanilla piece rules), so the boss trophies and materials spent upgrading it are lost, and moving a base means killing every boss again. Two ways to fix it; the second is preferred.
- **Refund the upgrades:** on deconstruction, drop everything spent on the levels it reached (trophies and materials, from the board's upgrade history; record what was actually paid per upgrade on the board's ZDO, since the data file's costs can change later). Simple, but it hands back trophies that could then be spent again, and you'd have to carry a pile of materials to the new base and pay them all over again.
- **A charter item (preferred):** deconstructing a board of level 2 or more drops a **Hiring Charter** (a small teleportable item) that remembers the board's level, and its contracts if we want to keep them. Placing a new board while carrying the charter gives it that level straight away and uses up the charter. Nothing can be spent twice, nothing heavy to carry, and it fits "moving the business".
  - The charter's level lives in the item's custom data (like other items' quality/crafter data), shown in its tooltip ("Hiring Charter: level 4").
  - Contracts: either they end when the board comes down (as now: hirelings drop their cargo and leave) and the charter carries only the level, or the charter also carries the roster and the hirelings walk to the new board (bigger: they'd need to travel, or arrive like new hires). Start with level only.
  - Multiplayer: a charter can be traded like any item; that's fine, it's one level for one board.
  - The "remove this board?" confirmation should say what will happen ("You'll get a Hiring Charter (level 4) to place a new board").
- **Either way:** respect the existing "board destroyed" rules (contracts end, cargo drops, funds drop like a chest's), and don't refund anything when the board is destroyed by monsters or damage, only when the player deconstructs it.

## Test kit UI (separate dev mod)
Tim's idea (2026-10-05): a small in-game panel so testing doesn't mean pasting into the console. A separate mod (for example `HiredHands.TestKit`), never shipped to players, that only works with `devcommands` on (or as an admin on a server).
- **Ground prep:** flatten around you (the `flatten` fixture: level the terrain, remove rocks, trees and bushes), and clear test leftovers (`clear_area`).
- **Run tests:** a list of the `vfh_t_*` macros from `alias_vfh.yaml`, grouped by area. Click to run one, or tick several and run them as a chain. Pass/fail is shown next to each name from the `evt=test.result` lines, with the failed checks on hover.
- **Player toggles:** god mode, ghost mode, fly, kill nearby enemies, time of day, `vfh_debug` categories.
- **Overlays:** `vfh_navlinks show/hide`.
- It reuses Hired Hands' existing commands and fixtures, so the panel is only buttons; no test logic is duplicated.

## Combat AI: blocking, dodging and tactics
Tim's idea (2026-10-06). Today hirelings fight like monsters: walk up, swing (or shoot), and soak the hits; guards' stances only decide *whether* to fight.
- **Block and dodge for everyone who fights:**
  - **Block:** a melee hireling with a shield (or a weapon that can block) raises it when an enemy's attack is coming (the enemy's attack animation has started and it's in range and facing us), like a player holding block. A well-timed block parries (vanilla's parry window on `Humanoid.BlockAttack` uses the block timer), so higher levels could get the timing right more often.
  - **Dodge:** roll out of the way of a big telegraphed attack (a troll's slam, an abomination's sweep, an AoE). Vanilla's dodge roll is player-only, so it's our own: the roll animation, a quick sidestep away from the attack's direction and a short window where hits miss (as the player's dodge i-frames). Stamina could be a cost, as for players.
  - Both scale with level (reaction time, chance to read the attack) and should be cheap: only checked against enemies already targeting this hireling.
- **Tactics, chosen per hireling** (Shift+E, beside the stance), so players can tune how each one fights:
  - **Melee:**
    - **Tank:** stays in front, blocks most attacks, swings when the enemy is open; draws aggro (enemies prefer it) so it protects the others and the player.
    - **DPS:** as much damage as possible: power attacks when it can, keeps swinging, blocks only big hits, dodges the deadly ones.
    - **Hit and run:** gets a couple of hits in, then backs off out of reach while the enemy recovers, and comes back in; good against slow, hard-hitting enemies (trolls).
  - **Ranged:**
    - **Stand and shoot:** today's behaviour.
    - **Kite:** keeps a set distance, stepping back when an enemy closes in, then shooting again.
    - **Shoot from cover:** finds cover near the fight (a rock, tree, wall or building corner that blocks the line from the enemy), hides behind it, steps out to a spot with a clear shot, shoots, and ducks back. The hard part, but the coolest:
      - Cover search: sample points around the hireling, keep those where a raycast from the enemy's eye to the hireling's chest is blocked and that the pathfinder can reach; the peek spot is a step or two to the side with a clear line.
      - Re-evaluate when the enemy moves (cover is relative to the enemy), and give up on cover against several enemies from different sides or flyers (fall back to kiting).
      - Base fights could use the nav links layer's knowledge of walls and doorways.
- **Order of work, if we do it:** blocking first (biggest effect on survival, simplest), then tactics for melee (Tank/DPS/Hit and run are mostly timing and distance rules), then Kite, then Shoot from cover as its own phase.
- **Balance:** watch the balance log (fight length, damage taken per job and level) before and after; blocking alone could make low-level guards far tougher.

## Hirelings passing each other
Seen on the live server (2026-10-05): two hirelings walking towards each other along a fence line push against each other until one slips past (they're solid to each other, and the game's walking AI doesn't steer round other creatures). It sorts itself out, so it's not urgent.
- **Keep right:** a hireling that sees another close ahead coming the other way steps half a metre to its right for a moment; both do, so they slide past like people in a corridor.
- **Pass through as a last resort:** two still pressed together after ~2 s (a gap too narrow to step aside) stop colliding with each other for a second.
