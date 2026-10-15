# The Farmer and the Cook

Each works to its own **orders**: Shift+E on the Farmer or the Cook, then **Orders**. It's a list of "keep at least X of
this in the chests" orders. Add one with *Add an order* (the list shows what it can make at its level), set the target
with - / + (Shift for steps of 1) or make it **∞** (never enough: the Farmer keeps the field full, the Cook cooks
whatever the ingredients allow), move orders up or down, pause or remove them. Orders are worked top to bottom; **seed
orders always come first**. The orders are kept on the board, so a new Farmer or Cook takes them over; the board's
**Orders** tab shows every worker's orders at a glance.

"Have" counts the chests in the board's area (above their reserves) plus what's growing in the field. Growing plants
count as what they'll yield: an order for 50 carrots with 20 in the chests and 30 carrot plants growing reads 50/50, so
the Farmer stops planting rather than over-plant while a crop grows (a plant that can't grow, too cold or without sun,
doesn't count). Until he harvests them, though, those 30 aren't in the chests for the Cook. Food made and waiting by the
board counts toward its order too.

Why an order isn't being filled (a missing ingredient, no seeds, no station) is in the hireling's Shift+E **Problems**
list.

## The Farmer

The Farmer works the ground **you** cultivated inside its radius (bring your own cultivator; it never tills new ground):

- It **harvests every ripe crop** and puts the harvest in chests that hold it, and picks **berry bushes, mushrooms and
  other regrowing plants** (wild, or planted with PlantEverything) only while an order for them is short. It never
  plants bushes. Stones, flint and branches are never picked.
- It **plants** for the orders: a seed order ("keep 20 carrot seeds") plants carrots to grow seeds; a produce order
  ("keep 50 carrots") plants the seeds **above** the seed order's target, so your seed stock is never planted away.
  Barley, flax and oats replant from their own harvest. With a few seeds, a big seed order and none of the produce to
  plant for seeds, the produce order may plant those seeds, so the two never lock each other up.
- Rows: it plants on a grid at the crop's spacing, lined up with the plants already there (with **PlantEasily**, at
  PlantEasily's spacing, and it harvests within PlantEasily's harvest radius). It only plants where the game would let
  the plant grow (cultivated, the right biome, open sky, room around it), so nothing it plants withers.
- Crops unlock by level with their biome (`cropLevels` in the data file): carrots and Black Forest plants at 2, turnips
  3, onions 4, barley, flax and cloudberries 5, Mistlands mushrooms 6, Ashlands plants 7, Deep North crops 8. A wild
  plant the table doesn't list counts as level 8. `vfh_crops` lists what it knows.

## The Cook

The Cook uses the kitchen you built in its radius:

- **Spits and the oven:** it loads raw food for short orders, keeps the oven fuelled with wood, and takes food off as
  soon as it's done. While its food is cooking it stays near the stoves, so nothing burns.
- **Cauldron, food preparation table, mead ketill:** it crafts what the station offers at its upgrade level, like you:
  fire under the cauldron and ketill, a roof over the prep table. When an order needs something it can make first (bread
  needs dough), it makes that and carries it straight on.
- Ingredients come from the chests above their reserves; it never uses what a seed order keeps, nor the produce the
  Farmer needs to grow seeds. Its status says what's missing ("Carrot soup: needs 2 more Carrot").
- Recipes unlock by the station: spits 1, cauldron 2 (+1 per cauldron upgrade level), iron spit 3, oven 5, prep table 6,
  mead ketill 2 (`stationLevels`, with per-item `recipeLevels`). `vfh_recipes` lists them.

## Settings

Both have Shift+E chore toggles (Harvesting / Planting, Spits and ovens / Cauldron…) and server settings in *11 - Farmer
and Cook* (`FarmerHarvest`, `FarmerPlant`, `CookStoves`, `CookCraft`). The Steward's board food chore puts the Cook's
food on the board, so a base can feed its own hirelings.
