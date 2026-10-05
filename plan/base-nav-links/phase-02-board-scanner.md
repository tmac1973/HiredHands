# Phase 02 — Board scanner, link registry and overlay

**Depends on:** 01 (`NavGraph`, `NavLink`, `StairProfile`, data lists, `BaseNavLinks`) · **Enables:** 03 (routes are planned over the registry's graphs), 04

## Goal
Every hiring board loaded in a game gets an up-to-date link graph of its area:
- a door link for every door;
- a stair or ladder link for every piece that joins two floors, found by its shape.

The graph is rescanned a couple of seconds after any piece in the area is built or removed. Players can see it with `vfh_navlinks show` and read it in the log. Hirelings don't use it yet: this phase is verifiable on its own through the overlay, the log and test checks.

## Files touched
- `src/VikingsForHire/Hirelings/Nav/NavConvert.cs` (new): `ToNav(Vector3)` and `ToUnity(NavPoint)`.
- `src/VikingsForHire/Hirelings/Nav/DoorRules.cs` (new): `Usable(Door, long boardOwner)`, `IsOpen(Door)` and `BoardOwner(string boardId)`, moved out of `Hirelings/DoorHelper.cs`, which then calls them, so its behaviour is unchanged.
- `src/VikingsForHire/Hirelings/Nav/StairSampler.cs` (new): raycasts a piece's own colliders along an axis into `(Along, Height)` samples, and checks the floors at a link's ends.
- `src/VikingsForHire/Hirelings/Nav/BoardNavScanner.cs` (new): the scan job for one board, spread over frames.
- `src/VikingsForHire/Hirelings/Nav/NavLinkRegistry.cs` (new): one `BoardNav` per loaded board (graph, centre, radius, dirty state, running job), `Tick()`, `MarkDirty(Vector3)`, `AreaAt(Vector3)` and `Graph(boardId)`.
- `src/VikingsForHire/Hirelings/Nav/NavPiecePatches.cs` (new): Harmony postfixes on `Piece.Awake` and prefixes on `Piece.OnDestroy` that call `NavLinkRegistry.MarkDirty(piece position)`, try/catch with `VfhLog.PatchFailed`.
- `src/VikingsForHire/Hirelings/Nav/NavOverlay.cs` (new): draws links with `LineRenderer`s.
- `src/VikingsForHire/Commands/NavCommands.cs` (new): `vfh_navlinks <show|hide|scan|list>`.
- `src/VikingsForHire/Plugin.cs`: `NavCommands.Register()`, `FixturesNav.Register()`, and `VfhLog.Guard(LogCat.Nav, "navlinks.tick_failed", Hirelings.Nav.NavLinkRegistry.Tick)` in `Update`.
- `src/VikingsForHire/Testing/FixturesNav.cs` (new):
  - fixtures `house2`, `stepladder` and `remove_piece`;
  - check `navlinks`.
- `test/alias_vfh.yaml`: macros `vfh_t_navscan1`, `vfh_t_navscan2` and `vfh_t_navscan3`.
- `docs/test-checklist.md`: rows VFH-NAVLINK-1 to 3.

## Steps
1. **`DoorRules`**: move `Usable`, `IsOpen` and `BoardOwner` from `DoorHelper` unchanged, as `internal static` methods. `DoorHelper` keeps its own behaviour by calling them.
2. **Board areas.** `NavLinkRegistry.Tick()` runs every frame, and does nothing when `BaseNavLinks` is off or `ZNetScene.instance` is null.
   - It keeps a `BoardNav` per `HiringBoard.Loaded` entry, keyed by board id. A board that disappears is dropped and its overlay removed.
   - The area radius is `new LevelRules(DataStore.Current).MaxWorkRadius(board.Level) + 8f`, centred on the board.
   - A level change (radius changed by more than 0.5 m) marks the board dirty.
   - `AreaAt(point)` returns the `BoardNav` whose centre is nearest in XZ with the point inside its radius, or null.
3. **Dirty marking.**
   - `MarkDirty(position)` marks every `BoardNav` whose area contains the position. It sets `DirtySince = Time.time` on the first mark and `LastMark = Time.time` on every mark.
   - A board is scanned once 2 s have passed since `LastMark`, or 10 s since `DirtySince`, so a base loading in (hundreds of `Piece.Awake` calls) produces one scan, not hundreds.
   - A new `BoardNav` starts dirty.
   - Doors opening and closing don't mark anything: a door link exists whether the door is open or not.
4. **Scan job** (`BoardNavScanner`, a plain class stepped from `Tick`):
   1. Collect `Piece.GetAllPiecesInRadius(centre, radius, list)` once at the start.
   2. Process up to 40 pieces per frame, stopping early if the frame's scan time passes 2 ms (measured with `Stopwatch`).
   3. For each piece:
      - **Door:** a piece with a `Door` component in itself or its children becomes a door link.
        - The endpoints are `door.transform.position ± door.transform.forward * 1.2`. Each is snapped to the floor below with `Physics.Raycast(p + up*1.0, down, 2.5, FloorMask)` (FloorMask: `terrain`, `piece`, `Default`, `static_solid`); with no hit it keeps the door's own height.
        - A door link has no waypoints, and `Length` is the distance between its ends.
        - Ward and lock rules are checked when routing (phase 03), not here, because wards change.
      - **Exclude list:** a prefab name in `DataStore.Current.NavLinks.Exclude` is skipped (Debug log, `reason=excluded`).
      - **Stair candidate:** the piece qualifies if any of these hold:
        - its name hints at stairs: the prefab name or `m_name` contains `stair`, `ladder` or `step` (case-insensitive), or the prefab is in `Include`;
        - or its combined non-trigger collider bounds are 0.8 m to 6 m high and at most 8 m across.
        - Other pieces are ignored without logging.
      - **Shape test** (`StairSampler.Sample`):
        - Along each of the piece's two horizontal axes (`forward`, `right`), cast 9 rays straight down through the bounds centre line, from `bounds.max.y + 0.5` to `bounds.min.y - 0.5`. Use `Collider.Raycast` against this piece's own colliders only.
        - At each sample, keep the highest hit whose `normal.y >= 0.5`, as a height.
        - Run `StairProfile.Classify(samples, nameHint)` for each axis and keep the accepted axis with the larger rise.
      - **Floors at both ends** (this is what rejects roofs, sloped walls and furniture):
        - Bottom: the bottom sample's point moved 0.7 m further out along the axis, away from the top. Raycast down from 1.0 m above it, 2.5 m, on FloorMask. It must hit within 0.5 m of the bottom height, with `normal.y >= 0.7`.
        - Top: the same, 0.7 m beyond the top sample, within 0.4 m of the top height.
        - Headroom: `Physics.Raycast(end + up*0.2, up, 1.5, FloorMask)` must hit nothing at either end.
        - Failures are rejected (Debug log) with `reason=no_bottom_floor`, `no_top_floor` or `no_headroom`.
      - **Ladder fallback:** applies only when `nameHint` is set and the shape test rejected the piece with `no_surface` or `too_steep`.
        - Bottom: the bounds' bottom centre, 0.6 m out on whichever horizontal side has a floor (checked as above).
        - Top: `bounds.max.y` at the bounds centre, 0.7 m out on whichever side has a floor within 0.5 m of that height.
        - Accepted as a ladder, with waypoints bottom, top.
        - With no floor on any side, rejected `no_top_floor`.
      - **Accepted stair:** endpoints as above. Waypoints are the kept samples from bottom to top (with the two end points added). `Length` is the summed waypoint distance.
   4. **Finish:**
      - `graph.Rebuild(links)` (bumps `Version`).
      - Log Info `navlinks.scan` with: `board`, `pieces`, `doors`, `stairs`, `ladders`, `rejected` (stair candidates rejected), `ms` (total scan time), `frames`, `version`.
      - Per rejected candidate with a name hint, log Debug `navlinks.piece_rejected` with: `prefab`, `pos`, `reason`. Unnamed rejects aren't logged; there are too many walls.
   5. A board marked dirty during its scan rescans once the current scan finishes.
5. **Piece patches.**
   - `Piece.Awake` postfix: `NavLinkRegistry.MarkDirty(__instance.transform.position)`, only when `BaseNavLinks` is on and the piece has a `ZNetView` with a valid ZDO (not the ghost a player is placing).
   - `Piece.OnDestroy` prefix: the same.
   - Both are wrapped in try/catch with `VfhLog.PatchFailed("NavPiecePatches.Awake"/"OnDestroy", e)`.
6. **Overlay** (`NavOverlay`, one `GameObject` per shown board, parented under the board):
   - Door links: a green line A→B.
   - Stairs: a yellow line through the waypoints.
   - Ladders: a cyan line.
   - Blocked links (`BlockedUntil > now`): drawn red.
   - Each endpoint: a 1 m white vertical tick.
   - Lines are 0.05 m wide, using the `Sprites/Default` shader. Rebuilt when the graph's `Version` changes or a link's blocked state changes, checked every 0.5 s.
   - Phase 03 adds hireling routes to the same overlay.
7. **Command** `vfh_navlinks`, not a cheat (it only draws on your own screen):
   - `show`: overlay on for every loaded board.
   - `hide`: overlay off.
   - `scan`: mark the nearest board dirty now, skipping the debounce.
   - `list`: prints the nearest board's links, one line each (`#id kind A→B length piece blocked`), and the counts.
   - Registered like the other `VfhCommand`s, with an option list.
8. **Fixtures** (`FixturesNav.cs`, using the `Spawn`/`OwnBuilt` helpers that `Testing/FixturesWork.cs` uses):
   - `house2 <tag>`: a 6×6 m two-floor building 12 m to the board's left (the `room` fixture is on its right).
     - Ground floor: wood floor pieces on the ground, `woodwall` sides, and a `wood_door` facing the board (tagged `<tag>_door`).
     - Inside: one `wood_stair` against the back wall (tagged `<tag>_stair`).
     - Upper floor: `wood_floor` pieces at the height of the stair's top (read from the placed stair's collider bounds, `bounds.max.y`), leaving the 2×2 m above the stair open.
     - A `piece_chest_wood` tagged `<tag>` upstairs, in the far corner from the stair. Optional item/count pairs fill the chest as in `room`.
     - Support wear is off on every piece, as in `room`.
     - Logs `fixture.house2` with the stair top height.
   - `stepladder <tag>`: the same building with the vanilla stepladder (prefab `wood_stepladder`) in place of the stair. If `ZNetScene.instance.GetPrefab("wood_stepladder")` is null, the fixture fails the test with `stepladder prefab missing`.
   - `remove_piece <tag>`: destroys the tagged piece through its `WearNTear.Remove()` (as a player deconstructing).
   - Check `navlinks <doors|stairs|ladders|floorlinks|rejected|version> [tag]` (`floorlinks` = stairs + ladders): that count for the board nearest to the tagged object (or to the player). `version` lets a macro wait for a rescan.
