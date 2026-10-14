# Hireling Enhancements — Project Overview

## Problem
Two hirelings of the same job and level are identical today: same health, gear, damage, speed and behaviour, decided
entirely by the level table and the job's gear list. Levelling a hireling makes it bigger, never different, so there is
nothing to decide about a hireling after hiring it, and no reason to grow attached to one guard over another.

Players have asked for hirelings that grow in a direction they choose: an archer that hunts with fire arrows or one that
snipes from a wall, a miner built for speed or for yield. The futures note "Skill trees for combat hirelings" (2026-10-10)
sketched this for combat jobs; this project does it for every job.

## Goals
- At each hireling level from 2 to 8 the player picks **one of two enhancements**: seven choices per hireling, made per
  hireling, so two level 8 guards can be built differently (128 possible builds per job).
- Every job has its own seven pairs: Guard, Archer, Wizard, Woodcutter, Miner, Steward, Farmer, Cook, Rancher.
- Each pair is a **real trade-off**: mostly a number boost against a new behaviour at the same level, with new behaviours
  concentrated at levels 4, 6 and 8.
- A fully enhanced level 8 hireling is about **25% stronger** than one without enhancements: each number boost is around
  5-10%, and builds differ more in what they do than in raw power.
- Choices are made in the hireling's **Shift+E panel**, any time after they're owed. Owed choices are flagged on the
  hireling's hover and on the board's Roster tab until made. Nothing applies until chosen.
- A hireling hired or promoted past a level (hired at 5, promoted 2 → 5) has every skipped level's choice owed.
- Choices are permanent, except for a **paid respec at the board**: reset all of one hireling's picks for half its hire
  fee at its current level (data-file `respecCostFraction`, default 0.5), paid from the board's funds; then choose again.
- Anyone with **ward access** to the board can choose or respec, as for other board and hireling management; a follower's
  owner can choose for it while it follows them.
- Choices **persist** with the contract: through death and return, respawn, the Hiring Charter (moving a board), and a
  follower joining another board. Permadeath ends them with the contract.
- Enhancement **numbers live in the data file** (`Spronglehump.HiredHands.yml`, server-synced): percentages, durations,
  cooldowns, chances. A server switch `Enhancements` (Hiring section) turns the whole system off.
- Enhancements show: the hover lists a hireling's picks; the balance log records each fight's enhancements.

## Non-goals
- No branching trees or prerequisites: each level's pair is the same for every hireling of that job, whatever was picked
  before.
- No data-file control over which pairs exist or what an effect does (only its numbers).
- No enhancements at level 1, and no choices beyond level 8.
- No random or automatic picks: an unmade choice simply waits.
- No new jobs (Healer, Scout) and no new item types; visual effects reuse the game's own (fire on arrows, status icons).
- No change to the level table's base values beyond what testing the 25% budget shows is needed.
- No per-player or per-board enhancement limits.

## Users & primary flow
**Players** managing hirelings at a base or in the field; **server owners** tuning numbers.

1. A player promotes Ragna, an archer, from level 3 to level 5 at the board (or hires her at level 5).
2. Her hover and the board's Roster tab show "2 enhancements to choose".
3. The player opens Ragna's Shift+E panel → **Enhancements** tab. It lists levels 2-8, each with its two options (name
   and one-line effect, with the data file's numbers); made choices are marked, owed ones are clickable, future ones are
   greyed with "at level N".
4. The player picks one option at each owed level (a click, then Confirm). The panel only opens for players with ward
   access (or a follower's owner), as today; the choice is sent as a roster op, and the board owner's game checks the
   level is owed, then saves it on the contract. The effect applies at once.
5. Her hover now lists her picks; in fights she uses them (fire arrows, volleys).
6. Changing their mind later, the player uses **Respec** on the board's Roster tab (her details): the cost (half her
   current hire fee) is shown, paid from the board's funds after a confirm click; all her picks clear and are owed again.

