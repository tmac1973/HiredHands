# Phase 01 — Ranch orders and the herd rules (Core, unit-tested)

**Depends on:** nothing · **Enables:** every later phase (the order kinds, and the pure rules that decide feeding,
slaughter and the Problems reasons)

## Goal
Add the ranch order kinds to the board's order list and a pure planner that, given the herd, the orders and the stock,
says which animal to slaughter next, whether to collect eggs, and why each ranch order isn't being met. All in `Core`,
with no Unity types, fully covered by unit tests, so the in-game phases only gather facts and carry out decisions.

## Files touched
- `src/VikingsForHire/Core/Orders/ProductionOrder.cs` — `OrderKind` gains `Meat`, `Egg`, `Herd`; `Group` 3 for all three
  (reordering stays within the ranch group).
- `src/VikingsForHire/Core/Orders/OrderList.cs` — kind codes `r` (Meat), `e` (Egg), `h` (Herd) in `KindCode`/`TryKind`;
  `Add`/`SetTarget` clamp a `Herd` target to at least 2; `Active()` unchanged in what it returns for farm and kitchen
  (ranch kinds are filtered by the planners that use them, like `IsFarm`); new `Ranch()` returning the active ranch
  orders in board order.
- `src/VikingsForHire/Core/Orders/RanchModel.cs` (new) — `AnimalKind` (Prefab, YoungPrefabs, Meats: item → base amount
  per kill, EggItem or "", MaxCreatures, TotalCheckRange, PartnerCheckRange) and `AnimalKind` also carries Foods (the items it eats, `MonsterAI.m_consumeItems`). `HerdAnimal` (Id, Kind, Stars
  (0-based: level - 1), Adult, Named, X/Z position, Hungry). The herd list arrives sorted by Id (a stable key), so
  "the first listed" in ties is deterministic.
- `src/VikingsForHire/Core/Orders/RanchPlanner.cs` (new) — `RanchPlan Plan(IReadOnlyList<AnimalKind>,
  IReadOnlyList<HerdAnimal>, OrderList, IReadOnlyDictionary<string,int> stock, bool feedingAvailable)`.
- `tests/VikingsForHire.Tests/RanchPlannerTests.cs` (new), `tests/VikingsForHire.Tests/OrdersTests.cs` (serialization
  cases for the new kinds).

## Steps
1. **Order kinds.** Add `Meat`, `Egg`, `Herd` to `OrderKind` and `Group` 3. Codes `r`/`e`/`h`. `Herd` targets clamp to
   2..9999 in `Add` and `SetTarget` (other kinds keep 0..9999). Old order strings (no new codes) parse unchanged; an
   unknown code is still skipped as today.
2. **Herd counting.** For each kind with a `Herd` order: the breeding stock is its unnamed adults; named animals and
   young are never counted and never chosen. `Pool(kind)` = the Herd order's target (no order: the kind is not managed
   for slaughter).
3. **Slaughter choice.** Walk the active Meat orders top to bottom. For a meat order short by `need` (target minus
   stock), the candidate kinds are the kinds whose `Meats` include that item and that have a Herd order with more
   unnamed adults than the pool. Among all candidates' unnamed adults, the pool for each kind is its `Pool` highest-star
   adults (ties: keep the one listed first); every other unnamed adult is cullable. The plan's `Slaughter` is the
   cullable animal with the lowest stars (ties: the first listed) of the first meat order that has any. Expected meat
   per kill = base amount × 2^stars (vanilla's level multiplier), used only for the reason text.
4. **Eggs.** `CollectEggs` = there is an active Egg order whose item's stock (chest stock of that egg item, as for meat)
   is below its target.
5. **Reasons (`RanchPlan.Why`, item or kind → activity text "token|arg|arg"):**
   - meat order short, no kind drops it: `$vfh_ranch_no_animal|<meat>`;
   - short, kinds drop it but none has a Herd order: `$vfh_ranch_need_pool|<meat>|<first kind>`;
   - short, kinds have pools but none has animals above it: `$vfh_ranch_at_pool|<meat>|<kind>|<pool>`;
   - herd order with fewer unnamed adults than 2: `$vfh_ranch_need_pair|<kind>`;
   - herd order where breeding is blocked, per kind, first that applies (from the animals' positions and the kind's
     limits): any animal hungry and `feedingAvailable` false (none of the kind's Foods in the chests) →
     `$vfh_ranch_no_feed|<kind>`; any animal hungry with food available (not fed yet) → `$vfh_ranch_hungry|<kind>`; no two unnamed adults within
     `PartnerCheckRange` of each other → `$vfh_ranch_no_partner|<kind>|<range>`; every adult has `MaxCreatures` or more
     of its kind (adults and young) within `TotalCheckRange` → `$vfh_ranch_crowded|<kind>|<max>|<range>`;
   - egg order short with no laying kind in the herd: `$vfh_ranch_no_layers|<egg>`.
   Orders being worked (a slaughter chosen for them, or eggs being collected) have no reason.
6. **Tests.** Cases: lowest star chosen; pool keeps the best; named never chosen nor counted; young never chosen; met
   meat order → no slaughter; no Herd order → no slaughter and `need_pool`; pool exactly met → `at_pool`; two kinds
   dropping the same meat; Herd target 1 clamps to 2; each breeding reason; egg collecting on/off; serialization round
   trip of all seven kinds and an old string without them.

## Verification
- `dotnet test tests/VikingsForHire.Tests` passes, with the new tests above.
- `dotnet build -c Release` succeeds; existing farm, kitchen and Steward-limit order tests still pass unchanged.

## Done when
- [ ] `OrderKind` has Meat/Egg/Herd with codes r/e/h and Herd clamps to ≥ 2.
- [ ] `RanchPlanner.Plan` returns the slaughter choice, egg collecting and per-order reasons as specified.
- [ ] Unit tests cover every rule in steps 2–5 and pass.
