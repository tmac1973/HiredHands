# Phase 02 — Steward chore loop, level gating, chore panel and status

**Depends on:** 01 · **Enables:** 03, 04, 05, 06, 07 (each adds chores to this loop)

## Goal
The Steward becomes a chore picker. Each decision it gathers candidate jobs from every chore that is all of:
- unlocked at its level;
- switched on for it;
- allowed on the server;
- not handled by another mod.

It then does the highest-scoring job.

Today's station feeding becomes the first two chores:
- **Stations:** smelter, charcoal kiln, blast furnace, eitr refinery.
- **Mills:** windmill, spinning wheel, using the same station code.

Each station is gated by its own level, so a level 1 Steward no longer smelts. The Shift+E panel lists every chore with its toggle and state, and the hover and roster status name the current chore or what's missing. Phases 03–06 then add one chore each.

## Files touched
- `src/VikingsForHire/Hirelings/Work/Steward/IChore.cs` (new):
  - `interface IChore`: `ChoreKind Kind`, `IEnumerable<ChoreJob> Candidates(StewardContext ctx)`, `void Begin(ChoreJob job, StewardContext ctx)`, `ChoreProgress Tick(HirelingAI ai, float dt)` (Running, Done or Failed with a reason), `void Abort()`, `string? Missing` (why it has nothing doable, e.g. `$vfh_need_item` with an item name).
  - `sealed class ChoreJob`: `Kind`, `Component Target`, `float Urgency`, `float Score`, `string Label` (a status token with arguments).
- `src/VikingsForHire/Hirelings/Work/Steward/StewardContext.cs` (new), built once per decision:
  - the hireling, home, radius and level;
  - the chests in the work radius (`ChestFinder.Find`, minus skipped ones);
  - chest stock above reserves (the code moved out of `SmelterBehaviour.Decide`: `ChestReserve` plus `keepInStorage`);
  - cargo counts and free slots.
- `src/VikingsForHire/Hirelings/Work/Steward/StewardSteps.cs` (new): shared steps moved out of `SmelterBehaviour`:
  - `Approach` and `PickSpot` (walk to an object's side, uses `WalkTo`, so the base nav links);
  - `FetchFromChests(items)`: take items from the nearest chests holding them, one chest at a time, respecting reserves;
  - `PickUpDrops(around, prefabs, radius)`: pick up dropped items near a point into cargo;
  - `Deliver()`: set `DeliverPending` so the existing `DeliverBehaviour` takes outputs to chests.
- `src/VikingsForHire/Hirelings/Work/Steward/StationsChore.cs` (new): today's `SmelterBehaviour` logic (decide, fetch, load, collect), now a chore. One class serves both `Stations` and `Mills` (a constructor argument picks the kind, filtering stations with `ChoreRules.KindOfStation`).
- `src/VikingsForHire/Hirelings/Work/Steward/StewardBehaviour.cs` (new; replaces `SmelterBehaviour` in `HirelingAI.Init` for `JobType.Smelter`): `Name => "Steward"`, `Priority => 200`, the decision loop (step 2).
- `src/VikingsForHire/Hirelings/Work/SmelterBehaviour.cs`: deleted; its code is in `StationsChore`, `StewardContext` and `StewardSteps`.
- `src/VikingsForHire/Hirelings/Work/StationSurvey.cs`: `Find` also filters by `ChoreRules.Unlocked(level, prefab)` and by kind. `Products()` includes the mills' outputs (computed from each station's conversions, so flour and linen thread are in it automatically).
- `src/VikingsForHire/Hirelings/Work/SmelterDeliveryPolicy.cs`: `Delivers` also covers each chore's outputs, through a set `StewardOutputs` filled by the chores (phase 04 adds Honey and Sap).
- `src/VikingsForHire/UI/HirelingPanel.cs`: for a Steward, a "Chores" list.
  - Each of the seven chores gets a row: name, plus state as an on/off button (`ShowToggle`, as the gather toggles), or "locked until level N", "handled by <mod>" or "off on this server".
  - Clicking an on/off button submits `RosterOp.SetGather` with the skip list updated by `ChoreRules.WithChore`.
  - Below the list: the current status line.
  - The panel signature includes the skip list and level.
