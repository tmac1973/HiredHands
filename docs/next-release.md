# Next release: what's fixed and what to test

A running tab between releases. Each fix lands here as it's made; the batch test after a play session works through
the "To test" list, then the results go into `docs/test-checklist.md` and this file starts over for the next version.

## 0.4.3 (unreleased)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 613aeea | Steward chore "Board food" (level 1, toggle in Shift+E, server `StewardBoard`): below `StewardBoardRefillDays` (3) days of upkeep it stocks its own board from the chests up to `StewardBoardFillDays` (7), cheapest food first, only food the board accepts (honours `AllowRawFood`), never the last of a food | the game simulating the Steward |

| 5b5dedb | A Steward follower in Gather Here picks up loose loot within the gather radius of its spot (nearest first, until cargo is full; not a player's fresh drops, the board's pile or warded items); status "Picking up loot" | the owner's game |

| (this commit) | `storage.full` log (once per 5 min per item): when work pauses for a full item, which chests holding it were counted and the room in each (Brand paused the kiln with a coal chest showing 10 free slots) | the game simulating the hireling |
### To test (batch)
- [ ] Macros (single player): `vfh_test_chain board_food loot`.
- [ ] By hand: take a Steward out, fight a few Greylings, park it in Gather Here: it picks up the drops; bring it home, the loot ends up in chests (or the pile) a few minutes later.
- [ ] Live server: Brand's board below 3 days of food (or `StewardBoardRefillDays` raised): he fetches cooked food and puts it on the board; status "Stocking the board with food"; your best food left alone.

## 0.4.2 (released 2026-10-06)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| e64878e | `PauseWhenStorageFull` (Work, on): at home, woodcutters and miners leave trees/rocks (and pickups) whose item's chests are full, Stewards stop loading a station whose product has no room, and skip beehives, sap and tidying for full items; status "Paused: no room left for Wood" / "Charcoal kiln: paused, no room left for Coal" | the game simulating the hireling |
| d0b9674 | Command Stone key hints: with the stone in hand the bottom-right hints read Recruit / order (left click), Recall / back to work (right click), Retreat (middle click), Hireling panel (Shift+E) instead of attack/block | your game |

### To test (batch)
- [x] Macros (single player): `vfh_test_chain pause1 pause2 repairs repairs2` (all passed 2026-10-06).
- [x] By hand: take out the Command Stone: the four hints show (and go back to the normal ones with another weapon). (Tim, local game, 2026-10-06)
- [ ] Live server: fill the coal chests: Brand stops loading the kiln and says why; fill the wood chests: the woodcutter pauses with "Paused: no room left for Wood".

## 0.4.1 (released 2026-10-05)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 3fe5c40 | Repairs: unroofed pieces worn by rain (it stops at half health) are left alone above 50%; pieces repaired from a player's hammer reach (5 m, in sight); a route is checked before starting; a piece it can't get to is skipped for 30 min and named in the status ("Repairs: can't get to Wood beam") | the game simulating the Steward |
| 4b459db | `steward.tidy_left` (once a minute, when items lie in the radius and none is taken): how many, free cargo slots, why each is left, and which items no chest holds | the game simulating the Steward |

### To test (batch)
- [x] Macros (single player): `vfh_test_chain repairs repairs2` (passed 2026-10-06).
- [ ] Live server: switch Brand's Repairs back on: no endless walking at rain-worn walls or the beam by the stake wall; `steward.repair_stuck` lines at most once per piece per 30 min.

## 0.4.0 (released 2026-10-05): Steward chores