## Constraints
- **Valheim + BepInEx + Jotunn**, the existing C# code base (`src/VikingsForHire`); pure rules in `Core/` with xUnit
  tests (`tests/VikingsForHire.Tests`).
- **Multiplayer:** contracts and the roster live on the board's ZDO and change only through roster ops on the board
  owner's machine (`RosterOps`, `BoardRosterOps`); a hireling's own state is on its ZDO (`HirelingZdo`) and changes through
  `MutationService`. The hireling is simulated by its owner's game. Enhancements must follow the same paths.
- **Data file** is server-synced and validated (`Core/Data/VfhData.cs`, `DataDefaults.cs`, `DataValidator.cs`); older files
  get new sections filled from defaults.
- **Version rule:** the mod checks major.minor between players; new roster op types and contract fields mean everyone
  updates together (a minor version bump, 0.10.0).
- **"Faster" for gathering** means more damage per swing (fewer swings per tree or rock), as the game measures chopping and
  mining; for crafting and walking it means less time.
- **Existing balance:** CombatSkill presets, `guardDamageMult` (1.0-1.3), `GuardMeleeDamage` / `GuardRangedDamage` /
  `WizardDamage`, `HealthRegen` stay and apply first; enhancements multiply on top.
- **Gear by level** comes from the data file's `gear` lists. The "Masterwork" options raise the main weapon's or tool's
  item quality by one (as upgrading it at a forge would: more damage and durability, more chop or mine damage), which
  works at every level; hireling gear is quality 1 today.
- **Saves:** existing contracts load with no picks; hirelings above level 1 then have their owed choices waiting.
- **Tests:** the in-game harness (ModTestBridge, `test/alias_vfh.yaml`, the `hired-hands-testing` skill) gets a row per
  behaviour and rows reading each job's number boosts; the full regression must still pass. Rows use the harness's
  existing fixtures and checks (`post`, `hirelings`, `stock_board`, `board_clear`, `board_storage`, `board_access`,
  `kill_hirelings`, `skip_days`, `charter_pack`/`charter_apply`, `stone`, `recruit_posted`, `board_far`,
  `transfer_posted`, `contract promote`, `cfg_set`, `hireling_health`, `enemies`, `enemy_level`, `enemies_immune`,
  `trees`, `deposits`, `deposited`, `cargo_put`, `station`, `fermenter_status`, `grow_all`, `pen`, `move_animal`,
  `outside_pen`, `patch_saplings`, `clear_area`, `fire`, `damaged`, `shieldgen`, `litter`, `piece`, `stance`, `roster_entry`,
  `animal_hungry`, `hireling <sel> <field>` (including `target`, `gear.right`, `cargo.<item>`, `cargo_weight`,
  `owner_dist`, and any `vfh_` key), and `fast_timers on`, the
  existing switch that shortens arrival, respawn, crop and station timers for tests) plus those each phase adds.

## Success criteria
- A level 5 hireling's panel shows levels 2-5 owed, and choosing each one applies its effect at once (checked by test rows
  reading the effect, e.g. max health, shot cooldown, cargo slots).
- Two hirelings of the same job and level with different picks behave measurably differently in a test row (damage per
  fight, yield per hour, chore results).
- A respec charges half the hire fee from the board's funds, clears the picks, and is refused when funds are short.
- Picks survive death and return, a Hiring Charter move and a follower joining another board (test rows).
- A player without ward access can't open the hireling's panel or the board, so can't choose or respec (test row).
- `Enhancements` off: the page says they're turned off, no effects apply and no owed flags show; existing picks are kept and
  return when it's turned back on.
- Duels and the balance log show a fully enhanced level 8 guard, archer or wizard (all-A and all-B builds) 20-35% better
  than an unenhanced one, and fully enhanced level 8 gatherers 20-35% more productive.
- `dotnet test` passes and the full in-game regression passes (apart from the two dev-environment rows).

## Draft enhancement catalog
For each job, levels 2-8; at each level the player takes A **or** B. Numbers are defaults (data file). This is the
draft for review: keep, swap or drop any line.

