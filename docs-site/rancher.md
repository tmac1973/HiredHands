# The Rancher

The Rancher keeps the base's animals: every creature that can be tamed, breeds and drops meat (boars, wolves, lox, hens,
asksvin, and modded animals such as Wizardry's sheep), tamed and inside the board's area. Needs a level 2 board; one per
board.

## Orders

Shift+E on the Rancher, then **Orders**, *Add an order* (kept on the board, which shows them on its Orders tab):

- **Meat:** "keep at least 40 raw meat" (any meat an animal you keep drops).
- **Eggs:** "keep at least 20 eggs". He collects eggs until there are that many; after that he leaves them where they lie,
  so they hatch (left warm, under a roof) and the flock grows.
- **Breeding pool:** "keep 4 boars breeding": how many of a kind to keep alive, never fewer than 2.

## Slaughter

Only while a meat order is short, and only animals above their kind's breeding pool. He slaughters the **lowest-star
adult** first, so the pool is always your best animals; young (piglets, cubs, chicks) are never slaughtered. Babies take
their mother's stars, and each star doubles the meat, so the herd climbs toward your best. He picks up everything it
drops (meat, leather, pelts, trophies) and puts it in the chests.

- **No breeding pool, no slaughter:** a kind without a pool order is never slaughtered; his Shift+E *Problems* list says
  to set one.
- **Pets:** an animal a player has named is never slaughtered and doesn't count toward the pool.

## Feeding

He feeds the herd from the chests, dropping the feed beside each hungry animal on a side with no wall between. A Steward
on the same board leaves feeding to him; with PetPantry both leave it to PetPantry. A **Put in only** chest (see
[chest modes](jobs.md#chests-what-workers-may-do-with-each)) makes a good feed chest in a pen.

## Pens

He works over the fence whenever he can reach (feeding and slaughtering across a low fence or half wall). When he has to
go in, he waits until no animal stands by the gate and shuts it right behind him. Every hireling does the same at any
door with tame animals near it. A pen is anything walled in all round with built pieces (fences, walls, a shut gate or door), up to about 30 m
across. An animal of the herd that gets out of its pen shows in his Problems list (boars can't be
led back, so you'll want to know). Mark a pen's gates **Rancher only** (Shift+E on the gate) so he can still
go in and out while everyone else keeps away from the pen.

## Problems

Shift+E lists each ranch order and why it isn't met: no animal above the pool, no pool set, a pen too crowded to breed
(vanilla stops breeding with too many of a kind close together: 5 boars within 10 m), no partner close enough, hungry,
nothing they eat in the chests. He doesn't move animals: build another pen.

## Settings

In *12 - Rancher*: `RancherFeed`, `RancherSlaughter`, `RancherEggs`.
