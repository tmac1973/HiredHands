# Phase 03 — Hirelings walk link routes

**Depends on:** 01 (`LinkPlanner`, `HirelingsCloseDoors`), 02 (`NavLinkRegistry`, `DoorRules`, `NavOverlay`, fixtures `house2`/`stepladder`/`remove_piece`) · **Enables:** 04 (followers chase through the same navigator), 05

## Goal
Inside a board's area, a hireling walking somewhere uses the game's own map when it has a full route on the right floor. Otherwise it plans a route through the board's links:
- it walks to a door, opens it, steps through and closes it behind;
- it walks to the bottom of a stair, walks its waypoints up, and is moved to the far end if it hasn't got there in time.

It replans around a link that fails. Every behaviour that calls `HirelingAI.WalkTo` gets this for free: delivering, idling, posts, the board call, smelting, fleeing home and gathering. `CanReach` also counts link routes, so spot pickers (idle, board call, smelter) accept spots upstairs. With `BaseNavLinks` off, or outside board areas, nothing changes.

## Files touched
- `src/VikingsForHire/Hirelings/Nav/LegOracle.cs` (new): answers the planner's "can you walk from a to b on the game's map?" with `Pathfinding.instance.GetPath(a, b, path, agentType, requireFullPath: true, cleanup: true, havePath: false)`.
  - Answers are cached per board, keyed by both ends rounded to 0.5 m, and cleared when the graph's `Version` changes. A yes lives 30 s and a no 15 s.
  - A failed leg is marked no for 60 s.
  - Unknown pairs are queued and worked through at most 8 `GetPath` calls per frame across all boards.
- `src/VikingsForHire/Hirelings/Nav/LinkNavigator.cs` (new, one per `HirelingAI`): when to use links, planning, the current route, executing steps, replanning and the hop.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`:
  - `WalkTo` hands over to `LinkNavigator` when it's active for the point;
  - `CanReach` also asks `LinkNavigator.Reachable`;
  - `nav.stuck` gains `links` and `step` fields;
  - exposes `MoveTowardsPublic(dir, run)` and `MoveToPublic(dt, point, stop, run)` wrappers for the navigator. The publicized `BaseAI` methods are protected at runtime, so the wrappers live inside `HirelingAI`.
- `src/VikingsForHire/Hirelings/DoorHelper.cs`:
  - `Detour` returns null when `LinkNavigator` is active for the goal;
  - new `MarkOpened(Door)` so doors the navigator opens are closed by the existing `CloseBehind` (2.5 s and 3 m away, no player within 2.5 m);
  - `CloseBehind` closes nothing when `HirelingsCloseDoors` is off.
- `src/VikingsForHire/Hirelings/Nav/NavLinkRegistry.cs`: `Tick` also steps `LegOracle`.
- `src/VikingsForHire/Hirelings/Nav/NavOverlay.cs`: while shown, draws each loaded hireling's current route as a white line through its remaining steps.
- `src/VikingsForHire/Testing/FixturesNav.cs`:
  - fixture `nav_links <on|off>`, which sets `BaseNavLinks` in single player and restores it at test end;
  - check `navlinks_hops`, the hops since the test began.
- `test/alias_vfh.yaml`: macros `vfh_t_nav1` to `vfh_t_nav4`.
- `docs/test-checklist.md`: rows VFH-NAVLINK-4 to 7.

## Steps
1. **When links are used** (`LinkNavigator.Active(goal)`): all of the following must hold.
   - `BaseNavLinks` is on.
   - `NavLinkRegistry.AreaAt(hireling position)` and `AreaAt(goal)` are the same board area.
   - That area's graph has at least one link.
   - The hireling isn't held: not in transit, stowed or returning, since those never call `WalkTo`.
2. **Direct first.** In `WalkTo`, when `Active(goal)`:
   - If the game's map has a full route that ends within `max(stop,1)+1.5` m of the goal and within 1.0 m of its height (`PathReaches` plus a height check on the route's last point), walk it exactly as today. The door helper still opens doors right ahead.
   - Otherwise, use a link route (step 3).
   - The direct check is re-made at most every 2 s per goal (the result is cached), so the route isn't redone every frame.
3. **Planning.**
   - The planner runs (`LinkPlanner.Plan(graph, me, goal, oracle.Answer, ZNet time)`) when there's no route, when the goal has moved more than 1.5 m from the route's goal, after a step failed, or when the board's graph `Version` has changed since the route was planned (a rescan renumbers links). It runs at most once per 0.5 s per hireling.
   - **Rescans:** each route step keeps its own copy of what it needs (endpoints, waypoints, the door's `ZDOID`), never just a link id. A door or stair step under way when the graph changes is finished from that copy, and the route is replanned straight after.
   - **Route:** start following it. Log Debug `navlinks.route` with: `hid`, `goal`, `steps` (for example `walk>door#3>walk>stair#7>walk`), `ms`.
   - **NeedLegs:** `oracle.Queue(pairs)`. Until the next plan, `WalkTo` behaves as today (game map, door helper), so the hireling isn't frozen. It's usually a frame or two.
   - **NoRoute:** behave as today. Log Debug `navlinks.no_route` with `hid` and `goal`, at most every 10 s per hireling. The existing `deliver.chest_unreachable` rule (25 s, skipped for 5 min) still handles a chest that can't be reached.
