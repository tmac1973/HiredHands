# Getting started

1. **Build a base.** The board can only be placed with a workbench, a bed and 40 built pieces within 20 m (all
   configurable), or within the board's own area when you place a higher-level one from a Hiring Charter (30 m for a
   level 3 board). Boards must be 100 m apart.
2. **Build the Hiring Board** with the hammer (Wood 45, Stone 20, Deer hide 10, Leather scraps 10, Resin 10).
3. **Stock it.** Use the board's *Funds (food & coins)* to put in cooked food and coins. Only food and coins fit, in 24
   slots. Food is counted in *food points* (a food's health + stamina + eitr: a cooked meat is 40), and the cheapest
   food is used first so your best food is left alone.
4. **Post a contract** under *Manage → Contracts*: choose the job, the level, the work radius (it starts at the most the
   job allows) and, for guards, a stance. The hire fee is taken from the board's funds, and a viking walks in a few
   minutes later.
5. **Feed them.** Upkeep is paid in food from the board every in-game day, only while the base is loaded (days away are
   free). If the board can't pay for 2 days, that hireling quits.

## What it costs

Hiring is paid in **coins** (level 1 in food, since there are hardly any coins in the Meadows); upkeep is always
**food**. Promoting a hireling costs the difference in hire fees, and bringing a dead one back costs half its hire fee
(or turn on permadeath). Each job scales the prices: Steward ×0.9, Woodcutter ×1, Miner ×1.1, guards ×1.3, wizards ×1.5.

| Level | Hire fee | Upkeep a day | Health | Cargo slots | Carries (weight) |
|---|---|---|---|---|---|
| 1 | 150 food | 40 food | 80 | 8 | 300 |
| 2 | 50 coins | 60 food | 120 | 10 | 325 |
| 3 | 150 coins | 90 food | 180 | 12 | 350 |
| 4 | 300 coins | 120 food | 250 | 16 | 375 |
| 5 | 500 coins | 160 food | 330 | 20 | 400 |
| 6 | 800 coins | 200 food | 420 | 24 | 425 |
| 7 | 1200 coins | 250 food | 520 | 28 | 450 |
| 8 | 1800 coins | 300 food | 650 | 32 | 475 |

A crew adds up: six level 3-4 hirelings eat about 700 food points a day. Richer food (stews, pies, meads) pays more
wages per board slot than cooked meat.

## Running low

- **The board's hover** shows the daily upkeep and how many days its food and coins last, orange when it's low and red
  on the last day. A full board that's low says richer food would pay more per slot.
- **Coming home** to a board that can't pay tomorrow's wages tells you so (again after 10 minutes).
- **Once a day**, when it's down to 2 days (`LowFundsWarnDays`), everyone at the base is told; when a day's wages go
  unpaid, they're told how many went unpaid, what the board needs and has, and after how many days they quit.
- **The board's builder** gets a map pin on it until it's topped up.
- **A Steward** keeps the board stocked from your chests (see [the Steward](steward.md)).

## Hirelings

Hover one to see its name, job, level, stance, health, what it's doing and its cargo (slots and weight). Open its cargo
with E like a chest; Shift+E opens its orders panel (stance, rename, and more for gatherers and followers) and its
**Problems** list: why its orders aren't being met. A hireling carries about what you can: it's full when its slots or
its weight limit run out, and a gatherer then goes to deliver.

Can't find one? **Call to board** on the board's Roster tab brings it to the board to wait there (off work) until you
press **Back to work** (there or in its Shift+E panel); a hireling with no way there is moved there. It holds still while
its Shift+E panel is open. A hireling stuck swimming in place (against a bank, under a dock) is lifted onto dry ground
after 30 seconds.

**They talk back.** Now and then a hireling near you says something, in a speech bubble like the trader's: about its
job, about what it's doing, or about you standing there watching. `HirelingChatter` turns it off and `ChatterMinutes`
(default 5) sets roughly how often each one speaks; both are your own settings.

**Workers only work while their area is loaded**, that is while a player is nearby, as with everything else in Valheim.

## Deaths

When a hireling dies, its cargo goes into a **grave** named after it, which floats and lasts like a player's (it doesn't
despawn). A follower's grave opens only for its owner, who gets a map pin on it; a base worker's opens for anyone with
ward access there. Its weapon and armour aren't dropped. You're told when one of yours dies. What happens next is the
server's choice (`DeathMode`, General):

- **ReturnAfterDays** (default for new installs): it comes back to the board by itself, free, after `ReturnAfterDays`
  in-game days (3; a day is 30 minutes of play, and sleeping skips the night).
- **PayToRespawn:** it comes back after `RespawnCooldownSeconds` once the board pays half its hire fee
  (`RespawnCostFraction`).
- **Permadeath:** it's gone, and the contract ends.

Either way its slot on the board stays taken while it's away. Configs from older versions keep what they had
(`PermadeathEnabled` on becomes Permadeath, off becomes PayToRespawn).
