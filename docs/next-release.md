# Next release: what's fixed and what to test

A running tab between releases. Each fix lands here as it's made; the batch test after a play session works through
the "To test" list, then the results go into `docs/test-checklist.md` and this file starts over for the next version.

## 0.12.0 (packaged 2026-10-15, branch `rancher-big-pen`): post any worker, order search, bigger pens, fleeing followers keep up (new rows pass; regression not run)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (rancher-big-pen branch) | A boar by the wall of Tim's 20 m stone-walled yard showed as "out of its pen": a pen was found by building pieces within 15 m in all 8 directions, and the far wall and corners were further than that. Now 32 m | the game simulating the Rancher |
| (same branch) | A search box at the top of every worker's order picker (Tim couldn't find "Barley wine base: Fire resistance" among the Cook's ~230 recipes, paged by level): matches any part of the name as shown, or the prefab, any case; cleared when the picker opens or an order is added. Panel fixture `search <text>` | the player's game (UI) |
| (same branch) | Every worker can be posted at home (Tim: the Rancher by the pen, the Cook in the kitchen, instead of all bunching at the board): the stone posts any follower at home (it put workers other than guards on hold as followers: Ingrid stayed on the stone). A posted worker still works, its area centred on the post (same radius; `Hireling.WorkCentre`), and waits there between jobs ("Waiting at their post"); the board's order stock also counts chests round posted workers' posts; the Steward counts the board's pile from the board; posted workers' combat leash is their work radius (guards keep PostLeashRadius). New check `herd_adults` | the game simulating the worker; the board owner (stock) |
| (same branch) | Sigrid (miner follower, Flee stance) fled a skeleton across the fields while Tim ran on and was lost: the catch-up (sprint boost, unstick, teleport behind the owner out of sight) ran only while following, and fleeing outranks following, so she ran out of his loaded area. A fleeing follower now gets the same catch-up | the owner's game |

