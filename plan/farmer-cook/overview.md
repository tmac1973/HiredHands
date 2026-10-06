# Farmer and Cook (0.5) — Project Overview

## Problem
Food is the base's running cost (upkeep is paid in food), but every crop and every meal still has to be made by hand. A player grows seeds for many cycles before a field is big enough to eat from, keeps track of which seeds to hold back, replants after each harvest, and stands at spits, ovens and cauldrons turning raw food into meals. Hired Hands automates wood, ore, smelting and the base's chores, but not the food chain that pays for it all. The Steward can already put food on the board (0.4.3), and nothing makes that food.

Tim plays with PlantEverything (berries, mushrooms and other plants can be planted and harvested again and again) and PlantEasily (planting in rows, harvesting an area), so a farmer that only knows vanilla crops would miss half his farm.

## Goals
- A **Farmer** job that works the player's cultivated ground inside its work radius: plants crops on empty cultivated soil, harvests everything ripe, and puts the harvest away, holding a cultivator.
- A **Cook** job that works the base's existing kitchen stations: spits over fires (cooking station, iron cooking station), the stone oven, the cauldron, the food preparation table and the mead ketill, holding a homemade ladle.
- **Production orders** on a new **Orders tab** on the Hiring Board, shared by the board's Farmer and Cook: "keep at least X of this in stock".
  - **Farm orders:** seed orders ("keep 20 carrot seeds") and produce orders ("keep 50 carrots"). Seed orders always come first and also protect the seeds: below a seed order's target, seeds are never planted for produce.
  - **Kitchen orders:** "keep 40 cooked meat", "keep 10 bread".
  - Orders run top to bottom (seed orders first); the player reorders them with arrow buttons.
- **The seed cycle:** to raise carrot seeds the Farmer plants carrots; to raise carrots it plants the seeds above the seed order's target. Barley and flax replant from their own harvest.
- **Regrowing plants** (wild or PlantEverything bushes, mushrooms, thistle…) in the radius are harvested when ripe and count towards their orders; the Farmer never plants them.
- **Rows and area harvest** for every Farmer: it plants in a grid at the crop's spacing, lined up with plants already in the ground, and harvests every ripe plant within reach in one go. With PlantEasily installed it uses PlantEasily's spacing settings, so its rows match the player's.
- **Levels** for both jobs, set in the data file like the Steward's `choreLevels`: crops by biome for the Farmer; for the Cook, by station and its upgrade level (spit, cauldron, iron spit, oven, prep table, mead ketill follow the biomes), with per-item overrides.
- **One Farmer and one Cook per board.**
- The unreleased **0.4.4 fixes** ship in 0.5: the Steward's `LootRadius`, deliveries to chests under raised floors, pausing only when there's no room at all, repairs skipping water wear and pieces that keep wearing.

## Non-goals
- The Farmer doesn't cultivate ground, clear land or plant trees: the player cultivates the field with their own cultivator.
- The Farmer doesn't plant bushes or other regrowing plants (PlantEverything): the player plants those once.
- No "make N, then stop" orders; only keep-in-stock.
- No new stations or recipes: the Cook only uses stations the player built and recipes those stations offer.
- The fermenter stays the Steward's; the Cook doesn't ferment.
- No animal husbandry (chickens, lox); no fishing or hunting.
- Orders aren't per hireling: they belong to the board.
- No limit changes for other jobs: Stewards, woodcutters, miners and guards stay unlimited per board.

## Users & primary flow
Players on a server (or single player) who've built a base with a Hiring Board of level 2 or more.

