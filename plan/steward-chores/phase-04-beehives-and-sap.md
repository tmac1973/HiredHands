# Phase 04 — Beehives and sap collectors

**Depends on:** 01 (`ChoreUrgency.Producer`, levels `beehives` 1 and `sap` 6), 02 (`IChore`, `StewardSteps.PickUpDrops`, `SmelterDeliveryPolicy.StewardOutputs`) · **Enables:** 08

## Goal
A Steward empties beehives from level 1, and sap collectors from level 6, once they're at least half full. It extracts the way a player does (the producer's own `RPC_Extract`, which drops the items at its spawn point), picks the items up into cargo, and delivers them to a chest that already holds them (Honey, Sap), else to the pile at the board, like any Steward output.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Steward/ProducersChore.cs` (new): one class for both kinds, with a constructor argument for `Beehives` or `Sap`.
  - Beehive: `Beehive` component, level from its ZDO (`ZDOVars.s_level`), max `m_maxHoney`, item `m_honeyItem`, drop point `m_spawnPoint`.
  - Sap collector: `SapCollector` component, ZDO `ZDOVars.s_level` against `m_maxLevel`, item `m_spawnItem`, drop point `m_spawnPoint`.
- `src/VikingsForHire/Hirelings/Work/Steward/StewardBehaviour.cs`: adds both.
- `src/VikingsForHire/Hirelings/Work/SmelterDeliveryPolicy.cs`: `StewardOutputs` gains each producer's item prefab when the chore is created (Honey, Sap).
- `src/VikingsForHire/Localization/English.json`: `vfh_steward_collect` ("Collecting from $1").
- `src/VikingsForHire/Testing/FixturesWork.cs`:
  - fixture `producer <prefab> <level> [tag]`: places a `piece_beehive` or `piece_sapcollector` 6 m from the board and sets its ZDO level (`ZDOVars.s_level`);
  - check `producer_level <tag>`.
- `test/alias_vfh.yaml`: `vfh_t_beehive`, `vfh_t_sap`.
- `docs/test-checklist.md`: rows VFH-CHORE-5 (beehive) and VFH-CHORE-6 (sap).

## Steps
1. **Candidates:**
   - Each producer piece in the radius, built by a player, with ward access for the board's owner.
   - Urgency is `ChoreUrgency.Producer(level, max)`.
   - Sap collectors also need to be connected to an Yggdrasil root (`m_mustConnectTo`). One that isn't never fills, so its level stays 0 and it never becomes a candidate.
   - A candidate whose item has no chest holding it still counts: the output goes to the pile at the board. After delivering, the status shows `vfh_need_room` ("Beehive: no chest holds Honey") until a chest holds it.
2. **Job:**
   1. Approach the producer.
   2. `m_nview.InvokeRPC("RPC_Extract")`.
   3. Wait 0.5 s, then `StewardSteps.PickUpDrops(m_spawnPoint.position, {item prefab}, 2 m)`. This picks up into cargo, respecting the weight and slot gate (`CargoGate`). Anything left on the ground stays there.
   4. If cargo now holds outputs, set `DeliverPending`; `DeliverBehaviour` (priority 300) delivers before the next chore.
   5. Done.

   With several producers of the same kind at half or more, it empties them all nearest-first in one trip, while cargo has room.
3. **AzuAutoStore:** with Azu, dropped honey may be swept into a chest before the Steward picks it up. That's fine: it ends up stored either way. `PickUpDrops` then picks up nothing and the job is Done.
4. **Logging:** Debug `steward.collect` (producer, level before, picked up); `steward.job`.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- `vfh_t_beehive` and `vfh_t_sap` pass in single player.

## Test plan
- **`vfh_t_beehive` (VFH-CHORE-5):**
  - Setup: a board at level 1, a level 1 Steward, `producer piece_beehive 4 B` (max 4), and `chest honey Honey 1 noazu`.
  - Assert `producer_level B == 0` within 120 s, and `deposited honey Honey >= 4` within 240 s.
- **`vfh_t_sap` (VFH-CHORE-6):**
  - Setup: a board at level 6, a level 6 Steward, and `producer piece_sapcollector 4 S` (max `m_maxLevel`, 4). The fixture sets the level directly; the Steward only needs the ZDO level, not a root.
  - Plus a chest seeded with `Sap 1 noazu`.
  - Assert `producer_level S == 0` within 120 s, and Sap deposited.
  - A level 5 Steward in the same setup leaves it full for 60 s (the gate).
- **By hand:** beehives on a roof terrace up a flight of stairs are emptied, and honey ends up in the honey chest.

## Commit
`feat(steward): empty beehives and sap collectors into chests`

## Rollback
Revert the commit, or set `StewardBeehives` / `StewardSap` to false on the server. Nothing is saved apart from the items moved.
