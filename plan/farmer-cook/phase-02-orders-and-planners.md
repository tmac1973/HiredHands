# Phase 02 — Orders and planners (pure logic)

**Depends on:** 01 (level tables, job types) · **Enables:** 05 (the Orders tab edits an `OrderList`), 07 (planting asks `FarmPlanner`), 08 and 09 (cooking asks `KitchenPlanner`)

## Goal
The order list and the two planners that turn "orders + stock" into "what to do next", as pure, unit-tested code in `Core/`, with no game types. Everything the overview says about priority, the seed cycle, the seed reserve and what the Cook may use is decided here.

## Files touched
- `src/VikingsForHire/Core/Orders/ProductionOrder.cs` (new): `ProductionOrder { string Item; int Target; bool Paused; OrderKind Kind }`, `enum OrderKind { Seed, Crop, Kitchen }`.
- `src/VikingsForHire/Core/Orders/OrderList.cs` (new): the board's ordered list and its edits; serialization.
- `src/VikingsForHire/Core/Orders/FarmPlanner.cs` (new): what to plant and pick.
- `src/VikingsForHire/Core/Orders/KitchenPlanner.cs` (new): what to cook or craft.
- `src/VikingsForHire/Core/Orders/CropInfo.cs`, `KitchenInfo.cs` (new): plain descriptions the game side fills in (phase 04).
- `tests/VikingsForHire.Tests/OrderListTests.cs`, `FarmPlannerTests.cs`, `KitchenPlannerTests.cs` (new).

## Steps
1. **`OrderList`:**
   - Holds `List<ProductionOrder>` in the player's order; `Add(item, kind, target)` (an item already listed just updates its target), `Remove`, `MoveUp`, `MoveDown`, `SetTarget` (0–9999), `SetPaused`.
   - `Active()`: unpaused orders, seed orders first (keeping their relative order), then the rest in list order.
   - `Serialize()`: `v1|Item:Target:Kind:Paused;…` (Kind as `s`/`c`/`k`, Paused `0`/`1`); `Parse` ignores malformed entries and unknown versions (returns an empty list), never throws. At most 64 orders.
2. **`CropInfo`:** `Plant` (sapling prefab, or the pickable prefab for regrowing ones), `Consumes` (seed item) and `ConsumesAmount`, `Yields` (item) and `YieldPerPlant` (int, already scaled), `ExtraYields` (item → expected count, e.g. Poteitr's seeds), `Regrowing` (bool), `Level`.
3. **`FarmPlanner.Plan(crops, orders, stock, growing, level, freeSpots)`** (`freeSpots`: `Dictionary<string,int>` by sapling prefab: free spots for that crop's spacing; planting `n` of any crop takes `n` from every entry, a conservative approximation since crops share the ground) → `FarmPlan { List<(CropInfo Crop, int Count)> Plant; HashSet<string> PickRegrowing; Dictionary<string,int> SeedReserve; List<string> Missing }`:
   - `stock[item]`: in the chests, above the usual reserves (never the last, `keepInStorage`); `growing[item]`: what plants already in the ground will yield.
   - **Seed reserve:** for every active Seed order, `SeedReserve[item] = Target`.
   - **For each active order, in `Active()` order**, while the crop's `freeSpots` entry is > 0:
     - `short = Target - (stock[item] + growing[item])`; skip if ≤ 0.
     - Candidate crops: not regrowing, `Level ≤ level`, yielding the item (as `Yields` or in `ExtraYields`).
     - **Seed and Crop orders alike:** plants needed = `ceil(short / yieldPerPlant)`; plants possible = `(stock[Consumes] - SeedReserve[Consumes]) / ConsumesAmount` for a Crop order, or `stock[Consumes] / ConsumesAmount` for a Seed order (raising seeds may spend produce down to the usual reserves; the seeds themselves are what's protected); `count = min(needed, possible, freeSpots[crop.Plant])`.
     - Spend: `stock[Consumes] -= count * ConsumesAmount`, every `freeSpots` entry `-= count`, add `(crop, count)`.
     - None possible: `Missing` gets `"<order item>: no <consumed item>"` (or "… above the reserve" when the reserve is what blocks it).
   - **Regrowing:** an active order whose item a regrowing crop yields, with `short > 0`, puts that item in `PickRegrowing`.
   - **Barley-style crops** (consumes = yields) just work: planting 1 Barley yields 2.
   - **Which items are seeds:** `CropInfo.IsSeedItem(item, crops, isFood)`: an item is a seed when some non-regrowing crop consumes it to yield a *different* item and the item has no food value (CarrotSeeds, TurnipSeeds, OnionSeeds, KaleSeeds, VineberrySeeds, PoteitrSeeds). Carrot (food), Barley and OatSeeds (each consumed only to yield itself) are crop items; KaleSeeds is a seed (sapling_Kale consumes it to yield Kale), even though sapling_seedkale also grows it from itself. The Orders tab (phase 05) uses this to make an order a Seed or a Crop order.
4. **`KitchenInfo`:** `Output`, `OutputAmount`, `Station` (prefab), `StationKind` (`Stove` for cooking stations and the oven, `Craft` for cauldron, prep table, ketill), `Inputs` (item → amount), `StationLevelNeeded`, `Level` (Cook level), `CookSeconds` (stoves).
5. **`KitchenPlanner.Next(infos, orders, stock, protectedItems, level, availableStations)`** → `KitchenTask?` (`Info`, `Batches`, `ForOrder`, `Then`: the follow-up `KitchenTask?` when this one is a chained intermediate) and `Missing`:
   - `protectedItems`: the `FarmPlanner`'s `SeedReserve` plus the seed-order planting it just planned (the produce the Farmer needs to plant for seed orders); the Cook never takes these.
   - For each active Kitchen order in order: `short = Target - stock[item]`; pick the first info that makes it with a station available and `Level ≤ level` (stove conversions before crafting when both exist); batches = `ceil(short / OutputAmount)` limited by inputs available (stock minus protected).
   - **One step of chaining:** if an input is missing but another info makes it (bread needs dough), return a task for that info for the missing amount, with `Then` = the original task (only one level deep: the input's own inputs must be in stock).
   - Nothing possible: `Missing = "<item>: needs <n> more <input>"` (first missing input).

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.

## Test plan
Unit tests, at least:
- `OrderList`: seed orders sort first; move up/down; serialize/parse round trip; junk input parses to empty.
- `FarmPlanner`: with "keep 6 CarrotSeeds" and "keep 6 Carrot", 2 Carrots and 0 seeds in stock: plants 2 seed-carrots, plants no carrots; with 10 seeds in stock and growing 0: plants 4 carrots (10 − 6 reserve) after the seed order is met; growing yields count; free spots cap; level gating; barley replants from itself; regrowing raspberries picked only while short.
- `KitchenPlanner`: "keep 4 CookedMeat" with 10 RawMeat → stove task of 4; protected carrots aren't used for carrot soup; bread chains through dough; missing input message.

## Commit
`feat(orders): production orders, farm and kitchen planners`

## Rollback
Revert the commit; nothing uses these yet.