1. The player cultivates a patch of ground near the base with their own cultivator, and builds kitchen stations (a spit over a fire, later a cauldron and an oven).
2. On the board they hire a Farmer and a Cook (one each per board) with a work radius that covers the field and the kitchen.
3. On the board's new **Orders** tab they add orders: "keep 20 carrot seeds", "keep 50 carrots", "keep 40 cooked meat", "keep 10 carrot soup", and put them in the order they want.
4. They put a few carrot seeds (or carrots) and some raw meat in a chest.
5. The Farmer plants carrots on the cultivated soil to grow seeds until there are 20 carrot seeds (chests plus what's growing), then plants the extra seeds for carrots. It harvests everything that ripens and puts it in chests that hold it.
6. The Cook keeps the spits fuelled and loaded with raw meat, takes cooked meat off before it burns, and crafts carrot soup at the cauldron from the chests' carrots, mushrooms and so on, up to the orders' targets.
7. The Steward puts the cooked food on the board (0.4.3's Board food chore), so the base feeds its own hirelings.
8. Each worker's hover and the Roster tab say what it's doing and what's missing ("Carrots: no carrot seeds above the reserve", "Carrot soup: needs 2 more Mushrooms").

## Constraints
- Valheim + BepInEx + Jotunn, the mod's existing structure (`src/VikingsForHire`), the pure-logic core in `Core/` with unit tests, the test harness and macros (`test/alias_vfh.yaml`), the data file (`Spronglehump.HiredHands.yml`) and synced cfg.
- **Vanilla rules:** the Farmer plants only on cultivated ground, in the crop's biome, at its spacing and under open sky, so it can't grow anything a player couldn't. The Cook crafts only recipes the station offers at its upgrade level, with the ingredients a player would need.
- **Chests and reserves:** ingredients and seeds come only from chests in the work radius, never the last of an item, `keepInStorage` respected, plus seed orders' targets (also protected from the Cook, including the produce the Farmer needs to plant for seed orders). Deliveries go to chests already holding the item; `PauseWhenStorageFull` applies.
- **Multiplayer:** orders saved on the board's ZDO, editable by anyone with ward access, synced like the board's other state; workers run on whichever game simulates them.
- **Mods:** PlantEverything and PlantEasily are optional; detected at runtime like the existing compat (no hard dependency). Must not break with them installed or absent.
- **Release:** one release, 0.5.0, everyone updates together (new data file blocks and board ZDO keys).

## Success criteria
- With a seed order and a produce order, starting from a handful of carrot seeds, a Farmer grows the seed stock to its target first, never plants seeds below it, then fills the produce order; nothing ripe stays in the ground.
- A Farmer harvests ripe raspberry bushes (PlantEverything) in its radius for a raspberry order and never plants a bush.
- Farmer rows are evenly spaced and line up with existing plants; with PlantEasily installed they use its spacing.
- A Cook keeps a "keep 20 cooked meat" order filled from raw meat in a chest on a spit over a fire, with nothing burnt, and crafts a cauldron recipe up to its target, waiting and saying why when an ingredient is missing.
- A second Farmer or Cook can't be hired on a board that has one.
- Test macros pass in single player for each of these except the PlantEasily spacing and the hiring limit (checked by hand in game, and the limit by unit tests); the orders, harvesting and spit macros also pass from a client of the local dedicated server; no `lvl=E` lines; existing tests (including the 0.4.4 regression set) still pass.

## Decisions
- **Scope** → Farmer and Cook both in 0.5, with the shared order system and the 0.4.4 fixes.
- **Where orders live** → a new Orders tab on the Hiring Board, shared by the board's Farmer and Cook.
- **Order kinds** → keep-in-stock only.
- **Priority** → list order (arrow buttons), seed orders always first.
- **One per board** → Farmer and Cook only; Stewards and other jobs stay unlimited.
- **The field** → everything in the work radius: any empty cultivated ground, any ripe crop, whoever planted it.
- **Harvest** → always harvest ripe crops; orders only decide what gets planted.
- **Counting stock** → chests in the radius plus the yield of crops already growing.
- **Seed reserve** → a seed order's target also protects those seeds from being planted for produce.
- **Crop levels** → by biome per crop, in the data file.
- **Regrowing plants (PlantEverything)** → harvested, counted towards orders, never planted.
- **PlantEasily** → rows and area harvest for every Farmer; with PlantEasily, its spacing settings.
- **Cook's stations** → cooking stations and oven, cauldron, food preparation table, mead ketill.
- **Recipes** → whatever the station offers at its upgrade level; the Cook's level for a recipe comes from its station and the station level it needs (stations follow the biomes), with per-item overrides in the data file.
- **Ingredients** → only seed orders (and the produce the Farmer plants for them) are protected from the Cook, plus the usual reserves.
- **Gear** → Farmer: cultivator; Cook: a homemade ladle built like the broom.
- **Board level** → both hireable from board level 2.
