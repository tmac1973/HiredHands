# Rancher hireling — Project Overview

## Problem
Players tame boars, wolves, lox, hens and modded animals for meat, but keeping a herd is all manual: feed them, wait for
them to breed, pick which ones to kill, carry the meat to the chests. The Steward already feeds tames (its Animals chore),
but nothing in Hired Hands manages a herd: no worker slaughters for meat, keeps a breeding stock, or breeds toward
better (higher-star) animals. A base's meat supply is the one food chain hirelings can't run.

## Goals
- **A new worker job, the Rancher:** feeds the herd and slaughters animals for meat, following the patterns of the
  Farmer, Cook and Steward (chores on the chore loop, board orders, Shift+E panel with the Problems view, one per board,
  board level 2, priced like the Farmer and Cook at 0.9x).
- **Any breeding tame:** the animals are found at run time: every creature that can be tamed, breeds (`Procreation`) and
  drops meat. Vanilla boars, wolves, lox, hens and asksvin, and modded animals, without a hard-coded list.
- **The herd is the tames of a managed kind inside the board's work area** (the area other workers use, with the ward
  check workers already use).
- **Orders on the board's Orders tab,** a new "Ranch" group beside farm and kitchen, shared by everyone who can use the
  board:
  - **Meat orders:** "keep at least N" of a meat item (raw meat, wolf meat, lox meat, chicken meat, modded meats).
  - **Egg orders:** "keep at least N eggs" for animals that lay eggs (hens).
  - **Breeding pool orders:** "keep N of this kind alive", minimum 2 (fewer can't breed).
- **Slaughter only when an animal's meat is short and its kind is above its breeding pool.** With the meat stocked the
  extra animals just live.
- **Selective breeding:** the breeding pool is always the highest-star adults; the Rancher slaughters the lowest-star
  adult first. Young animals (piglets, cubs, chicks) are never slaughtered. Babies take their mother's stars, so the
  herd climbs to the best stars the player has.
- **Named tames are pets:** fed, never slaughtered, and not counted in the breeding pool.
- **No breeding pool order, no slaughter** for that kind; a meat order with no pool for any animal that drops it says so
  in the Problems view ("set a breeding pool for boars first").
- **Feeding:** the Rancher feeds the herd (reusing the Steward's feeding). A Steward on the same board skips its Animals
  chore while the board has a Rancher. With PetPantry installed, the Rancher also steps aside from feeding (as the
  Steward does) and only slaughters.
- **Drops:** after a slaughter the Rancher collects everything dropped (meat, leather, pelts, feathers, trophies) and
  delivers it to the chests like other workers, a drop pile by the board when there's no room.
- **Eggs:** while the egg order is short he collects eggs to the chests; once it's stocked he leaves eggs where they lie
  so they hatch and the flock grows toward its pool. The Steward's tidy chore leaves eggs alone on a board with a
  Rancher.
- **Breeding problems reported:** his Problems list says why a kind isn't breeding (pen too crowded, no partner nearby,
  hungry, no food in the chests). He doesn't move animals.
- **Gear:** the vanilla Butcher knife (`KnifeButcher`) at every level. Levels give what every worker's do (work radius,
  carrying, health, wage); every animal kind is handled from level 1.
- **Chatter:** his own pool of lines, like other jobs.
- **Ships as 0.9.0.**

## Non-goals
- **Taming** wild animals (maybe later).
- **Moving or herding animals,** building or managing pens, or putting animals into pens.
- **Culling for space:** no herd maximum; nothing is slaughtered while its meat is stocked.
- **Slaughtering young animals,** named tames, or any kind without a breeding pool order.
- **Hatching eggs** (warming or carrying them anywhere); leaving them to hatch is all.
- **Other products** than meat and eggs as orders (leather, pelts, feathers are collected but not ordered).
- **Changes to vanilla breeding** (rates, crowding limits, offspring stars).
- **Combat changes:** combat hirelings still never target or hurt tames.

## Users & primary flow
**Players:**
1. With a level 2 board and a few tamed boars in its area, the player posts a Rancher contract (Contracts tab, "Rancher").
2. On the board's Orders tab, under Ranch, they add "Boar: keep 4 breeding" and "Raw meat: keep 40".
3. The Rancher feeds the boars from the chests. They breed; piglets grow up.
4. With 6 adult boars and raw meat at 12, he slaughters the lowest-star adult boar with his butcher knife, picks up the
   meat and leather scraps, and puts them in a chest. He repeats until raw meat reaches 40 or only the 4 best boars
   are left.
5. The player names their favourite boar: from then on the Rancher feeds it but never slaughters it, and it doesn't
   count toward the 4.
6. With hens and "Eggs: keep 20" set, he collects eggs until there are 20, then leaves the rest to hatch.
7. When something is stuck, the Rancher's Shift+E Problems list says why: "Raw meat: no boars above the breeding pool
   (4)", "Boars: pen too crowded to breed", "Wolves: set a breeding pool first".

