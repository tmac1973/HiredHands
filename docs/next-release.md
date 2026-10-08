# Next release: what's fixed and what to test

A running tab between releases. Each fix lands here as it's made; the batch test after a play session works through
the "To test" list, then the results go into `docs/test-checklist.md` and this file starts over for the next version.

## 0.6.0 (released 2026-10-08): blocking and dodging, CombatSkill, tree patches, passing

Plans: `plan/tree-patches/overview.md`, `plan/combat-ai/overview.md` (blocking and dodging).

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 6641ffc | Tree patch sign (inside a board's area; Shift+E: radius, kind; hover shows saplings/trees and draws the circle); woodcutters plant its free spots from seeds in the chests (kinds their axe can fell, clear of buildings), felling grown trees as usual; `WoodcuttersPlantTrees` | your game (sign); the game simulating the woodcutter |
| d87d581 | Woodcutters never fell saplings (a growing plant is a tree-type destructible) | the game simulating the woodcutter |
| f120e07 | The tree patch sign stands on its own (the wall sign it's made from collapsed without support) | everywhere |
| 881bdd1 | A patch set to Any plants only from ordinary tree seeds; PlantEverything's ancient (Ancient seed) and Ygga (Sap) trees only when chosen by name | the game simulating the woodcutter |
| cc40a2f | Test cleanup empties cargo before killing hirelings (no graves or pins from test runs) | tests |
| 6161f02 | Hirelings meeting head-on keep right and slide past; wedged for 2 s, they pass through each other for a moment (`HirelingsPassEachOther`); `vfh_t_pass` corridor test | the game simulating the hirelings |
| d8502e9…8aa9922 | Passing tuned with the corridor test: no sidestep with a wall just to the right; pass through when getting nowhere (under 0.5 m a second) with another hireling within 2.5 m; `PassSideRoom` (1.2 m), `PassThroughAfter` (1.5 s) | the game simulating the hirelings |
| f95f9f2 | Combat 1/6: hirelings in a fight notice swings coming at them (when they land, how hard after armor, area or not) with a per-level read chance (`readChance`, `parryChance`, `dodgeChance`, `dodgeCooldown` in the levels table); `BlockAndDodge` setting (now `CombatSkill`). Nothing reacts yet | the game simulating the hireling |

| f8e249e | Combat 2–4/6: guards with shields raise them in time for swings and projectiles they see coming (a well-timed one parries: "Parry!", the attacker staggers); any hireling rolls out of the way of a hit that would take over a quarter of its health or explodes (cooldown by level). The 0.5.0 late block hardly ever fired (guards were nearly always mid-swing) | the game simulating the hireling |
| 4d7731f | Combat 5/6: hit times learnt from hits that land (Troll swings were read up to a second late); fight records in the balance log carry blocks, parries and dodges; `scripts/defense-ab.sh` + `balance-report.py --compare-defense` | the game simulating the hireling; tests |
| d8c690f | `CombatSkill` presets (Off, Green, Trained, Veteran) replace `BlockAndDodge`; measured: L3 guard vs Troll takes 65/63/54/35% damage Off/Green/Trained/Veteran, every fight won | the game simulating the hireling |
| cd50e8e | `HealthRegen` (Combat): out-of-fight healing as a multiple of today's (0–3, default 1); VFH-REGEN-1 passes | the game simulating the hireling |
| bf15356 | Stair scan reads a piece along its own length, not its world-aligned bounds: a turned stepladder read gaps (not a stair) or a 1.37 m top depending on which way the board faced. Test house measures each flight with the scan's shape test (fails if it isn't a stair), stacks as many as reach a 1.8 m+ storey (wood_stair is 1.05 m a flight, as in the game: two; the stepladder 2.05 m: one) and warns below that; `vfh_fixture stair_probe <prefab>` | the game with the board loaded (scan); tests |
| b313640 | No reaching through floors and walls: within 3 m a chest (or station) counts as in reach only with no other building piece in the way, so a chest upstairs isn't filled from the ground below or from outside the wall. Walking up to it: only spots it can use it from, picked again while routes upstairs are worked out, closing in meanwhile; progress counted along the route (a detour round a hill gave up after 25 s). Deliveries plan without a chest just found unreachable (its share went to the pile). Test deposit counts reset per house | the game simulating the hireling; tests |
| 06b549b, 6d892bf | Tests protect the tester: from `vfh_test_begin` until the test queue is done (or aborted) you're in ghost mode and take no hits at all (vanilla's god and ghost modes only stop the death; area hits still landed and staggered you). The per-row `vfh_fixture ghost on` steps are gone; `ghost off` is left for a row that needs something to come for you. A chain no longer looks finished between two of its commands (protection went off right after `vfh_test_begin`). `vfh_t_protect` (VFH-PROTECT-1), `vfh_fixture hit_me`, checks `player_health`, `player_ghost`, `tester_hits_blocked` | tests |

