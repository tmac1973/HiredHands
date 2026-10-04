# Hired Hands — Future ideas

Ideas for after the 16-phase plan. None of these are scheduled. Each needs its own planning pass before it's built.

## Woodcutters replant
Woodcutters already pick up the seeds and cones trees drop (BeechSeeds, FirCone, PineCone, BirchSeeds, Acorn are in the default pickup list). The idea: higher-level woodcutters also **plant** them, so a managed forest regrows inside the work radius.
- Level-gated in the job table (e.g. a `plantFromLevel` value; planting from level 3 is a reasonable default).
- Plant the matching sapling with the vanilla cultivator rules (biome, spacing, open ground), keeping clear of buildings and of other saplings.
- Only plant where a tree was felled, or inside a configurable "forestry zone", so they don't fill the whole base with trees.
- Keep a configurable number of seeds of each type in cargo for replanting, and deliver the rest to chests as usual.

## Doors (done in 0.1.1)
Hirelings use creature pathfinding, which treats a closed door as a wall, so chests and stations behind a door are unreachable (the smelter skips the chest; gatherers drop at the board). Planned for 0.1.1: when a hireling's route is blocked and a door piece is within a couple of metres on the way, it opens it (Door.Interact as a Humanoid), walks through and closes it behind itself. Respect wards (only doors the board's owner could open) and never leave a door open. Test row VFH-NAV-2.

## Porter
Moves items between chests and to stations, and picks up loose items. Left out because AzuAutoStore covers it on Tim's server. The phase 08 delivery framework is the building block.

## Farmer (wanted, design open)
A farmer role is a priority once the basics work, but how it should behave isn't settled. Questions to answer in its planning pass, with possible answers:
- **What does it farm?** Only fields the player has already cultivated (simplest; the player stays in charge of the layout), or does it also cultivate new ground inside a marked area?
- **Harvest and replant:** harvest ripe crops (`Pickable`) and immediately replant the same crop from its own harvest or from seed chests. That keeps a field cycling without the player micromanaging which crop goes where.
- **Where do seeds come from?** Kept back from the harvest (like the woodcutter's seed reserve), or taken from chests that hold seeds (Carrot seeds, Turnip seeds, Onion seeds, Barley, Flax…).
- **Marking the field:** reuse the contract's work radius, or a separate "field" marker piece so farms and woodlots don't overlap.
- **Biome and growth rules:** respect the vanilla rules (cultivated ground, biome, spacing, sunlight/roof), so a farmer can't grow anything a player couldn't.
- **Level gating:** higher levels could handle later crops (Barley and Flax are Plains crops), farm a bigger field, or tend more plants per trip.
- **Animals:** out of scope at first (tending chickens or lox could be its own later role).

## Repairs
One hireling job (or a perk of a higher-level job) repairs damaged building pieces inside its work radius.
- **Who:** a dedicated Builder/Carpenter role, or a perk for higher-level Guards, who'd naturally patrol the base and patch it up after raids.
- **Rules to match vanilla:** vanilla repair is free but needs a hammer and a crafting station in range. The hireling should follow the same rule (only pieces within range of a workbench/forge/etc. of the right type), so it can't repair anything a player couldn't.
- **Pace and priority:** repair a piece every few seconds, worst-damaged first, and not while enemies are near (repairs after the fight, not during).
- **Compat:** Tim's server runs Azumatt AzuAreaRepair; check how it repairs so the two don't fight over the same pieces.

## Forager
Picks berries, mushrooms, thistle and the like in the work radius.

## Cargo weight limit
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
## Guard posts (stand watch instead of patrolling)
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

## Low funds warning
Tell players before a board runs dry, not after hirelings start going unpaid (today the first sign is "unpaid (1/2)" on a hireling, a day before it quits).
- **How long the funds last:** the board already knows its daily upkeep (the sum over its active contracts) and its funds, so it can work out "days left", separately for food points and for coins, since either can run out first.
- **Where it shows:**
  - The board's hover: "Funds: food for 3 days, coins for 1 day", in orange below a threshold and red at the last day.
  - The Manage panel (Roster or a Funds line at the top), with the same numbers and the daily cost.
  - A message when it crosses the threshold, once per in-game day at most: to players near the board, and to the players who posted its contracts wherever they are (they may be out exploring with followers). For example: "The hiring board at Home has food for 1 more day."
- **Settings:** `LowFundsWarnDays` (default 2) for when the warning starts, and an on/off switch for the messages (the hover and panel always show the days left).
- **Multiplayer:** the server runs upkeep for unloaded boards, so the check sits beside the upkeep code and the message goes out the way follower messages do (`FollowerServer.Tell`). Which player to tell comes from each contract's poster, which would need recording on the contract (it isn't stored today).
- **Nice to have:** a map pin or a marker on the board while it's low, and a "fill from my inventory" button in the Funds panel that tops it up with your cheapest food.
