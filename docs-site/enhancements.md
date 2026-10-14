# Enhancements

From level 2 to level 8, every hireling picks **one of two enhancements** at each level: seven picks in all, one per
level, so no hireling gets everything. The pairs are fixed for each job, and each choice is a real trade-off: often a plain
number (more health, faster swings) against something it does (a shield bash, a seed back from every tree).

## Choosing

- A hireling with a choice waiting shows it on its hover ("2 enhancements to choose") and in the board's **Roster** tab
  ("(2 to choose)").
- Open its **Shift+E** panel and press **Enhancements**. Each level shows its two options with what they do; click one,
  then click it again to confirm. Levels above its own are greyed out until it's promoted.
- Anyone who may use the board's area (ward access) may choose, as for the panel's other buttons.
- Picks are kept on the contract: they survive death and return, a Hiring Charter move, a follower joining another board,
  and a promotion (a promoted hireling has its new level's choice waiting).

## Respec

A **Respec** button on the hireling's row in the board's Roster tab clears all its picks so you can choose again. It costs
half the hire fee for its level (coins and food, from the board), as set by `respecCostFraction` in the data file.

## For server owners

- The `Enhancements` setting (section *3 - Hiring*) turns the whole system off: with it off nobody has any effect and
  nothing can be chosen; the picks are kept and come back when it's turned on again.
- Every number below is in the data file's `enhancements` section, per job, as `Name.number` (for example
  `GuardMelee: Toughened.health: 0.1`). Change them like any other data value; the panel's descriptions follow.

## Guard

| Level | A | B |
|---|---|---|
| 2 | **Toughened**: +10% max health | **Shield training**: Blocks absorb 15% more |
| 3 | **Heavy armour**: +4 armour | **Swift strikes**: Attacks 15% faster |
| 4 | **Shield bash**: Every 12 s a bash that staggers its target (10 blunt damage) | **Second wind**: Once a fight, below 30% health, heals 25% |
| 5 | **Masterwork sword**: Its sword is one quality level higher (1 up): more damage and durability | **Bodyguard**: Every 15 s, an enemy attacking its owner or a hireling within 6 m turns on it |
| 6 | **Taunt**: Every 20 s in a fight, enemies within 8 m attack it for 5 s | **Bloodthirst**: Heals 10% of the damage it deals |
| 7 | **Cleave**: Swings also hit up to 2 more enemies in front, for 50% of the damage | **Last stand**: Once every 600 s, a killing blow leaves it at 1 health and unhurt for 3 s |
| 8 | **Champion**: +10% damage and +10% max health | **Rallying cry**: While it fights, hirelings within 15 m deal 10% more |

## Archer

| Level | A | B |
|---|---|---|
| 2 | **Steady aim**: +10% arrow damage | **Quick draw**: Shoots 9% faster |
| 3 | **Eagle eye**: Sees and shoots 5 m further | **Light feet**: +6% move speed |
| 4 | **Fire arrows**: Arrows set the target burning (10 fire damage over 4 s) | **Frost arrows**: Arrows slow the target by 17% for 3 s (3 frost damage) |
| 5 | **Masterwork bow**: Its bow is one quality level higher (1 up): more damage and durability | **Hunter**: +14% damage to animals; picks up its kills' drops in the field |
| 6 | **Volley**: Every 15 s, 3 arrows in a spread | **Headshot**: 6% chance of double damage |
| 7 | **Marksman**: +20% damage to targets more than 15 m away | **Skirmisher**: Keeps its distance: backs off from anything within 10 m for up to 6 s, never swaps to its sidearm |
| 8 | **Rain of arrows**: Every 30 s, 8 arrows fall over the target's area, each at 50% damage | **Elite**: +6% damage and +6% max health |

## Wizard

| Level | A | B |
|---|---|---|
| 2 | **Focus**: +9% spell damage | **Haste**: Casts 5% faster |
| 3 | **Warding**: Takes 13% less damage | **Blink**: When an enemy closes within 4 m, it jumps 8 m away (every 20 s) |
| 4 | **Secondary spell**: Every 20 s, its staff's secondary attack (or a frost nova) | **Mender**: Every 15 s heals the most hurt player or hireling within 15 m by 7% |
| 5 | **Masterwork staff**: Its staff is one quality level higher (1 up): more damage and durability | **Elemental mastery**: Its spell's burn, freeze or shock lasts and hurts 12% more |
| 6 | **Protection**: Every 60 s, a protective bubble on its owner or itself | **Entangling roots**: Every 90 s, roots hold enemies at its target |
| 7 | **Long reach**: Casts from 4 m further | **Chain spell**: Each spell also strikes a second enemy within 6 m for 15% damage |
| 8 | **Archmage**: +13% damage, casts 9% faster | **Raise dead**: Every 180 s, a skeleton fights beside it for 20 s |

## Woodcutter

| Level | A | B |
|---|---|---|
| 2 | **Strong swing**: 10% more chopping or mining power per swing (fewer swings) | **Pack mule**: +2 cargo slots |
| 3 | **Forester**: Plants tree patches twice as often | **Strong back**: +50 carry weight |
| 4 | **Seed saver**: Each tree it fells also drops a seed of its kind | **Clean cut**: Clears logs and stumps 30% faster |
| 5 | **Masterwork axe**: Its axe is one quality level higher (1 up): more damage and durability | **Lumberjack**: +15% wood from logs |
| 6 | **Fine eye**: Prefers fine and core wood trees, +10% of those | **Wide range**: +20% work radius |
| 7 | **Timber!**: A felled tree also knocks down a small tree it falls on | **Swift**: +10% move speed |
| 8 | **Master woodsman**: Chops 15% faster and +15% yield | **Quiet work**: Its work makes no noise, so it draws no monsters |

