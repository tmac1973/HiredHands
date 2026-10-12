# Board levels and moving a board

| Level | Upgrade needs | Combat | Workers | Max radius |
|---|---|---|---|---|
| 1 | (build cost) | 1 | 2 | 20 m |
| 2 | Eikthyr trophy, Deer hide 30, Flint 20, Wood 50 | 1 | 4 | 25 m |
| 3 | The Elder trophy, Bronze 10, Core wood 40, Troll hide 5, Greydwarf eye 20 | 2 | 6 | 30 m |
| 4 | Bonemass trophy, Iron 20, Ancient bark 40, Guck 10, Withered bone 10 | 2 | 8 | 35 m |
| 5 | Moder trophy, Silver 20, Freeze gland 10, Wolf pelt 10, Obsidian 20 | 3 | 8 | 40 m |
| 6 | Yagluth trophy, Black metal 20, Linen thread 20, Needle 20, Lox pelt 5 | 3 | 8 | 45 m |
| 7 | The Queen trophy, Black core 3, Eitr 15, Yggdrasil wood 40, Carapace 20 | 4 | 8 | 50 m |
| 8 | Fader trophy, Flametal 20, Blackwood 40, Asksvin hide 10, Molten core 3 | 4 | 8 | 60 m |

A board hires up to its own level. Higher-level hirelings have more health and armour, better gear, work faster and carry
more, and cost more (see [what it costs](getting-started.md#what-it-costs)).

**The board's area** is its max radius: look at the board (or open its panel), or place one with the hammer, and it's
drawn on the ground as a gold circle. Woodcutters and miners work twice that.

**The board's storage** holds 24 slots of food and coins, like an iron chest.

## How many hirelings

A board has two caps:

- **Combat:** guards, archers and wizards in any mix.
- **Workers:** everyone else, with at most 2 woodcutters, 2 miners, 1 Steward, 1 Farmer, 1 Cook and 1 Rancher per board.

So a base can be fully worked from a level 4 board, while the fighting force stays small. A contract on its way out still
counts against its cap until the hireling has gone, though a replacement for its job can already be hired.

- **Upgrading from an older version:** boards that already have more than the caps allow keep their hirelings; only new
  contracts of that kind wait.
- **Changing the numbers:** server owners can change them in the data file (`combatCap` and `workerCap` per board level,
  `maxPerBoard` per job, where 0 means no limit).
- **Workers only:** a server can turn combat hirelings off (`CombatHirelings`, Hiring): no guards, archers or wizards can
  be hired, and the board offers workers only. Ones already hired stay.

## Moving a board

Deconstruct it with the hammer and you keep a **Hiring Charter**. It remembers the board's level (from level 2) and
carries its hirelings, packed away with their gear, cargo, names and contracts, followers included. Build the new board
while carrying the charter:

- it starts at that level, so you don't have to fight the bosses again;
- the hirelings walk back in, like new hires.

A level 1 board gives a charter too when it has hirelings. The board's stored food and coins drop like any chest's. A
board destroyed by monsters or damage gives no charter, and its hirelings leave.