- `src/VikingsForHire/UI/RosterTab.cs`: the Steward's status shows its current chore label, or its missing reason when idle.
- `src/VikingsForHire/Localization/English.json`: the shared status strings. Each later chore adds its own label in its phase.
  - `vfh_steward_load` ("Loading $1");
  - `vfh_steward_idle` ("All done");
  - `vfh_need_item` ("$1: no $2 in any chest");
  - `vfh_need_room` ("$1: no chest holds $2").
- `src/VikingsForHire/Testing/FixturesWork.cs`:
  - fixture `steward_chores <key=on|off>…`: sets the last contract's chore toggles through `RosterOp.SetGather`;
  - fixture `wind_on`: `EnvMan.instance.SetDebugWind(0, 1)`, so windmills turn;
  - check `steward_chore`: the chore kind the Steward from your last contract is doing now, or `none`.

  Stations are placed with the existing `stations <prefab>…` fixture (`Testing/FixturesSmelter.cs`) and read with the existing `station <prefab> <ore_ratio|fuel_ratio|queue|fuel|processed>` check.
- `test/alias_vfh.yaml`:
  - `vfh_t_work5`, `vfh_t_azu3`, `vfh_t_keep1` and `vfh_t_keep2` hire `Smelter 2` instead of `Smelter 1`, on a board at level 2 or more;
  - new macros `vfh_t_chore_gate`, `vfh_t_chore_toggle` and `vfh_t_mills`.
- `docs/test-checklist.md`: rows VFH-CHORE-1 (gate), VFH-CHORE-2 (toggle), VFH-CHORE-3 (mills); updated level notes on the smelter rows.

## Steps
1. **Moving the station code.** Move `SmelterBehaviour`'s decide/fetch/load/collect code into `StationsChore` unchanged, except:
   - its survey filters by kind and level;
   - its per-station work is offered as candidates: each station needing service is a `ChoreJob` scored with `ChoreUrgency.Station` and the distance bias;
   - when chosen, it runs today's plan for that station and every other station of the same kind the plan can serve on that trip (today's multi-station planning, limited by cargo, which is what makes higher levels "do more per trip").

   `Reservations` keep working as now, so two Stewards serve different stations.