**Server owners:** toggle the Rancher's chores (feed, slaughter, eggs) in the config like other chores, and set the job's
level, price and gear in the data file.

## Constraints
- **Where it fits in the code:**
  - `Core/JobType.cs` (new `Rancher`, a worker: not `IsCombat`);
  - `Core/Data/DefaultData.cs` / `VfhData.cs` (job entry: `MinBoardLevel` 2, `CostMult` 0.9, `MaxPerBoard` 1, gear);
  - `Core/Chores/ChoreKind.cs`, `ChoreRules.cs` (`ChoresFor`), `ChoreLoop.ServerAllows` and `Config/VfhConfig.cs` toggles;
  - `Hirelings/HirelingAI.cs` (wiring a `ChoreLoop` for the job), `Hirelings/HirelingChatter.cs`, `Localization/English.json`;
  - `Core/Orders/ProductionOrder.cs` / `OrderList.cs` (new order kinds, with new serialization codes; older boards
    without them still load), `Board/BoardOrders.cs`, `UI/OrdersTab.cs` (a Ranch picker);
  - `Hirelings/Work/Steward/AnimalsChore.cs` (shared feeding), `Compat/StewardCompat.cs` (PetPantry), the Steward's Tidy
    chore (eggs);
  - `Hirelings/DamagePatches.cs` (the hireling-on-tame block).
- **Slaughter in multiplayer:** a hit on a creature is checked on the game running that creature, so the exception to
  the tame block is by job: a Rancher's hits on a tame always land (combat never targets tames, so only his deliberate
  slaughter hits one). Every other hireling's hits on tames stay blocked.
- **Pure rules in Core with unit tests,** like the Farm and Kitchen planners: given the herd (kind, stars, adult, named,
  position), the orders and the stock, which animal to slaughter next, what to collect, and why each order isn't met.
- **Animal facts from the game at run time:** which prefabs are tameable breeders, their meat (from their drops), their
  offspring (young prefab, egg item) and crowding limits (`Procreation.m_maxCreatures`, `m_totalCheckRange`,
  `m_partnerCheckRange`), so modded animals work without data entries.
- **Data file compatibility:** a new job and order kinds; older data files get the job filled in from the defaults
  (`DataDefaults.FillMissing`). Everyone updates together.
- **Testing:** single-player rows through ModTestBridge in the dev profile, run when the dev game is free (not while
  Tim plays on the live server).

## Success criteria
- **Unit tests** cover:
  - which animal is slaughtered: lowest stars first, adults only, never named, never below the pool, only while the
    meat is short;
  - no slaughter for a kind without a pool order; pool minimum 2;
  - egg collecting stops at the egg order;
  - the Problems reasons per order (meat short with no animal above the pool, no pool order, crowded pen, no partner,
    hungry, no food);
  - order serialization round-trips with the new kinds, and older order strings still parse.
- **In-game rows** (ModTestBridge, single player):
  - a level 2 board takes a Rancher contract; he holds the butcher knife;
  - he feeds hungry boars, and a Steward on the same board doesn't;
  - with a pool of 2 and raw meat short, 4 boars (1, 2, 2 and 3 stars) become the 3-star and one 2-star, and the meat
    and leather end up in a chest;
  - with the meat order met, nothing is slaughtered;
  - a named boar is never slaughtered;
  - with no pool order, nothing is slaughtered and Problems says why;
  - eggs are collected up to the egg order and then left;
  - another hireling (a guard) still can't hurt a tame.
- **Existing rows still pass** (Steward animals, farm, kitchen, combat).
- **By hand (Tim):** on the live server, a Rancher works a real herd for a session.
- **The README** describes the Rancher; the CHANGELOG has 0.9.0.

## Decisions
- **Which animals** → any breeding tame, found at run time (vanilla and modded).
- **What counts as the herd** → tames of a managed kind inside the board's work area.
- **Named tames** → pets: fed, never slaughtered, not counted in the pool.
- **Where meat and pool targets are set** → the board's Orders tab, a new Ranch group.
- **When to slaughter** → only while the animal's meat is short and its kind is above its breeding pool.
- **Which one to slaughter** → lowest stars first, adults only; the pool is the highest-star adults.
- **Who feeds** → the Rancher; a Steward on the board steps aside; with PetPantry, both step aside.
- **Drops** → collect everything and deliver it to the chests.
- **Unlock** → board level 2, priced 0.9x, one per board.
- **What levels change** → the usual worker stats only; every kind from level 1.
- **Gear** → the vanilla Butcher knife.
- **Eggs** → an egg order: collect while short, then leave eggs to hatch (the Steward's tidy leaves them too).
- **A kind with no pool order** → never slaughtered; Problems says to set one.
- **Crowded or idle pens** → reported in Problems; the Rancher doesn't move animals.
- **Release** → 0.9.0.
