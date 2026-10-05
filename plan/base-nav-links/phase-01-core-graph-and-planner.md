# Phase 01 — Core link graph, stair test and route planner

**Depends on:** nothing · **Enables:** 02 (the scanner fills the graph and uses the stair test), 03 (route following plans with the planner)

## Goal
The pure logic of the layer, with no Unity types, unit-tested like the rest of `Core/`:
- the link graph (door and stair links between endpoints);
- the stair shape test, which decides from sampled surface heights whether a piece joins two floors;
- the route planner: an A* search over link endpoints that asks a reachability oracle about the walking legs between them;
- the two settings and the override lists in the data file.

Nothing changes in game yet.

## Files touched
- `src/VikingsForHire/Core/Nav/NavPoint.cs` (new): a plain `readonly struct NavPoint(float X, float Y, float Z)` with `Distance`, `DistanceXZ` and `Lerp`. Core can't use `UnityEngine.Vector3`.
- `src/VikingsForHire/Core/Nav/NavLink.cs` (new):
  - `enum NavLinkKind { Door, Stair }`
  - `sealed class NavLink`: `Id`, `Kind`, `A`, `B` (the endpoints: the two sides of a door, or the bottom and top of a stair), `Waypoints` (a stair's surface points from A to B; empty for doors), `Length`, `PieceId` (ZDOID text, for logs and the overlay) and `BlockedUntil` (game seconds).
- `src/VikingsForHire/Core/Nav/NavGraph.cs` (new): one board's links, with `Version` (bumped on every rebuild), `Links`, `Endpoints` (deduplicated, so endpoints within 0.5 m share a node) and `NearestEndpoints(point, max, within)`.
- `src/VikingsForHire/Core/Nav/StairProfile.cs` (new): `static StairResult Classify(IReadOnlyList<(float Along, float? Height)> samples, bool nameHint)`. Returns accepted or rejected with a reason, the bottom and top sample indices, and `IsLadder`.
- `src/VikingsForHire/Core/Nav/LinkPlanner.cs` (new): `PlanResult Plan(NavGraph g, NavPoint start, NavPoint goal, Func<NavPoint, NavPoint, LegAnswer> oracle, double now)`:
  - `LegAnswer` is `Yes(cost)`, `No` or `Unknown`.
  - `PlanResult` is `Route(steps)`, `NoRoute` or `NeedLegs(list of unknown pairs)`.
- `src/VikingsForHire/Core/Data/VfhData.cs`: add `NavLinksData { List<string> Include, List<string> Exclude }` as `NavLinks` (YAML key `navLinks`, both lists empty by default).
- `src/VikingsForHire/Core/Data/DefaultData.cs`, `DataDefaults.cs`, `DataValidator.cs`: the default (two empty lists), filling it into older files, and validation (entries non-empty, no entry in both lists).
- `src/VikingsForHire/Config/VfhConfig.cs`: add two synced settings in the Work section:
  - `BaseNavLinks` (bool, true): "Inside a hiring board's area, hirelings route through doors and up stairs and ladders found in your buildings. Off: the 0.2 door and stairs handling."
  - `HirelingsCloseDoors` (bool, true): "Hirelings close the doors they opened once through (never with a player in the doorway)."
- `tests/VikingsForHire.Tests/StairProfileTests.cs`, `LinkPlannerTests.cs`, `NavGraphTests.cs` (new).
- `tests/VikingsForHire.Tests/DataTests.cs` and `DataDefaultsTests.cs` (existing): `navLinks` round-trips, gets filled into an older file, and fails validation for an entry in both lists.

## Steps
1. **`NavPoint`**: a value struct with float fields and the helpers above. Converting to and from `Vector3` lives in the game code (phase 02: `NavConvert.cs`).
2. **`StairProfile.Classify` rules.** Samples are ordered along the piece's axis. `Height` is null where a ray found no surface. The thresholds are constants in the class, named and commented.
   1. Drop the leading and trailing null samples. Reject `"no_surface"` if fewer than 3 samples remain, or `"gaps"` if more than one interior sample is null.
   2. `rise = last - first` among the remaining samples. If it's negative, reverse the order and recompute, so the result always runs bottom to top.
   3. **Minimum rise:** reject `"too_low"` if `rise < MinRise`. `MinRise` is 1.0 m, or 0.6 m with `nameHint`.
   4. **Climbing steadily:** reject `"not_monotonic"` if any sample is lower than the one before it by more than `DropTolerance` (0.15 m).
   5. **Steepness:** `maxStep` is the largest rise between neighbouring samples, and `slopeDeg` is `atan(rise / run)` with `run` the distance along the axis between the first and last kept samples.
      - It's a stair if `maxStep <= MaxStep` (0.7 m) and `slopeDeg <= 60`.
      - It's a ladder if `slopeDeg > 60` and `nameHint` is set (an unnamed near-vertical surface is a wall or post).
      - Otherwise reject `"too_steep"`.
   6. **Accept:** `BottomIndex` and `TopIndex` (into the original list) and `IsLadder`.
   - Ladders with no walkable surface at all (a thin collider) are handled by the scanner from the piece's bounds when `nameHint` is set (phase 02, step 4). `Classify` only sees surfaces.
3. **`NavGraph`**:
   - `AddLink` assigns the next `Id`.
   - `Rebuild(IEnumerable<NavLink>)` replaces all links and bumps `Version`.
   - `Endpoints` merges link ends within 0.5 m into one node, so a door that opens onto the bottom of a stair connects through a single node.
   - `Block(linkId, until)` sets `BlockedUntil`, and the planner skips a blocked link until `now >= BlockedUntil`.
4. **`LinkPlanner.Plan`**, A* over a graph that is built per call.
   - **Nodes:** `start`, `goal` and every endpoint.
   - **Edges:**
     - Each unblocked link between its two endpoints, with cost `Length` for a stair and 2.4 for a door, both ways.
     - **Leg edges** between any two nodes on the same graph. A leg's cost comes from `oracle(a, b)`. Legs from `start` are only tried to the 8 endpoints nearest to `start` (XZ, within 30 m), plus `goal`. Legs into `goal` are tried from the 8 endpoints nearest to `goal`. Endpoint-to-endpoint legs are tried from each endpoint to its 6 nearest endpoints within 30 m.
     - **Hard cap:** one `Plan` call asks the oracle at most 300 times. Past that, every further leg counts as `Unknown`, so a huge base yields `NeedLegs`, never a slow frame.
   - **Heuristic:** the 3D straight-line distance to `goal`.
   - **Unknown legs:** if `oracle` answers `Unknown` for any leg the search needed, the search keeps going without that leg. If it finds no route, it returns `NeedLegs` with those pairs (at most 16, cheapest-looking first) so the caller can work them out and plan again. A route found without the unknown legs is returned as a `Route`.
   - **Route:** an ordered list of `Walk(to)`, `Door(linkId, fromA)` and `Stair(linkId, fromA)` steps, ending with `Walk(goal)`. The direct `start → goal` leg is the caller's job (phase 03 tries the game's map first), but the planner may still return a single `Walk(goal)` when the oracle says yes.
   - `NoRoute` when every leg is known and nothing connects.
