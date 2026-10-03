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

## Farmer
Harvests and replants crops in the work radius (vanilla `Pickable` plus the cultivator planting rules shared with replanting above).

## Forager
Picks berries, mushrooms, thistle and the like in the work radius.
