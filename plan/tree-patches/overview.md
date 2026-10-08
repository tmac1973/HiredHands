# Tree patches (woodcutters replant) — Overview

Tim's idea (2026-10-06): a small sign on a stick marks a patch where woodcutters plant trees from the seeds the base has
collected, so a woodlot grows back. Built straight away at Tim's request (no interview); the decisions below are defaults
chosen while building, open to change after playtesting.

## Goals
- A buildable **Tree patch** sign (hammer, Misc, Wood 2), placeable only inside a hiring board's area (the board level's
  woodcutter work radius around it, so a woodcutter there can reach it).
- **Shift+E on the sign:** radius (3–20 m, default 8) and what to plant (**Any** that grows here, or one kind). The hover
  shows the setting and how many saplings and trees are in it; looking at the sign draws the patch's circle on the ground.
- **Woodcutters plant** free spots in patches inside their work radius, from seeds in the chests (beech seeds, birch seeds,
  acorns, fir and pine cones, and any modded tree sapling the cultivator can plant), as a trip every so often between
  felling. Setting `WoodcuttersPlantTrees` (Work, on).
- **Felling is unchanged:** grown trees in a patch are felled like any other (saplings are plants, never felled). The
  sign itself doesn't count as a building for the "don't fell near buildings" rule.

## Decisions (defaults chosen while building)
- **Kinds:** read from the game (every cultivator sapling that grows into a tree), so PlantEverything's trees work too.
  A woodcutter only plants trees its axe can fell (the grown tree's tool tier), and only where the kind grows (biome, not
  too hot or cold), so nothing it plants is wasted. "Any" picks the kind with the most seeds in the chests among those.
- **Spacing:** the sapling's own grow radius × 2 (the game's minimum), on a grid from the sign.
- **Spots:** free when nothing solid is within the grow radius, open sky, a ward the board's owner may use, and at least
  `TreeSafetyDistanceFromPieces` from any building (other than the sign), so the grown tree can be felled.
- **Pace:** at most one planting trip a minute per woodcutter, up to 10 saplings a trip; seeds below the chest minimum and
  `keepInStorage` are left.
- **Several patches per board** are fine; a patch counts for every woodcutter whose work radius covers it.

## Non-goals
- No tree "orders" (keep N trees); a patch is simply kept planted.
- No thinning or age-based felling.
- Woodcutters don't plant outside patches.