### Guard (melee: sword and shield)
| Lvl | A | B |
|---|---|---|
| 2 | **Toughened:** +10% max health | **Shield training:** blocks absorb 15% more |
| 3 | **Heavy armour:** +4 armour | **Swift strikes:** attacks 15% faster |
| 4 | **Shield bash:** every 12 s a bash that staggers its target (and 10 blunt damage) | **Second wind:** once a fight, below 30% health, heals 25% |
| 5 | **Masterwork blade:** its sword is one quality level higher | **Bodyguard:** enemies attacking its owner or a hireling within 6 m turn on it (every 15 s) |
| 6 | **Taunt:** every 20 s, enemies within 8 m attack it for 5 s | **Bloodthirst:** heals 10% of the damage it deals |
| 7 | **Cleave:** swings also hit up to 2 more enemies in front, for half damage | **Last stand:** once every 10 minutes, a killing blow leaves it at 1 health and unhurt for 3 s |
| 8 | **Champion:** +10% damage and +10% max health | **Rallying cry:** hirelings within 15 m deal 10% more while it fights |

### Archer
| Lvl | A | B |
|---|---|---|
| 2 | **Steady aim:** +10% arrow damage | **Quick draw:** shoots 15% faster |
| 3 | **Eagle eye:** sees and shoots 5 m further | **Light feet:** +10% move speed |
| 4 | **Fire arrows:** arrows burn (fire damage over time, lights the target) | **Frost arrows:** arrows slow the target by 30% for 3 s (and 5 frost damage) |
| 5 | **Masterwork bow:** its bow is one quality level higher | **Hunter:** +25% damage to animals; picks up its kills' drops in the field |
| 6 | **Volley:** every 15 s, three arrows in a spread | **Headshot:** 10% chance of double damage |
| 7 | **Marksman:** +20% damage to targets more than 15 m away | **Skirmisher:** keeps its distance while shooting (backs off further and longer, no sidearm swap) |
| 8 | **Rain of arrows:** every 30 s, a volley of 8 arrows over the target's area, each at half damage | **Elite:** +10% damage and +10% max health |

### Wizard
| Lvl | A | B |
|---|---|---|
| 2 | **Focus:** +10% spell damage | **Haste:** casts 15% faster |
| 3 | **Warding:** takes 15% less damage | **Blink:** when an enemy closes within 4 m, it jumps 8 m away (every 10 s) |
| 4 | **Secondary spell:** uses its staff's secondary attack every 20 s (where the staff has one; otherwise a frost nova around it) | **Mender:** every 15 s heals the most hurt player or hireling within 15 m by 10% |
| 5 | **Masterwork staff:** its staff is one quality level higher | **Elemental mastery:** its spell's status effect (burn, freeze, shock) lasts and hurts 30% more |
| 6 | **Protection:** every 60 s, a protective bubble (Staff of Protection) on its owner or itself | **Entangling roots:** every 30 s, roots hold enemies at its target (Staff of the Wild) |
| 7 | **Long reach:** casts from 4 m further | **Chain spell:** each spell also strikes a second enemy within 6 m for half damage |
| 8 | **Archmage:** +15% damage, casts 10% faster | **Raise dead:** every 2 minutes, a skeleton fights beside it for 60 s |

### Woodcutter
| Lvl | A | B |
|---|---|---|
| 2 | **Strong swing:** chops 10% faster | **Pack mule:** +2 cargo slots |
| 3 | **Forester:** plants tree patches twice as often | **Strong back:** +50 carry weight |
| 4 | **Seed saver:** each tree it fells also drops a seed of its kind | **Clean cut:** clears logs and stumps 30% faster |
| 5 | **Masterwork axe:** its axe is one quality level higher | **Lumberjack:** +15% wood from logs |
| 6 | **Fine eye:** prefers fine and core wood trees, +10% of those | **Wide range:** +20% work radius |
| 7 | **Timber!:** a felled tree also knocks down one small tree it falls on | **Swift:** +10% move speed |
| 8 | **Master woodsman:** chops 15% faster and +15% yield | **Quiet work:** chopping makes no noise (draws no monsters) |

