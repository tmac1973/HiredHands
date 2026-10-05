# Base Nav Links — Project Overview

## Problem
Hirelings walk with Valheim's own navigation map (the navmesh), which is built for monsters crossing open ground, not for player houses. Checked in the game's `Pathfinding` code:
- **Doors:** the map is baked from the solid shapes of what's built, so a closed door is a wall. The map is rebuilt one 32 m square at a time, at most every 5 s after something changes, so a hireling that opens a door still sees a wall for a few seconds. The same delay leaves it with stairs that are already gone.
- **Stairs:** it plans for a 1.8 m tall walker that can step up 0.3 m. A stair up through a floor opening with less headroom than that doesn't connect the two floors, and steep stepladders don't connect at all.

Hired Hands has worked around this case by case: the door loop, followers using doorways, the chest upstairs, idle hirelings wandering up and down the stairs, and Gerd's door on the live server. The always-on `nav.stuck` log keeps showing routes that stop short indoors. Hirelings mostly work outdoors today, so this has been tolerable. The next feature, the Steward's indoor chores (smelters, kilns, cooking stations, chests on every floor), puts hirelings inside buildings most of the time, which is where the map is weakest.

## Goals
- Inside every hiring board's area, a hireling can reach any spot a player can walk to on any floor, through closed doors and up or down stairs, stepladders and modded stairs or ladders, without anyone setting anything up.
- Doors: a hireling plans its route through a door whether it's open or closed. It walks to the door, opens it, steps through and closes it behind itself (closing is a server setting).
- Floor to floor: stairs and ladders are found automatically from each piece's shape, so modded build pieces work without a list. An optional include/exclude list in the data file handles the odd piece the shape test gets wrong.
- If a hireling can't physically make it along a stair link (too steep, a ladder), it's moved to the far end after a few seconds.
- The links stay current: building or removing a piece near a board updates that board's links within seconds.
- The game's own map is still used outdoors, within one floor, and wherever it already has a full route. The links layer only steps in where that map falls short.
- You can see what was found: `vfh_navlinks show` draws every door and stair link (usable or blocked) and a hireling's current route, and each board's scan is logged.
- A server setting `BaseNavLinks` (on by default) switches the layer off, falling back to today's door and stairs handling.
- Inside board areas it takes over from today's door workarounds for finding a way: the detour to a door (`DoorHelper.Detour`) and, for followers, the doorway chase (`DoorwayTowards`). The door helper keeps its simple jobs everywhere: opening the door right ahead and closing doors behind. Followers in the field keep today's door handling unchanged.

## Non-goals
- No replacement of the game's map: no custom grid or floor scan, no path smoothing, no remembered player trails (VikingsForHire 1.3.0's approach).
- No links outside board areas. Other bases, field houses and dungeons keep today's door handling for followers.
- No new hireling jobs or chores. The Steward work comes after this and builds on it.
- No climbing animation for ladders: the fallback is a hop to the far end.
- No handling of player-locked (keyed) doors, or doors the board's owner can't use under a ward. Those stay closed, as today.
- No change to how followers chase their owner outside board areas (`Chase`, `FollowCatchUp`).

## Users & primary flow
Players who run a base with hirelings (single player, hosted, dedicated servers); server owners who never configure it.
1. A player builds a hiring board in a house with an upstairs and closed doors, and places a chest on the upper floor.
2. When the board is loaded (or a piece is placed or removed nearby), the board's area is scanned. Each door becomes a door link with a point on each side. Each piece whose surface climbs from one floor height to another becomes a stair link with a bottom and a top point.
3. A woodcutter comes back with wood and heads for the chest upstairs. The game's map has no full route there, so the hireling plans one through the links: to the front door, through it, to the bottom of the stairs, up, then to the chest.
4. It walks each leg with the game's map, opens the door when it reaches it, steps through and closes it behind, and walks up the stairs to their top point. If it isn't at the top after a few seconds, it's moved there.
5. It delivers and goes back to work by the reverse route.
6. If something goes wrong, the player runs `vfh_navlinks show` to see the links and the route. The log has the scan and the route choices for a bug report.

## Constraints
- Stack: C#, BepInEx 5, Jotunn, Harmony, publicized `assembly_valheim`. Same build, test and logging conventions as the rest of Hired Hands (`VfhLog` events, `VfhLog.Guard`, fixtures and macros in `Testing/` and `test/alias_vfh.yaml`).
- The links are built on each game that simulates hirelings (whoever owns a hireling's ZDO), from the pieces loaded there. Nothing new is sent over the network, and nothing new is saved in the world.
- Performance: a board's scan is spread over frames, with no frame spikes, on bases of a few thousand pieces. Answers to "can you walk from here to there on the game's map" are cached per board and shared by all its hirelings. Each hireling replans only when its goal moves, a step fails or the links change, and at most every 0.5 s.
- Must work with the live server's mod set (`1dotohsupermodded`, about 55 mods, including build-piece mods) and with AzuAutoStore/CraftyBoxes.
- Doors follow today's rules (`DoorHelper.Usable`): no keyed doors, and ward access for the board's owner.
- Ships as **0.3.0**. The data file gains a `navLinks` block (filled in by `DataDefaults.FillMissing`), which 0.2 builds reject. With Minor version strictness, the server and every player move to 0.3 together.
- 0.2.4 (idle spots, low funds pin, Shift+E hold, Call to board) is released on its own before this work starts, and is the baseline 0.3.0 is compared against.
- It must ship before the Steward work.

## Success criteria
- Automated macros pass in single player:
  - a hireling delivers to a chest in a closed room;
  - to a chest on an upper floor reached by stairs through a floor opening;
  - to one up a stepladder;
  - after the stairs are removed, it stops trying them within seconds and uses another route or chest.
- No `nav.stuck` lines for door or stairs cases during a normal play session on the live server, at Gerd's house and fence.
- `vfh_navlinks show` draws the expected links at Tim's base, and a modded staircase from the live mod set is found without any list entry.
- No frame spikes from the scan (`perf.minute` worst frame unchanged within noise) on the live base.
- With `BaseNavLinks` off, behaviour is identical to 0.2.4.

## Decisions
- **Where it applies** → board areas only: each hiring board's area. Followers in the field keep today's door handling.
- **Recognising stairs and ladders** → automatic, by each piece's shape (its surface climbs from one floor to another), with names like "stair"/"ladder" as a hint. An optional include/exclude list in the data file starts empty, for misdetected pieces only. Tim rejected a hand-made list: server owners won't add modded pieces one by one.
- **A stair link it can't walk** → hop to the far end: walk it, and if it isn't there after a few seconds, move it there.
- **Doors after walking through** → closed behind it (never in a player's face), with a server setting to leave them open.
- **Rollout** → server setting `BaseNavLinks`, on by default, to switch back to today's handling without a downgrade.
- **Release** → one release when everything is done, 0.3.0, batch-tested in single player and on the local server first.
- **0.2.4** → released on its own first; 0.3.0 is compared against it.
- **Followers at home** → when the follower and its owner are both inside the same board's area and the game's map has no full route, followers route through the links too. Outside board areas, following is unchanged.
- **Visibility** → `vfh_navlinks show` world overlay (links coloured usable/blocked, a hireling's current route) plus scan and route logging.