9. **Macros** (`test/alias_vfh.yaml`):
   - `vfh_t_navscan1`: a board, `room R` and `house2 H`, wait for a scan, then assert `navlinks doors >= 2` and `navlinks stairs >= 1`, with no `lvl=E` lines.
   - `vfh_t_navscan2`: a board and `stepladder S`, then assert `navlinks floorlinks S >= 1`. The stepladder is a stair or a ladder depending on its measured slope; both count.
   - `vfh_t_navscan3`: a board, `house2 H`, then `remove_piece H_stair`, then `vfh_assert_eventually 10 navlinks stairs == 0`.
   - Each macro ends with `clear_area` and asserts `log_errors == 0`.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- In game (single player, `vikingsforhire-dev` profile):
  - `vfh_t_navscan1`, `vfh_t_navscan2` and `vfh_t_navscan3` log `evt=test.result pass=true`.
  - `vfh_t_door1` still passes (DoorHelper refactor).

## Test plan
- **Macros:** the three macros above (rows VFH-NAVLINK-1..3).
- **By hand, single player:**
  - Build a house with a door and stairs to an upper floor near a board. `vfh_navlinks show` draws a green line through the door and a yellow line up the stairs.
  - Remove the stairs: within about 3 s the yellow line is gone, and a new `navlinks.scan` line appears.
  - Build a sloped roof: no line on it. With `vfh_debug Nav on`, no `piece_rejected` line either: it isn't named like stairs.
