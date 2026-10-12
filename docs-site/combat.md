# Fighting and wizards

Guards patrol and fight by stance; everyone else defends themselves, weakly, and flees or defends by stance.

## Stances

- **Passive:** fights back only when attacked.
- **Defensive:** fights anything in its radius, anyone attacking a friendly nearby, and anything a player has just hit
  near it.
- **Aggressive:** the same, and chases a little past its radius.

Workers have their own stances: **Flee** (runs from danger toward home) and **Defend** (fights what comes close).

## Keeping watch

Guards watch all round to their full sight (30 m), whichever way they face, and see the attacks coming at them. Other
hirelings notice anything within 20 m in line of sight, and further off what's in front of them. Guards also join in on
an enemy a player has just hit near them (within 30 m), so they help with what you're fighting.

A guard posted to a spot (a tower, a gate) stands watch there and walks back to its post after a fight. Archers hold
fire when the shot is blocked; an archer or wizard following you with no clear shot for 10 seconds (round a crypt's
corner, behind a wall) drops that target and carries on.

## Blocking, parrying and dodging

- **Blocking.** A guard with a shield raises it just before a swing it saw coming lands, and turns into arrows, thrown
  rocks and spears flying at it. A well-timed block is a **parry** ("Parry!" pops up): it blocks with extra force and
  staggers the attacker, as yours does. A swing the guard didn't see coming lands, or gets a late block at best.
- **Dodging.** Any hireling in a fight (guards, archers, workers) rolls out of the way of a hit that would take more than
  a quarter of its health, or of an explosion: the player's roll, and the hit misses while it lasts. Only towards safe
  ground (no ledges, no deep water, nothing in the way), and not again straight away.
- **Levels.** Higher levels see more attacks coming, parry more often, dodge more often and roll again sooner. Level 1
  sees about half of the attacks, parries a few and rolls again after 6 s; level 8 sees nearly all, parries most and
  rolls again after 2 s. The numbers are in the data file's levels table (`readChance`, `parryChance`, `dodgeChance`,
  `dodgeCooldown`).
- **How good they are** is the server's choice: `CombatSkill` (Combat, server-synced) is one of four presets.
    - **Off:** the 0.5.0 fighting.
    - **Green:** they block and roll now and then; you still have to look after them.
    - **Trained** (default): the levels table as tested.
    - **Veteran:** they parry most swings and roll often.

    The preset scales the whole levels table, so higher levels stay better than lower ones.

## Damage and healing

- **How hard they hit.** About like a player with the same weapon: their gear gets better with level, and the levels
  table's `guardDamageMult` adds a little on top (1.0 at level 1 to 1.3 at levels 7-8). `GuardMeleeDamage`,
  `GuardRangedDamage` and `WizardDamage` (Combat, server-synced) scale that; the default is 1, 0.5 halves it.
- **Never on you.** No hireling's hit hurts a player, whatever PvP is set to: a stray splash or a spell's blast landing by
  its target spares you.
- **Healing.** A hireling that hasn't been hit for 10 s heals 1% of its health every 2 s, a full heal in about 3.5
  minutes. `HealthRegen` (Combat, server-synced) scales that: 0.5 is about 7 minutes, 0 means no healing of their own
  (only the game's slow trickle), and the maximum is 3.
- **Something it can't hurt** (a creature stuck in the ground, one that's immune): after 25 s of attacking with no effect
  it's left alone for a minute and the hireling picks another target.

## Wizards

A wizard is a combat hireling (it counts against the board's combat cap) that fights with a staff's main spell:

- **When:** from a level 6 board, when the Mistlands and the Galdr table's staffs open up. With **Therzie's Wizardry**
  installed, from a level 2 board, as soon as Wizardry's first staff can be crafted in the Black Forest.
- **How it fights:** it casts from 10 to 22 m, one spell every 3 s (`WizardAttackCooldown`), and steps back from anything
  that gets within 8 m: strong at range, fragile up close. No sidearm and no shield; it dodges like everyone. Casting
  costs it nothing (no eitr bar). It takes stances, posts, orders and following like a guard or archer.
- **Staffs by level:**

    | Level | With Wizardry | Without |
    |---|---|---|
    | 2 | Evergrowth | |
    | 3 | Wizardry's Swamp staff | |
    | 4 | Wizardry's Mountain staff | |
    | 5 | Wizardry's Plains staff | |
    | 6 | Wizardry's Mistlands staff | Staff of Embers |
    | 7 | Dundr | Staff of Frost |
    | 8 | Dundr | Dundr |

    Only the main attack is used: no conjured roots, meteors, healing or summons.

- **Robes:** Wizardry's Spellslinger robes and capes by biome; without it, Eitr-weave robes from level 6 and Embla robes
  at 7-8. Looks only: a hireling's armour comes from its level.
- **What spells may hit:** enemies only, never players, hirelings, tames or buildings. `WizardSpellsHit` (Combat) decides
  props: `EnemiesOnly` (default), or `EnemiesAndTerrain` to let a blast damage breakable props like bushes and barrels,
  as your own staff would (greydwarf nests, trees and rocks shrug off spells either way).
- **Price:** 1.5x the base hire fee and upkeep (guards and archers: 1.3x).
- **If Wizardry is removed:** wizards already hired stay and carry the vanilla staff for their level (the Staff of Embers
  below 6); new wizards then need a level 6 board.
- **In the data file:** the wizard's staffs (`gear`), robes (`outfit`, with `cape`) and unlock level, and the same again
  under `withMods` for Therzie.Wizardry, which applies while that mod is loaded. Any job can have `withMods` entries.