### To test (batch)
- [x] Row VFH-RANCH-15: a boar moved 8 m from the middle of a 20 m pen isn't reported out (2026-10-15).
- [ ] Regression: VFH-RANCH-14 (a boar 9 m outside a 6 m pen still is), RANCH-11, 12, 13, DOOR-1.
- [x] Screenshot: the Cook's picker with `vfh_fixture panel last search barley` (and `fire`) lists only Barley Wine Base: Fire Resistance (2026-10-15).
- [ ] By hand: typing in the box doesn't move the player or close the panel.
- [x] Rows VFH-POST-W1 (a 12 m Rancher counts a pen's boars only once posted towards it) and VFH-POST-W2 (a posted Cook cooks) pass (2026-10-15).
- [ ] Regression: POST-1 and the other post rows, gatherers (WORK), Steward chores, farm, kitchen, ranch rows.
- [ ] By hand on the server: post Ingrid by the pen and Inga in the kitchen; both work and wait at their posts.
- [x] Row VFH-CATCHUP-2 (a Flee-stance miner follower 50 m behind, fleeing greydwarfs, is teleported to you) passes (2026-10-15).
- [ ] Regression: CATCHUP-1, RETREAT-1, LAG-1.
- [ ] By hand on the server: Ingrid's Problems list no longer says a boar is out of the stone-walled yard.

## 0.11.1 (packaged 2026-10-15, branch `guard-out-of-reach`): guards give up on what they can't reach, fight back at once

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (guard-out-of-reach branch) | Una (melee guard) stood under a blob on a swamp ruin while mobs chased Tim. Three fixes: a melee guard that hasn't got any closer to its target for 5 s, and it's above or below its reach or there's no route, leaves it alone for 20 s ("out of reach"); a target given up on (blocked, quiet) is also left out of the threat scan, so the next nearest enemy is fought (before, it stayed "nearest", was refused, and the guard fought nothing); something it can't fight from where it is (out of reach, in the water, no effect, no shot) that hits it doesn't pull it back unless it comes right up | the game simulating the guard |
| (same branch) | Una took ~20 s to fight a Fenring mage: it kept away for 10 s, she let it go ("quiet"), then it hit her four times in the 10 s she was ignoring it. Something she only lost interest in (quiet, blocked) is fought again as soon as it hits her | the game simulating the guard |

### To test (batch)
- [x] Row VFH-COMBAT-LEDGE (enemies fixture `ledge:H`, the floor set back so the blob is seen from below): an Aggressive guard engages a blob pinned 4 m up, drops it "out of reach" after ~6 s and kills a still greydwarf 10 m off (2026-10-14).
- [x] Combat regression, every row that spawns enemies (75: stance, combat, read, block, proj, dodge, defm, tame, retreat, noise, post, repairs, noeffect, wizard 3-18, all ENH-G/A/W and the worker combat picks) pass (2026-10-14). VFH-WIZ-11 first failed: the out-of-reach "unless it comes right up" exception let a wizard take up a greydwarf it had dropped for no effect; now only for out of reach. BLOCK-1 and DODGE-3 (known flaky counts) and COMBAT-10 passed on rerun; VFH-WATER-1 failed twice on the harness's coast teleport (once stayed at the test spot; once a leftover hireling from the first try), not combat.
- [ ] By hand in the swamp: a guard with a blob up on a ruin turns to the draugr instead.
- [ ] By hand in the mountains: a guard answers a Fenring mage (or anything) that hits her straight away, even just after giving up chasing it.

## 0.11.0 (packaged 2026-10-14, branch `steward-orders`): the Steward works to orders, ∞ orders, miners stay shallow

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (miner-dig-depth branch) | A miner dug down after a silver vein, 15 m deep (Sigrid; HearthBelow lets terrain go below vanilla's 8 m): it digs a buried chunk out when its top is within 1.5 m of the ground, and each dig lowered "the ground", bringing the next chunk within reach. Now also within 1.5 m (2.5 with Deep digger) of the ground as it was when miners first came to that deposit (kept on the deposit). Chunks already in the open (a vein you dug out) are mined as before | the game simulating the miner |
| 5bf874c | The Steward works to orders like the other workers: Shift+E, **Orders** (the Limits page is gone). No order for a product: it doesn't make it. A new Steward's board gets one "Everything else: ∞" order (new board op `StewardStart`, once per board, flag `vfh_steward_orders`), so it carries on making everything; its own order for an item overrides it. Every order can be ∞ (the ∞ button; keeps making it, never "met"); a Steward's new order starts at ∞. Old Limits are kept: they were already Station orders on the board | the board owner (op), the game simulating the Steward |
| (this commit) | The Shift+E panel's Close button stays in the top right corner on its wider pages (Orders, Enhancements, Problems): it sat inside the panel, where the narrow page's corner is | the player's game (UI) |

### To test (batch)
- [x] Row VFH-MINE-DEEP (fixtures `crack`, check `dug_depth`): a buried copper deposit is dug out (copper in cargo) and the ground goes down at most ~1.8 m (2026-10-14). It and VFH-WORK-3 are sensitive to the test spot's terrain after many runs (failed once each on a worn-out patch).
- [x] Rows VFH-SORD-1 (no order: the smelter isn't fed; ∞: it is) and VFH-SORD-2 (new Steward gets Everything else: ∞ and smelts; its own Coal order at 0 pauses kilns), plus limit1, limit2, kilns, mills, fermenter, beehive, sap, chore_urgency all pass (2026-10-14). 284 unit tests.
- [ ] By hand: an existing world with Limits set: they show on the Steward's Orders page and it keeps to them; the board gets Everything else: ∞ (the Steward then makes the rest too: delete it to make only what's ordered).
- [ ] By hand: the ∞ button on a Farmer, Cook and Rancher order; a Rancher's ∞ breeding pool keeps every animal.
- [ ] By hand on the server: a miner at a silver vein that's been broken into digs a little and stops; nothing deeper than ~2 m.

## 0.10.1 (packaged 2026-10-14, branch `worker-orders`): orders on each worker, gatherers' Problems

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| e8deca9 | Orders move to the workers: Shift+E on the Farmer, Cook or Rancher has **Orders (n)** beside **Problems** (as the Steward has Limits), a wider page with each order's have / target, - / +, up/down, pause and remove, and *Add an order* (a picker of what it can be ordered, with the level each needs). Still kept on the board (no format change; a new worker takes them over). The board's Orders tab is now a read-only overview, each worker's orders under its name ("Cook: none hired" when there's none) | the panel on the player's game; the edits are board ops as before |
| e8deca9 | Woodcutters and miners get a **Problems** button: from their last look round, what they left alone and why ("2 left alone: they give Wood, which is switched off in its panel"; chests full; another hireling on it; tool too weak; too close to buildings; nothing within its radius). Tim's case: a woodcutter in Gather Here with only fine wood on, by trees that give Wood | the game simulating the gatherer |

### To test (batch)
- [x] Regression `why1 why2 why3 why4 orders order1 order2 farm_harvest farm_seeds cook_cauldron cook_chain enh4b ranch5 limit1 limit2 work1` passes. New rows VFH-WHY-3 (woodcutter with Wood off: the problem shows; switched back on, it clears) and VFH-WHY-4 (a miner with nothing to mine); screenshots of the Cook's Orders page and picker, the board's overview, the Woodcutter's panel and Problems page (2026-10-14).
- [ ] By hand: add, reorder, pause and remove orders from a Farmer's and a Rancher's panel; the board's overview follows.

## 0.10.0 (packaged 2026-10-14, branch `enhancements`): hireling enhancements

The plan is `plan/enhancements/` (12 phases). Each phase lands as its own commit with its rows; results below.

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 1ebf18e | Workers wear a hat for their job instead of the helmet: woodcutter a red cap (`HelmetHat5`), miner the dverger circlet (`HelmetDverger`, a lamp on the forehead), Steward a yellow headscarf (`HelmetHat6`), farmer a straw hat, rancher a brown fur cap (`HelmetHat3`), cook a chef's hat of our own (`VFH_ChefHat`: a white toque built from shapes, seated where vanilla caps sit on the head). Guards, archers and wizards keep their helmets. Looks only: a hat counts as the level's shared helmet for armour. A new `hat` per job in the data file (filled in for old files; an unknown item is dropped with a warning; fighting jobs' hats are ignored) | every client (gear is default items, shown through VisEquipment) |
| 2d96619 | Phase 01: the catalog of 126 enhancements (7 pairs per job), picks, owed/choose/respec rules, data section `enhancements` + `enhancementSettings.respecCostFraction` with defaults | data file (server-synced) |
| fabbf89 | Phases 02-03: picks saved on the contract (roster format 4) and copied to the hireling; choose and respec ops; `Enhancements` switch; the Enhancements page in the Shift+E panel (confirm step), owed counts on hover and in the Roster tab, Respec at the board | the board owner's game runs the ops; the panel on the player's |
| (phase 05) | Guard behaviours: Shield training (block power x1.15), Shield bash (stagger + 10 blunt every 12 s), Second wind (heal 25% below 30%, once a fight), Bodyguard (an enemy hitting someone within 6 m is made to go for the guard), Taunt (enemies within 8 m go for it for 5 s, with a shout), Bloodthirst (heals 10% of damage dealt), Cleave (a landed swing hits 2 more in front for half), Last stand (a killing blow leaves 1 health and 3 s untouchable, every 10 min), Rallying cry (hirelings within 15 m deal x1.10 while it fights). Taunt and Bodyguard only steer enemies this game owns (in multiplayer: usually the nearest player's) | the game simulating the guard; the damage hooks where the hit lands |
| (phase 06) | Archer behaviours: Eagle eye (+5 m sight and range), Fire arrows (+10 fire, burning 4 s), Frost arrows (+5 frost, slowed by 30% at first, 3 s), Hunter (x1.25 against animals: anything tameable, the animal faction or passive wildlife; a following archer picks up what it killed within 20 m), Volley (3 arrows every 15 s, ammo used once), Headshot (10% chance of double, "Headshot!"), Marksman (x1.20 beyond 15 m), Skirmisher (keeps its bow up close, backs off from 10 m for up to 6 s), Rain of arrows (8 arrows over the target's area every 30 s at half damage) | the game simulating the archer; the hit's effects where it lands |
| (phase 07) | Wizard behaviours: Blink (an enemy within 4 m: 8 m away in an instant, every 10 s), Secondary spell (the staff's second attack, or a frost nova, every 20 s), Mender (heals the most hurt player or hireling within 15 m by 10%, every 15 s), Elemental mastery (elemental damage x1.30 and its burning/chill/shock/poison lasts 1.3x), Protection (the Staff of Protection's bubble on its owner when following, else itself in a fight, every 60 s), Entangling roots (the Staff of the Wild's roots on its target every 30 s), Long reach (+4 m casting range), Chain spell (each spell also strikes the nearest other enemy within 6 m for half), Raise dead (a friendly skeleton for 60 s, every 120 s in a fight) | the game simulating the wizard; the hit's effects where it lands |
| (phase 08) | Woodcutter and Miner behaviours: yields on what their own hits drop (Lumberjack +15% wood, Fine eye +10% fine/core wood and goes for those trees first, Master woodsman +15% everything; Rich veins +15% ore, Master miner +15% everything), Seed saver (a felled tree gives its seed), Gem eye (10% of chunks give the biome's find: flint, amber, crystal, obsidian, amber pearls, rubies; picked up), Forester (looks at tree patches twice as often), Clean cut (x1.3 swing on logs and stumps), Rock breaker (x1.3 on rocks with no ore), Timber! (a felled tree takes down a small tree it falls on), Quiet work (no noise), Deep digger (digs chunks out from 2.5 m deep, from 1.5), Prospector (looks 30% further for ore), Tireless (carries up to 1.25x its weight limit), Heavy hauler (ore counts half its weight). Yields only apply where the game applying the hit is the hireling's | the game simulating the gatherer |
| (phase 09) | Steward behaviours: Keeper of flames (tops fires and shield generators up from 75%), Handyman (repairs from 10 m, 10 s after the last enemy), Bonesetter (hurt hirelings within 10 m, not fighting, heal 2% a second), Quartermaster (stocks the board 3 more days), Tidy mind (tidies after 20 s, sweeps twice as far), Smelter's hand (reinterpreted, see below), Watchman (may be Defensive, x1.25 damage), Errand runner (straight on to the next chore within 10 m, up to 4 in a row), Fermenter's touch (a fermenter it loads starts 20% of the way through), Master steward (15% quicker per item, shorter pauses), Shield keeper (looks after shield generators twice its radius out) | the game simulating the Steward |
| (phase 10) | Farmer: Green thumb and Master farmer (plant 10%/15% quicker), Bountiful (25%: one more crop), Seed keeper (25%: one more seed), Double harvest (10%: the whole pick twice), Master farmer (+15% of everything), Close planting (replaces Sower: its crops need 15% less room, so a field holds about 38% more), Forager (picks wild berries and mushrooms with no order), Tender care (its crops start 15% grown), Hardy stock (its crops grow where it's a little too cold or shaded), Replanter (replants a harvested spot at once from seeds it carries). Cook: Quick hands and Head chef (craft and load 10%/15% quicker), Frugal (15% per ingredient unit not used; Head chef +5%), Double batch (20%: a second batch; Head chef +5%), Keeper of stoves (oven kept full), Two stations (may start at a second stove while one is busy), Pantry sense (takes from chests twice its radius out), Chef's touch and Banquet (reinterpreted), Spit master (loads every free stove of the same kind in one trip), Prep chef (with the order's own station not usable yet, makes the in-between ingredient ahead: dough before there's an oven) | the game simulating the Farmer/Cook |
| (phase 11) | Rancher: Gentle hand (feeds from 2 m further, over the fence), Hearty feed (food it drops feeds 50% longer), Butcher and Skinner (33%: one more meat / hide; Master rancher +10% each), Breeder (animals it fed are due 15% sooner), Star breeder (10%: their young get one more star, up to 2), Big herd (keeps 2 more than the pool), Herd guard (fights whatever just hurt something of its own in its area, x1.25), Herder (an escaped herd animal follows it until walled in again, up to 3 min), Wrangler (a wild animal of a kind the board keeps, in its area, is tamed, every 5 min), Master rancher (slaughter 15% quicker) | the game simulating the Rancher |
| (phase 04) | The shared number boosts: health, armour, damage dealt and taken, attack and cast speed, move speed, cargo slots and weight, gathering swing, work radius, Masterwork (+1 quality on the main weapon or tool). The cargo grid gains a fifth row so Pack mule adds slots at level 8 (32 was the grid) | the game simulating the hireling |

### To test (batch)
- [x] Unit tests (`JobHatTests`) pass; new row VFH-HAT-1 (farmer, cook, miner wear their hats; a guard keeps its bronze helmet) and regression `vfh_test_chain hat1 hire3 hire4 hire5 recruit1 wiz2` pass in the dev game (2026-10-14, on top of 0.10.0).
- [x] By eye (new fixture `portrait <last|posted|nearest> <front|side|back|top>`, a close-up PNG in `BepInEx/`): all six hats sit right on standing hirelings; the chef's hat sits on the brow, eyes clear. Tuned twice (it first covered the eyes and was too wide): `Seat`/`Snug` in `ChefHatItem`.
- [ ] By eye, by hand: the chef's hat while the cook works and crouches; the miner's circlet light at night; the hats in daylight.
- [ ] Armour: a level 3 farmer and a level 3 guard (same chest and legs) show the same armour in `hireling.stats`.
- [ ] Existing hirelings in a save get their hats the next time they load (a relog).
- [x] Phase 04 rows VFH-ENH-5, 5b, 6, 6b-6f, 7, 8 pass (2026-10-13); regression `combat1 combat5 stance4 stance5 block1 dodge1 noeffect work1 work3 tier1 store1 deliver1 cargo_weight` and VFH-ENH-1..4c pass (BLOCK-1 failed once on its parry count, then passed twice).
- [x] Phase 05 rows VFH-ENH-G0..G8 pass (2026-10-13); regression `combat1 combat5 combat6 read1 block1 block2 block3 dodge1 stance4 post1 retreat1 noeffect` passes.
- [x] Phase 06 rows VFH-ENH-A1..A9 and A4b pass (2026-10-13).
- [ ] By hand: fire arrows visibly burn; the volley's spread and the rain of arrows look right.
- [x] Phase 07 rows VFH-ENH-W1..W9 pass (2026-10-13).
- [ ] By hand: the blink, the bubble, the roots and the skeleton look and behave like the player's own staffs.
- [x] Phase 08 rows VFH-ENH-L1..L8 and M1..M8 pass (2026-10-13). Yield rows set the chance to 1 and check the bonus fires; M1 checks the dig depth only.
### Full regression (2026-10-14)
All 270 rows, in two parts (the tester's teleport home got stuck after VFH-NAVLINK-2; the game was restarted and the
chain resumed there). Failures, each rerun alone at the test spot:
- Expected in the dev game: VFH-CFG-1 (needs a dedicated server), VFH-WORK-5 (needs AzuAutoStore off).
- Passed alone: STN-3, PROJ-1, HOME-1, CHORE-7, LOOT-1 (known flaky), FARM-4, LIMIT-2, RANCH-12, ENH-A1, NOISE-1, GATHER-1,
  PORTAL-1.
- VFH-WORK-3: the miner mines the deposit and delivers (27 copper and the stone to their chests), but delivers part-loads
  so its cargo never holds more than 5 copper at a check; the second half of the regression (run from the wrong spot)
  had also raised the ground behind the test board, which was levelled again (`flatten 80`).
- **To watch (by hand):** twice in a long session the player's teleport (a portal, the harness's move home) never
  finished: the game waits for every saved object at the destination to be created, and one wasn't. Never in a fresh
  session; check `area_not_ready` if it happens again.

### Balance pass (phase 12, 2026-10-13)
Duels in the dev game: a level 8 hireling with no picks, all-A and all-B, against 5 one-star trolls, 10 draugr and 3 lox
(3-5 runs each); "better by" = the mean of the kill-time and damage-taken reductions against no picks. Target 20-35%.

| Build | First | Final | Changes |
|---|---|---|---|
| Guard all-A | 25% | 25% | none |
| Guard all-B | 22% | 22% | none |
| Archer all-A | 30% | 30% | none |
| Archer all-B | 47% | 22% | Quick draw 0.15→0.09, Light feet 0.10→0.06, Frost arrows slow 0.30→0.17 and damage 5→3, Hunter 0.25→0.14, Headshot 0.10→0.06, Elite 0.10→0.06 |
| Wizard all-A | 41% | 29% | Focus 0.10→0.09, Warding 0.15→0.13, nova 20→18, Protection 100→90, Archmage 0.15/0.10→0.13/0.09 |
| Wizard all-B | 61% | ~50% (32% on kill time alone) | Haste 0.15→0.05, Mender 0.10→0.07, Elemental mastery 0.30→0.12, Chain spell 0.5→0.15, Roots every 90 s (30), Raise dead 20 s every 180 s (60 s every 120), Blink every 20 s (10) |

Wizard all-B is still above the band on the plan's figure: its picks (Roots, Blink, the skeleton) keep trolls and lox
off it, so it takes almost no damage; its kill times are in the band. Measuring one pick removed at a time showed no
single culprit (each worth 2-14 points). **Accepted as is** (Tim, 2026-10-13): it's what those picks are for.
The duels are noisy (the same build measured 19% and 29% between runs): treat the figures as ±10. Worker jobs (step 3 of
the plan) weren't measured: their picks are plain rates checked by the rows.

### Reinterpreted (confirmed by Tim, 2026-10-13)
- **Smelter's hand** ("loads N times as much per trip") would do nothing: a trip already fills each station to its own capacity. Now: tops stations up whenever they aren't full, and loads twice as fast.
- **Sower** is replaced by **Close planting** (Tim, 2026-10-13): its crops need 15% less room, so the same field holds about 38% more. (At 30% less room a test field went from 32 spots to 70, more than double: too strong, so the default is 15%; `ClosePlanting.space` in the data file.)
- **Chef's touch / Banquet** ("food it makes pays 10%/25% more upkeep"): tagging food would need item tags the game merges away when stacking, or a roster format change for payments. Now: makes 10%/25% more food from the same ingredients (the same upkeep value in the end).
- **Keeper of stoves**: keeps the oven full; the fires under spits are the Steward's job and aren't included.

- [ ] By hand: a Deep digger miner digs out a deposit buried 2 m deep that a plain miner leaves; a Heavy hauler's cargo label reads half for ore.
- [ ] By hand: Shield bash visibly staggers; Taunt's shout shows over the guard; Last stand leaves a guard on a sliver of health.
- [ ] By hand: a level 8 worker with Pack mule: its cargo window shows a fifth row with 2 usable slots.
- [ ] By hand: an enhanced Guard's hover shows its picks; turning `Enhancements` off on the server puts its health and armour back.

## 0.9.3 (packaged 2026-10-13): dug-out veins, portal mods, heavy stacks

Tested in the dev game 2026-10-13: `cargo_weight work1 work3 tier1 keep1 keep2 deliver1 deliver2 store1 store2 portal1 catchup1 home1 home3 recruit1 gather1 order1 order2 loot loot_guard death5 move1 pass` all pass (VFH-LOOT-1/-2 failed once on a randomly dropped log, then passed twice each). The first version of the open-sky rule broke VFH-WORK-3 (a miner aimed at a tall rock's top and at buried chunks under its visible top): fixed and rerun. Not testable here: TargetPortal and HearthBelow (live server), the weight label's look.

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (miner-exposed branch) | Sigrid (level 4 miner, iron pickaxe) wouldn't touch a silver vein Tim had dug right out ("None of your miners can mine that"; Gather Here: "only buried or out-of-reach chunks left"). A chunk counted as above ground only by the game's ground height at its centre, and with HearthBelow a dug-out pit is its own terrain while that height stays the old surface. Now a chunk also counts as not buried when a ray straight down from the sky reaches that very chunk before anything else (still only within reach above the ground) | the game simulating the miner; the stone order on the player's |
| (miner-exposed branch) | Sigrid carried silver through a portal although the portal doesn't take ore: the server's TargetPortal stops the portal at its trigger, opens the map and moves you itself later, so no portal was teleporting and the trip counted as a dungeon door (takes anything). The portal you step into is now noted at its trigger (before any portal mod), and a teleport while you still stand at it (within 2 minutes) is that portal's: a follower with ore or metal stays behind in Stay, as you would | the game of the player travelling |
| (miner-exposed branch) | Loading a hireling's cargo yourself: a stack heavier than its whole weight limit was refused outright, so an empty hireling took nothing (Una, a stack of silver: "can't carry that much (0/375)"). Dragging, dropping or click-moving a stack now puts in as much as its weight allows ("Una took 18 of the 30 (that's all they can carry)"), and refuses only when not even one fits | the game of the player loading it |
| (miner-exposed branch) | A hireling's cargo window shows its weight against its limit ("308/375", red when full), as the player's inventory does; it showed only "308" like a chest's | the game of the player looking |

### To test (batch)
- [x] Regression `vfh_test_chain work1 work3 tier1` passes; row VFH-CARGO-W (a full stack of silver ore goes in partly) passes.
- [ ] By hand: open a follower's cargo: the weight under it reads "carried/limit" and fits its box (red at the limit).
- [ ] By hand: drag, then right-click, a full stack of silver ore onto an empty follower: as many as fit go in, the rest stays, the message says how many; a stack that fits whole still goes in whole.
- [ ] By hand (live server, TargetPortal): a follower carrying silver ore; you go through a portal and pick a destination on the map: it stays behind ("can't take … through the portal"), `follow.travel_left` in the log. Regression `vfh_test_chain portal1 catchup1 home1`.
- [ ] By hand (live server, HearthBelow): dig a silver vein out, point the stone at it: "1 miner mining"; Sigrid breaks it and the chunks under the dug ground stay alone.

## 0.9.1 (packaged 2026-10-12): the board pays its wages; the ground under roofs and in caves

Full regression run overnight 2026-10-12 in the dev game (all 176 rows, plus 3 new): everything passes except
VFH-CFG-1 (needs a dedicated server) and VFH-WORK-5 (needs AzuAutoStore off). Failures found and fixed on the way:
feed put back in its chest (RANCH-3, -11, -12, -13), feed dropped over a fence (RANCH-12), guards not watching behind
them (STN-5), loot written off after a fight (LOOT-2); test fixes for PROJ-2, ORDER-2, RANCH-11, RANCH-14, READ-1, the
pen fixture, the error count carried between rows, and a failed row stranding the tester (now `test.back_home`).

HearthBelow work (another session): HearthBelow 0.6.3 (Azumatt) was decompiled and checked: it patches `ZoneSystem.GetGroundHeight` to find a dug-out cave's
floor, and leaves our miners' terrain protection working (its `SpawnOnHitTerrain` prefix doesn't skip). A cave dug below
y 30 holds water up to y 30 (every zone has the sea's water volume, inland too), so hirelings keep out of it like any
other water; the pathing clamp at sea level needs no change. Nothing below references HearthBelow; without it,
everything behaves as before except the roof fix.

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (hearthbelow branch) | Hirelings' ground checks found the top of whatever was over a spot (the game's solid-height check casts down from a kilometre up): under a roof that's the roof, in a HearthBelow cave the hill above. Now, when there's a ceiling over the spot, it's the floor under the board's (or the work's) height. Used for a returning hireling's set-down spot, guards' patrol points, the Steward's repair spots, the reach check's floor fallback and newcomers' arrival spots. In the open, inside a hill or a rock, the answer is the game's as before | the game simulating the hireling; the board owner's (arrivals) |
| (hearthbelow branch) | A hireling coming home to a board in a hall or cave, held at a spot inside the hill (more than 4 m above the board), is set down 2 m from the board instead of on the hilltop (`follow.return_by_board`) | the game simulating the hireling |
| (hearthbelow branch) | Hirelings in a cave complained about the rain: the shelter check for the "wet" chatter now counts rock overhead | the game simulating the hireling |
| (board-wages branch) | Brand (Steward) swam against a bank below the base from day 133 to 137 (`nav.stuck inWater=true`, every chore failing), so nobody restocked the board and on day 136 all six went unpaid. A hireling stuck in the water for 30 s is now lifted onto the nearest dry ground: its route's next dry waypoint within 12 m, else a ring search (lowest floor above the water with room to stand), logged `nav.water_rescue` | the game simulating the hireling |
| (board-wages branch) | The Steward stocked the board cheapest first, and 8 slots of cheap food can't hold a big crew's wages (about 700 food a day). BoardFill: cheapest first while the remaining slots can still hold what's wanted, richer food per slot when they can't; room in stacks already on the board first. When even the richest food can't fill it to StewardBoardFillDays, Problems says how many days it holds (`vfh_board_food_short`). Unit tests | the game simulating the Steward |
| (board-wages branch) | Warnings: a missed day's wages tells the players at the base how many went unpaid, what the board needs and has, and when they quit (`funds.missed`); coming within 64 m of a board that can't pay the next day tells you, again after 10 min (`funds.cant_pay_told`); a full board that's low says richer food pays more per slot (hover and message) | the board owner's machine (missed); every game (arrival, hover) |
| (board-wages branch) | Boards not loaded here read as empty (the game keeps a chest's items as a byte array; we read a string): the server's low-food map pins always said 0 food (`funds.pin_low savedLength=0`), and hiring, promoting or refunding on a board not loaded where the roster runs would have seen no food. Now read and written as the game does | the server (pins); the roster's machine |
| (board-wages branch) | The board holds as much as an iron (reinforced) chest: 6 x 4 = 24 slots (was 4 x 2), with that chest's look. Eight slots held about 7000 food at best, ten days for a small crew but not for six level 3-4 hirelings at 700 a day once the Steward topped up from 3 days left. Boards already built get the new size when loaded; their food stays put | every game (the prefab) |
| (board-wages branch) | Feeding over a fence: the feed was dropped 0.8 m in front of the animal, so a boar at a pen's far fence, facing it, got its berry on the far side and refused it (VFH-RANCH-12). Now it goes in front, behind or to a side, whichever has no wall between | the game simulating the hireling |
| (board-wages branch) | Feeding animals (Rancher, Steward): the feed taken from a chest could be put straight back by the delivery rule, over and over, so the animal stayed hungry (VFH-RANCH-3). The feeding trip now counts as fetching supplies, as the Steward's board run and the Cook's do | the game simulating the hireling |
| (board-wages branch) | Field loot (Steward and guards in Gather Here): a fight that pulled the hireling away used up its 12 s walk to an item, so on coming back it wrote off the items at its feet for good (VFH-LOOT-2: a wild Wizardry sheep charged the archer, three pieces of wood left). Now only time spent walking to the item counts, and an item given up on is tried again after 2 minutes | the game simulating the hireling |
| (board-wages branch) | Thorvald ignored a bear (Monstrum grizzly) that his owner shot dead in 6 s, while the miner beside him ran from it: followers and roaming guards only noticed what was in their view cone. Now guards watch all round to their full sight (30 m, as posted guards already did) and other hirelings notice anything within 20 m in line of sight whichever way they face (VFH-STN-5 found an Aggressive archer standing 50 s with a neck 25 m behind it), and guards (Defensive, Aggressive) join in on an enemy a player hit within 30 m in the last 5 s | the game simulating the hireling |
| (board-wages branch) | Hirelings on the map (Command Stone in hand) are small coloured dots with a dark rim instead of the hammer and house pins, which piled up into a heap round a base: workers blue, guards red, your followers gold; names as before | every game (the map) |
| (board-wages branch) | Archers in crypts (Thorvald went missing in a burial chamber): at a wall corner every shot at a skeleton read as blocked (123 times, the wall 0-0.5 m off), because the arrow's start was worked out from the way he was facing, not the way he'd turn to shoot; walking in for a clear shot got stuck and nothing gave up, so he never followed his owner out. Now the shot is checked from facing the target; a free archer or wizard with no clear shot for 10 s, or stuck 5 s getting closer, leaves that target alone for 20 s (`combat.ignored why="no clear shot"`); archers back off for at most 3 s (into a corridor wall, forever, before) | the game simulating the hireling |
| (board-wages branch) | Guards patrolled to spots in the water (Thorvald swam below the base, like Brand): a patrol spot is now on dry ground or a floor (at least 0.3 m above the water) and reachable, else the board's spot | the game simulating the hireling |

### To test (batch)
- [ ] Macro (single player, regression; not yet run, the dev game was in use): `vfh_test_chain home1 home3 post1 con1 repairs repairs2 door1 chat1`.
- [ ] By hand: a guard posted to a board inside a roofed hall patrols on the floor, never heads for the roof (debug log, AI: `patrol.point` y is the floor's).
- [ ] By hand: a follower sent home to a board in a roofed hall arrives standing inside, not on the roof.
- [ ] By hand: a board by the sea: hirelings still keep out of the water (idle, park, arrivals).
- [ ] Optional (HearthBelow added to a test profile): a board in a dug-out cave above sea level: guards patrol the cave, a follower sent home turns up in the cave, the Steward repairs in it, no rain chatter inside.
- [x] Rows VFH-BOARD-SLOTS, VFH-CHORE-16, VFH-WATER-1 pass; unit tests pass (BoardFill). By-hand items below still open.
- [ ] By hand: walk a hireling into deep water by a steep bank (or `vfh_fixture` it there): within about 40 s it's on dry ground, `nav.water_rescue` in the log.
- [ ] By hand: a board with 8 slots of cheap food and a level 4-5 crew: the hover says the board is full and to use richer food; coming home says it can't pay tomorrow's wages.
- [ ] By hand: chests with cheap and rich cooked food, a big crew, the board empty: the Steward brings a mix (`steward.board_fill enough=true`), cheap food first.
- [ ] By hand (live server): the builder's map pin shows the board's real food days when far away.
- [ ] By hand: an archer follower in a crypt with skeletons round corners: it shoots when it has a line, walks round for one, and after 10 s without one drops the target and follows you (`combat.ignored why="no clear shot"`). Regression `vfh_test_chain combat1 stance4 stance5 noeffect` and `vfh_t_wiz_all`.
- [ ] By hand: a guard patrolling a board by the water never walks into it.
- [ ] By hand: an archer follower facing away while you shoot a creature behind it: it turns and joins in; a boar walking up behind it within 20 m is engaged (Aggressive). Regression `vfh_test_chain combat1 stance4 stance5`; workers on Flee may run a little more often.
- [ ] By hand: Command Stone in hand, open the map: dots in three colours, readable at a busy base, on the big map and the minimap; they follow a moving follower.
- [ ] By hand: an existing board opens with 24 slots and its food where it was; a new board too. Regression `vfh_test_chain con2 deliver2 store1 hire4` (payments).

## 0.9.0 (packaged 2026-10-11): the Rancher (new rows all pass; regression partly run: storage, orders, Steward, farm, kitchen spit and cauldron pass; combat, wizards, navigation not rerun)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (rancher branch) | The Rancher (plan/rancher): ranch orders (meat, eggs, breeding pools ≥ 2) on the Orders tab; RanchPlanner (slaughter the lowest-star adult above its kind's pool while its meat is short; never young, named or unpooled; eggs while short; reasons); AnimalCatalog/HerdScan from the game (modded animals too); feeding (the Steward and PetPantry step aside); SlaughterChore (one killing blow, drops collected and delivered; only a Rancher's hits land on tames); EggsChore (the Steward's tidy leaves eggs on a ranch board); Problems list. Rows VFH-RANCH-1 to -11 | the game simulating the Rancher; orders on the board owner's |
| (rancher branch) | Pen safety (plan/rancher phase 07, Tim: boars can't be led back): over the fence first (a piece below the eye doesn't block reaching a creature; spots further out are tried); any hireling waits while a tame is within 3 m of a door (20 s at most) and shuts a door at once (1.5 m past) with tames about; a herd animal seen penned and now out is reported (Problems). Rows VFH-RANCH-12, -13, -14 | the game simulating the hireling |
| (rancher branch) | Combat balance: the default level table's guardDamageMult was 1.0-5.2 on top of gear that already rises with level (Thorvald, level 4 archer: 160-185 per arrow, about 2.2x a player with the same bow; the Elder in 44 s). Now 1.0, 1.05, 1.1, 1.15, 1.2, 1.25, 1.3, 1.3. Existing data files keep theirs (CHANGELOG says what to set) | the game hitting |
| (rancher branch) | Chest modes (Tim: a pen's feed chest the Farmer fills and the Cook mustn't empty): Shift+E on a chest near a board cycles Normal / Put only / Take only / Off (ZDO `vfh_chest_mode`, ward-checked, hover shows it, `ChestModeOnShiftUse` to turn the key off). Taking uses only Normal/Take-only chests, storing and room only Normal/Put-only, stock counts (orders, limits, panels) only Normal/Take-only. Rows VFH-CHEST-1, -2 | every game (taking, storing); the player setting it |
| (rancher branch) | Doors marked for hirelings to leave shut (Tim: hirelings walked through the pen, gates on both sides, and the animals got out): Shift+E on a door near a board toggles ZDO `vfh_door_avoid`; DoorRules.Usable refuses it, detours skip it even open, link routes treat it as blocked, the route scan leaves it out. Row VFH-DOOR-1 | the game simulating the hireling; the player marking it |
| (rancher branch) | Torches out of reach (Tim places them high for looks): two stuck trips and a fire is left alone for the session (`steward.fire_out_of_reach`), listed in Problems (`vfh_fire_out_of_reach`); Shift+E on a fire near a board marks it for workers to leave alone (ZDO `vfh_fire_avoid`). Row VFH-CHORE-13 | the game simulating the Steward; the player marking it |
| (rancher branch) | Seed order deadlock (Grima, 7 onion seeds, OnionSeeds order 100, no onions): a seed order that can't plant anything (no produce to plant, nothing growing) no longer reserves its seeds from the crop order. Unit test | the game simulating the Farmer |
| (rancher branch) | Gather Here for guards (melee, archers, wizards) now picks up loot round their spot between fights, like the Steward (FieldLoot, `LootRadius`); delivered when released or sent home. Row VFH-LOOT-2 | the game simulating the hireling |
| (rancher branch) | Board upgrade costs: only the boss trophy is a boss item at every level (level 5 DragonTear 5 → FreezeGland 10; level 2 HardAntler 3 dropped, DeerHide 20 → 30). Existing data files keep theirs (CHANGELOG says what to change) | the board owner's machine (upgrade) |

### To test (batch)
- [ ] Macro (single player): `vfh_t_ranch_all` (VFH-RANCH-1 to -14 with -3b, -7b); regression `vfh_test_chain animals tidy orders farm_seeds cook_spit cook_cauldron combat1 stance4 noeffect store1 hire4 why1 why2`.
- [ ] By hand: the Orders tab's ranch picker and rows, the Rancher's Problems view.
- [ ] By hand (live server, after setting the new guardDamageMult in the server's data file): Thorvald's arrows land about 80-100 on Black Forest monsters; the balance log's fights show it.
- [ ] Regression: the combat and wizard rows still pass with the gentler multiplier (`vfh_t_wiz_all`, `vfh_test_chain combat1 stance4 stance5 block2 dodge1 noeffect`).
- [ ] Macro: `vfh_test_chain loot loot_guard` (Steward and archer Gather Here pick up loot).
- [ ] Macro: `vfh_test_chain chest_putonly chest_put door_mark fire_mark fires farm_seeds`. By hand: Shift+E on a pen gate, hover shows "leave this door shut", hirelings walk round the pen. By hand: Shift+E on a chest cycles the four modes, the hover text shows it, a chest outside your ward can't be changed.
- [ ] By hand (live server): a Rancher works a real herd for a session (orders, slaughter, a named pet spared, Problems readable).

## 0.8.4 (not released): stuck under an overhang

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (hotfix-084 branch) | Grima stuck on a lower terrace under a stone floor's overhang, running into the wall: with no walkable map there, the game put him on the floor above, so "is there a direct route?" said yes and the base's stairs (link routes) weren't used. A route that starts more than 1 m off our height now doesn't count, so he takes the stairs | the game simulating the hireling |
| (hotfix-084 branch) | Seed order deadlock (Grima: 7 onion seeds, OnionSeeds order 100, no onions; neither order moved): a seed order that can't plant anything (no produce to plant, nothing growing) no longer reserves its seeds from the crop order, so the seeds are planted and the onions then feed the seed order. Unit test | the game simulating the Farmer |

### To test (batch)
- [ ] By hand: Grima's terrace: he walks back up the stone stairs instead of into the wall (`navlinks.hop` up in the log).
- [ ] Regression: `vfh_test_chain door1 nav1 nav2 nav3 nav4 nav5 nav6 navscan1 navscan2 navscan3 pass` still pass.

## 0.8.3 (packaged 2026-10-11, in-game tests deferred): arrows go where the archer aims, orders count the board's pile, reach fixes, stuck torches

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (arrow-aim branch) | Archers' arrows (and wizards' spells) could fly off up to 90 degrees to the side (Thorvald, 2026-10-11): vanilla turns every projectile towards the AI's own target creature, which for a hireling was whatever last hit it, not what it was shooting at. Hirelings now report no vanilla target, so shots follow their aim | the game simulating the hireling |
| (arrow-aim branch) | A Cook made mead base without end (Inga, 5 ordered): the first of a new item goes to the board's pile (a chest only takes what it already holds), and the order counted only the chests, so it stayed short. What's in the board's pile now counts toward farm and kitchen orders and the Steward's limits (never as an ingredient), and the Problems list says it's waiting at the board: put one in a chest | the game simulating the hireling |
| (arrow-aim branch) | Grima piled carrots at the board: the carrot drawer (MissingPieces, stacked on three others) was "unreachable". From the floor, the reach check's line to its bottom edge grazed the drawer below; so it looked for a higher spot, where 0.8.2's floor rule refused it. Now reaching is also checked as a player aims (at a point just inside it, it's the first thing hit), spots on the hireling's own floor are tried too, and standing above something needs it in sight from the eye (over a floor's edge yes, straight down through a floor no). Row VFH-REACH-1 | the game simulating the hireling |
| (arrow-aim branch) | A Steward stuck walking to one torch never set it aside (Brand: 38 tries in a row, fermenters never emptied, fuelling outranks them): a fuelling trip visits every low fire, and a stuck walk counted a failure against the trip's first fire, not the one it was stuck on. Now that fire is skipped for 5 minutes at once, and `steward.fire_stuck` says which fire and where | the game simulating the hireling |

### To test (batch)
- [ ] Macro (single player): `vfh_test_chain combat1 stance4 stance5 block2 dodge1 noeffect post1 retreat1 catchup1` and `vfh_t_wiz_all` (archer and wizard rows, all shots now through the look direction).
- [ ] By hand: an archer hit by one creature, then shooting another off to the side: the arrows go to the one it's facing.
- [ ] Macro: `vfh_test_chain reach_stack cook_floor` (stacked chest reachable; no cooking through a floor).
- [ ] By hand: Tim's stacked carrot drawer: the Farmer stores into it again.
- [ ] By hand: Brand empties the fermenters again; the log's `steward.fire_stuck` names the torch he couldn't reach (then we can see why).
- [ ] By hand: a kitchen order for something no chest holds yet (mead base): the Cook makes the order's amount, it waits at the board, she stops; Problems says to put one in a chest.

## 0.8.2 (packaged 2026-10-11, in-game tests deferred): no ghost followers at portals, no working through floors

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (follower-ghost branch) | A follower taken through a portal could leave a frozen copy at the departure portal for players standing there (the vanilla portal "ghost": the server only sends a player what's near them, so they never heard it had gone). The server now resends a hireling's position to everyone when it jumps 64 m or more | the server |
| (follower-ghost branch) | Workers used things through the floor below them (Inga cooked from the floor above the kitchen): the height check used the cooking station's own height, raised on its fire, and nothing looked for a floor in between. Standing above something now needs no building piece between. Row VFH-COOK-9 | the game simulating the hireling |

### To test (batch)
- [ ] Macro (single player): `vfh_test_chain cook_floor`; regression `vfh_test_chain store1 gather1 cook_spit cook_cauldron farm_seeds` (all chores share the reach check).
- [ ] By hand (live server, two players): A stands by a portal; B goes through it with a follower: A doesn't see the follower left standing there (server debug log, Follow on: ghost.resent).

## 0.8.1 (packaged 2026-10-11, in-game tests deferred): follower fixes, Shift+E problems list, players' logs

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (order-reasons branch) | Shift+E "Problems (n)" view for Farmers, Cooks and Stewards: every order the job works (have / target, then done, on it, or why not) and every chore problem, not just the one the hover shows. The planners now give a reason per order (Cook: no station, ingredient, level, nothing makes it; Farmer: no field, no seed, seed reserve, level, nothing grows it); chores note all their reasons. Farm reasons now show item names ("Carrots", not "Carrot") in the hover too. Rows VFH-WHY-1, -2 | the game simulating the hireling (the list is stored on it); the panel on yours |
| (order-reasons branch) | A follower recruited at someone else's base didn't follow its new owner through a portal (Una, 2026-10-11): portals only took followers your own game was running, and nothing handed a follower to the player it follows. Now your game takes over your followers within 64 m every second, and a portal takes any of yours in range (claiming them first); one left behind is logged with why (`follow.travel_left`) | your game |
| (order-reasons branch) | A follower left behind never went home either (Una, still "following POPPA" at the base): the server sends the go-home order to the game running the follower, and when that was its far-away owner's (not loaded there) the order was dropped, every two minutes. Now it comes back to the server, which sends it on once to whoever runs the follower now, else takes the follower over and sends it home itself | the server; the game running the follower |
| (order-reasons branch) | Players' logs on the server: `ClientLogsToServer` (Debug, server setting, off) has every player's game send its Hired Hands lines at `ClientLogLevel` (default Info) to the server every 5 s, into `BepInEx/HiredHands/clients/<player>.log`; `vfh_send_log [lines]` sends a player's last lines once, whatever the setting. Rate-limited on the server | every player's game; the server writes |

### To test (batch)
- [ ] Macro (single player): `vfh_test_chain why1 why2`; regression `vfh_test_chain orders farm_bush farm_seeds`.
- [ ] By hand (live server, two players): player B recruits a guard posted at player A's base (A standing there), walks through a portal and back and through again: the guard goes every time (B's log: follow.claimed / follow.travel_claimed).
- [ ] By hand (live server): `ClientLogsToServer = true` on the server; after a minute of play `BepInEx/HiredHands/clients/<name>.log` exists for each player; with it off, `vfh_send_log` from a player still writes theirs.
- [ ] By hand (live server): a Cook and a Farmer with several stuck orders: Shift+E, Problems; the list matches and updates as you fix things (add a stove, stock seeds). A Steward's list shows fuel, paused stations and the like.

## 0.8.0 (packaged 2026-10-11): the Wizard, with the 0.7.8 playtest fixes

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| (wizard branch) | No hireling's hit ever damages a player, whatever PvP says (a stray splash or blast landed on players by the target). Row VFH-COMBAT-10 | the game hit |
| (wizard branch) | Wizard job (plan/wizard): combat, 1.5x, board level 6, or 2 with Therzie's Wizardry (`withMods`); per-job outfits with capes; `WizardDamage`, `WizardSpellsHit`, `WizardAttackCooldown`. Rows VFH-WIZ-1, -12 | the board owner's machine (contracts) |
| (wizard branch) | Wizard's staff and robes per level (Wizardry's or vanilla), with fallbacks when an item is missing (Wizardry removed: vanilla staff, shared armour; nobody dismissed). Row VFH-WIZ-2 | every game (gear) |
| (wizard branch) | Wizards cast their staff's main spell from 10-22 m every 3 s, step back from anything inside 8 m, hold posts, follow, obey attack and retreat orders. Rows VFH-WIZ-3, -4, -5, -8, -9, -10, -11 | the game simulating the hireling |
| (wizard branch) | `WizardSpellsHit`: wizard hits spare props by default (EnemiesOnly) or may damage them (EnemiesAndTerrain). Rows VFH-WIZ-6, -7 | the game hit |
| (wizard branch) | Found in the first in-game run: wizards never cast (vanilla refuses an eitr-costing attack from anything with no eitr bar; hirelings now cast for free), backed off for ever from an enemy they couldn't outrun (now 3 s, then stand and cast), missed moving targets with slow spells (casts now lead the target), overshot with every staff that launches tilted up (Swamp, Plains, Mistlands, Embers: the aim now undoes the launch angle), never fired Dundr (it needs reloading, which only players do: a hireling's weapon is always loaded), and `EnemiesAndTerrain` never let a spell hurt a prop (the gatherers' stray-hit rule ate it; the wizard rule now lives there). A re-geared wizard could end up with its staff in the bag and an empty hand (now equipped when the hand is empty). | the game simulating the hireling |
| (wizard branch) | Test harness: an ordering check against a word (`cast_dist none >= 8`) passed as text; now fails. New checks/fixtures: `cast_dist_max`, `enemies … ahead`, `spell_props`, `object … at <tag>` | tests only |
| e98bc65 | As a passenger on someone else's ship, your followers didn't board: "under way" was read from the ship's rigidbody velocity, which is zero on every game but its owner's (it's moved by position updates there). Thorvald stayed on the dock and was sent home as too far behind. Now the speed is measured from how far the ship moved between checks | your game |
| 95d37ee | Two hirelings spawned beside a board placed a moment before left it as "gone" (the first board check ran at once, and the new board wasn't in the server's index yet). The check now runs 30 s after a hireling appears and needs two misses in a row (a minute apart) before it leaves | the game simulating the hireling |

### To test (batch)
- [ ] Setup (Tim, once, in Gale): add Therzie-Wizardry and its dependencies to the vikingsforhire-dev profile.
- [ ] Macro (single player, Wizardry in the dev profile): `vfh_t_wiz_all` (VFH-WIZ-1 to -18, VFH-COMBAT-10; 13-18 cast each staff), all pass; regression `vfh_test_chain store1 orders death3 recruit1 post1 retreat1 catchup1 combat1 stance4 stance5 block2 dodge1 noeffect move1 cap1 cap3 gather1 hire4 hire5`.
- [ ] By hand (live server, Wizardry): wizards look right (robes, cape, staff, casting animation); one on a post and one following; `WizardDamage 0.5` visibly weakens them.
- [x] Test debt (2026-10-11): `vfh_t_debt`, 32 of 33 pass first time: home3 chat1 store1 store2, all five Cook rows, farm 1/3/4, NAVLINK-1 to -9 and NAV-2 (the new water-avoiding agent), portal1 catchup1 home1 recruit1 retreat1 gather1 order1 order2 move1 death5. VFH-PASS-1 failed once (its hirelings left the brand-new board as "gone"): fixed above; then `pass con1 con3 charter1 home1 home3 move1`, all pass. By-hand items in those tabs still open.
- [ ] By hand (live server, two players): one steers, you ride along with a follower nearby: as the ship gets going the follower goes below deck; it steps off with you when you land.

## 0.7.7 (released 2026-10-10, tests deferred at Tim's request): chatter, chest puffs, walking not swimming, Cook watches the food, followers left at home, lost hirelings

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 50d6faa | A puff of dust at a chest when a hireling puts something in or takes something out: the chest's own "placed" dust (no sound), shown on every game that has the chest loaded, at most once a second per chest. `ChestPuffs` (Work, your own setting) turns it off for you. Players on older versions would log a missing-RPC line for each puff: update together | the game simulating the hireling (sends); every nearby game (shows) |
| 24b70f6 | Sigrid (a follower miner) vanished entering a sunken crypt: our travel moved her to the crypt's interior, she never appeared, and her world object was gone with no death or grave (cause not found: the server didn't roll back; the profile has two new teleport mods, OdinPlus QuickTeleport and Zenox TeleportEverything). The board showed her "away" until the server's 10-minute check settled her as a death (3 days). Now that check marks a vanished hireling lost: it comes back after the usual walk-in time, free, posts cleared (`contract.lost`). And any deletion of a living hireling's object is logged with what called it (`hireling.zdo_destroyed`), to find the culprit next time | the server (check); the board owner's machine (roster); every game (watch) |
| 60aa256 | A friend parked a miner in Stay near the base and went through a portal: the miner stayed his follower for good and nobody else could manage it. The lost-follower rules skipped anything inside its work area (a miner's is twice the board's). Now the same rules apply there, and a follower inside its area is released back to work at its board instead of walking home ("X got tired of waiting and went back to work", `follow.orphan_released`): in Stay or Gather Here after 2 min with its owner 150 m off, following after 30 s at 60 m, or 5 min after its owner logs off. Row VFH-HOME-3 | the server |
| 55d539e | Hirelings still swam where they could walk (Brand jumped in the sea and swam round instead of walking out of the house): the game's Humanoid path agent costs water only 4x land, so any walk round longer than that lost. They now use the game's HumanoidAvoidWater agent: water isn't walkable for it, so a land route always wins; with no land route they still head straight there. Arrival spots and the doors/stairs planner use the same agent | the game simulating the hireling |
| addda8b | Hirelings talk: now and then one near you (15 m) says a line in the trader-style speech bubble, about half the time about what it's doing right now (you standing there watching, following you, idle, cargo full, fighting, scared, out in the rain), otherwise about its job or something generic. 108 lines in English.json (`vfh_say_<pool>_<n>`). About every 5 minutes per hireling (`ChatterMinutes`), never two lines within 20 s; `HirelingChatter` turns it off. Both are your own settings; each game picks its own lines, nothing is sent | your game |
| d4c7aba | The Cook put meat on the spit, walked back to the board, then ran back to take it off before it burned. Now, with food cooking within reach and nothing more urgent, it stands by the stove ("Watching the …"); every 10 s it re-plans (fuel, the next load, a craft still come first) and the moment the food is done it takes it off | the game simulating the Cook |

### To test (batch)
- [ ] Macro: `vfh_test_chain cook_spit cook_oven cook_chain cook_protect` (the Cook's rows still pass).
- [ ] By hand: a meat order and a spit: the Cook loads it and stays by it until it's done, then takes it off.
- [ ] Macro: `vfh_test_chain chat1`.
- [ ] By hand: stand still beside a working woodcutter for a few minutes: a speech bubble now and then, some about you watching; `ChatterMinutes 1` for more; `HirelingChatter` off: silence.
- [ ] Macro (navigation regression, every route changed): `vfh_test_chain navscan1 navscan2 navscan3 nav1 nav2 nav3 nav4 nav5 nav6 door1 portal1 catchup1 home1 home3 gather1 farm_harvest pass`.
- [ ] By hand (live server): Brand at the seaside house walks out over land.
- [ ] Macro: `vfh_test_chain home3 home1 recruit1 retreat1`.
- [ ] By hand (live server): a friend parks a follower at the base and portals away: within about 2 minutes it's back at work and you can recruit it.
- [ ] By hand (live server): if a hireling vanishes again, its log line `hireling.zdo_destroyed` names the caller; 10 minutes later the board says it's arriving (not "returning" in days).
- [ ] By hand: a woodcutter delivering wood, a Steward taking ore: a small puff at the chest each time; a second player nearby sees it too; `ChestPuffs` off: you don't.

## 0.7.6 (released 2026-10-10): playtest fixes (turnips, planting, CoalPaste, mead ketill, roster and upgrade buttons)

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 6407847 | The farm order picker listed turnips as a seed ("Turnip (seed (kept in reserve))"): raw turnips aren't food, and planting one grows turnip seeds, which was the seed rule. Now food or a cooking ingredient is never a seed, so turnips and onions are produce orders | your game (picker) |
| c41fac4 | The Farmer planted carrots among picked mushroom stalks: a picked mushroom, berry bush or dandelion hides the part that collides, so the game's own space check sees open ground. Now no crop goes within its grow radius of a regrowing plant, picked or not (wild ones included) | the game simulating the Farmer |
| 87a7344 | The Roster tab offered "Cancel (refund)" for a hireling waiting to come back from death; it never refunded (the hire fee was spent), so the button now says "End contract (no refund)" | your game |
| d337d4a | The Steward loaded a board with 76 CoalPaste (Balrond Shipyard's shipbuilding material, which carries food values but can't be eaten). Food now has to be a consumable, as the game requires to eat it: on the board, for the Steward's board food, in the Cook's stove list and in the raw-food check. CoalPaste already on a board stays but no longer counts or pays; take it out by hand | everywhere (board, Steward, Cook) |
| 4600dd8 | The Cook made mead and potion bases (mead ketill recipes) only from level 7; a ketill is built in the Black Forest era, so now from level 2 (`stationLevels` → `piece_MeadCauldron`). Data files without the key take the new default | the board owner's machine (orders) |
| 76668d7 | The Upgrade tab's button covered the last materials (it sat at a fixed height from the smaller panel): now it goes below the materials and the "not refunded" note | your game |

### To test (batch)
- [ ] By hand: Upgrade tab at a level with five materials: every material readable, the button below the note.
- [ ] By hand: Orders → Add kitchen order lists mead bases at level 2; a level 2 Cook with a mead ketill (fire under it) makes one.
- [ ] By hand (live server): CoalPaste can't be put on the board; the board's food days don't count it; the Steward doesn't bring it.
- [ ] By hand: a dead hireling waiting to return shows "End contract (no refund)"; a newly posted one still shows "Cancel (refund)".
- [ ] By hand (live server): picked mushrooms on cultivated ground, a carrot order: no carrots planted between the stalks.
- [ ] By hand: Orders → Add farm order: "Turnip" (no "(seed)") and "Turnip Seeds (seed)" both listed; an order for turnips makes the Farmer plant turnip seeds.

## 0.7.5 (released 2026-10-10): keep to dry land, Gather Here stays put

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| a4d110e | Hirelings keep to dry land (board by a cliff over the sea): idle spots and "call to board" spots are never in the water; gatherers skip rocks and trees standing in the water (`in the water` in work.no_targets) and give up a target as soon as getting to it means swimming (`work.needs_swim`); followers wait on the shore while you swim ("Waiting on the shore") and catch up once you're back on land; melee guards don't wade out after a target in the water and archers don't close in through it (they shoot from the shore if in range), giving it up after 5 s (`combat.target_in_water`). Gather Here: a big rock is picked by its centre, so a chunk of it outside the 15 m circle (plus a pickaxe's reach) is left alone (`work.part_outside`); Sigrid swam 30 m to one | the game simulating the hireling |

### To test (batch)
- [ ] By hand (live server): the board by the cliff: idle hirelings stand on land.
- [ ] By hand: swim out with a follower: it waits on the shore ("Waiting on the shore"); land 60 m along the coast out of its sight: it catches up.
- [ ] By hand: a melee guard fighting something that swims off: it stays on the shore and lets it go after a few seconds.
- [ ] By hand: a miner in Gather Here beside a rock, with rocks in the water nearby and a big rock whose far side is 30 m off: it mines only within about 15 m, on land.

## 0.7.4 (released 2026-10-10): base check follows the board's area

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| b08de9b | Placing a board, the base check (workbench, bed, pieces) looks within the larger of `BaseCheckRadius` (20 m) and the placed board's own area: a level 4 board from a Hiring Charter counts a bed or workbench anywhere in its 35 m circle, the circle the hammer shows. The "needs a bed within N m" hint says the radius used | your game |

### To test (batch)
- [ ] By hand: with a level 4 charter, place a board where the only bed is 30 m away (outside 20 m, inside 35 m): allowed; without the charter (level 1) it says a bed is needed within 20 m.

## 0.7.3 (released 2026-10-10): playtest fixes, Steward limits, follower moves, board area

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 316b4a9 | A hireling that dies loses its guard post: it comes back (after ReturnAfterDays or a paid respawn) patrolling, and can be posted again with the stone | the board owner's machine |
| 56322bb | A posted guard that couldn't walk to its post (Una, back from death at the base's edge, walled off from her post 26 m away) stopped after 15 s and stood there for good, "on guard", the Roster tab saying "Posted". Now it tries again every 20 s, shows "Can't reach their post" (Roster tab too), and after 3 failed tries goes straight to the post when nobody is looking (`post.moved`) | the game simulating the hireling |
| a4fe3ac | The greydwarf Thorvald shot at had 0 health and never died: only the game that owns a creature runs its death, none did, and damage (arrows, `killall`) is ignored at 0 health. Now such a creature isn't a threat, and a hireling that notices one claims it so it dies on that game (`combat.claimed_dead`) | the game simulating the hireling |
| 0c1a24b | Thorvald (posted archer) shot at a greydwarf stuck in the ground for good: in sight, but every arrow hit the dirt, and each shot counted as fighting. Now a hireling whose target loses no health for 25 s of attacking gives it up for 60 s and picks another (`combat.ignored` at Info: what, where, why); targets below the water's surface (fish, serpents by the shore) aren't picked. `vfh_dump_state` shows each hireling's current target. (The DefendYourBase troll is the game's own AI and still attacks it) | the game simulating the hireling |
| fe10bcc | Melee guards swung forever at a mob just the other side of a wall (in reach, so they swung, and every swing kept the fight going). A swing now needs a clear line (middle or eyes to the target's middle or head, past pieces and terrain); blocked, the guard walks round to it (doors and all), and after 5 s still blocked gives it up and ignores it for 20 s. `combat.swing_blocked` log | the game simulating the hireling |
| bbd786e | Raw food from mods paid upkeep: the Steward filled a board with Witch Eyes (a mod mushroom), counted as 9 days of food. Now any edible item that nothing makes (no recipe, cooking station, oven or fermenter turns anything into it) is raw, as well as what the data file's raw list names (which also lacked the Ashlands Smoke Puff). `AllowRawFood` still lets it all in. `food.raw_unmade` logs the unmade foods found. Raw food already on a board stays there but no longer counts or pays; take it out by hand | everywhere (board, Steward, hiring) |
| 476a59c | `RetreatKey` (Followers, your own setting, unset by default): a key or mouse button for the retreat order, stone in hand or not (the stone's middle click still works). Right click your follower in another board's area (the board level's largest work radius, you need ward access): it joins that board if the board would take the same contract (hireling level, job gate, combat and worker caps, per-job limits, CombatHirelings; no fee), leaves the old board's roster and goes to work at the new board (unposted). Refused: the reason with the numbers. New op type and fields: server and clients must all update | your game (key, click); the server (transfer); the new board's owner (cap check); the old board's owner (removal) |
| 1319717 | Looking at a hiring board (or having its panel open) draws its area on the ground (gold circle: the level's largest work radius, 20 m at level 1 to 60 m at level 8); the hover says "Area: 30 m (woodcutters and miners 60 m)". The tree patch circle uses the same code (AreaRing) | your game |
| 7d76449 | Placing a hiring board with the hammer draws its area round the ghost (gold circle following it): level 1's radius, or the carried Hiring Charter's level, which the new board takes | your game |
| d4780b3 | The board hover said "Food lasts 9 days, coins — days" though upkeep is food only: now "Food lasts 9 days", with the coins only when a data file charges coin upkeep | your game |
| 716ffce | Steward chores: charcoal kilns are their own toggle ("Charcoal kilns"), apart from "Smelters, furnaces and refineries". Same server setting (StewardStations). A Steward that had smelting switched off has kilns on after the update | the game simulating the Steward |
| bf7265c, (this) | Steward limits, set in the Steward's Shift+E panel ("Limits (N set)"; kept on its board, so a new Steward keeps them): "make no more once the chests in its area hold this many", for anything it makes: coal, bars, eitr, flour, linen, modded station products, honey, sap, each mead. At the limit a station isn't loaded ("Kiln: paused, Coal is at its limit (100)"), a beehive or sap collector is left full, a fermenter isn't loaded with that mead's base; paused = makes none; anything without a limit as before. Counted over every chest in its area, reserves included. The board panel is bigger (1000 x 720) with larger order rows (10 a page) and picks (18 a page); the Orders tab stays farm and kitchen | your game (panels); the game simulating the Steward |
| 06db69e | With the Command Stone in hand, the map shows your hirelings: your followers (dot, their name) and the workers and guards of the boards you built (hammer, name and job), not those leaving or on a trip home. Asked of the server every 2 s (so far-off bases show too); ones near you are pinned where they are, so a follower's pin keeps up. Pins aren't saved and go when you put the stone away | your game (pins); the server (positions) |

### To test (batch)
- [x] Macro (single player, 2026-10-10): `vfh_test_chain store2 kilns limit1 limit2 death5 noeffect move1`, all pass (MOVE-1 after two fixes to its own fixture: Testboy now goes along to the other board, and the checks name both boards). Covers: Smoke Puff refused / oven bread taken; kilns toggle; Coal limit holds then releases the kiln; Honey limit leaves a full hive; a guard's post cleared on death; an archer gives up an immune target; a follower joins another board (off the old roster, on the new one, not a follower) and map pins show with the stone.
- [x] Regression (single player, 2026-10-10): `vfh_test_chain store1 orders death3 recruit1 post1 retreat1 catchup1 combat1 stance4 stance5 block2 dodge1 chore_gate chore_toggle beehive sap fermenter mills cook_spit cook_chain farm_harvest farm_seeds order1 order2 cap1 cap3 pass`, 28 of 28 pass.
- [x] Unit tests: 226 pass. `BigBaseStaysCheap` failed once in 25 runs under load (a 5 ms wall-clock bound): widened to 25 ms.
- [ ] By hand: a posted guard dies and comes back: the Roster tab shows it working (not "Posted"), patrolling.
- [ ] By hand: a posted guard that can't walk to its post (e.g. post it in a walled yard, then close the only door): the Roster tab shows "Can't reach their post"; within about a minute and a half, looking away, it's on its post.
- [ ] By hand: a guard against something it can't hurt (a creature stuck in the ground, or `vfh_fixture` an immortal target): within about 25 s it stops, `combat.ignored why="no effect"` in the log, and it takes on other enemies meanwhile.
- [ ] By hand (live server): a mob against the outside of a wall near a melee guard: the guard doesn't swing at the wall; it goes round through a door, or gives up within about 5 s.
- [ ] By hand (live server): the board with the Witch Eyes shows food days from cooked food only; the Steward doesn't bring more Witch Eyes; putting one on the board by hand is refused. The server log's `food.raw_unmade` lists Witch Eye and no cooked foods (check what else is in it).
- [ ] By hand: set RetreatKey (e.g. a mouse side button) in ConfigurationManager; in a fight with no stone in hand, press it: "N followers retreat with you" and they drop the fight.
- [ ] By hand (the server side passed in VFH-MOVE-1; this is the click and the messages): a follower from board A walked to board B (with room for it): right click it: "X joins this board and gets to work"; A's Roster tab no longer lists it, B's does, it works at B; upkeep comes off B.
- [ ] By hand: the same into a board that's full for that kind or job (or below the hireling's level): refused with the reason; it stays your follower and on A's roster.
- [ ] By hand (dedicated server): the same, ideally with the two boards owned by different players' games.
- [x] By hand (Tim, dev game, 2026-10-10): hammer, select the hiring board: the circle follows the ghost and is bigger with a higher-level charter; picked up a board with hirelings (`vfh_board_setlevel`, `vfh_spawn_contract`), placed it elsewhere: they came back.
- [x] By hand (Tim, dev game, 2026-10-10): looking at a board shows the circle.
- [ ] By hand: look at a board: gold circle on the ground at its level's radius, follows the ground; open Shift+E: the circle stays; the hover's area line. A tree patch's circle still shows.
- [ ] By hand (logic passed in VFH-CHORE-15; this is the panel): a Steward's Shift+E chores list "Charcoal kilns" separately; switch it off: the kiln is left alone while the smelter is still fed.
- [ ] By hand (logic passed in VFH-LIMIT-1/2; this is the panel and the mead case): Steward Shift+E → Limits: add Coal, set it below what the chests hold: the Steward stops loading the kiln ("paused, Coal is at its limit"); raise it above: it loads again; pause: no coal made. The same with Honey (hives left full) and a mead (that base isn't loaded). The count matches the chests.
- [x] By hand (Tim, 2026-10-10): the bigger board panel looks good.
- [ ] By hand: the Orders tab with 10+ orders: rows readable, paging works; the panel fits on your screen (and at your GUI scale).
- [ ] By hand: stone in hand, open the map: a dot per follower moving with them, a hammer per hireling at your boards (also a base far away), named; put the stone away: the pins go. On a dedicated server too.

## 0.7.2 (released 2026-10-09): board moving, stone on a chest, combat settings

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 06eea66 | Command Stone on a chest (left click): followers near you carrying something that chest already holds walk to it and put those items in (only what it holds, like base deliveries), then carry on (following you, or back to their spot). "You can't use that chest" without access; "Nobody near you carries anything this chest holds" otherwise | your game (the order); the game simulating the follower |
| 4f78d7b | `GuardMeleeDamage`, `GuardRangedDamage` (Combat, default 1): combat hirelings' damage multiplier on top of the level table's guardDamageMult | the game simulating the hireling |
| 6de1704 | Moving a board keeps its hirelings: deconstructing (after the confirm) asks the server to pack them (each hireling's ZDO saved into its contract and removed; posts cleared; a waiting respawn keeps its wait) into the Hiring Charter, even at level 1; building a board while carrying it restores the roster and they arrive like new hires; tooltip lists them | the server (packing); the placer's game (unpacking); the board owner's (arrivals) |
| 4161559 | `CombatHirelings` (Hiring, default on): off = workers only: combat contracts refused ("Combat hirelings are turned off on this server"), the Contracts tab offers workers only, counts and Upgrade tab show workers; combat hirelings already hired stay | the board owner's machine; your game (tabs) |

### To test (batch)
- [x] Macro (single player, 2026-10-09): `vfh_test_chain order2 dmg1 charter1 cap5 con2 cap1 cap2 cap3 order1 recruit1 combat1 combat5 block2 dodge1 death3`: all 15 pass first time.
- [ ] By hand: CombatHirelings off: the Contracts tab cycles through workers only and reads "Workers 0/2"; turn it back on and the guards reappear.
- [ ] By hand: move a real board: deconstruct (the popup says they'll go into the charter), hover the charter (level and names), build the new board elsewhere: same level, everyone walks in with their cargo; posted guards come back unposted.
- [ ] By hand (dedicated server): the same as a client (packing runs on the server).
- [ ] By hand (live server): set GuardMeleeDamage to taste (e.g. 0.5) and fight with a guard along.
- [ ] By hand: followers with mixed cargo; point the stone at a chest holding some of it: they walk over, put only those items in, then follow you again; a follower in Stay goes back to its spot.

## 0.7.1 (released 2026-10-09): orders catalog fix

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| 4ffa952 | Orders tab: the Cook's list had gold weapons, Deep North armor, shields and staffs (the vanilla frost foundry is built as a cooking station) and the prep table's fishing bait. Now stoves only count when they make food, the cauldron, prep table and mead ketill when they make consumables or materials, and the Farmer never lists gear. Raw fish ("any one fish") was listed as needing all 12 fish at once, so the Cook could never make it: now one way per fish. Rejections are logged (`kitchen.rejected`, `crops.rejected`) | everywhere (catalogs) |

### To test (batch)
- [x] Catalog dumps (single player, 2026-10-09): `vfh_recipes` has no gear and FishRaw once per fish; `vfh_crops` crops, seeds, berries and mushrooms only.
- [x] Macro (single player, 2026-10-09): `vfh_test_chain cook_spit cook_oven cook_cauldron cook_chain cook_protect farm_harvest farm_seeds farm_rows` pass. COOK-5 had failed after COOK-3 in one chain: `crafted_by_cook` counted from game start, so the cauldron row's carrot soup counted against it (the Cook itself refused the reserved carrots correctly). The cook rows now start with the new `cook_stats_reset`; COOK-3 then COOK-5 pass twice. FARM-4 failed once on sloped ground (Testboy had been moved off the test area by a hung launch) and passes at the test area.
- [x] Dev game: Valheim had saved its window as 1 x 28 px (the first hung launch), so later launches rendered into nothing (`RenderTextureDesc width must be greater than zero` every frame). Set to 1920 x 1080 windowed in the Proton prefix's user.reg; 0 errors since.
- [ ] By hand (live server, the 1dotohsupermodded profile): the board's Orders tab lists no armor or weapons for the Cook or the Farmer; the client log's `kitchen.rejected`/`crops.rejected` lines show what the server's mods added.

## 0.7.0 (released 2026-10-09): split caps, DeathMode

Plan: `plan/split-caps/overview.md` (combat and worker caps per board level, per-job limits).

### Fixed or added
| Commit | What | Where it runs |
|---|---|---|
| f924ebb | Caps 1/2: each board level has a combat cap (1, 1, 2, 2, 3, 3, 4, 4) and a worker cap (2, 4, 6, 8, 8, 8, 8, 8); per board at most 2 woodcutters, 2 miners, 1 Steward, 1 Farmer, 1 Cook (`combatCap`, `workerCap`, `maxPerBoard` in the data file, filled into older files); boards over a cap keep their hirelings, only new contracts are refused | the board owner's machine (posting a contract) |

| 49c1787 | Caps 2/4: a refused contract says which limit is full, with the numbers ("Combat hirelings: 1/1 at this board level", "Woodcutter: 2/2 per board"), in each player's language; `contract.refused` log; test fixture `caps`, check `cap_counts`, rows VFH-CAP-1..4, VFH-CON-2 rewritten | the board owner's machine; the message on the player's |
| ccc76c6 | Caps 3/4: Contracts tab shows "Combat 1/2 · Workers 3/6 · Woodcutter 1/2" (red when the chosen job can't be posted, with the reason under the button); Upgrade tab shows "Workers: 2 → 4 · Combat hirelings: 1" | your game |
| 61de7c4 | Death mode: `DeathMode` = Permadeath / PayToRespawn / ReturnAfterDays (new default: back by itself, free, after `ReturnAfterDays` in-game days, 3); replaces `PermadeathEnabled` (on → Permadeath, off → PayToRespawn, on first start); death message says when it'll be back; Roster tab counts down in days | the board owner's machine |
| 23b6f8e | Stale rows: VFH-HIRE-4 rewritten for the cargo weight limit (feathers fill the 8 slots; wood stops at 3 stacks, 300 weight; new `hireling … cargo_weight` field); VFH-HIRE-5's snapshot round trip ignores the animator's movement floats (`forward_speed`, `turn_speed`, `sideway_speed`, ZDO keys -1489121593 and -1488745797), which differ whenever the hireling was walking or turning when snapshotted | tests |

### To test (batch)
- [x] Macro (single player, 2026-10-09, merged build 17d7695): `vfh_test_chain death1 death2 death3 death4 hire4 hire5 con2 cap1 cap3`, all pass. The dev profile's `PermadeathEnabled = true` became `DeathMode = Permadeath` on start (`config.migrated` logged) and the old line is gone.
- [ ] By hand: in ReturnAfterDays a hireling dies: "… has died, and will be back at the board in 3 days."; the Roster tab shows "Returning (3.0 days)" counting down, and sleeping moves it on by the night.
- [x] Macro (single player, 2026-10-09): `vfh_test_chain con2 cap1 cap2 cap3 cap4`, all pass.
- [x] Regression (2026-10-09, 36 rows: contract-posting rows, nav, farm, cook, trees, passing, combat): 31 pass outright;
  DIS-1, BLOCK-1 pass on rerun (flaky), STN-5 is the known flaky one (the archer doesn't spot the Neck within 4 s).
  HIRE-4 and HIRE-5 fail every time but don't post contracts and last passed on 2026-10-03 (0.1.x): HIRE-4 predates the
  cargo weight limit (20 stacks of wood stop at 3 slots: the weight limit, as intended), HIRE-5's snapshot round trip
  differs in two of the game's own ZDO floats. Old stale rows, not 0.7.0; left for a separate look.
- [x] Macro (single player, 2026-10-09): `vfh_test_chain hire4 hire5 hire4 hire5`, all pass (rows fixed above); feathers 8 of 20 stacks in, wood 3 of 20, round trip 47 values equal.
- [ ] By hand: a level 1 board's Contracts tab with Woodcutter chosen reads "Combat 0/1 · Workers 0/2 · Woodcutter 0/2"; with a guard chosen "Combat 0/1 · Workers 0/2". Post a guard, choose a guard again: the line is red, Post is greyed out, and the reason reads "Combat hirelings: 1/1 at this board level. Upgrade the board for more".
- [ ] By hand: the Upgrade tab at level 1 reads "Workers: 2 → 4 · Combat hirelings: 1", and the materials list doesn't run into the Upgrade button (a level with five materials).
- [ ] By hand (dedicated server): a refused contract shows its message for a client too.

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
