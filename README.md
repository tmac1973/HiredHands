# Hired Hands

Build a **Hiring Board** at your base and post contracts to hire wandering vikings. They chop wood, mine, farm, cook,
keep your animals, look after your base (fires, smelters, beehives, repairs and more) and guard it. Hire them for coins
(level 1 for food) and feed them every day. Upgrade the board with boss trophies to hire better vikings and more of them.
Craft a **Command Stone** to take them out with you: they follow you through portals and dungeons and aboard ships,
gather and fight on your orders, and find their own way home.

Works in single-player and on dedicated servers. Every client and the server need the mod.

**📖 Full documentation: [tmac1973.github.io/HiredHands](https://tmac1973.github.io/HiredHands/)**

> **In active development.** New versions come out often, and some change how hirelings behave or what the data file
> holds. Read the [changelog](https://github.com/tmac1973/HiredHands/blob/main/CHANGELOG.md) when you update, and update
> the server and every player together.

> **About balance.** Hirelings are tuned for games made harder by mods such as Creature Level and Loot Control. On a
> normal game, guards may make fights too easy: turn down `GuardMeleeDamage` / `GuardRangedDamage`, set `CombatSkill` to
> Green, or turn `CombatHirelings` off for workers only.

## Requirements

- BepInExPack Valheim
- Jotunn 2.30+
- YamlDotNet (ValheimModding)
- Optional: AzuAutoStore, AzuCraftyBoxes, PetPantry, Torches Eternal, PlantEverything, PlantEasily, Therzie's Wizardry
  ([how they work together](https://tmac1973.github.io/HiredHands/compatibility/)).

## Quick start

1. **Build a base**: a workbench, a bed and 40 built pieces within 20 m.
2. **Build the Hiring Board** with the hammer (Wood 45, Stone 20, Deer hide 10, Leather scraps 10, Resin 10).
3. **Stock it** with cooked food and coins (its *Funds* tab).
4. **Post a contract** under *Manage → Contracts*; a viking walks in a few minutes later.
5. **Feed them**: upkeep is paid in food from the board every in-game day. Unpaid for 2 days, a hireling quits.

More in [Getting started](https://tmac1973.github.io/HiredHands/getting-started/): costs, warnings, deaths.

## Jobs

| Job | Does | Board level |
|---|---|---|
| [Woodcutter](https://tmac1973.github.io/HiredHands/woodcutter-miner/) | Fells trees and clears logs; plants tree patches | 1 |
| [Miner](https://tmac1973.github.io/HiredHands/woodcutter-miner/) | Mines ore, rocks and boulders, never near your buildings | 2 |
| [Steward](https://tmac1973.github.io/HiredHands/steward/) | Fires, beehives, smelters, mills, repairs, fermenters, the board's food | 1 |
| [Farmer](https://tmac1973.github.io/HiredHands/farmer-cook/) | Plants and harvests your field to the board's orders | 2 |
| [Cook](https://tmac1973.github.io/HiredHands/farmer-cook/) | Spits, oven, cauldron, prep table, mead ketill, to the board's orders | 2 |
| [Rancher](https://tmac1973.github.io/HiredHands/rancher/) | Feeds your tame animals, collects eggs, slaughters above a breeding pool | 2 |
| [Guard, archer](https://tmac1973.github.io/HiredHands/combat/) | Patrol and fight by stance; block, parry and dodge | 1 |
| [Wizard](https://tmac1973.github.io/HiredHands/combat/#wizards) | Casts a staff's spell from range | 6 (2 with Wizardry) |

Each hireling picks one of two [enhancements](https://tmac1973.github.io/HiredHands/enhancements/) at every level from 2 to 8.

## Documentation

- [Getting started](https://tmac1973.github.io/HiredHands/getting-started/)
- [Jobs and chests](https://tmac1973.github.io/HiredHands/jobs/) ·
  [Enhancements](https://tmac1973.github.io/HiredHands/enhancements/)
- [The Woodcutter and the Miner](https://tmac1973.github.io/HiredHands/woodcutter-miner/) ·
  [The Steward](https://tmac1973.github.io/HiredHands/steward/) ·
  [The Farmer and the Cook](https://tmac1973.github.io/HiredHands/farmer-cook/) ·
  [The Rancher](https://tmac1973.github.io/HiredHands/rancher/)
- [Fighting and wizards](https://tmac1973.github.io/HiredHands/combat/)
- [Followers and the Command Stone](https://tmac1973.github.io/HiredHands/followers/)
- [Doors, stairs and ladders](https://tmac1973.github.io/HiredHands/navigation/)
- [Board levels and moving a board](https://tmac1973.github.io/HiredHands/board/)
- [Configuration](https://tmac1973.github.io/HiredHands/configuration/) ·
  [Other mods](https://tmac1973.github.io/HiredHands/compatibility/) ·
  [Bug reports and commands](https://tmac1973.github.io/HiredHands/troubleshooting/)

## Bug reports

Open an issue on [GitHub](https://github.com/tmac1973/HiredHands/issues) with what happened and your
`BepInEx/HiredHands.log`. See [Bug reports and commands](https://tmac1973.github.io/HiredHands/troubleshooting/).

## License

MIT; see [LICENSE](https://github.com/tmac1973/HiredHands/blob/main/LICENSE).