### Miner
| Lvl | A | B |
|---|---|---|
| 2 | **Strong swing:** mines 10% faster | **Pack mule:** +2 cargo slots |
| 3 | **Deep digger:** digs to ore up to 2.5 m down (from 1.5 m) | **Strong back:** +50 carry weight |
| 4 | **Prospector:** goes for ore before any stone, searching 30% further for it | **Rock breaker:** plain rock and boulders break 30% faster |
| 5 | **Masterwork pickaxe:** its pickaxe is one quality level higher | **Rich veins:** +15% ore from deposits |
| 6 | **Quiet work:** mining makes no noise | **Swift:** +10% move speed |
| 7 | **Tireless:** keeps working until its cargo is full plus 25% over the weight limit | **Gem eye:** 10% chance of a bonus drop from rock (amber, flint, gems the biome has) |
| 8 | **Master miner:** mines 15% faster and +15% yield | **Heavy hauler:** ore it carries weighs half, so twice as much per trip |

### Steward
| Lvl | A | B |
|---|---|---|
| 2 | **Brisk:** +10% move speed | **Pack mule:** +2 cargo slots |
| 3 | **Keeper of flames:** tops fires and kilns up at 75% instead of half | **Strong back:** +50 carry weight |
| 4 | **Handyman:** repairs from 10 m and needs only 10 s without enemies (from 20 s) | **Bonesetter:** a hurt hireling within 10 m of it, not fighting, regains 2% health a second |
| 5 | **Quartermaster:** stocks the board up to 3 more days | **Tidy mind:** tidies items after 20 s on the ground (from 60 s) and from twice as far |
| 6 | **Smelter's hand:** loads stations with twice as much per trip | **Watchman:** fights like a guard (Defensive stance allowed, +25% damage) |
| 7 | **Errand runner:** does every chore on its way in one trip (up to 4 in a row) instead of one chore per trip | **Fermenter's touch:** fermenters it loads finish 20% sooner |
| 8 | **Master steward:** every chore 15% faster (shorter pauses, 15% more per trip) | **Shield keeper:** keeps shield generators topped up from twice as far |

### Farmer
| Lvl | A | B |
|---|---|---|
| 2 | **Green thumb:** plants and harvests 10% faster | **Pack mule:** +2 cargo slots |
| 3 | **Bountiful:** 25% chance of an extra crop when harvesting | **Seed keeper:** 25% chance of an extra seed when harvesting |
| 4 | **Close planting** (replaced Sower, 2026-10-13): its crops need 15% less room, so a field holds more | **Forager:** picks wild berries and mushrooms in its area even when no order asks |
| 5 | **Swift:** +10% move speed | **Strong back:** +50 carry weight |
| 6 | **Tender care:** crops it plants grow 15% faster | **Hardy stock:** crops it plants survive a little cold or shade (one step outside their biome) |
| 7 | **Bigger fields:** +20% work radius | **Double harvest:** 10% chance a harvested crop drops twice |
| 8 | **Master farmer:** 15% faster and +15% yield | **Replanter:** replants each spot as it harvests it (from the seeds it carries), so the field is never left bare |

