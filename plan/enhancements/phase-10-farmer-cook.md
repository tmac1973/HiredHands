# Phase 10 — Farmer and Cook behaviours

**Depends on:** 04 (effects framework, shared boosts: Pack mule, Swift, Strong back, Bigger fields) · **Enables:** 12

## Goal
Farmer: Green thumb, Bountiful, Seed keeper, Close planting (replaced Sower), Forager, Tender care, Hardy stock, Double harvest, Master farmer,
Replanter. Cook: Quick hands, Frugal, Spit master, Double batch, Keeper of stoves, Chef's touch, Prep chef, Two stations,
Pantry sense, Head chef, Banquet.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Farm/HarvestChore.cs` — Green thumb (harvest step time × 0.9); Bountiful (chance of
  one more of the main yield), Seed keeper (chance of one more seed), Double harvest (chance the whole yield twice),
  Master farmer (+0.15 yield, × 0.85 step time); Replanter (after harvesting a crop spot, plants the same crop there at
  once from carried seeds or the chests, as PlantChore would).
- `src/VikingsForHire/Hirelings/Work/Farm/PlantChore.cs` — Green thumb (`PlantEvery` × 0.9); Close planting (replaced Sower: plants at 70% of the usual spacing, and a `Plant` patch lets its crops grow there; was up to 4 plants per trip,
  from 1); Tender care (each plant it places has its plant time set back by `TenderCare.share` 0.15 of its grow time);
  Hardy stock (plants it places get a ZDO flag `vfh_hardy`; a `Plant` patch lets flagged plants grow one step outside
  their biome's temperature (cold) and with partial sky, where the game would have them wither).
- `src/VikingsForHire/Hirelings/Work/Farm/HarvestChore.cs` (its scan of regrowing plants) — Forager: picks wild berries and
  mushrooms in its area even with no short order, delivering to chests that hold them.
- `src/VikingsForHire/Hirelings/Work/Kitchen/CraftChore.cs` — Quick hands (`CraftSeconds` × 0.9); Frugal (each ingredient
  unit 0.15 chance not consumed); Double batch (0.20 chance of one extra output); Prep chef (makes the next order's
  intermediate ingredient when idle); Pantry sense (ingredient search radius × 2); Head chef (× 0.85 time, and its own
  0.05 chance of saving an ingredient and 0.05 of an extra output, added to Frugal's and Double batch's when those are
  picked).
- `src/VikingsForHire/Hirelings/Work/Kitchen/StovesChore.cs` — Quick hands (stove load and unload time × 0.9); Spit master
  (loads every free spit in one trip); Keeper of stoves (keeps the oven, and the fires under its spits, fuelled even with
  nothing cooking); Two stations (may start a stove load while a craft waits).
- `src/VikingsForHire/Core/FoodPoints.cs` — `PointsFor` with a bonus factor; unit-tested.
- `src/VikingsForHire/Board/BoardStorage.cs` — Chef's touch and Banquet: food made by a Cook with either carries item
  custom data `vfh_chef` (the bonus, 0.10, 0.25 or 0.35 with both) and is worth that much more in food points on the board. Tagged food stacks only with tagged food (the game keeps items
  with different custom data apart); the Cook's own stacks stay together, and the tooltip says "Made by <name>".
- `test/alias_vfh.yaml` — rows VFH-ENH-F1 … F10 (farmer), VFH-ENH-C1 … C11 (cook).

## Steps
1. Implement each as listed, reading numbers through `Enh` (defaults as in the overview).
2. Food points: `BoardStorage.PointsPerItem` reads the `vfh_chef` bonus; unit-tested in Core (`FoodPoints`).
3. Rows (level 8, one pick each, fast timers and `grow_all`): F1 bountiful (harvest of 20 carrots yields ≥ 22); F2 seed
   keeper (seeds > 0 extra); F3 sower (4 plants placed in one trip); F4 forager (wild raspberries picked with no order);
   F5 tender care (a carrot it planted ripens before one planted by a plain Farmer at the same time); F6 replanter (a
   harvested spot is planted again within 5 s); F7 hardy stock: by hand in the dev world, at the Meadows-Mountain edge where carrots wither, a
   carrot planted by a Hardy-stock Farmer grows and one planted by a plain Farmer withers (logged as a by-hand result). C1 frugal (50 crafts use fewer
   ingredients than 50 × recipe); C2 double batch (outputs > crafts); C3 spit master (all four spits loaded in one trip);
   C4 chef's touch (`board_storage food` 10% higher for the same count of its food than plain); C5 two stations (a spit
   and the cauldron working at once); C6 banquet (the same, 25% higher; with Chef's touch too, 35%). F8 green thumb (10 carrots planted in ≤ 0.92 × a plain
   farmer's time in the same row); F9 double harvest (`data_set Farmer DoubleHarvest.chance 1`: a harvest of 10 carrots
   yields 20, then `data_reset`); F10 master farmer (yield of 20 carrots ≥ 1.1 × plain); C7 quick hands (10 crafts in ≤
   0.92 × plain's time); C8 keeper of stoves (with nothing to cook for 2 minutes, `station piece_oven fuel > 0`); C9 prep
   chef (with a bread order and flour in the chests, dough is in the chests before the bread is made); C10 pantry sense
   (ingredients only in a chest at 1.5 × its radius: the order is filled); C11 head chef (10 crafts in ≤ 0.87 × plain's
   time).

## Build gate
- Compile check; unit tests (food point maths with the tag).
- In game: rows F1-F10, C1-C11 pass; regression `farm_harvest farm_bush farm_seeds farm_rows cook_spit cook_oven
  cook_cauldron cook_chain cook_protect cook_floor orders why1 why2 board_food` passes.

## Test plan
Rows above; the farm and kitchen regression; by hand: hardy stock at a biome edge, the "Made by" tooltip.

## Commit
`feat(enhancements): farmer and cook behaviours (green thumb, bountiful, seed keeper, sower, forager, tender care, hardy stock, double harvest, replanter, quick hands, frugal, spit master, double batch, keeper of stoves, chef's touch, prep chef, two stations, pantry sense, banquet, head chef, master farmer); rows VFH-ENH-F1..F10, C1..C11`

## Rollback
Gated by `Enh.Has`. Food tagged by Chef's touch keeps its tag after a rollback (it just stacks separately and is worth
its normal points).