4. **Walking the route** (`LinkNavigator.Step`, called from `WalkTo`, which returns its result). `WalkTo` returns true only when the last `Walk(goal)` step is within `stopDistance` of the goal, as today.
   - **`Walk(to)`:**
     - Use `MoveToPublic(dt, to, 0.4, run)`. If the point is within 0.4 m in XZ but more than 1.3 m away in height, use the existing `Climb`.
     - The step is done when within 0.8 m in 3D, or, for the last step, within `stopDistance` (XZ) and 1.3 m height.
     - **Failed:** `StuckSeconds(to) > 10`. Mark that leg no in the oracle for 60 s and replan.
   - **`Door(link, fromA)`:**
     - Find the door: `ZDOID` from `link.PieceId`, then `ZNetScene.instance.FindInstance`, then `Door`. If it's missing, `graph.Block(link, now + 60)` and replan.
     - At the near endpoint, within 0.8 m: if the door is closed, check `DoorRules.Usable(door, DoorRules.BoardOwner(boardId))`. If it isn't usable (locked, or ward), `Block(link, now + 120)`, log Info `navlinks.door_blocked` (`hid`, `door`, `why`) and replan. If it is usable, open it the way `DoorHelper.Open` does (swinging away from the hireling) and call `_doors.MarkOpened(door)`.
     - Then walk straight through with `MoveTowardsPublic` towards the far endpoint (XZ direction). Done within 0.6 m XZ of it.
     - **Failed:** not through in 6 s. `Block(link, now + 60)` and replan.
     - Closing happens through the door helper's existing `CloseBehind` once the hireling is clear.
   - **`Stair(link, fromA)`:**
     - Walk the waypoints in order (reversed when going down) with `MoveTowardsPublic`. Each waypoint is passed within 0.5 m XZ and 1.0 m height.
     - A time limit of 4 s plus 1 s per metre of `Length` applies to the whole stair.
     - **The hop:** if it isn't at the far end by then, `Followers.FollowCatchUp.Place(ai, farEnd, next point)`. Count it, and log Info `navlinks.hop` with: `hid`, `link`, `piece`, `ladder`, `secs`. A ladder whose piece can't be climbed hops every time; that's the agreed behaviour.
     - Going down is the same, starting at the top.
   - **Replanning limit:** a goal that has failed 3 replans falls back to today's behaviour for 30 s. Log Info `navlinks.route_failed` with: `hid`, `goal`, `last` (the failed step).
5. **`CanReach(point)`** returns `HavePath(point)` or `LinkNavigator.Reachable(point)`.
   - `Reachable` plans using only answers the oracle already has. A route found means true. `NeedLegs` queues the pairs and returns false for now. `NoRoute` means false.
   - The result for each point (0.5 m grid) is cached for 5 s.
   - Spot pickers already re-pick periodically: idle every 30–75 s, the board call after 5 s with no route. So a spot upstairs is accepted within a few seconds.
6. **Door helper inside board areas.**
   - With `Active(goal)`, `Detour` returns null: no detour-door logic. The `Through` handling stays for routes it already started.
   - `Tick` (open the door just ahead, close behind) still runs.
   - `CloseBehind(false)` returns early when `HirelingsCloseDoors` is off.
7. **Overlay.** With `vfh_navlinks show` on, each loaded hireling with a route draws a white line from its position through its remaining step points. Refreshed every 0.5 s.
8. **Fixture and check.**
   - `nav_links <on|off>`: sets `VfhConfig.BaseNavLinks.Value` locally (single player only; it fails the test on a client connected to a server). It registers an end step that restores the previous value.
   - `navlinks_hops`: the number of `navlinks.hop` events since `vfh_test_begin`.