### Post-merge regression (2026-10-08, merged build 1185828 + row fixes, 36 rows through ModTestBridge)
All pass, NAVLINK-4/5/6 included (the stair fix holds). PROJ-1 and STN-3 failed once and passed twice on rerun
(flaky). Two rows needed fixing for the merged changes: VFH-DODGE-3 turns `HealthRegen` off (the archer healed out of
"badly hurt" before the first arrow), and VFH-NAV-2 now waits for the delivery to finish (a woodcutter now walks into the
room through the door instead of reaching the chest through the wall, so it takes longer).

### Blocking and dodging measured (2026-10-08, single player, `scripts/defense-ab.sh 5`, plus 8 more archer runs each way)
Damage taken per fight (share of max health) is lower with blocking and dodging on (`CombatSkill` Trained) for the melee guards and the archer, with every
fight still won; the woodcutter is unchanged (no troll hit is over a quarter of its health, and it has no shield, so it
neither rolls nor blocks: as designed). Read rates match the levels table; parry and dodge roll rates had too few rolls to
judge (n < 30): check by eye.

```
job          lvl  enemy            mode  runs  taken  median  won   died  length  read rate          parry roll  dodge roll  blocks/parries  dodges/missed
-----------  ---  ---------------  ----  ----  -----  ------  ----  ----  ------  -----------------  ----------  ----------  --------------  -------------
GuardMelee   1    Greydwarf        off   5     42%    42%     100%    0%  24s     reads 0            rolls 0     dodges 0    2/2             0/0          
GuardMelee   1    Greydwarf        on    5     36%    33%     100%    0%  27s     51% vs 50% (n=72)  n/a (n=23)  n/a (n=0)   12/2            0/0          
GuardMelee   3    Troll            off   5     65%    72%     100%    0%  30s     reads 0            rolls 0     dodges 0    2/2             0/0          
GuardMelee   3    Troll            on    5     54%    50%     100%    0%  34s     62% vs 63% (n=56)  n/a (n=23)  n/a (n=1)   14/9            0/0          
GuardRanged  4    Draugr_Ranged**  off   13    23%    0%      100%    0%  14s     reads 0            rolls 0     dodges 0    0/0             0/0          
GuardRanged  4    Draugr_Ranged**  on    13    13%    0%      100%    0%  21s     n/a (n=19)         n/a (n=0)   n/a (n=1)   0/0             1/1          
Woodcutter   5    Troll            off   5     25%    24%     100%    0%  38s     reads 0            rolls 0     dodges 0    0/0             0/0          
Woodcutter   5    Troll            on    5     27%    26%     100%    0%  41s     79% vs 76% (n=73)  n/a (n=0)   n/a (n=0)   0/0             0/0
```

### Regression run before release (2026-10-08, single player, 38 rows through ModTestBridge)
29 pass outright. Rerunning the failures with `BlockAndDodge` off and on gives identical results, so none come from
blocking and dodging:
- WORK-3, WORK-7, CHORE-7, PASS-1: flaky, pass on the rerun (both modes).
- WORK-5: needs AzuAutoStore disabled (the dev profile has it on); HIRE-1: `hire1_a`/`hire1_b` are the two halves of a
  relog test and were chained back to back (cleanup killed the hireling in between). Not bugs.
