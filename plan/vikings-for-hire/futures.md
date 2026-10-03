# Vikings for Hire — Future ideas

Ideas for after the 16-phase plan. None of these are scheduled. Each needs its own planning pass before it's built.

## Woodcutters replant
Woodcutters already pick up the seeds and cones trees drop (BeechSeeds, FirCone, PineCone, BirchSeeds, Acorn are in the default pickup list). The idea: higher-level woodcutters also **plant** them, so a managed forest regrows inside the work radius.
- Level-gated in the job table (e.g. a `plantFromLevel` value; planting from level 3 is a reasonable default).
- Plant the matching sapling with the vanilla cultivator rules (biome, spacing, open ground), keeping clear of buildings and of other saplings.
- Only plant where a tree was felled, or inside a configurable "forestry zone", so they don't fill the whole base with trees.
- Keep a configurable number of seeds of each type in cargo for replanting, and deliver the rest to chests as usual.

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
