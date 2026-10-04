# Next release: what's fixed and what to test

A running tab between releases. Each fix lands here as it's made; the batch test after a play session works through
the "To test" list, then the results go into `docs/test-checklist.md` and this file starts over for the next version.

## 0.2.3 (unreleased)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| b2f0df6 | Chests upstairs: height counts when arriving (no more standing on the stairs under a chest); a chest it can't reach for 25 s is skipped for 5 min; `nav.stuck` Info log (always on) + a jump or two when stuck (Gerd on the stairs) | the game simulating the hireling |
| 38d3ee2 | Hiring Charter: deconstructing a board of level 2+ gives a charter with its level; building a new board while carrying it starts it at that level and uses it up | your game |
| 3dee659 | Door loop: a hireling that opens a door for lack of a route walks straight through it before anything else, and the door stays open until it's through (Gerd at the fence) | the game simulating the hireling |
| c57f686 | Followers use doorways: with a wall between them and you and no route, they go to the door (open or closed), open it if needed and walk straight through, instead of pushing at the wall and jumping (Gerd at a single door) | your game (followers) |
| 4307a1a | A chop or mine order puts the worker in Stay at the job (finishes, then waits there; not sent home as left behind); orders last 10 minutes | your game (followers) |

### To test (batch)
- [ ] By hand: a chest on an upper floor (up stairs) holding wood; a woodcutter delivers into it. Check the log for `nav.stuck` / `deliver.chest_unreachable` lines (they're always on) wherever a hireling gets stuck during the session.
- [ ] Charter: deconstruct a level 3 board (once with hirelings, so the confirmation shows the charter line, once without): a Hiring Charter (level 3) lands in your inventory, its tooltip shows the level; build a board elsewhere carrying it: the board is level 3 and the charter is gone; a level 1 board gives no charter; a board broken by monsters gives none.
- [ ] `vfh_t_door1` (automated): a hireling gets into a closed room through its door to deliver.
- [ ] Gerd's fence on the live server: a hireling outside a fenced area with a door goes in and out without looping.
- [ ] By hand: walk into a building through a door (close it behind you, and again leaving it open) with followers: they come through the doorway (opening the closed door) without jumping or getting stuck at the wall.
- [ ] `vfh_t_order1` (automated, now also checks the worker switches to Stay).
- [ ] By hand: order a woodcutter onto a tree, walk 40-80 m away: it keeps chopping the tree and its logs, then waits there; a right click on nothing calls it back.

### Released but not yet tested in game (0.2.1 / 0.2.2)
- [ ] VFH-WEIGHT-1: a level 1 miner at copper goes to deliver at about 300 weight; the hover shows the weight; handing a follower too much says "can't carry that much".
- [ ] VFH-FUNDS-1: a board with about a day of food: red days-left on the hover, the daily message, the builder's map pin until topped up.
- [ ] VFH-GRAVE-2: open your follower's grave yourself; the map pin goes once it's empty; a second player can't open it.
- [ ] Prices: Contracts/Roster tabs show real amounts ("50 coins", "60 food pts"), hiring from level 2 takes only coins, upkeep only food.
- [ ] VFH-SNEAK-1: crouch with followers near: they crouch and creep, stand when you stand or a fight starts.
- [ ] VFH-POST-3: a woodcutter with "Works at home" off posted on a spot at home stands there instead of wandering, delivers and comes back; right click or Shift+E clears it.
