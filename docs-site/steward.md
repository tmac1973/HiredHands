# The Steward

A Steward works inside its board's radius from the chests there, taking on more chores as it levels up. Each chore
unlocks with the biome whose boss makes its resources available:

| Level | Chores it gains |
|---|---|
| 1 | **Fires and lights** (fires, torches, braziers, hot tubs: fuelled when below half), **beehives** (emptied at half full), **tidying up**, **board food** (stocks its board from the chests) |
| 2 | **Smelters and charcoal kilns** (loaded and fuelled, bars collected), **tamed animals** (fed when hungry) |
| 3 | **Repairs** of damaged building pieces |
| 4 | **Fermenters** (loaded with a mead base, tapped when ready) |
| 5 | **Blast furnaces, windmills and spinning wheels** |
| 6 | **Eitr refineries, sap collectors** |
| 7 | **Shield generators** (fed bones) |
| 8 | nothing new; more health and cargo |

- **Most urgent first.** Every few seconds it looks over everything it may do and takes the most urgent job (an empty
  fire before a half-full smelter, anything before tidying), nearer jobs first when it's close.
- **You can see them at it:** a puff of dust at a chest when a hireling puts something in or takes something out
  (`ChestPuffs`, your own setting, turns it off).
- **From your chests only.** It never takes the last of an item from a chest, and leaves a reserve of chosen items in
  storage (`keepInStorage`, default 50 Wood). What it collects (bars, honey, sap, meads, flour, thread, tidied items)
  goes into chests that already hold that item.
- **Shift+E** on a Steward lists every chore with its state (on, off, locked until level N, handled by another mod) and
  switches each one on or off for that Steward. Its hover and the Roster tab say what it's doing ("Fuelling Hearth") or
  what's missing ("Smelter: no Coal in any chest").

## Board food

When its board has less than 3 days of upkeep left in food, it brings food from the chests up to 7 days
(`StewardBoardRefillDays`, `StewardBoardFillDays`), only food the board accepts (raw food only with `AllowRawFood`).

It brings the **cheapest food first**, so your best food stays put, as long as the board's free slots can still hold
the full stock that way. When they can't (a big crew, or cheap food filling the board), it brings richer food instead.
If even the richest food in the chests can't stock the board that far, its Problems list says how many days of wages the
board holds.

## Fires and torches

Fuelled when below half (`StewardFireRefillFraction`). A torch it can't reach (placed high, for looks) is remembered after
two tries and left alone for the session, and its Problems list names it so you fuel it yourself. **Shift+E on a fire or
torch** near a board marks it for workers to leave alone altogether.

## Repairs

Repairs follow your rules: a crafting station for the piece in range, ward access, and free as for you. They wait until
no enemy has been within 30 m of the Steward or the piece for 20 s. Wood out in the rain without a roof, or standing in
water, wears down to half health and no further, so the Steward leaves such pieces alone until something takes them
below half; a piece that keeps wearing soon after a repair with no enemy about (weak support) is left for an hour. It
repairs from where a player could (5 m, in sight), and skips a piece it can't get to for 30 minutes ("Repairs: can't get
to …").

## Orders

Like the Farmer, Cook and Rancher, the Steward works to **orders**: Shift+E on it, then **Orders**. It makes coal, bars,
flour, linen, honey, sap, meads, and anything a modded station makes, but only what it has an order for:

- **Everything else: ∞** is where it starts: it makes everything it can, as it always did. Remove it and it makes only
  what you've ordered.
- **An order with a number** ("keep 200 Coal") stops it once the chests in its area hold that many (a beehive at that
  point is left full; a fermenter isn't loaded with that mead's base); below it, it makes more.
- **∞** ("Iron bars: ∞") means never enough: it smelts all the ore it can find.
- Paused, it makes none of it.

Its Problems list says when it leaves a station because there's no order ("Smelter: no order for Iron") or the order is
met. Orders are kept on the board, so a new Steward follows them. Each chore, kilns included (apart from smelters), can
also be switched off per Steward.

## Other chores

- **Tamed animals** get one item they eat, dropped beside them on a side with no wall between (AzuAutoStore leaves it
  alone). With a [Rancher](rancher.md) on the same board, the Steward leaves feeding to him.
- **Fermenters** need cover as for you (a roof and mostly walled in); the Steward says when one doesn't have enough
  instead of loading it.
- **Tidying up** puts items that have lain on the ground for a minute away in chests that already hold them. It's the last
  thing it does.
- **In the field:** a Steward you take along with the Command Stone and park in **Gather here** picks up loot lying
  within `LootRadius` (30 m) of its spot (monster drops, trophies, coins), until its cargo is full. Back home it puts it
  away like other leftovers.
- **Other mods:** with **PetPantry** (animals eat from chests) the Steward leaves animals alone, and with **Torches
  Eternal** (fires never burn out) it leaves fires alone.

## Server settings

In *10 - Steward*: each chore can be turned off for everyone (`StewardFires`, `StewardBeehives`, `StewardStations`,
`StewardMills`, `StewardSap`, `StewardAnimals`, `StewardRepairs`, `StewardFermenters`, `StewardShields`, `StewardTidy`,
`StewardBoard`), plus `StewardFireRefillFraction` (0.5), `StewardRepairBelow` (0.95), `StewardRepairQuietSeconds` (20)
and `StewardTidyMinSeconds` (60). The levels are `choreLevels` in the data file.
