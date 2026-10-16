# The Woodcutter and the Miner

The gatherers: a woodcutter fells trees and a miner breaks rocks, around the base or out in the field with you, and
bring what they get back to your chests. Up to two of each per board; a miner needs a level 2 board. Both work **twice
the board's radius**, so they reach the woods and rocks around a base, not just inside it.

| | Woodcutter | Miner |
|---|---|---|
| Gathers | Fells trees, clears the fallen logs and stumps, picks up wood, resin and seeds | Mines copper, tin and other ore, rocks and boulders (ore before stone), picks up ore and stone |
| Tool by level | Stone axe (1), flint (2), bronze (3), iron (4–5), black metal (6), Jötun bane (7–8) | Antler pickaxe (1–2), bronze (3), iron (4–5), black metal (6–8) |
| Hat | A red cap | A dverger circlet (a lamp on the forehead) |

The tool decides what it can work: a tree or rock its axe or pickaxe is too weak for is left alone, and its
**Problems** list says so. The levels' tools are in the data file (`gear`).

## Around your buildings

- **Trees:** a woodcutter leaves trees with a building within 6 m of the trunk (`TreeSafetyDistanceFromPieces`), and
  fells a tree only in a direction where it won't land on anything you built (`TreeFallCorridorHalfWidth`). If no
  direction is clear, the tree stays standing.
- **Rocks:** a miner leaves rocks that touch your builds or have a piece within 3 m of them
  (`MinerSafetyDistanceFromPieces`): the rock may be holding the build up.
- **Digging:** at home a miner never digs the ground (`MinerProtectsTerrain`). Out in the field, with no building within
  20 m (`MinerFieldDigClearance`), it digs down to ore just under the ground, as you would, but only to about 1.5 m
  below the ground as it was when miners first came to that deposit (2.5 m with Deep digger). It never digs up a deposit
  nobody has found yet, like a hidden silver vein. Ore you've already dug out into the open is mined however deep it is.

## What to gather

**Shift+E** on a woodcutter or miner switches items on and off: Wood, Fine wood, Core wood, Ancient bark, Yggdrasil
wood, Blackwood; Stone, Copper, Tin, Iron scrap, Silver, Black metal, Obsidian, Flametal, Grausten. Each tree or rock
counts as its best drop, so with Wood off a woodcutter still fells birches for their fine wood, and with Stone off a
miner takes only the copper from a deposit and leaves the stone on the ground.

**Works at home: off** keeps a gatherer from touching anything inside its board's area (for bases with decorative trees
and rocks); it still works in the field with you.

**Not chopping or mining?** Its **Problems** button (Shift+E) lists what it left alone and why: items you've switched
off, chests with no room, trees too close to buildings, a tool too weak for the rock, another hireling already on it, or
nothing in reach at all.

## Bringing it home

Gatherers put each item into a chest that **already holds** that item (they never mix items into other chests), nearest
first; whatever doesn't fit is left in front of the board. Put one of an item in a chest to make it the home for that
item. When those chests are full, work on that item pauses (`PauseWhenStorageFull`): a woodcutter leaves trees for that
wood, a miner that ore, and the status says "Paused: no room left for Wood". More on the
[chests](jobs.md#chests-what-workers-may-do-with-each).

**Posted** with the Command Stone (recruit it, then left click a spot in its area), a gatherer works round its post
instead of round the board, and waits there between loads. See [Posts](followers.md#posts).

## Tree patches

Build a **Tree patch** sign (hammer, Misc, 2 Wood) inside a hiring board's area to mark a woodlot. Shift+E on it sets
its radius (3–20 m) and what to plant (any common tree that grows there, from beech seeds, birch seeds, acorns, fir or
pine cones; or one kind, including PlantEverything's ancient or Ygga trees, which take Ancient seeds or Sap and are only
planted when chosen); looking at it shows the patch's circle.

Woodcutters whose work radius covers it plant free spots from the seeds in your chests (beech seeds, birch seeds, acorns,
fir and pine cones, modded tree saplings), now and then between felling, only kinds their axe can fell and only where
they'll grow and can be felled later (clear of buildings). Grown trees in a patch are felled like any other.
`WoodcuttersPlantTrees` (Work) turns it off.

## In the field

Take one out with the [Command Stone](followers.md):

- **Left click a tree or a log, or a rock**: your woodcutters or miners harvest it and what it leaves behind, staying
  there until you call them.
- **Gather Here** (Shift+E): it works the trees or rocks around the spot you leave it (15 m, `GatherNearbyRadius`)
  until its cargo is full.
- What it carries comes home with it. Ore and metal go through portals only if the world lets them (as for you), and a
  follower sent home keeps them unless the server turns `ReturnHomeWithNonTeleportable` off.

## Noise and danger

**Chopping and mining make noise**, as yours does, so nearby monsters come for a gatherer (`GatheringMakesNoise` turns
it off, and the *Quiet work* enhancement silences one gatherer). Gatherers defend themselves weakly and flee or defend by their stance; a guard nearby helps.

## Enhancements

From level 2 to 8 each picks one of two [enhancements](enhancements.md) at every level: bigger yields, more cargo,
faster work, and more. See [the Woodcutter's](enhancements.md#woodcutter) and [the Miner's](enhancements.md#miner).
