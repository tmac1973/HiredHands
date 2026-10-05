# Steward Chores — Project Overview

## Problem
The Steward (internally `JobType.Smelter`, renamed in 0.2.1) does one thing: it keeps the smelting stations listed in the data file fed and stocked from chests (`SmelterBehaviour`, `SmelterDeliveryPolicy`, `StationSurvey`; stations `smelter`, `blastfurnace`, `charcoal_kiln`, `eitrrefinery`). The day-to-day running of a base is still all on the player:
- topping up fires, braziers, torches and hot tubs;
- emptying beehives and sap collectors;
- loading windmills and spinning wheels and collecting their flour and linen;
- feeding tamed animals;
- repairing what raids and weather damage.

Most of this happens indoors, which hirelings couldn't reliably reach before 0.3.0's base nav links. They now can, so the Steward can take this work on.

Some servers already cover parts of this with other mods. Tim's live server (Gale profile `1dotohsupermodded`) runs AzuAutoStore, PetPantry, Torches Eternal (Xenofell), AzuAreaRepair and RepairStation. The Steward must not fight those mods. It must still do the whole job on servers without them.

## Goals
- **A Steward that runs the base:** inside its work radius it does these chores, most urgent first:
  - **Fires and lights:** fuel fireplaces, hearths, braziers, torches, sconces and hot tubs (vanilla `Fireplace`) from chests holding their fuel.
  - **Beehives:** empty them (honey) into chests.
  - **Stations:** keep smelters, kilns, blast furnaces and eitr refineries fed and emptied, as today.
  - **Windmills and spinning wheels:** load them from chests (barley, flax) and collect their output (barley flour, linen thread).
  - **Sap collectors:** empty them (sap) into chests.
  - **Tamed animals:** feed hungry tamed animals with food they eat, from chests.
  - **Repairs:** repair damaged building pieces, following the vanilla rule (only pieces in range of a crafting station of the right type), worst-damaged first, and not while enemies are near.
- **Chores unlock with the biome.** Each chore needs a minimum Steward level, tied to the resources that boss unlocks. The levels are data-file values server owners can change. Hireling level is capped by board level, so this follows board upgrades.
  - **Level 1, Meadows:** fires and lights, beehives.
  - **Level 2, Black Forest:** smelters, charcoal kilns, tamed animals.
  - **Level 3, Swamp:** repairs.
  - **Level 4, Mountains:** nothing new. Like every level, its bigger cargo (the hireling level table) carries more per trip, so it serves more stations at once.
  - **Level 5, Plains:** blast furnace, spinning wheel, windmill.
  - **Level 6, Mistlands:** eitr refinery, sap collectors.
  - **Levels 7 and 8:** nothing new beyond the level table's bigger cargo and work radius.
- **Per-Steward chore toggles:** in the Shift+E panel, like gatherers' item toggles, all on by default. Two Stewards can split the work. Each chore shows its state next to its toggle: on, off, locked until level N, or handled by another mod.
- **Steps aside for other mods**, detected at load and logged once:
  - fires get no fuel when Torches Eternal is installed (it keeps every fireplace, torch and hot tub full); any single fire that never burns down is skipped too;
  - animals aren't fed when PetPantry is installed;
  - AzuAreaRepair is *not* treated as covering repairs: it only widens a repair you make with the hammer, so the Steward still repairs on its own.

  Each chore also has a server setting to turn it off everywhere.