## Miner

| Level | A | B |
|---|---|---|
| 2 | **Strong swing**: 10% more chopping or mining power per swing (fewer swings) | **Pack mule**: +2 cargo slots |
| 3 | **Deep digger**: Digs to ore up to 2.5 m down | **Strong back**: +50 carry weight |
| 4 | **Prospector**: Goes for ore before any stone, searching 30% further for it | **Rock breaker**: Plain rock and boulders break 30% faster |
| 5 | **Masterwork pickaxe**: Its pickaxe is one quality level higher (1 up): more damage and durability | **Rich veins**: +15% ore from deposits |
| 6 | **Quiet work**: Its work makes no noise, so it draws no monsters | **Swift**: +10% move speed |
| 7 | **Tireless**: Keeps working until its cargo is 25% over its weight limit | **Gem eye**: 10% chance of a bonus drop from rock (amber, flint, gems the biome has) |
| 8 | **Master miner**: Mines 15% faster and +15% yield | **Heavy hauler**: Ore it carries weighs 50% as much |

## Steward

| Level | A | B |
|---|---|---|
| 2 | **Brisk**: +10% move speed | **Pack mule**: +2 cargo slots |
| 3 | **Keeper of flames**: Tops fires and kilns up at 75% instead of half | **Strong back**: +50 carry weight |
| 4 | **Handyman**: Repairs from 10 m and needs only 10 s without enemies | **Bonesetter**: A hurt hireling within 10 m of it, not fighting, regains 2% health a second |
| 5 | **Quartermaster**: Stocks the board up to 3 more days | **Tidy mind**: Tidies items after 20 s on the ground, from 2 times as far |
| 6 | **Smelter's hand**: Tops stations up whenever they aren't full, loading 2 times as fast | **Watchman**: Fights like a guard (Defensive stance allowed, +25% damage) |
| 7 | **Errand runner**: Does every chore on its way (up to 4 in a row) instead of one per trip | **Fermenter's touch**: Fermenters it loads finish 20% sooner |
| 8 | **Master steward**: Every chore 15% faster: shorter pauses, more per trip | **Shield keeper**: Keeps shield generators topped up from 2 times as far |

## Farmer

| Level | A | B |
|---|---|---|
| 2 | **Green thumb**: Plants and harvests 10% faster | **Pack mule**: +2 cargo slots |
| 3 | **Bountiful**: 25% chance of an extra crop when harvesting | **Seed keeper**: 25% chance of an extra seed when harvesting |
| 4 | **Close planting**: Plants its crops closer together: they need 15% less room, so a field holds more | **Forager**: Picks wild berries and mushrooms in its area even when no order asks |
| 5 | **Swift**: +10% move speed | **Strong back**: +50 carry weight |
| 6 | **Tender care**: Crops it plants grow 15% faster | **Hardy stock**: Crops it plants survive a little cold or shade |
| 7 | **Bigger fields**: +20% work radius | **Double harvest**: 10% chance a harvested crop drops twice |
| 8 | **Master farmer**: 15% faster and +15% yield | **Replanter**: Replants each spot as it harvests it, so the field is never bare |

## Cook

| Level | A | B |
|---|---|---|
| 2 | **Quick hands**: Crafts and loads stoves 10% faster | **Pack mule**: +2 cargo slots |
| 3 | **Frugal**: 15% chance an ingredient isn't used up | **Spit master**: Loads every free spit in one trip |
| 4 | **Double batch**: 20% chance a craft makes one extra | **Keeper of stoves**: Keeps the oven fuelled at all times |
| 5 | **Swift**: +10% move speed | **Chef's touch**: Makes 10% more food from the same ingredients |
| 6 | **Prep chef**: Makes in-between ingredients (dough, mince) ahead, for the next order | **Strong back**: +50 carry weight |
| 7 | **Two stations**: Works two stations at once | **Pantry sense**: Takes ingredients from chests up to 2 times as far |
| 8 | **Head chef**: 15% faster, and 5% more chance of saving an ingredient and 5% of an extra | **Banquet**: Makes 25% more food from the same ingredients |

## Rancher

| Level | A | B |
|---|---|---|
| 2 | **Gentle hand**: Feeds from 2 m further (more over the fence) | **Pack mule**: +2 cargo slots |
| 3 | **Butcher**: 33% chance of one more meat per slaughter | **Hearty feed**: Animals it feeds stay fed 50% longer |
| 4 | **Herder**: Leads an escaped animal back into its pen | **Skinner**: 33% chance of one more hide or leather per slaughter |
| 5 | **Swift**: +10% move speed | **Strong back**: +50 carry weight |
| 6 | **Breeder**: Animals it feeds breed 15% sooner | **Herd guard**: Fights anything attacking one of its animals, with +25% damage |
| 7 | **Star breeder**: 10% chance a newborn gets one more star (up to 2) | **Big herd**: Keeps 2 more of each kind before slaughtering |
| 8 | **Master rancher**: 15% faster, and 10% more chance of an extra meat and of an extra hide | **Wrangler**: Tames wild animals of a kind it keeps when they wander into its area (one every 300 s) |

