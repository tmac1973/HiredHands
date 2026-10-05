# Phase 06 — Repairs

**Depends on:** 01 (`ChoreUrgency.Repair`, level `repairs` 3, `StewardRepairBelow`, `StewardRepairQuietSeconds`), 02 (`IChore`, `StewardSteps.Approach`) · **Enables:** 09

## Goal
From level 3, a Steward repairs damaged building pieces in its work radius, worst first, under vanilla's own hammer rules:
- **Station in range:** the piece's crafting station must be in range of where the Steward stands, unless the world has the "no workbench" modifier.
- **Ward access:** the board's owner must have access under any ward.
- **Free:** repairs cost nothing, as in vanilla.

It never repairs while enemies are around. AzuAreaRepair and RepairStation don't overlap, so there's no step-aside here.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Steward/RepairsChore.cs` (new).
- `src/VikingsForHire/Hirelings/Work/Steward/StewardBehaviour.cs`: adds it.
- `src/VikingsForHire/Localization/English.json`: `vfh_steward_repair` ("Repairing $1"), `vfh_need_station` ("$1: no $2 in range"), `vfh_steward_wait_enemies` ("Repairs: waiting until the enemies are gone").
- `src/VikingsForHire/Testing/FixturesWork.cs`:
  - fixture `damaged <prefab> <health 0..1> [tag] [near_workbench|far]`: places a piece 6 m from the board (within the `base` fixture's workbench range), or 25 m away with `far` (beyond a workbench's 20 m range, inside the 30 m work radius a level 3 Steward can have), and sets its health through `WearNTear.m_health` and the ZDO `ZDOVars.s_health`;
  - check `piece_health <tag>` (`WearNTear.GetHealthPercentage()`).
- `test/alias_vfh.yaml`: `vfh_t_repairs`.
- `docs/test-checklist.md`: row VFH-CHORE-8.

## Steps
1. **Quiet check:** repairs are offered only when no enemy has been within 30 m of the Steward, or of the piece to be repaired, for `StewardRepairQuietSeconds`. The Steward's `ThreatScanner` reports hostiles near it, and `BaseAI.FindClosestEnemy`-style scans (`Character.GetAllCharacters`, hostile to players) cover the piece; the chore keeps the time of the last one seen. Otherwise `Missing = vfh_steward_wait_enemies`.
2. **Candidates:** pieces in the radius (`Piece.GetAllPiecesInRadius`) that:
   - have a `WearNTear` with `GetHealthPercentage() < StewardRepairBelow`;
   - were built by a player;
   - pass the ward check for the board's owner (`PrivateArea` at the piece, as `DoorRules` does).

   Urgency is `ChoreUrgency.Repair(health, StewardRepairBelow)`.
   - **Station check:** if the piece has `m_craftingStation` and the world doesn't have `GlobalKeys.NoWorkbench`, `CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, piece.transform.position)` must be true. Vanilla checks from the player's position, but the Steward stands next to the piece, so checking at the piece gives the same result.
   - A piece failing the station check isn't a candidate. It sets `Missing = vfh_need_station(piece, station)` if nothing else is doable.
3. **Job:**
   1. Approach the piece, ending within 3 m.
   2. Re-check the station from the Steward's own position (as vanilla does from the player's).
   3. `WearNTear.Repair()` (sends `RPC_Repair` to the piece's owner).
   4. Create the piece's `m_placeEffect` at it, like the hammer, and turn to face the piece.
   5. Done.

   One piece per job; the loop then picks the next. A short 1 s pause between repairs keeps it looking like work.
4. **Interrupted:** the quiet check runs again before each repair, so a raid mid-way stops the repairs, and combat takes over anyway.
5. **Logging:** Debug `steward.repair` (piece, health before), plus `steward.job`.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- `vfh_t_repairs` passes in single player.

## Test plan
- **`vfh_t_repairs` (VFH-CHORE-8):**
  - Setup: `flatten`, a board at level 3, and a level 3 Steward posted with radius 30 (`post Smelter 3 30`). The `base` fixture includes a workbench at the board.
  - Place `damaged woodwall 0.3 W near_workbench` and `damaged woodwall 0.3 F far`.
  - Assert `piece_health W >= 0.99` within 120 s, and `piece_health F < 0.5` after a further 60 s.
  - Then spawn a `Greyling` within 20 m, damage W again to 0.3, and assert it isn't repaired while the Greyling lives. Kill the Greyling and assert W is repaired within `StewardRepairQuietSeconds` + 60 s.
- **By hand:** after a raid at the live base, the Steward walks around the walls repairing the worst-damaged pieces first, inside and upstairs.

## Commit
`feat(steward): repair damaged pieces near their crafting station, worst first, after fights`

## Rollback
Revert the commit, or set `StewardRepairs = false` on the server.
