# Jobs and chests

| Job | Does | Notes |
|---|---|---|
| Woodcutter | Fells trees in its radius, clears the fallen logs and stumps, picks up wood, resin and seeds | Fells trees away from your buildings and leaves trees right next to them alone. Axe tier rises with level (stone, flint, bronze…), which decides what it can cut. See [the Woodcutter and the Miner](woodcutter-miner.md) |
| Miner | Mines copper, tin, rocks and boulders, ore before stone | Needs a level 2 board. Leaves rocks touching your builds alone and never digs near your buildings; out in the field it digs down to ore buried up to 1.5 m deep (but never digs up a deposit nobody has found, like a hidden silver vein). Pickaxe tier rises with level. See [the Woodcutter and the Miner](woodcutter-miner.md) |
| Steward (was the Smelter) | Looks after the base: fires, beehives, smelting stations, mills, animals, repairs, the board's food and more | One per board. Armed with a broom. Still `Smelter` in the data file and commands. See [the Steward](steward.md) |
| Farmer | Plants and harvests your cultivated field to the board's orders | Needs a level 2 board; one per board. Carries a cultivator. See [the Farmer and the Cook](farmer-cook.md) |
| Cook | Cooks on your spits and in your oven, crafts at the cauldron, prep table and mead ketill, to the board's orders | Needs a level 2 board; one per board. Carries a ladle. See [the Farmer and the Cook](farmer-cook.md) |
| Rancher | Feeds your tamed animals and slaughters for meat, keeping a breeding pool of the best of each kind | Needs a level 2 board; one per board. Carries a butcher knife. See [the Rancher](rancher.md) |
| Guard (melee) | Patrols the radius and fights | Stances: Passive, Defensive, Aggressive. See [Fighting](combat.md) |
| Guard (ranged) | Same, with a bow | |
| Wizard | Same, casting a staff's spell from range | Needs a level 6 board, or level 2 with Therzie's Wizardry. See [Wizards](combat.md#wizards) |

**Workers wear a hat for their job**, so you can tell them apart at a glance: the woodcutter a red cap, the miner a
dverger circlet (a lamp on the forehead), the Steward a yellow headscarf, the farmer a straw hat, the cook a chef's hat
and the rancher a fur cap. Fighters keep their helmets. A hat is only looks: a worker is as well armoured as if it wore
its level's helmet. Each job's `hat` is in the data file (empty for the helmet).

From level 2 to 8 each hireling picks one of two [enhancements](enhancements.md) at every level.

Woodcutters and miners work twice the board's radius. Everyone defends themselves; non-guards fight weakly and flee or
defend by stance. **Chopping and mining make noise**, as yours does, so nearby monsters come for a worker
(`GatheringMakesNoise` turns it off).

## Woodcutters and miners

What they gather, what they leave alone near your buildings, switching items on and off (Shift+E), their Problems
list, tree patches (the Tree patch sign) and taking them out with you are on
[the Woodcutter and the Miner](woodcutter-miner.md).

## Deliveries

Gatherers deliver each item type only to a chest that **already holds** that item (they never mix items into other
chests), nearest first. Whatever doesn't fit is left in front of the board. Put one of an item in a chest to make it the
home for that item.

**When those chests have no room left, work on that item pauses** (`PauseWhenStorageFull`, on): a woodcutter leaves trees
for that wood, a miner that ore, a Steward stops loading a kiln whose coal has no room; the status says "Paused: no room
left for Wood", and work picks up again once there's room.

## Chests: what workers may do with each

Workers use the chests in their area: they store what they bring into a chest that already holds that item, and take
what they need from any. **Shift+E on a chest** near a hiring board sets what workers may do with that one (its hover
text says which):

- **Put in and take out** (the default).
- **Put in only:** workers store into it but never take from it, and what's in it doesn't count as the base's stock for
  orders and limits. For a feed chest in an animal pen: the Farmer stores carrots there, the Cook never takes them.
- **Take out only:** workers take from it but never store into it (a supply chest you fill yourself).
- **Leave alone:** workers ignore it.

Changing it follows the wards, like the board. If another mod already uses Shift+E on chests, turn `ChestModeOnShiftUse`
off in *4 - Work* (your own setting).

## Doors

Hirelings open doors in their way and close them behind themselves (never in a player's face). They only use doors the
board's owner may use under wards, never locked doors, and don't wander through doors when idle. `HirelingsOpenDoors`
turns this off. How they find their way through buildings is on
[Doors, stairs and ladders](navigation.md).

**Door marks:** Shift+E on a door near a hiring board goes round three marks: *hirelings may use it* (the default),
*only the Rancher may use it* (a pen's gates: he goes in to feed and slaughter, nobody else walks through the pen and lets
the animals out), and *leave it shut* (no hireling). A hireling a door is marked against never opens it, never routes
through it, and doesn't walk through it even when it's standing open.