Plan: `plan/steward-chores/`.

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 748bf86 | Chore list, urgency scoring, `choreLevels` in the data file (biome table), windmill and spinning wheel as Steward stations, `10 - Steward` settings, PetPantry/Torches Eternal detection | everywhere (data, pure logic) |
| af9eca8 | Steward chore loop (most urgent job first), stations gated per station level (level 1 Stewards no longer smelt), Mills chore, Shift+E chore list with toggles and states, status lines naming the chore or what's missing | the game simulating the Steward; panel on yours |
| d7ebfd8 | Fires chore: fires, torches, braziers and hot tubs fuelled from chests (level 1), a trip per fuel type; steps aside for Torches Eternal | the game simulating the Steward |
| cef3728 | Beehives (level 1) and sap collectors (level 6) emptied at half full, a trip per kind; honey and sap delivered to chests holding them | the game simulating the Steward |
| 811918a | Tamed animals (level 2): food from chests dropped in front of each hungry tamed animal, nearest first; marked so AzuAutoStore leaves it; steps aside for PetPantry | the game simulating the Steward |
| 0f1608b | Repairs (level 3): damaged pieces repaired worst first under vanilla's rules (station in range, wards, free), not until 20 s after the last enemy near the base | the game simulating the Steward |
| fbe2004 | Fermenters (level 4: tap, store meads, load a base), shield generators (level 7: bones), tidying up (level 1, last priority: items on the ground into chests that hold them) | the game simulating the Steward |
| 60671cf | Sap and honey collected on a trip are always delivered (sap stayed in cargo as eitr refinery supply) | the game simulating the Steward |
| 15b7aaf | A fermenter without enough cover says so ("needs more walls and a roof round it to brew") instead of being skipped silently | the game simulating the Steward |
| f0eed65 | The broom in the data file was dropped as an unknown item (the data is checked before the mod's own items are registered); our items and other mods' Jotunn items now count as known | everywhere (data) |
| 485973d | The Steward's broom: club stats, the Steward's weapon in new data files | the Steward |
| 4c5ad0d | The broom is built from simple shapes (wooden handle, straw bristles); the cultivator is left for a Farmer | your game, drawing the Steward |
| b3357ee | Older data files: the Steward's old default club becomes the broom (chosen gear kept); README Steward section, CHANGELOG, version 0.4.0 | everywhere (data) |

### To test (batch)
- [x] By hand: the broom (looks great, 2026-10-05). With a fresh data file, or the Steward's `gear` set to `VFH_Broom`, a Steward holds the homemade broom (wooden handle, straw bristles at the far end, not through its arm) and swats a Greyling with it; `vfh_broom_info` names the materials.
- [x] Macros: `vfh_test_chain fires chore_urgency` (passed).
- [x] Macros: `vfh_test_chain beehive sap animals repairs fermenter shield tidy` (all passed, some across reruns).
- [x] Macros (single player): `vfh_test_chain chore_gate chore_toggle mills azu3 keep1 keep2` (passed; work5 needs AzuAutoStore absent).
- [x] By hand: a level 2 Steward's Shift+E panel lists the seven chores. Fires, beehives, stations and animals are on (fires and animals show "handled by TorchesEternal"/"PetPantry" with those mods installed). Mills, sap and repairs show "locked until level 5/6/3". Under Stations: "Blast furnace: locked until level 5, Eitr refinery: locked until level 6". Switching Stations off stops it loading the smelter.
- [ ] Dedicated server (Tim, after publishing): a Steward at a real base; the full single-player run is skipped. The Roster tab and hover show what the Steward is doing ("Loading Smelter") or why it's idle ("Smelter: no Coal in any chest", "All done").

## 0.3.0 (released 2026-10-05): base nav links

Plan: `plan/base-nav-links/`. All of it runs on the game simulating the hireling (and the overlay on yours).

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| f4a7fa2 | Core link graph, stair shape test, route planner; settings `BaseNavLinks`, `HirelingsCloseDoors`; data `navLinks.include/exclude` | everywhere (pure logic, unit-tested) |
| 65c2098 | Each board's area scanned into door and stair/ladder links, rescanned 2 s after a piece is built or removed; `vfh_navlinks show/hide/scan/list` | the game with the board loaded |
| d26a4f5 | Hirelings route through doors and up stairs when the game's map can't get them there; hop to the top of a ladder; replan around failures; `CanReach` counts upstairs spots | the game simulating the hireling |
| 0970367 | Followers follow you through doors and upstairs while you're both in a board's area; another floor at home counts as far; no stuck-teleport while crossing a door or stair | your game (followers) |
| f86c258 | Code review fixes before the first in-game run (walk steps finishing when running, stale steps, doors re-opened and not shut on another hireling, no grinding under an owner it can't reach, stair waypoints) | as above |
| 15e1adf…e39ebaa | From the first test runs: rescan when a piece is removed; fewer map queries per frame; chained flights (a stair running straight onto another) join; short same-level steps (small landings, the last step to a chest) walkable without the game's map; `vfh_navlinks why`; `vfh_deliver`; single-player fixes in the test harness (follower requests, server checks) | as above |

### To test (batch)
- [x] Macros in single player (`vikingsforhire-dev`, `vfh_debug Nav on`): `vfh_t_navscan1`, `vfh_t_navscan2`, `vfh_t_navscan3`, `vfh_t_nav1` … `vfh_t_nav6`; then the regression set `vfh_t_door1`, `vfh_t_order1`, `vfh_t_gather1`, `vfh_t_deliver1`, `vfh_t_deliver2`, `vfh_t_catchup1`, `vfh_t_recruit1`, `vfh_t_portal1`, `vfh_t_home1`, `vfh_t_post1`, `vfh_t_lag1a`, `vfh_t_lag1b`. Easiest: `vfh_test_chain navscan1 navscan2 navscan3 nav1 nav2 nav3 nav4 nav5 nav6` then the rest.
- [x] By hand at a house of yours near a board: `vfh_navlinks show` draws the door green and the stairs yellow; remove the stairs and the line goes within ~3 s; a roof gets no line.
- [x] By hand: a woodcutter delivering to a chest upstairs behind a closed door; it closes the door behind it; `vfh_navlinks show` draws its route (white).
- [x] By hand: followers into the house, door closed behind you, upstairs: they follow without a teleport; back down and out.
- [x] Local dedicated server: `vfh_t_nav1`, `vfh_t_nav5`, `vfh_t_nav6` and `vfh_t_portal1` from a client (passed).
- [ ] Live mod set (single-player copy with `1dotohsupermodded`): modded staircases show as links without any list entry; `navlinks.scan` `ms`/`frames` reasonable; `perf.minute` worst frame and `aiMsPerFrame` about as in 0.2.4.
- [ ] Live server after release: Gerd's door and fence, no door/stairs `nav.stuck` lines.

## 0.2.4 (released 2026-10-05, testing on the live server)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| d7466ba | Idle hirelings (nothing to do at home, e.g. "Works at home" off and no post) stand at an open-air spot near the board they have a full route to, moving now and then, instead of wandering into buildings and up and down the stairs (Gerd) | the game simulating the hireling |
| e30b18a | Low funds map pin said "food for 0 days" while the board's hover said 20: a board loaded in your game now gets its pin from what your game sees in it (as the hover does); the server logs `funds.pin_low` (funds, upkeep, items it read) whenever it calls a board low, to find why its copy was wrong | your game (pins), server (log) |
| 11ed182 | A hireling stands still facing you while its Shift+E panel is open, as with its cargo (works whichever game runs it; lapses after 3 s if your game goes away) | your game + the game simulating the hireling |
| ae03459 | Roster tab: **Call to board** brings a working hireling to the board to wait (off work) until **Back to work** (Roster or Shift+E); moved there if no route or stuck 20 s; logs `park.called`/`park.arrived`/`park.moved`/`park.released` | your game (button) + the game simulating the hireling |

### To test (batch)
- [ ] Call to board: on the Roster tab pick a woodcutter out chopping, press **Call to board**: it says "Coming to the board", walks over, stands beside the board facing you, roster shows "Waiting at the board"; **Back to work** (Roster, then again from Shift+E) sends it back. A hireling upstairs in a closed building (no route) is moved to the board within ~5 s. Recruit a waiting one with the stone, release it at home: it goes to work, not back to waiting.
- [ ] Shift+E on a working woodcutter mid-walk: it stops and faces you until you close the panel (Esc or walk away), then carries on. Same on the live server for a hireling another player's game runs.
- [ ] Live server: the low funds pin on a stocked board goes away within a minute of the update (the hover and pin agree). If a pin still disagrees with the hover anywhere, grab the server's `funds.pin_low` line.
- [ ] By hand (live server): Gerd with "Works at home" off and no post stands near the board outside the house; every minute or so he moves to another spot nearby; no stair runs. A board inside a hall: idle hirelings stand on the hall floor near it.

## 0.2.3 (released 2026-10-04, testing on the live server)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| f3f3500 | Board upgrades take materials from chests near the board via AzuCraftyBoxes (inventory first); Upgrade tab counts them; per-player `UpgradeFromNearbyChests` | your game |
| b2f0df6 | Chests upstairs: height counts when arriving (no more standing on the stairs under a chest); a chest it can't reach for 25 s is skipped for 5 min; `nav.stuck` Info log (always on) + a jump or two when stuck (Gerd on the stairs) | the game simulating the hireling |
| 38d3ee2 | Hiring Charter: deconstructing a board of level 2+ gives a charter with its level; building a new board while carrying it starts it at that level and uses it up | your game |
| 3dee659 | Door loop: a hireling that opens a door for lack of a route walks straight through it before anything else, and the door stays open until it's through (Gerd at the fence) | the game simulating the hireling |
| c57f686 | Followers use doorways: with a wall between them and you and no route, they go to the door (open or closed), open it if needed and walk straight through, instead of pushing at the wall and jumping (Gerd at a single door) | your game (followers) |
| 4307a1a | A chop or mine order puts the worker in Stay at the job (finishes, then waits there; not sent home as left behind); orders last 10 minutes | your game (followers) |

### To test (batch)
- [ ] By hand (AzuCraftyBoxes installed): put some of the next upgrade's materials in a chest near the board and the rest in your inventory: the Upgrade tab heading says "counting nearby chests" and the counts include the chest; upgrade: your inventory's share goes first, the rest leaves the chest (log `upgrade.requested ... from_chests=`). A chest outside CraftyBoxes' range, or with pulling toggled off, isn't counted. With `UpgradeFromNearbyChests = false` only the inventory counts.
- [ ] By hand: a chest on an upper floor (up stairs) holding wood; a woodcutter delivers into it. Check the log for `nav.stuck` / `deliver.chest_unreachable` lines (they're always on) wherever a hireling gets stuck during the session.
- [ ] Charter: deconstruct a level 3 board (once with hirelings, so the confirmation shows the charter line, once without): a Hiring Charter (level 3) lands in your inventory, its tooltip shows the level; build a board elsewhere carrying it: the board is level 3 and the charter is gone; a level 1 board gives no charter; a board broken by monsters gives none.
- [x] `vfh_t_door1` (automated, passed): a hireling gets into a closed room through its door to deliver.
- [ ] Gerd's fence on the live server: a hireling outside a fenced area with a door goes in and out without looping.
- [ ] By hand: walk into a building through a door (close it behind you, and again leaving it open) with followers: they come through the doorway (opening the closed door) without jumping or getting stuck at the wall.
- [x] `vfh_t_order1` (automated, passed, now also checks the worker switches to Stay).
- [ ] By hand: order a woodcutter onto a tree, walk 40-80 m away: it keeps chopping the tree and its logs, then waits there; a right click on nothing calls it back.

### Released but not yet tested in game (0.2.1 / 0.2.2)
- [ ] VFH-WEIGHT-1: a level 1 miner at copper goes to deliver at about 300 weight; the hover shows the weight; handing a follower too much says "can't carry that much".
- [ ] VFH-FUNDS-1: a board with about a day of food: red days-left on the hover, the daily message, the builder's map pin until topped up.
- [ ] VFH-GRAVE-2: open your follower's grave yourself; the map pin goes once it's empty; a second player can't open it.
- [ ] Prices: Contracts/Roster tabs show real amounts ("50 coins", "60 food pts"), hiring from level 2 takes only coins, upkeep only food.
- [ ] VFH-SNEAK-1: crouch with followers near: they crouch and creep, stand when you stand or a fight starts.
- [ ] VFH-POST-3: a woodcutter with "Works at home" off posted on a spot at home stands there instead of wandering, delivers and comes back; right click or Shift+E clears it.