- **Live mod set** (`1dotohsupermodded` profile, single-player copy of a base or the live base read-only):
  - The `navlinks.scan` line shows `ms` and `frames`. No frame over 2 ms of scan work (the `perf.minute` worst frame is unchanged).
  - A modded staircase is drawn yellow without a list entry. A modded piece wrongly taken as stairs is fixed by adding it to `navLinks.exclude`, followed by `vfh_navlinks scan`.
- **Logs:** with `vfh_debug Nav on`, every scan logs `navlinks.scan`, and named rejects log `navlinks.piece_rejected` with a reason. No `lvl=E`.

## Commit
`feat(nav): scan each board's doors and stairs into a link graph, with vfh_navlinks overlay`

## Rollback
Revert the commit. The registry, patches and overlay go with it. `DoorHelper` returns to holding its own door rules, with identical behaviour. Nothing is saved in the world. With the build kept, setting `BaseNavLinks = false` stops all scanning; the patches return early.

## As built
- The check is `navlinks <kind> [tag]`. With a tag it counts only links with an end within 8 m of the tagged object, so other buildings in the test world don't disturb a macro.
- `house2` has no ground-floor pieces: the hireling walks on the terrain inside the walls. Its upper floor is the back 2 m strip (three `wood_floor` pieces, the middle one tagged `<tag>_up`), with no walls upstairs. The fixture turns the stair round if it was placed climbing the wrong way, then puts its high end at the floor's front edge and sets the floor's surface level with the stair's top.
- The vanilla prefab names `wood_stair` and `wood_stepladder` were confirmed in the game's asset manifest.