- **Always says what it's doing:** the hover and roster status name the current chore, or what's missing. For example: "Fuel: no wood in any chest", "Spinning wheel: nothing to load: flax", "Repairs: no workbench in range".
- **Uses the base nav links** to reach every chest, station, fire and animal indoors and upstairs.
- **A broom.** The Steward's weapon is a broom: an item with a club's stats and the game's broom model, which is found by name inside Valheim's own assets (there's no broom item). It sweeps with it and swats enemies with it too. If the model can't be found or doesn't sit right in the hand, the item uses the vanilla cultivator's model instead.
- Same chest rules as today's station feeding:
  - **Taking:** only from chests in the work radius, respecting the data file's reserves (`keepInStorage`).
  - **Delivering:** outputs go to a chest that already holds that item, else onto the pile at the board.

## Non-goals
- **No Cook, Farmer or production orders.** No cooking stations, cauldron, oven or fermenter, and no planting or harvesting fields; those are separate futures.
- **No new hireling job:** this widens the Steward; it doesn't add a Caretaker or Builder.
- **No crafting or building:** it never places pieces and never crafts items.
- **No player-set chore order:** urgency decides the order.
- **No feeding of animals outside the work radius**, and no breeding management.
- **Repairs never cost more than vanilla's (free), and never repair what a player couldn't:** no station in range means no repair.
- **No change to how gatherers or guards work.**

## Users & primary flow
Players running a base, and server owners who mostly never touch the settings.
1. A player with a level 2 board hires a level 2 Steward. Its hover says what it's doing; its Shift+E panel lists the chores:
   - fires, beehives, stations and animals on;
   - Mills (windmill, spinning wheel) "locked until level 5", Sap "locked until level 6", Repairs "locked until level 3";
   - Stations "on (blast furnace at level 5, eitr refinery at level 6)".
2. The Steward checks its radius for the most urgent need:
   - a hearth with little fuel left;
   - a beehive with honey;
   - a smelter with ore waiting in a chest;
   - a hungry tamed boar.
3. It fetches what's needed from chests, going through doors and up stairs as needed. It loads or feeds, collects outputs, and delivers them to chests.
4. When nothing needs doing, it idles at the board. Its status says why, for example "All fires fuelled", or "Kiln: no wood in any chest".
5. The player upgrades the board to level 3 and promotes the Steward. Repairs unlock: after a raid, once enemies are gone, it repairs damaged pieces near the workbench, worst first.
6. On Tim's server, PetPantry, Torches Eternal and AzuAreaRepair are installed. The Steward's panel shows "Animals: handled by PetPantry", and it never feeds animals. Torches Eternal keeps every fire full, so the Steward's fires chore shows "handled by TorchesEternal". It still repairs: AzuAreaRepair only widens repairs you make yourself.

## Constraints
- **Stack and conventions:** C#, BepInEx 5, Jotunn, Harmony, publicized `assembly_valheim`. Same logging (`VfhLog` events), guarded ticks and patches, fixtures and macros (`Testing/`, `test/alias_vfh.yaml`), unit tests for pure logic in `Core/`.
- **Builds on 0.3.0's base nav links** (`HirelingAI.WalkTo`, `CanReach`, `LinkNavigator`) for reaching things indoors. It doesn't change them.
- **Existing station feeding is reused and extended, not rewritten:** `SmelterBehaviour`, `SmelterDeliveryPolicy`, `StationSurvey` and the data file's `stations` and `keepInStorage`.
- **Simulation:** chores run on the game simulating the Steward (its ZDO owner), like all hireling work.
  - Interactions go through each object's own RPCs, as a player's would: fuel, smelter add/empty, beehive extract, repair.
  - Ward rules apply as for doors and chests: only what the board's owner could use.
- **Data file:** a new key for each chore's minimum level, plus the windmill and spinning wheel stations. Older builds reject the file, so this ships as **0.4.0** and the server and every player update together, as with 0.3.0. `DataDefaults.FillMissing` fills the keys into existing files.
- **Compat:**
  - AzuAutoStore pulls items off the ground, so food dropped for animals must survive long enough to be eaten.
  - CraftyBoxes and PullMats exclusions still apply to the board and cargo.
  - PetPantry (`Azumatt.PetPantry`) and Torches Eternal (`Xenofell.TorchesEternal`) are detected by their plugin GUIDs. AzuAreaRepair and RepairStation don't overlap: the first only widens a player's own hammer repair, and the second repairs items.
- **Behaviour change for existing Stewards:** a level 1 Steward stops feeding smelters and kilns, which are now level 2. The roster, hover and panel say "Stations: locked until level 2", so players know to promote.

## Success criteria
- Automated single-player macros pass for each chore. Each starts with `flatten`.
  - **Fires:** a fire with low fuel is refuelled from a chest.
  - **Beehives:** a beehive's honey ends up in the honey chest.
  - **Windmill and spinning wheel:** each is loaded from a chest, and its output delivered.
  - **Sap collector:** it's emptied into a chest.
  - **Tamed animal:** a hungry tamed boar is fed and is no longer hungry.
  - **Repairs:** a damaged piece near a workbench is repaired; one with no station in range isn't.
  - **Urgency:** with two chores waiting, the more urgent one is done first.
  - **Level gating:** a level 1 Steward doesn't touch a smelter, and a level 2 one does.
  - **Toggles:** a chore switched off is never done.
- With PetPantry or Torches Eternal installed, the matching chore is skipped and shown as handled by that mod.
- On the live server, a Steward runs Tim's base for a play session: fires kept lit, smelters fed, beehives emptied. No `nav.stuck` lines on the way to indoor chores, no `lvl=E` lines.
- The existing station macros still pass with a level 2 Steward (`vfh_t_work5`, `vfh_t_azu3`, `vfh_t_keep1` and `vfh_t_keep2`).

## Decisions
- **Which chores** → all four groups: fires and lights, collecting from producers (beehives, sap; windmill and spinning wheel loading and output), repairs, and feeding tamed animals. Tim runs AzuAutoStore, PetPantry and Torches Eternal, but the chores must work for servers without them.
- **Overlap with other mods** → detect and step aside: skip what another installed mod already does, log it once, show it in the panel. A server setting can still turn each chore off.
  - **Covered by another mod:** PetPantry covers animals; Torches Eternal covers all fires.
  - **Not covered:** AzuAreaRepair, which only acts on a player's own repair, so the Steward keeps repairing.
- **Choosing chores** → per-Steward toggles in the Shift+E panel, all on by default.
- **Order of work** → most urgent first: each chore scores its own urgency, with a small bias towards what's close.
- **Level gating** → by the hireling's level (so capped by the board's), tied to each biome's resources:
  - L1 fires/lights and beehives;
  - L2 smelters, kilns, animals;
  - L3 repairs;
  - L4 nothing new (bigger cargo, as every level);
  - L5 blast furnace, spinning wheel, windmill;
  - L6 eitr refinery, sap;
  - L7–8 nothing new beyond the level table.

  All values are in the data file. Existing level 1 Stewards stop smelting until promoted, and the UI says why.
- **Feedback** → a status line naming the current chore or what's missing, plus each chore's state in the Shift+E panel.
- **Release** → one release, 0.4.0, after batch testing. The server and players update together.
- **Broom** → yes, as the last phase. It's the Steward's weapon at every level: a club's stats with the game's broom model, found by name in Valheim's assets, so it fights with it too. The cultivator's model is the fallback. The chores don't depend on it.
