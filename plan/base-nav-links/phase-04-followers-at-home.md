# Phase 04 — Followers use links at home

**Depends on:** 02 (`NavLinkRegistry.AreaAt`, fixture `house2`), 03 (`LinkNavigator`, `LegOracle`) · **Enables:** 05

## Goal
When you walk into your house with followers, and both you and they are inside the same board's area, a follower with no full route to you on the game's map follows you through the house's links. It goes through the door, opening and closing it, and up or down the stairs to the floor you're on, instead of today's doorway chase or a catch-up teleport. Outside board areas, following is exactly as today.

## Files touched
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: `Chase` hands over to `LinkNavigator` when it's active for the owner's position and the game's map has no full route to them.
- `src/VikingsForHire/Hirelings/Nav/LinkNavigator.cs`: a chase mode for a moving goal (`ChaseStep`), and `OnLinkStep` (true while a door or stair step is under way).
- `src/VikingsForHire/Followers/FollowCatchUp.cs`: the **stuck** catch-up teleport doesn't fire while `OnLinkStep` is true, or while a link route is being followed and the follower is getting closer to its next step point. The "far" rule is unchanged.
- `src/VikingsForHire/Testing/FixturesFollow.cs`:
  - fixture `player_to <tag> [height offset]`, which moves you to a tagged object (a direct move, not a teleport);
  - check `follower_gap <dist|dy>`, the largest 3D distance, or height difference, between you and any of your followers right now.
- `test/alias_vfh.yaml`: macros `vfh_t_nav5` and `vfh_t_nav6`.
- `docs/test-checklist.md`: rows VFH-NAVLINK-8 and 9.

## Steps
1. **When.** In `Chase(dt, point, stopDistance, run)`, before the existing logic: if `LinkNavigator.Active(point)` and `!PathReaches(point, max(stopDistance,1)+3)`, use `ChaseStep`.
   - `PathReaches` counts here only if its route also ends within 1.0 m of the owner's height.
   - Otherwise the existing chase runs unchanged: route, then doorway, then direct.
2. **`ChaseStep`.**
   - **Planning:** plans like phase 03 with the owner's position as the goal, then replans when the owner has moved more than 3 m from the route's goal, at most once per second.
   - **Mid-step:** a door or stair step under way is always finished before replanning, so it doesn't turn round in a doorway.
   - **Last step:** `Walk(goal)` uses the chase's `stopDistance`, and is done as soon as the game's map has a full route to the owner. The ordinary chase then takes over again.
   - **Unknown legs and no route:** handled as in phase 03, falling back to the existing chase.
3. **Catch-up.**
   - `FollowCatchUp` already teleports a follower that's stuck or too far behind.
   - While `OnLinkStep` is true, or while a link route is active and the distance to the current step point has dropped in the last 4 s, it doesn't count the follower as stuck.
   - The "far" rule (out of sight and too far) still applies, so a follower left far behind still catches up.
4. **Sneaking and running** are unchanged. `WantSneak` and the run boost apply to link steps through the same `MoveTowardsPublic`/`MoveToPublic` wrappers.
5. **Fixture and check.**
   - `player_to <tag> [dy]`: moves you to the tagged object plus `dy` metres up (default 0.5). It sets `transform.position`, the rigidbody position and `m_maxAirAltitude` directly, as `FollowCatchUp.Place` does for hirelings. It deliberately doesn't use `Player.TeleportTo`: `TeleportTravel` carries followers within `PortalFollowRadius` along on any `TeleportTo`, which would defeat the test.
   - `follower_gap dist` returns the largest 3D distance from you to your followers. `follower_gap dy` returns the largest absolute height difference.
6. **Macros** (`fast_timers on`, a board at level 5, `stock_board 400 0`, a level 1 woodcutter posted, `stone 1`, `recruit_posted`, and `vfh_assert_eventually 5 followers == 1` first; ending with `kill_hirelings`, `clear_area 60` and `log_errors == 0`):
   - `vfh_t_nav5` (VFH-NAVLINK-8, follow upstairs): `house2 H`, then `follow_stats_reset`, then `player_to H 0.5` (the chest upstairs).
     - `vfh_assert_eventually 45 follower_gap dist < 4`.
     - `vfh_assert follower_gap dy < 1`.
     - `vfh_assert follow_teleports == 0`.
   - `vfh_t_nav6` (VFH-NAVLINK-9, follow back down and out): runs all of `vfh_t_nav5`'s steps first (macros are independent), plus `chest out` at the start, then:
     - `player_to out`: a marker beside the board, outside the house.
     - `vfh_assert_eventually 45 follower_gap dist < 4`.
     - `vfh_assert_eventually 20 door_open H_door == false`.
     - `vfh_assert follow_teleports == 0`.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- In single player:
  - `vfh_t_nav5` and `vfh_t_nav6` pass.
  - `vfh_t_catchup1`, `vfh_t_recruit1`, `vfh_t_portal1`, `vfh_t_lag1a`, `vfh_t_lag1b` and every `vfh_t_nav*` macro still pass.
  - No `lvl=E` lines.

## Test plan
- **Macros:** the ones above.
- **By hand, single player:**
  - With two followers, walk into your house, close the door behind you, go upstairs. They come through the door (it's closed behind them) and up the stairs to you without a teleport (`follow.teleport` not in the log).
  - Go back down and out: they follow the same way.
  - Away from your base, in a house with no board, followers behave exactly as in 0.2.4.
- **Live server** (after release, phase 05): the same at Tim's base, and Gerd's door.

## Commit
`feat(nav): followers follow you through doors and up stairs inside board areas`

## Rollback
Revert the commit. `Chase` and `FollowCatchUp` return to their phase 03 state, where followers use today's doorway chase everywhere. Workers keep link routes. `BaseNavLinks = false` also turns this off.

## As built
- **Another floor counts as far.** Followers measure the gap to their owner along the ground, so with the owner upstairs right above it, a follower thought it had arrived. Inside a board's area, a height difference of more than 1.5 m now counts as far, both in `FollowBehaviour.OwnerDistance` and in `Chase`'s "close enough" check, so it follows you up.
- `ChaseStep` is `LinkNavigator.Chase`. It shares the walking code with `Walk`, with a 3 m goal-moved threshold and 1 s between plans. It drops back to the usual chase as soon as the game's map has a full route to the owner.
- `FollowCatchUp`'s stuck counter (jumps, sideways detours and the stuck teleport) is reset while `LinkNavigator.Progressing` (mid door or stair, or closing in on the next point within the last 4 s).
- `player_to` targets the `<tag>_up` floor piece (0.3 m above it), so you stand on the floor, not on the chest.
