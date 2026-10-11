# Phase 02 — Animal catalog and herd scan (read from the game)

**Depends on:** 01 (`AnimalKind`, `HerdAnimal`) · **Enables:** 04 (order pickers, Problems), 05 (slaughter), 06 (eggs)

## Goal
Find, at run time, every animal the Rancher can manage and what it gives, and read the herd around a board into the
Core model. Modded animals work without data entries.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Ranch/AnimalCatalog.cs` (new) — builds `AnimalKind`s from `ZNetScene` prefabs,
  rebuilt when the prefab count changes (as `PreparedFoods` does).
- `src/VikingsForHire/Hirelings/Work/Ranch/HerdScan.cs` (new) — the herd in a work area as `HerdAnimal`s.
- `src/VikingsForHire/Commands/DebugCommands.cs` — `vfh_animals`: print the catalog (kind, meats, egg, young, limits).
- `src/VikingsForHire/Testing/FixturesWork.cs` — `tame` fixture gains `[stars <n>] [young] [named <name>]`; check
  `herd <kind> <adults|young|named|stars <n>>` (stars: unnamed adults at that many stars).
- `test/alias_vfh.yaml` — row VFH-RANCH-1.

## Steps
1. **Catalog.** A prefab is an animal kind when it has `Tameable`, `Procreation`, `Character` and `CharacterDrop`.
   - Meats: its `CharacterDrop` drops whose item is a Consumable food (`m_food`/`m_foodStamina`/`m_foodEitr` > 0), with
     base amount `(m_amountMin + m_amountMax) / 2` × `m_chance`.
   - Young: `Procreation.m_offspring` when that prefab has `Growup` (its `m_grownPrefab`/`m_altGrownPrefabs` lead back to
     this kind); those young prefabs map to this kind for counting.
   - Egg: `Procreation.m_offspring` when it's an item with `EggGrow` (hens: `ChickenEgg`); the egg's `m_grownPrefab` is
     the young, mapped to this kind.
   - Limits: `m_maxCreatures`, `m_totalCheckRange`, `m_partnerCheckRange`.
   - Foods: the prefab's `MonsterAI.m_consumeItems` item names.
   Logged once per build (`ranch.catalog`, kinds and meats).
2. **Herd scan.** Tamed, alive characters whose prefab is a kind or its young, inside a radius around a centre: the
   Rancher's radius around his home when one works the board, else the board's level-1 worker radius around the board
   (used by the test checks with no Rancher hired), and passing `WorkContext.BoardOwnerMayUse` (the same filters as `AnimalsChore.Hungry`). Stars = `GetLevel() - 1`.
   Adult = the kind's own prefab (young prefabs are not adult). Named = named by a player: the ZDO's
   `s_tamedNameAuthor` is set, or its `s_tamedName` is set and isn't one of `Tameable.m_randomStartingName`. Hungry =
   `Tameable.IsHungry()`. The list is sorted by ZDO id.
3. **Fixtures.** `tame <prefab> [tag] [dist] [stars <n>] [young] [named <name>]`: `young` spawns the kind's young prefab
   (from the catalog); `named` sets `s_tamedName` and `s_tamedNameAuthor` as a player's naming does. Check
   `herd <kind> <adults|young|named|stars <n>>` counts the nearest board's herd.

## Verification
- `dotnet build -c Release` succeeds.
- In the dev game: `vfh_animals` lists Boar (RawMeat, young Boar_piggy), Wolf (WolfMeat, young Wolf_cub), Lox
  (LoxMeat, young Lox_Calf), Hen (ChickenMeat, egg ChickenEgg), Asksvin, plus any modded kinds in the profile.
- Row VFH-RANCH-1 (catalog): `vfh_fixture tame Boar B 10 stars 2`, `tame Boar Y 12 young`, `tame Boar N 14 named Bob`:
  `herd Boar adults == 2`, `herd Boar young == 1`, `herd Boar named == 1`.

## Done when
- [ ] The catalog finds the vanilla kinds above (and modded ones) with meats, young, eggs and limits.
- [ ] The herd scan reports stars, adult, named (player-named only) and hungry correctly.
- [ ] Row VFH-RANCH-1 passes.
