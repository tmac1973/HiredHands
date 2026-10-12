# Hired Hands

Build a **Hiring Board** at your base and post contracts to hire wandering vikings. They chop wood, mine, farm, cook,
keep your animals, look after your base (fires, smelters, beehives, repairs and more) and guard it. Hire them for coins
(level 1 for food: there are hardly any coins in the Meadows) and feed them every day. Upgrade the board with boss
trophies to hire better vikings and more of them. Craft a **Command Stone** to take them out with you: they follow you
through portals and dungeons and aboard ships, gather and fight on your orders, and find their own way home.

Works in single-player and on dedicated servers. Every client and the server need the mod.

!!! warning "In active development"
    Hired Hands changes quickly: new versions come out often, sometimes several a day, and some change how hirelings
    behave or what the data file holds. Read the [changelog](https://github.com/tmac1973/HiredHands/blob/main/CHANGELOG.md)
    when you update, and update the server and every player together.

!!! note "About balance"
    I play with mods that make Valheim harder, such as Creature Level and Loot Control (CLLC), which gives monsters more
    stars and health, so the hirelings are tuned not to tip that kind of game too far. On a normal game without
    difficulty mods, guards and archers may make fights too easy. If so, turn their fighting down (server-synced, in the
    Combat section):

    - `GuardMeleeDamage` and `GuardRangedDamage`, for example 0.5 to halve their damage;
    - `CombatSkill`, from Trained to Green, so they block and dodge less often;
    - `HealthRegen`, below 1, so they heal more slowly between fights;
    - or `CombatHirelings` off (Hiring), for workers only.

## Requirements

- BepInExPack Valheim
- Jotunn 2.30+
- YamlDotNet (ValheimModding)
- Optional: AzuAutoStore, AzuCraftyBoxes, PetPantry, Torches Eternal, PlantEverything, PlantEasily, Therzie's Wizardry
  (see [Other mods](compatibility.md)).

## Where to start

- New to the mod? [Getting started](getting-started.md) takes you from an empty base to your first hireling.
- [Jobs and chests](jobs.md) lists who does what, and how they use your chests.
- The workers with the most to them have their own pages: [the Steward](steward.md),
  [the Farmer and the Cook](farmer-cook.md) and [the Rancher](rancher.md).
- [Fighting and wizards](combat.md) and [Followers and the Command Stone](followers.md) cover guards and taking
  hirelings out with you.
- Server owners: [Configuration](configuration.md) and [Board levels](board.md).

## Coming next

Ideas being considered: a Healer (with Therzie's Wizardry healing spells), fighting tactics (tank, hit and run, shooting
from cover), skill trees for combat hirelings, meal plans (more food for stronger hirelings), patrol markers for base
guards, a scout. Balance tuning from the balance logs.

## License

MIT; see [LICENSE](https://github.com/tmac1973/HiredHands/blob/main/LICENSE).