9. **Macros** (all with `fast_timers on`, a board at level 5, `stock_board 400 0`, a level 1 woodcutter posted with `post Woodcutter 1 40` and `trees Beech1 3 34`, ending with `kill_hirelings`, `clear_area 60` and `log_errors == 0`):
   - `vfh_t_nav1` (VFH-NAVLINK-4, upstairs chest): `house2 H Wood 1` is the only chest.
     - Wait for cargo, then `deliver_now`.
     - Assert `vfh_assert_eventually 180 deposited H Wood > 0`.
     - Then `vfh_assert_eventually 20 door_open H_door == false`.
   - `vfh_t_nav2` (VFH-NAVLINK-5, stepladder): `stepladder S Wood 1` is the only chest; same flow.
     - `deposited S Wood > 0` within 180 s. Hops are allowed: no assert on `navlinks_hops`.
   - `vfh_t_nav3` (VFH-NAVLINK-6, stairs removed): `house2 H Wood 1` and `chest B Wood 1` beside the board.
     - `remove_piece H_stair`, then `vfh_assert_eventually 10 navlinks stairs H == 0`.
     - Then `deliver_now` and `vfh_assert_eventually 180 deposited B Wood > 0`.
   - `vfh_t_nav4` (VFH-NAVLINK-7, setting off): `nav_links off`, then the `vfh_t_door1` steps, which must pass as in 0.2.
   - `vfh_t_door1` and `vfh_t_order1` still pass with the layer on.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- In single player:
  - `vfh_t_nav1` to `vfh_t_nav4`, `vfh_t_door1`, `vfh_t_order1`, `vfh_t_navscan1` to `vfh_t_navscan3` and `vfh_t_gather1` all pass.
  - No `lvl=E` lines.

## Test plan
- **Macros:** the ones above.
- **By hand, single player, your own house with an upper floor and a closed front door:**
  - A woodcutter delivers to a chest upstairs: it opens the door, closes it behind, walks up the stairs, delivers and comes back down and out.
  - `vfh_navlinks show` draws its route while it walks.
  - Turn `HirelingsCloseDoors` off: doors stay open behind it.
  - Lock a door with a ward it has no access to: `navlinks.door_blocked` appears and it uses another door or gives up on the chest (`deliver.chest_unreachable`).
  - **Idle upstairs:** a board on the upper floor of a building. Idle hirelings stand near it upstairs, not downstairs.
  - **Call to board:** called while upstairs in a closed room, it walks out through the door rather than being moved (no `park.moved` line).
- **Logs** (`vfh_debug Nav on`): `navlinks.route` per new route; `navlinks.hop` only on ladders, stepladders and blocked stairs; no `nav.stuck` lines with `links=true` in a normal session.
- **Performance:** `perf.minute` `aiMsPerFrame` within 0.2 ms of 0.2.4 with three hirelings working at home.

## Commit
`feat(nav): hirelings route through doors and up stairs via the board's link graph`

## Rollback
Revert the commit. `WalkTo`, `CanReach` and `DoorHelper` return to their phase 02 state (the scanner and overlay stay). With the build kept, `BaseNavLinks = false` turns route-following off: `Active` is false, so every code path is the 0.2 one.

## As built
- **LegOracle** works out up to 4 legs per frame (12 made a 10–13 ms frame in the first test run). A plan's own questions use that budget first, synchronously; only the rest are queued. Most routes are planned in the frame they're asked for. A leg's end counts if the map's route ends within 1.6 m along the ground and 0.8 m in height of the point asked for (a chest's middle is inside it).
- **While waiting for answers** (`NeedLegs`), the hireling stands still for up to 3 s instead of walking the old way: moving would change the start and ask again. After 3 s it falls back for 10 s (`navlinks.route_failed last=slow_map`).
- **Fallback:** when there's no route through the links, `WalkTo` behaves as in 0.2 for that goal for 10 s, including the door detour (`DoorHelper.Detour`) as a last resort. That's the only time the detour runs inside a board's area. The direct check (`HirelingAI.FullRouteTo`) makes its own path query, because `BaseAI.FindPath` hands back its last result for any target within a second.
- **`nav_links off`** has no automatic restore: `vfh_t_nav4` ends with `nav_links on`. The `navlinks_hops` check counts from `nav_stats_reset`.