### Cook
| Lvl | A | B |
|---|---|---|
| 2 | **Quick hands:** crafts and loads stoves 10% faster | **Pack mule:** +2 cargo slots |
| 3 | **Frugal:** 15% chance an ingredient isn't used up | **Spit master:** loads every free spit in one trip |
| 4 | **Double batch:** 20% chance a craft makes one extra | **Keeper of stoves:** keeps the oven, and the fires under its spits, fuelled at all times |
| 5 | **Swift:** +10% move speed | **Chef's touch:** food it makes pays 10% more upkeep on the board |
| 6 | **Prep chef:** makes intermediate ingredients (dough, mince) ahead, for whatever order is next | **Strong back:** +50 carry weight |
| 7 | **Two stations:** works two stations at once (cooks on the spit while the cauldron crafts) | **Pantry sense:** takes ingredients from chests up to twice as far |
| 8 | **Head chef:** 15% faster, and 5% more chance of saving an ingredient and of an extra (on top of Frugal and Double batch, or on its own) | **Banquet:** food it makes pays 25% more upkeep on the board (with Chef's touch, 35%) |

### Rancher
| Lvl | A | B |
|---|---|---|
| 2 | **Gentle hand:** feeds from 2 m further (more over the fence) | **Pack mule:** +2 cargo slots |
| 3 | **Butcher:** 33% chance of one more meat per slaughter | **Hearty feed:** animals it feeds stay fed 50% longer |
| 4 | **Herder:** leads an escaped animal back into its pen | **Skinner:** 33% chance of one more hide or leather per slaughter |
| 5 | **Swift:** +10% move speed | **Strong back:** +50 carry weight |
| 6 | **Breeder:** animals it feeds breed 15% sooner | **Herd guard:** fights anything attacking one of its animals in its area, with +25% damage while doing so |
| 7 | **Star breeder:** 10% chance a newborn gets one more star (up to 2 stars) | **Big herd:** keeps up to 2 more of each kind before slaughtering (pools +2) |
| 8 | **Master rancher:** 15% faster, and 10% more chance of an extra meat and of an extra hide (on top of Butcher and Skinner, or on its own) | **Wrangler:** tames wild animals of a kind it keeps when they wander into its area (one every 5 minutes) |

## Decisions
- **Pick levels** → every level from 2 to 8 (seven choices); skipped levels' choices are owed.
- **Tree shape** → a fixed pair at each level per job, the same whatever was picked before.
- **Choosing** → in the hireling's Shift+E panel, any time; owed choices flagged on the hover and the Roster tab.
- **Respec** → paid, at the board, resetting all of one hireling's picks.
- **Effect kinds** → both: mostly a number boost against a new behaviour at the same level; behaviours mainly at 4, 6, 8.
- **Config** → which pairs exist is in code; their numbers are in the data file; one server switch turns it all off.
- **Power** → modest: a fully enhanced level 8 about 25% stronger than an unenhanced one.
- **Who picks** → anyone with ward access to the board; a follower's owner while it follows them.
- **Scope** → all nine jobs in the first version.
- **Ideas** → I draft the full catalog; Tim edits it at review.
- **Storage** → on the contract (authoritative), copied to the hireling's own record for the game simulating it.
- **Release** → one 0.10.0 with all nine trees, built and tested phase by phase on a branch.
- **Confirm step** → yes: a click selects, a second click confirms.
- **Testing** → a test row per behaviour; number boosts by unit tests plus rows reading each job's values (about 80 rows).
  Hardy stock (needs a real biome edge) and the look of effects are checked by hand.
- **Ward check** → as today, in the panel (the panel and board only open with ward access); no new board-side check.
- **Respec cost** → half the hireling's hire fee at its current level, from the board's funds (`respecCostFraction` 0.5).
- **Better gear (level 5, "Better X")** → "Masterwork": the weapon or tool is one item quality higher, at any level (no next-tier items).
- **Miner level 8 B** → Heavy hauler (ore weighs half), replacing Silver sense (nobody needs silver by then).
- **Steward level 4 B** → Bonesetter (heals hirelings near it), replacing Beekeeper.
- **Farmer level 8 B** → Replanter, replacing Self-sufficient (seed orders already come first).
- **Rancher level 3 B** → Hearty feed, replacing Egg finder (eggs only come with Plains hens, and it wasn't that useful).
- **Steward level 7 A** → Errand runner, replacing Swift (the Steward already had +10% move speed as Brisk at level 2).
- **Cook level 8 B** → Banquet (food it makes pays 25% more upkeep), replacing Feast maker (feasts aren't made by any
  recipe the Cook can follow today).
- **Rancher level 6 B** → Herd guard, replacing Gate keeper (it would have made getting into pens harder).