2. **The decision loop** (`StewardBehaviour`):
   - **Wants:** the hireling is `Working` and either a job is running or the survey is due. Surveys run every 3 s when idle, as `SmelterBehaviour` did.
   - **Survey:** build the `StewardContext`. For each chore, skip it if:
     - its server setting is off;
     - it's toggled off for this Steward;
     - its level is locked (stations check per prefab inside `StationsChore`);
     - `StewardCompat.HandledBy(kind)` returns a mod.

     Collect every remaining chore's candidates and pick the top `Score`. Ties go to the nearer job (`ChoreUrgency.Score`), then the earlier kind in enum order.
   - **Run:** `Begin`, then `Tick` each frame until Done or Failed. Done or Failed leads to a new survey after 1 s.
     - A chore that fails 3 times on the same target skips that target for 5 min (`Reservations.Skip`, as chests are skipped today).
     - A job interrupted by combat or fleeing calls `Abort` and the next survey decides afresh. Combat and flee outrank the Steward at priorities 900 and 950, as now.
   - **Status:**
     - while running: `h.SetActivity(job.Label)`;
     - when there are no candidates: the first chore's `Missing` reason in enum order, or `vfh_steward_idle` ("All done");
     - nothing doing: it falls to `IdleBehaviour` (0.2.4's open-air spot by the board), keeping the status line.
3. **Logging:**
   - Debug `steward.survey`: candidates per kind, the winner, its score and the skipped chores with their reasons.
   - Info `steward.job` when a job starts and ends: kind, target, result, seconds.
   - Existing `smelter.*` events keep their names.
4. **Panel.**
   - The rows are built from `ChoreKind` in enum order.
   - Locked rows show the lowest level among that chore's keys. For Stations that's the lowest level among its stations, e.g. "Stations: locked until level 2".
   - A Stations or Mills row that's partly unlocked shows each still-locked station, e.g. "on (blast furnace at level 5, eitr refinery at level 6)".
   - Toggling is allowed only with ward access to the board (the panel's existing rule).
5. **Fixtures and macros.**
   - **`vfh_t_chore_gate` (VFH-CHORE-1):** a board at level 2, `stations smelter`, and a chest with copper ore and coal.
     - Hire `Smelter 1`. After `wait 60`, assert `station smelter queue == 0` and `steward_chore != Stations`.
     - Then `contract promote` (level 2). Assert `station smelter queue > 0` within 120 s.
   - **`vfh_t_chore_toggle` (VFH-CHORE-2):** a level 2 Steward with `steward_chores stations=off`; the smelter stays empty for 60 s. Then `stations=on` and it's loaded within 120 s.
   - **`vfh_t_mills` (VFH-CHORE-3):** a board at level 5, a level 5 Steward, `stations windmill piece_spinningwheel`, `wind_on`, a chest with Barley 20 and Flax 20, and chests seeded with `BarleyFlour 1 noazu` (tag `flour`) and `LinenThread 1 noazu` (tag `linen`).
     - `station windmill queue > 0` and `station piece_spinningwheel queue > 0` within 120 s.
     - `deposited flour BarleyFlour > 0` and `deposited linen LinenThread > 0` within 300 s.
   - Every macro starts with `flatten`, asserts `log_errors == 0` and ends with `clear_area`.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- In single player:
  - `vfh_t_chore_gate`, `vfh_t_chore_toggle` and `vfh_t_mills` pass;
  - the updated `vfh_t_work5`, `vfh_t_azu3`, `vfh_t_keep1` and `vfh_t_keep2` pass;
  - no `lvl=E`.

## Test plan
- The macros above.
- **By hand:** a level 2 Steward's Shift+E panel lists:
  - Fires on (nothing to do yet: phase 03), Beehives on, Stations on, Animals on;
  - Mills locked until 5, Sap locked until 6, Repairs locked until 3.

  Switching Stations off shows "off" and the Steward stops loading the smelter.
- **Roster tab:** the Steward's status reads e.g. "Loading Smelter", or "Smelter: no copper ore in any chest".

## Commit
`feat(steward): chore loop picks the most urgent job; stations gated by level; mills; chore panel and status`

## Rollback
Revert the commit. `SmelterBehaviour` comes back and the station rows behave as in 0.3.0. `chore:` entries already saved in contracts' skip lists are ignored by 0.3 builds, so they're harmless.

## As built
- **Status lines with arguments** are stored in the activity ZDO field as `token|arg|arg` (`ActivityText`), and expanded where they're shown: the hover, the roster status and the panel. That way each game shows them in its own language.
- **`IChore`** also has `RestAfter`, the pause before the next survey: 2 s after loading (the old settle pause), 5 s after a failure, 0 otherwise.
- **Interruptions:** a job not ticked for 2 s (combat, fleeing) is dropped and the Steward surveys afresh. A running job whose chore becomes switched off, locked or handled by another mod is dropped within 1 s.
- **Claims:** stations no longer in the plan are released on every survey; stopping (leaving Working) releases every chore's claims. Skipped stations (3 failures) are left out of the plan, so one unreachable station doesn't block the rest.
- **Approach:** "close enough" also needs the hireling within 1.8 m of the object's base height (on its floor, not under it). The spot beside an object is found on the floor at the object's height, not on the roof above it.
- **Leftovers:** `LeftoverSince` is reset only by a load job. When idle, the leftovers are the cargo items that aren't station products.
- **`StewardBehaviour.ServerAllows(kind)`** maps each chore to its server setting, and `WhyNot` gives the reason a chore is out (server, off, locked, or a mod's name).
- **Panel:** one full-width row per chore, built from `ChoreKeys.All` (enum order), so new kinds appear without panel changes. A partly unlocked Stations or Mills row has a dim line underneath listing the stations still locked.
- **`steward_chores` fixture:** it edits the contract, so it works before the Steward arrives.
- **`vfh_t_mills`** ends with a `wind_off` fixture (`EnvMan.ResetDebugWind`).