5. **Data.** `NavLinksData.Include` lists prefab names to always test as stairs or ladders, treated as a name hint. `Exclude` lists prefab names never to use as stairs or ladders. Both are matched case-insensitively on the prefab name. Validation logs a warning (`data.invalid`) and rejects the file as usual for an entry in both lists or an empty entry.
6. **Settings.** Bind `BaseNavLinks` and `HirelingsCloseDoors` with `Synced(w, …)` next to `HirelingsOpenDoors`, so the server's value wins.
7. **Logging.** Core has no logger. Each rejection reason is a short string the scanner logs in phase 02.

## Build gate
- `dotnet build -c Release` (from the repo root) succeeds with 0 warnings.
- `dotnet test` passes, including the new tests.

## Test plan
- `StairProfileTests`:
  - A vanilla wood stair profile (2 m rise over 2 m in 0.25 m steps) is accepted as a stair.
  - The same profile reversed is accepted bottom to top.
  - A flat floor (rise 0) is rejected `too_low`.
  - A 26° roof (2 m rise over 4 m, no steps) is accepted by the shape test alone; the scanner's floor checks reject roofs (phase 02).
  - A wall (one sample at 2 m, the rest null) is rejected `no_surface` or `gaps`.
  - A stepladder (2 m over 1 m, 0.33 m steps, 63°) is rejected `too_steep` without a name hint and accepted as a ladder with one.
  - Stairs with one missing sample are accepted; with two missing, rejected `gaps`.
  - A dip of 0.3 m is rejected `not_monotonic`.
  - A 1.2 m single step is rejected `too_steep`.
  - With the name hint, a 0.8 m rise is accepted.
- `NavGraphTests`:
  - Endpoints within 0.5 m merge.
  - `Rebuild` bumps `Version`.
  - `Block` hides a link until its time.
- `LinkPlannerTests`, with a fake oracle over a hand-built graph:
  - Start and goal in two rooms joined by one door returns `Walk(door side A)`, `Door`, `Walk(goal)`.
  - A two-floor house (door plus stair) returns the door, then the stair, in order.
  - A blocked stair with a second stair available uses the second.
  - All legs `No` returns `NoRoute`.
  - Unknown legs return `NeedLegs`, and after answering them the next call returns a route.
  - A graph with 200 endpoints asks the oracle at most 300 times in one call and finishes in under 5 ms.
- Data tests: `navLinks` loads, fills in and validates.

## Commit
`feat(nav): core link graph, stair shape test and link route planner`

## Rollback
Revert the commit. Nothing in game uses these types yet. The YAML loader (`Core/Data/DataYaml.cs`) does **not** ignore unknown keys, so a build without this phase rejects a data file that already has `navLinks` (it logs the rejection and uses the defaults): after reverting, delete the `navLinks:` block from `Spronglehump.HiredHands.yml` on any machine that ran this build. This is also why the release is 0.3.0 (phase 05): with Minor version strictness every player and the server must move to 0.3 together.