- NAVLINK-5 (stepladder) and NAVLINK-6 (stair removed) failed in both modes, then both passed after a game restart.
  Not a blocking/dodging regression. Followed up the same day (bf15356, b313640): the stair scan's samples depended on
  which way the piece was turned, a single wood_stair is only 1.05 m up (the house now stacks two), and the upstairs
  chest was "in reach" through the floor from the ground (0.5.0's reach rule), which also hid that NAVLINK-4/5 never
  climbed to it. See below.

### Stair fixture and reach (2026-10-08, single player, ModTestBridge)
- `vfh_fixture stair_probe wood_stair` / `wood_stepladder` (12 turns, 0-165 degrees): before bf15356 the stepladder was no
  stair at 15/75/105/165 degrees (`gaps`) and read rises of 0.76-0.96 m for the wood stair by turn; after, every turn reads
  the same (wood_stair top step 1.05 m, footprint 2.0 x 2.0 m; stepladder top plank 2.05 m, 1.0 x 2.2 m).
- `fixture.house` the same on every run: wood_stair 2 flights x 1.12 m, storey 2.17 m; stepladder 1 flight, 2.13 m;
  both `climbs=back`, top step 0.1 m inside the upper floor.
- With b313640: `vfh_test_chain nav1 nav2 nav3 nav1 nav2 nav3 nav1 nav2 nav3 nav5 nav6` all 11 pass, each delivery a
  fresh count (NAVLINK-5 routes `walk>door>walk>stair>walk` and deposits upstairs; NAVLINK-6 gives up on the upstairs
  chest and fills chest B), plus the pair 2x more on the build just before the count reset; `navscan1 navscan2 navscan3`
  pass. Before b313640, NAVLINK-4/5 had passed by filling the upstairs chest from the ground: they never climbed.
- Reach regression on b313640: `deliver1 deliver2 door1 work3 chore_toggle mills fires beehive sap fermenter shield tidy
  repairs repairs2 board_food cook_spit cook_oven cook_chain farm_harvest keep1 azu3` all pass, no `lvl=E` (repairs
  first failed when the Greyling killed Testboy, which left its board and broke the next rows' setup; passed with
  `vfh_fixture ghost on`).

### To test (batch)
- [x] Macro (single player): `vfh_test_chain nav1 nav2 nav3 nav5 nav6 navscan1 navscan2 navscan3`, the pair run 3+ times (2026-10-08, see above).
- [x] Macro (single player), reach regression (2026-10-08, see above): `vfh_test_chain deliver1 deliver2 door1 work3 chore_toggle mills fires beehive sap fermenter shield tidy repairs repairs2 board_food cook_spit cook_oven cook_chain farm_harvest keep1 azu3`.
- [x] Macro (single player): `vfh_test_chain protect stance1 stance2 stance3 stance4 stance5 tame1 retreat1 noise1 post1 repairs read1 block1 dodge1 protect` all 15 pass (2026-10-08); `test.protect on=true` once at the start and `on=false` once at the end, Testboy never died; VFH-PROTECT-1: a hit worth half your health is dropped (`tester_hits_blocked`), the same hit outside a test takes 25 to 12.5.
- [ ] By hand: a chest on an upper floor: a woodcutter delivers only after climbing the stairs (not from below, not through the wall); remove the stairs and it fills a chest downstairs instead, no hang. A Steward still fuels a sconce or torch up on a wall and loads a smelter from the open side.
- [x] Macro (single player): `vfh_test_chain tree_patch` (passed 2026-10-06).
- [x] Macro (single player): `vfh_test_chain pass` (10 runs, 5–9 s, no jams, 2026-10-07).
- [x] Sign stays standing (Tim, 2026-10-06).
- [x] By hand: build a Tree patch sign only inside a board's area (outside it the ghost is red and placing says so). Tested by Tim 2026-10-07.
- [x] Macro (single player): `vfh_test_chain read1 read2 block1 block2 block3 proj1 proj2 dodge1 dodge2 dodge3 dodge4` plus the combat regression rows (stance1-5 combat1 combat5 combat6 tame1 retreat1 noise1 post1): all passing, 2026-10-07/08 (flaky rows reworked to be deterministic).
- [ ] By hand: a level 5+ guard against a Greydwarf Brute: the shield comes up just before each swing lands, sometimes "Parry!" and the Brute staggers, and the guard swings back straight after.
- [ ] By hand: a guard posted in front of Draugr archers turns into the arrows and blocks most of them; arrows at someone else don't make it react.
- [ ] By hand: a worker or archer near a troll rolls (sideways or back) out of its big swings; never off a ledge or into water; rolls are spaced out at low levels.
- [ ] By hand (dedicated server): a hireling owned by one player dodges a troll owned by another: the hit misses and both players see the roll.
- [x] (Tim, 2026-10-08) By hand: on a Tree patch sign, Shift+E changes radius and kind; looking at it draws the circle; a woodcutter plants it from seeds in a chest and later fells the grown trees.

## 0.5.0 (released 2026-10-06): Farmer, Cook and production orders

Plan: `plan/farmer-cook/`. Includes the held 0.4.4 fixes (first four rows).

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 3e51435 | Field looting has its own radius, `LootRadius` (Followers, 30 m), instead of the gatherers' `GatherNearbyRadius` (15 m) | the owner's game |
| d600744 | Deliveries walk to a standing spot within reach of the chest instead of the chest itself (chests tucked under a raised floor) | the game simulating the hireling |
| 1a6bc10 | Pause when full only when the chests holding an item have no room at all; repairs leave pieces in water above half health and skip pieces that keep wearing for an hour | the game simulating the hireling |
| e9c22bd | Farmer and Cook jobs (board level 2, one of each per board), `cropLevels` / `stationLevels` / `recipeLevels` in the data file | everywhere (data, rules) |
| b3d19f0 | Production orders and the farm and kitchen planners (seed cycle, seed reserve, what the Cook may use, dough-then-bread chaining) | everywhere (pure logic, unit-tested) |
| 0b1c6ff | The Steward's chore loop shared by Steward, Farmer and Cook; server toggles `FarmerHarvest`, `FarmerPlant`, `CookStoves`, `CookCraft` | the game simulating the hireling |
| 34348fc | Crops (cultivator saplings, PlantEverything's and wild regrowing plants) and kitchen recipes read from the game; `vfh_crops`, `vfh_recipes` | everywhere |
| 96b6223 | Orders tab on the board (add, target, reorder, pause, remove), saved on the board through its owner | your game; the board's owner applies |
| 4215729 | Farmer with a cultivator: harvests ripe crops (always) and bushes for short orders, area harvest | the game simulating the Farmer |
| cdba9c6 | Farmer plants in rows on free cultivated ground for the orders (seed orders first, seeds below the reserve never planted for produce) | the game simulating the Farmer |
| ce0b397 | Cook with a ladle: spits and the oven (takes food off before it burns, fuels the oven, stays near while food cooks) | the game simulating the Cook |
| 1a8d9f2 | Cook crafts at the cauldron, prep table and mead ketill (station level, fire, roof as a player), carries dough straight to the oven | the game simulating the Cook |
| 448994e | Review fixes before the first run: jobs that can't be done are set aside, unreachable planting spots avoided for 10 min, fetched seeds/ingredients/board food never delivered back mid-job, food cooking and items carried count as stock, only usable stations (fire, roof, free slot, level) planned, crops not planted where too hot or cold, no duplicated items on full cargo, no raw food lost on a full stove | as above |

### To test (batch)
- [x] Macros (single player, with PlantEverything and PlantEasily): `vfh_test_chain catalog orders farm_harvest farm_bush farm_seeds farm_rows cook_spit cook_oven cook_cauldron cook_chain cook_protect` (all passed 2026-10-06, after fixes).
- [x] Macros (single player), Steward regression after the shared loop (all passed 2026-10-06): `vfh_test_chain chore_gate chore_toggle fires beehive animals repairs repairs2 fermenter shield tidy board_food loot pause1 pause2`, then `deliver1 deliver2 nav1 nav5 keep1 azu3`.
- [x] Local dedicated server, from a client: `vfh_test_chain orders farm_harvest cook_spit` (passed 2026-10-06, no server errors).
- [ ] By hand: hire a Farmer (holds a cultivator) and a Cook (holds a ladle); a second Farmer is refused; the Orders tab adds, reorders, pauses and removes orders; `vfh_crops` / `vfh_recipes` list what you'd expect (with PlantEverything: raspberry bushes etc. as regrowing). — cultivator, `vfh_crops`, `vfh_recipes` checked OK (2026-10-06); board and Orders tab still to look at.
- [ ] By hand (live mod set copy): a Farmer on a real field with a seed order and a produce order; PlantEasily rows match yours; a Cook on spits and a cauldron for half an hour; no `lvl=E`.
- [ ] Live server (carried over from 0.4.4): Brand delivers to the chests under the raised house without `deliver.chest_unreachable`; a Steward in Gather Here collects loot up to 30 m out.

## 0.4.3 (released 2026-10-06)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 613aeea | Steward chore "Board food" (level 1, toggle in Shift+E, server `StewardBoard`): below `StewardBoardRefillDays` (3) days of upkeep it stocks its own board from the chests up to `StewardBoardFillDays` (7), cheapest food first, only food the board accepts (honours `AllowRawFood`), never the last of a food | the game simulating the Steward |
| 5b5dedb | A Steward follower in Gather Here picks up loose loot within the gather radius of its spot (nearest first, until cargo is full; not a player's fresh drops, the board's pile or warded items); status "Picking up loot" | the owner's game |
| 80bf330 | Field looting reaches like a player (2.5 m), gives up on an item after 12 s and logs it (`loot.unreachable`) | the owner's game |
| fde0b02 | `storage.full` log (once per 5 min per item): when work pauses for a full item, which chests holding it were counted and the room in each (Brand paused the kiln with a coal chest showing 10 free slots) | the game simulating the hireling |
| e76a316 | The pause counts room in every chest in the area, including chests skipped for a minute after a failed fetch (a skip only stops taking from them); `steward.tidy_left` names the items left because their chests are full | the game simulating the Steward |
| fc4ddcb | The pause also covers fermenters (a ready mead stays in until its chests have room) and emptying stations that hold their output (spinning wheel, eitr refinery) | the game simulating the Steward |

### To test (batch)
- [x] Macros (single player): `vfh_test_chain board_food loot` (both passed 2026-10-06).
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
