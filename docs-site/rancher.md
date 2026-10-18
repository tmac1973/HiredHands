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
[chest modes](jobs.md#chests-what-workers-may-do-with-each)) makes a good feed chest in a pen, or **the Rancher only**
when the pen's gate is Rancher only too, so no one else goes in for what's in it.

## Pens

He works over the fence whenever he can reach (feeding and slaughtering across a low fence or half wall). When he has to
go in, he waits until no animal stands by the gate and shuts it right behind him. Every hireling does the same at any
door with tame animals near it. Mark a pen's gates **Rancher only** (Shift+E on the gate) so he can still go in and out while everyone else keeps away
from the pen.

## Pens

**The Pen sign** (hammer, Misc, 2 Wood, inside a hiring board's area) marks a pen: the animals inside its circle, on
its level (within 3 m up or down), are in the pen; anywhere else they're out. E on the sign widens the circle, Shift+E
narrows it (3–25 m); looking at it shows the circle and how many animals are in it. Place one in every pen: it's the
only sure way, for a pen on a terrace, a pen of odd shape, or a base with other walled rooms (a basement).

Without a sign, a pen is anything walled in all round, up to about 30 m across: built pieces (fences, walls, a shut gate
or door) or a steep bank of earth.

An animal of the herd that gets out of its pen shows in his Problems list. A Rancher with **Herder** (a level 4
enhancement) goes and fetches it: he walks up to it, it follows him, and he leads it back into its pen, waiting when it
lags and going back for it if it wanders off. Without Herder, fence it in where it is or bring it back yourself.

## Problems

Shift+E lists each ranch order and why it isn't met: no animal above the pool, no pool set, a pen too crowded to breed
(vanilla stops breeding with too many of a kind close together: 5 boars within 10 m), no partner close enough, hungry,
nothing they eat in the chests. He doesn't move animals: build another pen.

## Settings

In *12 - Rancher*: `RancherFeed`, `RancherSlaughter`, `RancherEggs`.
